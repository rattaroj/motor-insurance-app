using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Common;

namespace MotorInsurance.Api;

/// <summary>Maps domain/application exceptions to the uniform ApiResponse error envelope.</summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task Invoke(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex) { await WriteAsync(ctx, ex); }
    }

    private async Task WriteAsync(HttpContext ctx, Exception ex)
    {
        var (status, message, errors) = ex switch
        {
            ValidationException v => (400, "Validation failed", (IReadOnlyDictionary<string, string[]>?)v.Errors),
            UnauthorizedException => (401, ex.Message, null),
            NotFoundException => (404, ex.Message, null),
            ConflictException => (409, ex.Message, null),
            InvalidStateTransitionException => (409, ex.Message, null),
            DbUpdateException => (409, "The operation conflicts with an existing record.", null),
            _ => (500, "An unexpected error occurred.", null),
        };

        if (status == 500) _logger.LogError(ex, "Unhandled exception");

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";

        var payload = ApiResponse.Fail(message, errors, ctx.TraceIdentifier);
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
