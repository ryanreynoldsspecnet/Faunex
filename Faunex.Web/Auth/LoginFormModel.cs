using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Auth;

public sealed class LoginFormModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
