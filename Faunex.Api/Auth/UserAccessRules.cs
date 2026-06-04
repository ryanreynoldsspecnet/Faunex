using Faunex.Application.Auth;

namespace Faunex.Api.Auth;

public static class UserAccessRules
{
    public static IReadOnlyCollection<string> NormalizeRoles(IReadOnlyList<string>? requestedRoles) =>
        (requestedRoles is { Count: > 0 } ? requestedRoles : [FaunexRoles.Buyer])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static UserAccessRuleResult ValidateRoleTenantShape(Guid? tenantId, IReadOnlyCollection<string> assignedRoles)
    {
        var invalidRoles = assignedRoles.Except(FaunexRoles.All, StringComparer.OrdinalIgnoreCase).ToArray();
        if (invalidRoles.Length > 0)
        {
            return UserAccessRuleResult.Invalid($"Unsupported roles: {string.Join(", ", invalidRoles)}.");
        }

        var hasPlatformRole = IsPlatformRoleSet(assignedRoles);
        var hasTenantPrivilegedRole = assignedRoles.Any(IsTenantPrivilegedRole);

        if (hasPlatformRole && assignedRoles.Any(x => !IsPlatformRole(x)))
        {
            return UserAccessRuleResult.Invalid("Platform roles cannot be mixed with tenant roles on the same user.");
        }

        if (hasPlatformRole && tenantId.HasValue)
        {
            return UserAccessRuleResult.Invalid("Platform users must not be assigned to a tenant.");
        }

        if (hasTenantPrivilegedRole && tenantId is null)
        {
            return UserAccessRuleResult.Invalid("Tenant administrator and seller users must be assigned to a tenant.");
        }

        return UserAccessRuleResult.Valid();
    }

    public static bool IsPlatformRoleSet(IEnumerable<string> assignedRoles) => assignedRoles.Any(IsPlatformRole);

    public static bool IsPlatformRole(string role) =>
        string.Equals(role, FaunexRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, FaunexRoles.PlatformSuperAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, FaunexRoles.PlatformComplianceAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, FaunexRoles.PlatformSupport, StringComparison.OrdinalIgnoreCase);

    public static bool IsTenantPrivilegedRole(string role) =>
        string.Equals(role, FaunexRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, FaunexRoles.Seller, StringComparison.OrdinalIgnoreCase);
}

public sealed record UserAccessRuleResult(bool IsValid, string? Error)
{
    public static UserAccessRuleResult Valid() => new(true, null);

    public static UserAccessRuleResult Invalid(string error) => new(false, error);
}
