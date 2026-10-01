using System.Diagnostics;
using System.Text.Json;
using Application.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

/// <summary>
/// Last-resort error handler. Every unhandled exception is logged with a trace id; the client
/// receives the standard envelope with a safe message. Database and framework internals are
/// only included in the response in the Development environment.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away — nothing to report.
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var (statusCode, message) = Map(ex);

            if (statusCode >= 500)
                _logger.LogError(ex, "Unhandled exception {TraceId} on {Method} {Path}", traceId, context.Request.Method, context.Request.Path);
            else
                _logger.LogWarning(ex, "Request failed {TraceId} with {StatusCode} on {Method} {Path}", traceId, statusCode, context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errors = new List<string> { message, $"traceId: {traceId}" };
            if (_env.IsDevelopment())
                errors.Add(ex.InnerException?.Message ?? ex.Message);

            var json = JsonSerializer.Serialize(ApiResponse<object>.Fail(errors, statusCode),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await context.Response.WriteAsync(json);
        }
    }

    internal static (int StatusCode, string Message) Map(Exception ex) => ex switch
    {
        UnauthorizedAccessException      => (403, "You are not allowed to perform this action."),
        KeyNotFoundException             => (404, "The requested record was not found."),
        DbUpdateConcurrencyException     => (409, "The record was changed by someone else. Reload and try again."),
        DbUpdateException dbEx           => (409, DescribeDbError(dbEx)),
        BadHttpRequestException bad      => (bad.StatusCode, "The request could not be read."),
        JsonException                    => (400, "The request body is not valid JSON for this endpoint."),
        ArgumentException                => (400, "The request contains an invalid value."),
        _                                => (500, "An unexpected error occurred.")
    };

    private static string DescribeDbError(DbUpdateException ex)
    {
        var detail = ex.InnerException?.Message ?? ex.Message;
        if (detail.Contains("foreign key", StringComparison.OrdinalIgnoreCase))
            return "A referenced record does not exist (for example the selected karigar, customer or item).";
        if (detail.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("unique", StringComparison.OrdinalIgnoreCase))
            return "This record already exists.";
        return "The change could not be saved.";
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
