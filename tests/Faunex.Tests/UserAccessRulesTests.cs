using Faunex.Api.Auth;
using Faunex.Application.Auth;

namespace Faunex.Tests;

public sealed class UserAccessRulesTests
{
    [Fact]
    public void NormalizeRoles_DefaultsToBuyer()
    {
        var roles = UserAccessRules.NormalizeRoles(null);

        Assert.Equal([FaunexRoles.Buyer], roles);
    }

    [Fact]
    public void NormalizeRoles_TrimsAndDeduplicatesRoles()
    {
        var roles = UserAccessRules.NormalizeRoles([" Seller ", "seller", FaunexRoles.TenantAdmin]);

        Assert.Equal(2, roles.Count);
        Assert.Contains("Seller", roles);
        Assert.Contains(FaunexRoles.TenantAdmin, roles);
    }

    [Fact]
    public void ValidateRoleTenantShape_RejectsMixedPlatformAndTenantRoles()
    {
        var result = UserAccessRules.ValidateRoleTenantShape(null, [FaunexRoles.PlatformAdmin, FaunexRoles.Buyer]);

        Assert.False(result.IsValid);
        Assert.Contains("Platform roles cannot be mixed", result.Error);
    }

    [Fact]
    public void ValidateRoleTenantShape_RejectsPlatformUserWithTenant()
    {
        var result = UserAccessRules.ValidateRoleTenantShape(Guid.NewGuid(), [FaunexRoles.PlatformAdmin]);

        Assert.False(result.IsValid);
        Assert.Contains("must not be assigned to a tenant", result.Error);
    }

    [Theory]
    [InlineData(FaunexRoles.TenantAdmin)]
    [InlineData(FaunexRoles.Seller)]
    public void ValidateRoleTenantShape_RequiresTenantForPrivilegedTenantRoles(string role)
    {
        var result = UserAccessRules.ValidateRoleTenantShape(null, [role]);

        Assert.False(result.IsValid);
        Assert.Contains("must be assigned to a tenant", result.Error);
    }

    [Fact]
    public void ValidateRoleTenantShape_AllowsTenantSellerWithTenant()
    {
        var result = UserAccessRules.ValidateRoleTenantShape(Guid.NewGuid(), [FaunexRoles.Seller]);

        Assert.True(result.IsValid);
    }
}
