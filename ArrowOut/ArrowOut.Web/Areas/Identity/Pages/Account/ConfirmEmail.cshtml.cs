using System.Text;
using ArrowOut.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace ArrowOut.Web.Areas.Identity.Pages.Account;

// Where the link in the e-mail goes. If the code is right the account is confirmed and the
// player is signed in straight away, so they can start playing.
[AllowAnonymous]
public class ConfirmEmailModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<ConfirmEmailModel> logger) : PageModel
{
    public bool Confirmed { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            // Same page as a wrong code, so the link can't be used to check which ids exist.
            return Page();
        }

        if (user.EmailConfirmed)
        {
            Confirmed = true;
            return Page();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return Page();
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogWarning("E-mail confirmation failed (old or broken link).");
            return Page();
        }

        logger.LogInformation("E-mail confirmed.");
        Confirmed = true;
        await signInManager.SignInAsync(user, isPersistent: false);
        return Page();
    }
}
