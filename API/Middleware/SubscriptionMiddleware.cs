using System.Text.Json;
using Application.Common;
using Domain.Enums;
using Infrastructure.Services;

namespace API.Middleware;

/// <summary>
/// Blocks tenant API access (402 Payment Required) when the governing subscription is suspended,
/// cancelled, or past its trial / paid end date with no free plan to fall back to (see SubscriptionResolver).
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

    public async Task InvokeAsync(HttpContext context, ISubscriptionResolver subscriptions)
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
            var reason = (await subscriptions.ResolveAsync(tenantId, shopId, context.RequestAborted)).BlockReason;
            if (reason != null)
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                context.Response.ContentType = "application/json";
                // data.subscriptionBlocked tells clients this is a locked shop, not a plan limit (both are 402).
                var body = new ApiResponse<object>
                {
                    Success = false, Message = reason, Errors = new List<string> { reason },
                    Data = new { subscriptionBlocked = true }
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(body,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return;
            }
        }

        await _next(context);
    }

    /// <summary>The lock rule when no free plan exists to fall back to (kept for the existing tests).</summary>
    internal static string? BlockReason(ShopSettings? governing, DateTime utcNow)
        => SubscriptionResolver.Classify(governing, freePlanExists: false, utcNow).BlockReason;
}

public static class SubscriptionMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionCheck(this IApplicationBuilder builder)
        => builder.UseMiddleware<SubscriptionMiddleware>();
}
