using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MotorInsurance.Application.Common.Models;

namespace MotorInsurance.Api;

/// <summary>
/// Wraps every successful action result in the uniform ApiResponse envelope
/// ({ success, data, traceId }). Errors are produced already-wrapped by the
/// exception middleware and the invalid-model-state factory, so they are skipped.
/// </summary>
public class ApiResponseWrapperFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception is not null) return;

        var traceId = context.HttpContext.TraceIdentifier;

        switch (context.Result)
        {
            case ObjectResult obj when obj.Value is not IApiResponse && obj.Value is not ProblemDetails:
            {
                var status = obj.StatusCode ?? StatusCodes.Status200OK;
                if (status is >= 200 and < 300)
                {
                    obj.Value = new ApiResponse<object?> { Success = true, Data = obj.Value, TraceId = traceId };
                    obj.DeclaredType = typeof(ApiResponse<object?>);
                }
                break;
            }
            case NoContentResult:
            case EmptyResult:
                context.Result = new ObjectResult(
                    new ApiResponse<object?> { Success = true, Data = null, TraceId = traceId })
                { StatusCode = StatusCodes.Status200OK };
                break;
            case StatusCodeResult scr when scr.StatusCode is >= 200 and < 300:
                context.Result = new ObjectResult(
                    new ApiResponse<object?> { Success = true, Data = null, TraceId = traceId })
                { StatusCode = scr.StatusCode };
                break;
        }
    }
}
