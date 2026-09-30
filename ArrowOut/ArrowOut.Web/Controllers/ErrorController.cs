using System.Diagnostics;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers;

// UseExceptionHandler and UseStatusCodePagesWithReExecute send errors here.
[Route("error")]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken] // a POST that failed still needs to show the error page
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorController : Controller
{
    [Route("404")]
    public IActionResult PageNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound", Build(404, "Page not found", "That page slid off the board. It may have moved or never existed."));
    }

    [Route("500")]
    public IActionResult ServerError()
    {
        // The exception was already logged by the middleware.
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("ServerError", Build(500, "Something went wrong", "An arrow collided on our side. The error has been logged — please try again."));
    }

    [Route("{statusCode:int}")]
    public IActionResult Status(int statusCode)
    {
        if (statusCode == StatusCodes.Status404NotFound)
        {
            return PageNotFound();
        }

        var title = statusCode switch
        {
            400 => "Bad request",
            401 => "Please sign in",
            403 => "Access denied",
            _ => "Unexpected response",
        };

        Response.StatusCode = statusCode is >= 400 and < 600 ? statusCode : StatusCodes.Status500InternalServerError;
        return View("Status", Build(Response.StatusCode, title, "The request could not be completed."));
    }

    private ErrorViewModel Build(int code, string title, string message) =>
        new(code, title, message, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
