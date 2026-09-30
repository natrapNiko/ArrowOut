using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Controllers.Api;

// Base class for the API controllers. [ApiController] gives us the automatic 400 for bad
// input, and you have to be logged in unless an action says otherwise.
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string CurrentUserId => User.GetRequiredUserId();

    protected bool IsAdministrator => User.IsAdministrator();
}
