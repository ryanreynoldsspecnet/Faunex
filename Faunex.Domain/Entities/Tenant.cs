using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
