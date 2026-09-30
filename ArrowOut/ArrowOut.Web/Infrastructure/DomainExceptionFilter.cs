using ArrowOut.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ArrowOut.Web.Infrastructure;

// Handles the errors we expect on normal page requests. API requests go to
// ApiExceptionHandler instead, and anything unexpected ends up on the 500 page.
public sealed class DomainExceptionFilter(ITempDataDictionaryFactory tempDataFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ExceptionHandled
            || context.Exception is not ArrowOutException exception
            || context.HttpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        switch (exception)
        {
            case EntityNotFoundException:
                // Return an empty 404 and let UseStatusCodePagesWithReExecute show our 404 page.
                context.Result = new NotFoundResult();
                break;

            case LevelLockedException:
                SetMessage(context, exception.Message);
                context.Result = new RedirectToActionResult("Index", "Home", new { area = string.Empty });
                break;

            default:
                SetMessage(context, exception.Message);
                var referer = context.HttpContext.Request.Headers.Referer.ToString();
                context.Result = Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                    && string.Equals(uri.Host, context.HttpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
                    ? new LocalRedirectResult(uri.PathAndQuery)
                    : new RedirectToActionResult("Index", "Home", new { area = string.Empty });
                break;
        }

        context.ExceptionHandled = true;
    }

    private void SetMessage(ExceptionContext context, string message)
    {
        var tempData = tempDataFactory.GetTempData(context.HttpContext);
        tempData[StatusMessages.ErrorKey] = message;
    }
}

public static class StatusMessages
{
    public const string SuccessKey = "StatusSuccess";
    public const string ErrorKey = "StatusError";
}
