using System.Text.Json;
using Application.Common;
using Domain.Enums;
using Infrastructure.Services;

namespace API.Middleware;

/// <summary>
/// Blocks tenant API access (402 Payment Required) when the governing subscription is suspended,
/// cancelled, or past its trial / paid end date.
///
/// The governing subscription is the current branch's own status, or — for branches added after
/// provisioning, which carry no status — the tenant's first provisioned branch.
/// Auth, admin, health and Swagger routes are exempt, as is the Owner's billing area so an
/// expired tenant can still see what it owes.
/// </summary>
public class SubscriptionMiddleware
{
    private static readonly PathString[] ExemptPrefixes =
    {
        "/api/auth", "/api/admin", "/api/billing", "/health", "/swagger"
    };

    private readonly RequestDelegate _next;

    public SubscriptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IShopSettingsCache shops, TimeProvider clock)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true
            || user.IsInRole(AppRoles.SystemAdmin)
            || ExemptPrefixes.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        if (Guid.TryParse(user.FindFirst("tenantId")?.Value, out var tenantId)
            && Guid.TryParse(user.FindFirst("shopId")?.Value, out var shopId))
        {
            var governing = await shops.GetGoverningSubscriptionAsync(tenantId, shopId, context.RequestAborted);
            var reason = BlockReason(governing, clock.GetUtcNow().UtcDateTime);
            if (reason != null)
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                context.Response.ContentType = "application/json";
                var body = ApiResponse<object>.Fail(reason, 402);
                await context.Response.WriteAsync(JsonSerializer.Serialize(body,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return;
            }
        }

        await _next(context);
    }

    internal static string? BlockReason(ShopSettings? governing, DateTime utcNow)
    {
        if (governing?.Status == null) return null;   // legacy tenant with no subscription data — not blocked
        return governing.Status switch
        {
            SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled
                => "Your shop's subscription is suspended or cancelled.",
            SubscriptionStatus.Trial when governing.TrialEndsAt.HasValue && governing.TrialEndsAt.Value < utcNow
                => "Your trial has expired. Please contact support to upgrade to a paid plan.",
            SubscriptionStatus.Active when governing.SubscriptionEndsAt.HasValue && governing.SubscriptionEndsAt.Value < utcNow
                => "Your subscription has expired. Please renew to continue using the system.",
            _ => null
        };
    }
}

public static class SubscriptionMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionCheck(this IApplicationBuilder builder)
        => builder.UseMiddleware<SubscriptionMiddleware>();
}
