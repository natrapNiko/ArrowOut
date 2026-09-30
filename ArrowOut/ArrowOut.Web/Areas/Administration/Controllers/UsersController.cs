using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using ArrowOut.Services.Users;
using ArrowOut.Web.Areas.Administration.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Areas.Administration.Controllers;

public class UsersController(IUserAdminService userAdminService) : AdministrationController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UserQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            query = new UserQuery();
        }

        var users = await userAdminService.GetPagedAsync(query, cancellationToken);
        return View(new UsersIndexViewModel { Query = query, Users = users, CurrentUserId = CurrentUserId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken) =>
        View(await userAdminService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Lock(string id) =>
        RunAsync(id, () => userAdminService.SetLockedAsync(id, locked: true, CurrentUserId), "User locked.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(string id) =>
        RunAsync(id, () => userAdminService.SetLockedAsync(id, locked: false, CurrentUserId), "User unlocked.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Promote(string id) =>
        RunAsync(id, () => userAdminService.SetAdministratorAsync(id, isAdministrator: true, CurrentUserId), "User is now an administrator.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Demote(string id) =>
        RunAsync(id, () => userAdminService.SetAdministratorAsync(id, isAdministrator: false, CurrentUserId), "Administrator role removed.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await userAdminService.DeleteAsync(id, CurrentUserId);
            Success("User deleted.");
        }
        catch (OperationNotAllowedException ex)
        {
            Error(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RunAsync(string id, Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            Success(successMessage);
        }
        catch (OperationNotAllowedException ex)
        {
            Error(ex.Message);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
