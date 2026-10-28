namespace Faunex.Application.Interfaces;

public interface IListingFileStorage
{
    Task WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    bool Exists(string key);
}
