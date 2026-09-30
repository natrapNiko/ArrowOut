using System.ComponentModel.DataAnnotations;
using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArrowOut.Web.Areas.Identity.Pages.Account;

// Our own sign-up page. There's no e-mail confirmation (RequireConfirmedAccount = false),
// so new players are logged in right away.
[AllowAnonymous]
public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IThemeResolver themeResolver,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = "/";

    public sealed class InputModel
    {
        [Display(Name = "Display name (optional)")]
        [StringLength(DataConstants.User.DisplayNameMaxLength, MinimumLength = DataConstants.User.DisplayNameMinLength,
            ErrorMessage = "Use {2}–{1} characters.")]
        [RegularExpression(DataConstants.User.DisplayNamePattern, ErrorMessage = "Use letters, digits, spaces, dots, dashes or underscores.")]
        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a password.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least {2} characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repeat your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Repeat password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

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

        var email = Input.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(Input.DisplayName) ? null : Input.DisplayName.Trim(),
        };

        // Keep the dark/light mode they picked before signing up.
        var theme = await themeResolver.ResolveAsync(HttpContext);
        if (theme.Mode is not null)
        {
            user.ThemeKey = theme.Initial.Key;
        }

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code switch
                {
                    nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) or
                        nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName) => "Input.Email",
                    _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => "Input.Password",
                    _ => string.Empty,
                };

                // User name and e-mail are the same thing here, so only show the error once.
                if (error.Code == nameof(IdentityErrorDescriber.DuplicateUserName) && result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
                {
                    continue;
                }

                ModelState.AddModelError(field, error.Description);
            }

            return Page();
        }

        logger.LogInformation("New player account created.");
        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(ReturnUrl);
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
}
