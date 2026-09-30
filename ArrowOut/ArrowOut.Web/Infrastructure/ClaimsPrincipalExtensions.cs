using System.Security.Claims;
using ArrowOut.Data.Common;

namespace ArrowOut.Web.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    // The user id always comes from the login cookie, never from anything in the request.
    public static string GetRequiredUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The current request is not authenticated.");
    }

    public static bool IsAdministrator(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole(Roles.Administrator);
    }
}
