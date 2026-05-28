namespace MotorInsurance.Application.Common.Models;

/// <summary>Marker so the response-wrapping filter can detect already-wrapped values.</summary>
public interface IApiResponse;

/// <summary>
/// Uniform envelope for every API response (success and error).
/// Shape: { success, data, message, errors, traceId }.
/// </summary>
public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    public string? TraceId { get; init; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null, string? traceId = null) =>
        new() { Success = true, Data = data, Message = message, TraceId = traceId };

    public static ApiResponse<object?> Fail(
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}
