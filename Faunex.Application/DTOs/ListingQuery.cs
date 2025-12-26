namespace Faunex.Application.DTOs;

public sealed record ListingQuery(
    string? AnimalClass,
    Guid? SpeciesId,
    string? Location,
    bool? ActiveOnly,
    int Skip,
    int Take
);
