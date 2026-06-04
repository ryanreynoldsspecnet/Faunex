using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Auth;

public sealed class ForgotPasswordFormModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
