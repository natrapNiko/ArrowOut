using System.ComponentModel.DataAnnotations;
using ArrowOut.Data.Models;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace ArrowOut.Web.Areas.Identity.Pages.Account;

// Sends a new confirmation link. The answer is the same whether the address has an account or
// not, so this page can't be used to find out who's registered.
[AllowAnonymous]
public class ResendEmailConfirmationModel(
    UserManager<ApplicationUser> userManager,
    AccountEmails accountEmails,
    IOptions<EmailOptions> emailOptions,
    IWebHostEnvironment environment,
    ILogger<ResendEmailConfirmationModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet(string? email) => Input.Email = email ?? string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is { EmailConfirmed: false })
        {
            await RegisterModel.SendConfirmationAsync(
                user, accountEmails, emailOptions.Value, environment, Url, Request.Scheme, TempData, logger);
        }

        return RedirectToPage("./RegisterConfirmation", new { email });
    }
}
