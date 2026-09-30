using System.Security.Claims;
using ArrowOut.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace ArrowOut.Tests.Web;

internal static class ControllerTestHelper
{
    public static T WithUser<T>(this T controller, string userId, bool isAdmin = false)
        where T : ControllerBase
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId) };
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, Roles.Administrator));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        if (controller is Controller mvc)
        {
            mvc.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        return controller;
    }
}
