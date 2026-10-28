namespace Faunex.Api.Files;

public sealed class FileUploadOptions
{
    public string? RootPath { get; set; }
    public string? ScannerHost { get; set; }
    public int ScannerPort { get; set; } = 3310;
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxStoredImageBytes = 4 * 1024 * 1024;
    public const int MaxFilesPerListing = 30;
    public const int MaxPhotosPerListing = 10;
    public const long MaxTenantBytes = 1024L * 1024 * 1024;
}

public sealed class FileSecurityException(string message) : Exception(message) { }
public sealed class FileServiceUnavailableException(string message) : Exception(message) { }
public interface IUploadScanner
{
    Task ScanAsync(byte[] bytes, CancellationToken cancellationToken);
}
