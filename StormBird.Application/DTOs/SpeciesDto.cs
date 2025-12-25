using StormBird.Domain.Enums;

namespace StormBird.Application.DTOs;

public sealed record SpeciesDto(
    Guid Id,
    string ScientificName,
    string CommonName,
    CITESAppendix CitesAppendix,
    bool IsEndangered,
    string? Notes
);
