using Application.Interfaces.Services;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace API.Middleware;

public class SubscriptionMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for auth and admin endpoints (so they can still login and admins can manage)
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path) || path.Contains("/api/auth") || (path.Contains("/api/shops") && context.Request.Method != "GET"))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            // If the user is a SystemAdmin, bypass
            if (user.IsInRole("SystemAdmin"))
            {
                await _next(context);
                return;
            }

            var tenantClaim = user.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
                
                // Using AsNoTracking to avoid tracking issues, just checking status
                var shop = await dbContext.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == tenantId);
                
                if (shop != null)
                {
                    bool isBlocked = false;
                    string blockReason = "";

                    if (shop.Status == SubscriptionStatus.Suspended || shop.Status == SubscriptionStatus.Cancelled)
                    {
                        isBlocked = true;
                        blockReason = "Your shop's subscription is suspended or cancelled.";
                    }
                    else if (shop.Status == SubscriptionStatus.Trial && shop.TrialEndsAt.HasValue && shop.TrialEndsAt.Value < DateTime.UtcNow)
                    {
                        isBlocked = true;
                        blockReason = "Your trial has expired. Please contact support to upgrade to a paid plan.";
                    }
                    else if (shop.Status == SubscriptionStatus.Active && shop.SubscriptionEndsAt.HasValue && shop.SubscriptionEndsAt.Value < DateTime.UtcNow)
                    {
                        isBlocked = true;
                        blockReason = "Your subscription has expired. Please renew to continue using the system.";
                    }

                    if (isBlocked)
                    {
                        context.Response.StatusCode = 402; // Payment Required
                        context.Response.ContentType = "application/json";
                        var result = JsonSerializer.Serialize(new { success = false, message = blockReason });
                        await context.Response.WriteAsync(result);
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}

public static class SubscriptionMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionCheck(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubscriptionMiddleware>();
    }
}
