using Faunex.Domain.Abstractions;
using Faunex.Domain.Enums;

namespace Faunex.Domain.Entities;

public sealed class BirdSpecies : BaseEntity
{
    public string ScientificName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;

    public CITESAppendix CitesAppendix { get; set; } = CITESAppendix.NotListed;

    public bool IsEndangered { get; set; }
    public string? Notes { get; set; }
}
