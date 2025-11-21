namespace ToskaMesh.Common;

/// <summary>
/// Standard API response wrapper for consistent response format across services
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string? message = null, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message,
            TraceId = traceId
        };
    }

    public static ApiResponse<T> ErrorResponse(string error, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = error,
            Errors = new List<string> { error },
            TraceId = traceId
        };
    }

    public static ApiResponse<T> ErrorResponse(List<string> errors, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Errors = errors,
            Message = string.Join("; ", errors),
            TraceId = traceId
        };
    }
}

/// <summary>
/// API response for operations that don't return data
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Success(string? message = null, string? traceId = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message,
            TraceId = traceId
        };
    }

    public static new ApiResponse ErrorResponse(string error, string? traceId = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = error,
            Errors = new List<string> { error },
            TraceId = traceId
        };
    }
}
