using Application.Common;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace API.Middleware;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteErrorResponseAsync(context, ex);
        }
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        (int statusCode, string message) = ex switch
        {
            UnauthorizedAccessException => (401, "Unauthorized."),
            KeyNotFoundException         => (404, ex.Message),
            ArgumentException           => (400, ex.Message),
            // EF/Npgsql write failures (FK violation, unique constraint, invalid value)
            DbUpdateException dbEx       => (409, DescribeDbError(dbEx)),
            InvalidOperationException   => (422, ex.Message),
            _                           => (500, "An unexpected error occurred.")
        };

        context.Response.StatusCode = statusCode;

        // In Development, surface the real cause (and inner exception) so 500s are debuggable.
        var errors = _env.IsDevelopment()
            ? new List<string> { message, ex.InnerException?.Message ?? ex.Message }
            : new List<string> { message };

        var response = ApiResponse<object>.Fail(errors);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static string DescribeDbError(DbUpdateException ex)
    {
        var detail = ex.InnerException?.Message ?? ex.Message;
        if (detail.Contains("foreign key", StringComparison.OrdinalIgnoreCase))
            return "A referenced record does not exist (e.g. the selected karigar/customer was not found).";
        if (detail.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("unique", StringComparison.OrdinalIgnoreCase))
            return "This record already exists.";
        return "The operation could not be saved due to a data conflict.";
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
