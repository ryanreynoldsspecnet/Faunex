namespace Faunex.Api.Tenancy;

public interface IDomainOwnershipVerifier
{
    Task<bool> VerifyAsync(string hostname, string token, CancellationToken cancellationToken);
}
