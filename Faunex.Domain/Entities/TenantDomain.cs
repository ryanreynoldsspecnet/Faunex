using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class TenantDomain : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Hostname { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public string? VerificationToken { get; set; }
    public DateTimeOffset? VerificationRequestedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
