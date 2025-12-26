namespace Faunex.Application.DTOs;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total
);
