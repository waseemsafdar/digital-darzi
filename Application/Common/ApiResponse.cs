namespace Application.Common;

/// <summary>Generic API response wrapper used by all controllers.</summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();

    /// <summary>Success constructor — mirrors MobilePosApi: new ApiResponse&lt;T&gt;(data)</summary>
    public ApiResponse(T? data, string? message = null)
    {
        Success = true;
        Data    = data;
        Message = message;
    }

    /// <summary>Failure constructor — new ApiResponse&lt;T&gt;(errorMessage, statusCode)</summary>
    public ApiResponse(string error, int statusCode = 400)
    {
        Success = false;
        Errors  = new List<string> { error };
        Message = error;
    }

    // Default ctor for object initializer use
    public ApiResponse() { }

    public static ApiResponse<T> Ok(T? data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string error)
        => new() { Success = false, Errors = new List<string> { error } };

    public static ApiResponse<T> Fail(List<string> errors)
        => new() { Success = false, Errors = errors };
}

/// <summary>Non-generic wrapper for simple success/failure messages.</summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ApiResponse Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static ApiResponse Fail(string error)
        => new() { Success = false, Errors = new List<string> { error } };
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
