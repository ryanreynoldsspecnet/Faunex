namespace Faunex.Application.DTOs;

public sealed class ListingValidationException(string message) : ArgumentException(message) { }
public sealed class ListingEditConflictException(string message) : InvalidOperationException(message) { }
