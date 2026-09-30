using ArrowOut.Services.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Infrastructure;

// Turns exceptions from /api into ProblemDetails JSON (RFC 9457).
// Errors we expect keep their message. Unexpected ones get logged and the details hidden.
// Normal pages are left alone and end up on the 500 page.
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // By the time this runs, UseExceptionHandler("/error/500") has already changed Request.Path
        // to the error page, so we have to get the original path from the feature.
        var originalPath = httpContext.Features.Get<IExceptionHandlerFeature>()?.Path ?? httpContext.Request.Path.Value;
        if (!new PathString(originalPath).StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ProblemDetails problem;
        if (exception is ArrowOutException domain)
        {
            problem = new ProblemDetails { Status = domain.StatusCode, Title = domain.Title, Detail = domain.Message };

            if (domain is InvalidLevelDesignException design)
            {
                problem.Extensions["errors"] = design.Errors;
            }
        }
        else
        {
            logger.LogError(exception, "Unhandled API exception for {Path}", originalPath);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server error",
                Detail = "Something went wrong on our side. Please try again.",
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });

        if (!written)
        {
            // None of the writers wanted this request (weird Accept header or similar). API callers
            // should still get JSON and not the HTML error page, so write it ourselves.
            logger.LogDebug("No ProblemDetails writer matched {Path}; writing problem+json directly", originalPath);
            await httpContext.Response.WriteAsJsonAsync(
                problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        }

        return true;
    }
}
