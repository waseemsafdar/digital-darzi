using Application.Common;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

/// <summary>
/// Delivers pending outbox rows. Safe with several API instances: each row is claimed with a
/// conditional UPDATE (Pending → Sending) and only the instance whose update hit the row sends it.
/// Failures retry with backoff up to <see cref="NotificationOptions.MaxAttempts"/>.
/// </summary>
public sealed class NotificationDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly NotificationOptions _options;
    private readonly TimeProvider _clock;

    public NotificationDispatcher(IServiceScopeFactory scopes, ILogger<NotificationDispatcher> logger,
        IOptions<NotificationOptions> options, TimeProvider clock)
    {
        _scopes = scopes;
        _logger = logger;
        _options = options.Value;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled) return;
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Notification dispatch batch failed");
            }

            try { await Task.Delay(delay, _clock, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Processes up to 50 due messages. Public so tests can drive it deterministically.</summary>
    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISystemDbContextFactory>();
        var resolver = scope.ServiceProvider.GetRequiredService<NotificationSenderResolver>();
        await using var db = factory.Create();
        db.AuditEnabled = false;

        var now = _clock.GetUtcNow().UtcDateTime;
        var staleSending = now.AddMinutes(-10);

        var due = await db.NotificationMessages.IgnoreQueryFilters()
            .Where(m => !m.IsDeleted
                && ((m.Status == NotificationStatus.Pending && m.ScheduledAt <= now)
                    || (m.Status == NotificationStatus.Sending && m.UpdatedOn < staleSending)))
            .OrderBy(m => m.ScheduledAt)
            .Select(m => new { m.Id, m.Status })
            .Take(50)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var candidate in due)
        {
            var claimed = await db.NotificationMessages.IgnoreQueryFilters()
                .Where(m => m.Id == candidate.Id && m.Status == candidate.Status)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, NotificationStatus.Sending)
                    .SetProperty(m => m.UpdatedOn, now), ct);
            if (claimed != 1) continue;   // another instance took it

            var message = await db.NotificationMessages.IgnoreQueryFilters().FirstAsync(m => m.Id == candidate.Id, ct);
            SendResult result;
            try
            {
                result = await resolver.For(message.Channel).SendAsync(message.Recipient, message.Body, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = new SendResult(false, Error: ex.Message);
            }

            message.Attempts++;
            message.UpdatedOn = _clock.GetUtcNow().UtcDateTime;
            if (result.Success)
            {
                message.Status = NotificationStatus.Sent;
                message.SentAt = message.UpdatedOn;
                message.ProviderMessageId = result.ProviderMessageId;
                message.LastError = null;
                sent++;
            }
            else if (result.Permanent || message.Attempts >= _options.MaxAttempts)
            {
                message.Status = NotificationStatus.Failed;
                message.LastError = result.Error;
            }
            else
            {
                message.Status = NotificationStatus.Pending;
                message.LastError = result.Error;
                message.ScheduledAt = message.UpdatedOn.AddMinutes(Math.Pow(2, message.Attempts));   // 2, 4, 8, 16 min
            }
            await db.SaveChangesAsync(ct);
        }
        return sent;
    }
}

/// <summary>
/// Once a day per branch (after <see cref="NotificationOptions.ReminderHour"/> shop-local time):
/// a delivery reminder for orders due tomorrow, and a payment reminder for orders that have been
/// ready for pickup for 3+ days with a balance (at most once a week per order).
/// </summary>
public sealed class ReminderJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReminderJob> _logger;
    private readonly NotificationOptions _options;
    private readonly TimeProvider _clock;

    public ReminderJob(IServiceScopeFactory scopes, ILogger<ReminderJob> logger, IOptions<NotificationOptions> options, TimeProvider clock)
    {
        _scopes = scopes;
        _logger = logger;
        _options = options.Value;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RemindersEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reminder job failed");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(30), _clock, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Queues today's reminders for every branch past its reminder hour. Returns messages queued.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISystemDbContextFactory>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var now = _clock.GetUtcNow().UtcDateTime;

        List<(Guid TenantId, Guid ShopId, string TimeZone)> shops;
        await using (var system = factory.Create())
        {
            shops = (await system.Shops.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => !s.IsDeleted && s.NotificationsEnabled && s.ActiveStatus == ActiveStatus.Active)
                    .Select(s => new { s.TenantId, s.Id, s.TimeZone })
                    .ToListAsync(ct))
                .Select(s => (s.TenantId, s.Id, s.TimeZone)).ToList();
        }

        var queued = 0;
        foreach (var (tenantId, shopId, tzId) in shops)
        {
            var tz = ShopCalendar.ResolveTimeZone(tzId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
            if (local.Hour < _options.ReminderHour) continue;

            var today = ShopCalendar.Today(now, tz);
            await using var db = factory.CreateForBranch(tenantId, shopId);
            db.AuditEnabled = false;
            var shopCache = new ShopSettingsCache(db, cache);
            var notifier = new NotificationService(db, shopCache, NoUser.Instance, _clock);
            var (dayStart, _) = ShopCalendar.LocalDayBounds(today, tz);

            // Delivery reminders for tomorrow's orders — one per order per day.
            var tomorrow = today.AddDays(1);
            var dueTomorrow = await db.Orders.Include(o => o.Customer)
                .Where(o => o.DeliveryDate >= tomorrow && o.DeliveryDate < tomorrow.AddDays(1)
                    && o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled
                    && !db.NotificationMessages.Any(m => m.OrderId == o.Id
                        && m.EventType == NotificationEvent.DeliveryReminder && m.CreatedOn >= dayStart))
                .Take(500)
                .ToListAsync(ct);
            foreach (var order in dueTomorrow)
                if (await notifier.EnqueueOrderEventAsync(order, NotificationEvent.DeliveryReminder, ct)) queued++;

            // Payment reminders: ready for 3+ days with a balance, at most weekly.
            var readySince = now.AddDays(-3);
            var weekAgo = now.AddDays(-7);
            var unpaidReady = await db.Orders.Include(o => o.Customer)
                .Where(o => o.Status == OrderStatus.ReadyToDeliver && o.BalanceDue > 0
                    && o.ReadyDate != null && o.ReadyDate <= readySince
                    && !db.NotificationMessages.Any(m => m.OrderId == o.Id
                        && m.EventType == NotificationEvent.PaymentReminder && m.CreatedOn >= weekAgo))
                .Take(500)
                .ToListAsync(ct);
            foreach (var order in unpaidReady)
                if (await notifier.EnqueueOrderEventAsync(order, NotificationEvent.PaymentReminder, ct)) queued++;

            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        }
        return queued;
    }

    /// <summary>The job acts as the system, not as a signed-in user.</summary>
    private sealed class NoUser : ICurrentUserService
    {
        public static readonly NoUser Instance = new();
        public Guid UserId => Guid.Empty;
        public Guid TenantId => Guid.Empty;
        public Guid ShopId => Guid.Empty;
        public string UserName => "system";
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsInRole(string role) => false;
        public bool IsSystemAdmin => false;
        public bool IsOwner => false;
        public bool IsManager => false;
        public bool IsKarigar => false;
        public bool IsReceptionist => false;
        public bool IsSupervisor => false;
        public string? IpAddress => null;
    }
}
