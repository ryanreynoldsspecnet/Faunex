using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Faunex.Api.Email;

public sealed class SmtpEmailSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpEmailOptions _options = options.Value;

    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default) =>
        SendMarketplaceNoticeAsync(toEmail, "Reset your Faunex password", BuildPasswordResetBody(resetLink), cancellationToken);

    public async Task SendMarketplaceNoticeAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new EmailDeliveryException("SMTP email is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail!, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(_options.Host!, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Faunex email sent");
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            throw new EmailDeliveryException("Faunex email could not be sent.", ex);
        }
    }

    private static string BuildPasswordResetBody(string resetLink) =>
        $"""
        A password reset was requested for your Faunex account.

        Use this link to choose a new password:
        {resetLink}

        If you did not request this, you can ignore this email.

        Faunex
        """;
}
