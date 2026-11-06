using Microsoft.AspNetCore.Identity;

namespace Faunex.Api.Auth;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }
    public bool IsPlatformAdmin { get; set; }

    public string? DisplayName { get; set; }
    public string? MfaProtectedKey { get; set; }
    public string? MfaRecoveryHashes { get; set; }
    public long? MfaLastStep { get; set; }
    public Guid? MfaLoginNonce { get; set; }
    public int MfaFailures { get; set; }
    public DateTimeOffset? MfaFailureWindowEnd { get; set; }
}
