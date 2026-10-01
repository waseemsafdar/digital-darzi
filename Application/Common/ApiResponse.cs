using System.Text.Json.Serialization;

namespace Application.Common;

/// <summary>Generic API response wrapper used by all controllers.</summary>
/// <remarks>
/// The JSON shape is <c>{ success, data, message, errors }</c>. <see cref="StatusCode"/> is not
/// serialised; it tells <c>BaseController</c> which HTTP status to return on failure so that
/// status codes no longer have to be guessed from the error text.
/// </remarks>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();

    /// <summary>HTTP status for a failed response. Null means "infer" (400 by default).</summary>
    [JsonIgnore]
    public int? StatusCode { get; init; }

    /// <summary>Success constructor: <c>new ApiResponse&lt;T&gt;(data)</c>.</summary>
    public ApiResponse(T? data, string? message = null)
    {
        Success = true;
        Data    = data;
        Message = message;
    }

    /// <summary>Failure constructor: <c>new ApiResponse&lt;T&gt;(errorMessage, statusCode)</c>.</summary>
    public ApiResponse(string error, int statusCode = 400)
    {
        Success    = false;
        Errors     = new List<string> { error };
        Message    = error;
        StatusCode = statusCode;
    }

    public ApiResponse() { }

    public static ApiResponse<T> Ok(T? data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string error, int statusCode = 400)
        => new() { Success = false, Message = error, Errors = new List<string> { error }, StatusCode = statusCode };

    public static ApiResponse<T> Fail(List<string> errors, int statusCode = 400)
        => new() { Success = false, Message = errors.FirstOrDefault(), Errors = errors, StatusCode = statusCode };

    public static ApiResponse<T> NotFound(string error) => Fail(error, 404);
    public static ApiResponse<T> Forbidden(string error) => Fail(error, 403);
    public static ApiResponse<T> Conflict(string error) => Fail(error, 409);
    public static ApiResponse<T> PaymentRequired(string error) => Fail(error, 402);
}

/// <summary>Non-generic wrapper for simple success/failure messages.</summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();

    [JsonIgnore]
    public int? StatusCode { get; init; }

    public static ApiResponse Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static ApiResponse Fail(string error, int statusCode = 400)
        => new() { Success = false, Message = error, Errors = new List<string> { error }, StatusCode = statusCode };
}

/// <summary>Paginated result wrapper.</summary>
public class PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> From(List<T> items, int total, int page, int pageSize)
        => new() { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
}

/// <summary>Clamps client-supplied paging values so one request cannot ask for an unbounded page.</summary>
public static class Paging
{
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize) Normalize(int page, int pageSize, int defaultSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = defaultSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;
        return (page, pageSize);
    }
}

public static class ApiResponseExtensions
{
    /// <summary>Re-types a failed response, keeping its errors and status code.</summary>
    public static ApiResponse<T> As<T>(this ApiResponse<object> failure)
        => ApiResponse<T>.Fail(failure.Errors, failure.StatusCode ?? 400);
}
