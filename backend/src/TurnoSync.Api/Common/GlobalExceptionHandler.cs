using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace TurnoSync.Api.Common;

/// <summary>Rede de segurança para o inesperado: 500 em ProblemDetails, sem stack trace.</summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Falhas de requisição do cliente (corpo grande demais, por exemplo) não são erro do servidor.
        var status = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError;

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status >= StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : "The request could not be processed.",
            },
        });
    }
}
