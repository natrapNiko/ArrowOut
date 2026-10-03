using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using ArrowOut.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ArrowOut.Web.Infrastructure;

// SMTP settings for outgoing mail. Put the real values in user secrets, never in appsettings.json.
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    // The address the mail comes from. With Gmail this has to be your own Gmail address.
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "ArrowOut";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(FromAddress);
}

// Sends real e-mail through the SMTP server from the Email settings.
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var settings = options.Value;

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
        };
        message.To.Add(email);

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = string.IsNullOrEmpty(settings.UserName) ? null : new NetworkCredential(settings.UserName, settings.Password),
        };

        await client.SendMailAsync(message);
    }
}

// Used when no SMTP server is set up. Nothing gets sent, the mail just goes to the log, so you
// can still copy the confirmation link out of the console while developing.
public sealed class LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        logger.LogWarning(
            "No SMTP server set up (Email:SmtpHost), so this e-mail was NOT sent. To: {Email}. Subject: {Subject}. Body: {Body}",
            email, subject, htmlMessage);
        return Task.CompletedTask;
    }
}

// Builds and sends the "confirm your e-mail" message. Used by sign-up and by the resend page.
public sealed class AccountEmails(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
{
    // Returns the confirmation link, so Development can show it on the page when there's no SMTP.
    public async Task<string> SendConfirmationAsync(ApplicationUser user, IUrlHelper url, string scheme)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(url);

        // The token has characters like + and /, so encode it to be safe in a URL.
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { area = "Identity", userId = user.Id, code }, protocol: scheme)
            ?? throw new InvalidOperationException("Could not build the confirmation link.");

        var name = HtmlEncoder.Default.Encode(user.PublicName);
        var href = HtmlEncoder.Default.Encode(link);
        var body = $"""
            <p>Hi {name},</p>
            <p>Thanks for signing up for ArrowOut! Click the link below to confirm your e-mail address and start playing:</p>
            <p><a href="{href}">Confirm my e-mail</a></p>
            <p>The link works for 24 hours. If you didn't create an account, just ignore this e-mail.</p>
            """;

        await emailSender.SendEmailAsync(user.Email!, "Confirm your ArrowOut account", body);
        return link;
    }
}
