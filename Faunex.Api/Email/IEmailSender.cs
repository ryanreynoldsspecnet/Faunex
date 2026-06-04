namespace Faunex.Api.Email;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);
}
