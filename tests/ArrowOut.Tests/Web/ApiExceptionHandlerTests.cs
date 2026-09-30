using ArrowOut.Services.Exceptions;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArrowOut.Tests.Web;

public class ApiExceptionHandlerTests
{
    // This is what UseExceptionHandler("/error/500") gives the handlers: Request.Path already points
    // at the error page, and only the feature still has the original path.
    private static DefaultHttpContext ReExecutedContext(string originalPath, Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/error/500";
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Path = originalPath, Error = exception });
        return context;
    }

    private static Mock<IProblemDetailsService> Writer(bool canWrite = true)
    {
        var writer = new Mock<IProblemDetailsService>();
        writer.Setup(w => w.TryWriteAsync(It.IsAny<ProblemDetailsContext>())).ReturnsAsync(canWrite);
        return writer;
    }

    [Fact]
    public async Task ApiDomainError_BecomesProblemDetailsWithItsStatus_EvenAfterThePathWasRewritten()
    {
        var exception = new EntityNotFoundException("Challenge", 5);
        var context = ReExecutedContext("/api/challenges/5", exception);
        var writer = Writer();

        var handled = await new ApiExceptionHandler(writer.Object, NullLogger<ApiExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        writer.Verify(w => w.TryWriteAsync(It.Is<ProblemDetailsContext>(p => p.ProblemDetails.Status == 404)), Times.Once);
    }

    [Fact]
    public async Task PageError_IsLeftToTheHtmlErrorPage()
    {
        var exception = new InvalidOperationException("boom");
        var context = ReExecutedContext("/challenge/5", exception);

        var handled = await new ApiExceptionHandler(Writer().Object, NullLogger<ApiExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task ApiError_StillGetsJson_WhenNoProblemDetailsWriterMatches()
    {
        var exception = new InvalidGameStateException("The board is not cleared yet.");
        var context = ReExecutedContext("/api/challenges/5/completion", exception);
        context.Response.Body = new MemoryStream();

        var handled = await new ApiExceptionHandler(Writer(canWrite: false).Object, NullLogger<ApiExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        Assert.Contains("The board is not cleared yet.", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }
}
