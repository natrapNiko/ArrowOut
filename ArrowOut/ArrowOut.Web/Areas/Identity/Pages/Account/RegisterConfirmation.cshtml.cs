using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArrowOut.Web.Areas.Identity.Pages.Account;

// "Check your inbox" page after signing up (and after asking for a new link).
[AllowAnonymous]
public class RegisterConfirmationModel : PageModel
{
    public const string DevLinkKey = "ConfirmationDevLink";
    public const string SendFailedKey = "ConfirmationSendFailed";

    public string Email { get; private set; } = string.Empty;

    // Only set in Development when there's no SMTP server, so you can test without a real mailbox.
    public string? DevLink { get; private set; }

    public bool SendFailed { get; private set; }

    public IActionResult OnGet(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToPage("./Register");
        }

        Email = email;
        DevLink = TempData[DevLinkKey] as string;
        SendFailed = TempData[SendFailedKey] is true;
        return Page();
    }
}
