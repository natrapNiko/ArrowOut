using ArrowOut.Data.Common;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Areas.Administration.Controllers;

// All admin controllers inherit from this, so they get the area route and admin-only access.
[Area("Administration")]
[Authorize(Roles = Roles.Administrator)]
public abstract class AdministrationController : Controller
{
    protected string CurrentUserId => User.GetRequiredUserId();

    protected void Success(string message) => TempData[StatusMessages.SuccessKey] = message;

    protected void Error(string message) => TempData[StatusMessages.ErrorKey] = message;
}
