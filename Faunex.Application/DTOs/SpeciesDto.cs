using Faunex.Domain.Enums;

namespace Faunex.Application.DTOs;

public sealed record SpeciesDto(
    Guid Id,
    string ScientificName,
    string CommonName,
    CITESAppendix CitesAppendix,
    bool IsEndangered,
    string? Notes
);
