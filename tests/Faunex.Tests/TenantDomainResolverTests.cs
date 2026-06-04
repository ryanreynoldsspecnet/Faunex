using Faunex.Api.Tenancy;

namespace Faunex.Tests;

public sealed class TenantDomainResolverTests
{
    [Theory]
    [InlineData("Example.Co.Za", "example.co.za")]
    [InlineData("https://Auctions.Example.Co.Za/register", "auctions.example.co.za")]
    [InlineData("example.co.za:443", "example.co.za")]
    [InlineData("example.co.za.", "example.co.za")]
    public void NormalizeHost_ReturnsCanonicalHostname(string input, string expected)
    {
        var result = TenantDomainResolver.NormalizeHost(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    public void NormalizeHost_ReturnsNullForMissingOrInvalidHosts(string? input)
    {
        var result = TenantDomainResolver.NormalizeHost(input);

        Assert.Null(result);
    }
}
