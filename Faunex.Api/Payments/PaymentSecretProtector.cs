using Microsoft.AspNetCore.DataProtection;

namespace Faunex.Api.Payments;

public interface IPaymentSecretProtector
{
    string Protect(Guid tenantId, string value);
    string Unprotect(Guid tenantId, string value);
}

public sealed class PaymentSecretProtector : IPaymentSecretProtector
{
    private readonly IDataProtectionProvider _provider;
    public PaymentSecretProtector(IConfiguration configuration)
    {
        var path = configuration["Payments:KeyPath"] ?? "/var/lib/faunex/payment-keys";
        _provider = DataProtectionProvider.Create(new DirectoryInfo(path), builder => builder.SetApplicationName("Faunex.Payments"));
    }
    public PaymentSecretProtector(IDataProtectionProvider provider) => _provider = provider;
    private IDataProtector For(Guid tenantId) => _provider.CreateProtector("PayFast.Sandbox.v1", tenantId.ToString("N"));
    public string Protect(Guid tenantId, string value) => For(tenantId).Protect(value);
    public string Unprotect(Guid tenantId, string value) => For(tenantId).Unprotect(value);
}
