namespace Faunex.Api.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    Guid? TenantId,
    bool IsPlatformAdmin,
    IReadOnlyList<string> Roles
);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid ActorId,
    string Email,
    Guid? TenantId,
    bool IsPlatformAdmin,
    IReadOnlyList<string> Roles
);

public sealed record MeResponse(
    Guid ActorId,
    string Email,
    Guid? TenantId,
    bool IsPlatformAdmin,
    IReadOnlyList<string> Roles
);
