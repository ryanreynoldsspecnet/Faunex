namespace Faunex.Application.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    bool IsPlatformAdmin { get; }
}
