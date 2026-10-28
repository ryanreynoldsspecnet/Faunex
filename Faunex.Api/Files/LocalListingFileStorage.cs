using Faunex.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Faunex.Api.Files;

public sealed class LocalListingFileStorage(IOptions<FileUploadOptions> options, IWebHostEnvironment environment) : IListingFileStorage
{
    private string FilePath(string key)
    {
        if (key.Length != 32 || key.Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("Invalid storage key.");
        var root = Path.GetFullPath(options.Value.RootPath ?? Path.Combine(environment.ContentRootPath, "App_Data", "listing-files"));
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) || root.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Listing file storage must be outside wwwroot.");
        return Path.Combine(root, key[..2], key + ".blob");
    }

    public async Task WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken)
    {
        var path = FilePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var created = false;
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
            created = true;
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await file.WriteAsync(bytes, cancellationToken);
            await file.FlushAsync(cancellationToken);
        }
        catch { if (created && File.Exists(path)) File.Delete(path); throw; }
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = FilePath(key);
        if (!File.Exists(path)) return null;
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length > FileUploadOptions.MaxUploadBytes) throw new FileServiceUnavailableException("Stored file integrity check failed.");
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
    public Task DeleteAsync(string key, CancellationToken cancellationToken) { File.Delete(FilePath(key)); return Task.CompletedTask; }
    public bool Exists(string key) => File.Exists(FilePath(key));
}
