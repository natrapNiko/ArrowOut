using System.ComponentModel.DataAnnotations;
using ArrowOut.Data.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArrowOut.Web.Areas.Identity.Pages.Account;

// Our own sign-in page instead of the default Identity one.
[AllowAnonymous]
public class LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = "/";

    [TempData]
    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        // Clear any half-done external login so it can't mix with this one.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = SafeReturnUrl(returnUrl);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = SafeReturnUrl(returnUrl);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Wrong passwords count towards the lockout from Program.cs (5 tries, then 10 minutes).
        var result = await signInManager.PasswordSignInAsync(
            Input.Email.Trim(), Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("User signed in.");
            return LocalRedirect(ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("./LoginWith2fa", new { ReturnUrl, Input.RememberMe });
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
        }

        // Same message whether the user doesn't exist or the password is wrong, so nobody can check which e-mails have accounts.
        ModelState.AddModelError(string.Empty, "That email and password don't match. Please try again.");
        return Page();
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
}
