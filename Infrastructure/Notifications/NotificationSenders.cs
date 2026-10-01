using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

public sealed record SendResult(bool Success, string? ProviderMessageId = null, string? Error = null, bool Permanent = false);

/// <summary>Delivers a rendered message on one channel. Business code never calls a sender directly.</summary>
public interface INotificationSender
{
    NotificationChannel Channel { get; }
    bool IsConfigured { get; }
    Task<SendResult> SendAsync(string recipient, string body, CancellationToken ct);
}

public sealed class NotificationOptions
{
    public const string Section = "Notifications";
    public bool DispatcherEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public bool RemindersEnabled { get; set; } = true;
    /// <summary>Shop-local hour after which the daily reminders are queued.</summary>
    public int ReminderHour { get; set; } = 9;
    /// <summary>Default country calling code applied to local numbers such as 03001234567.</summary>
    public string DefaultCountryCode { get; set; } = "92";
    public WhatsAppOptions WhatsApp { get; set; } = new();
    public SmsOptions Sms { get; set; } = new();
}

public sealed class WhatsAppOptions
{
    public string? AccessToken { get; set; }
    public string? PhoneNumberId { get; set; }
    public string ApiVersion { get; set; } = "v21.0";
    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    /// <summary>
    /// Business-initiated WhatsApp messages must use an approved template. When set, the rendered
    /// text is sent as the single body parameter {{1}} of this template.
    /// </summary>
    public string? TemplateName { get; set; }
    public string TemplateLanguage { get; set; } = "en";
}

public sealed class SmsOptions
{
    /// <summary>Gateway endpoint receiving POST {"to": "...", "message": "..."}.</summary>
    public string? Url { get; set; }
    public string? ApiKey { get; set; }
    public string ApiKeyHeader { get; set; } = "Authorization";
    public string? SenderId { get; set; }
}

public static class PhoneNumbers
{
    /// <summary>Normalises "0300-1234567" / "+92 300 1234567" to "923001234567".</summary>
    public static string ToInternational(string raw, string defaultCountryCode)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];
        else if (digits.StartsWith('0')) digits = defaultCountryCode + digits[1..];
        return digits;
    }
}

/// <summary>Fallback for any channel without provider credentials: writes the message to the log and reports success.</summary>
public sealed class LogNotificationSender : INotificationSender
{
    private readonly ILogger<LogNotificationSender> _logger;
    public LogNotificationSender(NotificationChannel channel, ILogger<LogNotificationSender> logger)
    {
        Channel = channel;
        _logger = logger;
    }

    public NotificationChannel Channel { get; }
    public bool IsConfigured => true;

    public Task<SendResult> SendAsync(string recipient, string body, CancellationToken ct)
    {
        _logger.LogInformation("Notification ({Channel}) to {Recipient}: {Body}", Channel, recipient, body);
        return Task.FromResult(new SendResult(true, "log"));
    }
}

public sealed class WhatsAppCloudApiSender : INotificationSender
{
    private readonly HttpClient _http;
    private readonly NotificationOptions _options;

    public WhatsAppCloudApiSender(HttpClient http, IOptions<NotificationOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public NotificationChannel Channel => NotificationChannel.WhatsApp;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.WhatsApp.AccessToken) && !string.IsNullOrWhiteSpace(_options.WhatsApp.PhoneNumberId);

    public async Task<SendResult> SendAsync(string recipient, string body, CancellationToken ct)
    {
        var wa = _options.WhatsApp;
        var to = PhoneNumbers.ToInternational(recipient, _options.DefaultCountryCode);
        object payload = string.IsNullOrWhiteSpace(wa.TemplateName)
            ? new { messaging_product = "whatsapp", to, type = "text", text = new { body } }
            : new
            {
                messaging_product = "whatsapp", to, type = "template",
                template = new
                {
                    name = wa.TemplateName,
                    language = new { code = wa.TemplateLanguage },
                    components = new[] { new { type = "body", parameters = new[] { new { type = "text", text = body } } } }
                }
            };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{wa.BaseUrl.TrimEnd('/')}/{wa.ApiVersion}/{wa.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wa.AccessToken);

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new SendResult(false, Error: $"WhatsApp API {(int)response.StatusCode}: {Truncate(text)}",
                Permanent: (int)response.StatusCode is 400 or 401 or 403);

        string? id = null;
        try { id = JsonDocument.Parse(text).RootElement.GetProperty("messages")[0].GetProperty("id").GetString(); }
        catch { /* id is informational only */ }
        return new SendResult(true, id);
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}

public sealed class HttpSmsSender : INotificationSender
{
    private readonly HttpClient _http;
    private readonly NotificationOptions _options;

    public HttpSmsSender(HttpClient http, IOptions<NotificationOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public NotificationChannel Channel => NotificationChannel.Sms;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Sms.Url);

    public async Task<SendResult> SendAsync(string recipient, string body, CancellationToken ct)
    {
        var sms = _options.Sms;
        using var request = new HttpRequestMessage(HttpMethod.Post, sms.Url)
        {
            Content = JsonContent.Create(new
            {
                to = PhoneNumbers.ToInternational(recipient, _options.DefaultCountryCode),
                message = body,
                sender = sms.SenderId
            })
        };
        if (!string.IsNullOrWhiteSpace(sms.ApiKey))
            request.Headers.TryAddWithoutValidation(sms.ApiKeyHeader, sms.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return new SendResult(true);
        var text = await response.Content.ReadAsStringAsync(ct);
        return new SendResult(false, Error: $"SMS gateway {(int)response.StatusCode}: {(text.Length > 300 ? text[..300] : text)}",
            Permanent: (int)response.StatusCode is 400 or 401 or 403);
    }
}

/// <summary>Picks the configured sender for a channel, or the log sender when none is configured.</summary>
public sealed class NotificationSenderResolver
{
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly ILogger<LogNotificationSender> _logLogger;

    public NotificationSenderResolver(IEnumerable<INotificationSender> senders, ILogger<LogNotificationSender> logLogger)
    {
        _senders = senders;
        _logLogger = logLogger;
    }

    public INotificationSender For(NotificationChannel channel)
        => _senders.FirstOrDefault(s => s.Channel == channel && s.IsConfigured)
           ?? new LogNotificationSender(channel, _logLogger);
}
