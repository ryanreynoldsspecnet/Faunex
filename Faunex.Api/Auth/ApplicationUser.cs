using Microsoft.AspNetCore.Identity;

namespace Faunex.Api.Auth;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }
    public bool IsPlatformAdmin { get; set; }
}
