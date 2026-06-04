namespace Faunex.Api.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName,
    string? RegistrationHost = null
);

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(
    string Message,
    string? ResetToken = null
);

public sealed record ResetPasswordRequest(
    string Email,
    string ResetToken,
    string NewPassword
);

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
