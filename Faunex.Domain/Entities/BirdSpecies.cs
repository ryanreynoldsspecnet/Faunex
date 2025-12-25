using StormBird.Domain.Abstractions;
using StormBird.Domain.Enums;

namespace StormBird.Domain.Entities;

public sealed class BirdSpecies : BaseEntity
{
    public string ScientificName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;

    public CITESAppendix CitesAppendix { get; set; } = CITESAppendix.NotListed;

    public bool IsEndangered { get; set; }
    public string? Notes { get; set; }
}
