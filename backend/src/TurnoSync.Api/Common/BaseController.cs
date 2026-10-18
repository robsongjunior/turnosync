using Microsoft.AspNetCore.Mvc;
using TurnoSync.Application.Common;

namespace TurnoSync.Api.Common;

[ApiController]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    /// <summary>Maps a service result to an HTTP response; errors become ProblemDetails.</summary>
    protected IActionResult HandleResult<T>(Result<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        return onSuccess is null ? Ok(result.Value) : onSuccess(result.Value!);
    }

    protected IActionResult HandleResult(Result result, Func<IActionResult>? onSuccess = null)
    {
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        return onSuccess is null ? Ok() : onSuccess();
    }

    private ActionResult ToProblem(ResultError error) => error.Type switch
    {
        ErrorType.Validation => ValidationProblem(
            new ValidationProblemDetails(new Dictionary<string, string[]>(error.FieldErrors))),
        ErrorType.NotFound => Problem(
            detail: error.Message, title: "Not Found", statusCode: StatusCodes.Status404NotFound),
        ErrorType.Conflict => Problem(
            detail: error.Message, title: "Conflict", statusCode: StatusCodes.Status409Conflict),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
