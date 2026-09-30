using System.Security.Cryptography;

namespace ArrowOut.Web.Infrastructure;

// Adds the security headers. The CSP only allows scripts from this site (plus a nonce per
// request), so even if some XSS got past Razor it couldn't run inline.
// Inline styles are allowed because Bootstrap and the theme block need them. The theme
// colours are checked with a regex, so nobody can put their own CSS in there.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string NonceItemKey = "csp-nonce";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItemKey] = nonce;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "frame-ancestors 'none'";
            return Task.CompletedTask;
        });

        return next(context);
    }
}

public static class HttpContextNonceExtensions
{
    public static string GetCspNonce(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[SecurityHeadersMiddleware.NonceItemKey] as string ?? string.Empty;
    }
}
