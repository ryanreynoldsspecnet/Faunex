namespace Faunex.Api.Email;

public sealed class SmtpEmailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string? FromEmail { get; set; }
    public string? FromName { get; set; } = "Faunex";
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromEmail);
}
