using Faunex.Api.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Faunex.Tests;

public sealed class LocalListingFileStorageTests
{
    [Fact]
    public async Task Storage_survives_new_instance_never_overwrites_and_rejects_unsafe_keys_or_public_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "faunex-file-test-" + Guid.NewGuid().ToString("N"));
        var environment = new FileEnvironment { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") };
        var options = Options.Create(new FileUploadOptions { RootPath = Path.Combine(root, "private") });
        var storage = new LocalListingFileStorage(options, environment);
        var key = Guid.NewGuid().ToString("N");
        byte[] bytes = [10, 20, 30];
        try
        {
            await storage.WriteAsync(key, bytes, CancellationToken.None);
            await Assert.ThrowsAsync<IOException>(() => storage.WriteAsync(key, [99], CancellationToken.None));
            var reopened = new LocalListingFileStorage(options, environment);
            Assert.Equal(bytes, await reopened.ReadAsync(key, CancellationToken.None));
            Assert.True(reopened.Exists(key));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(options.Value.RootPath!, key[..2], key + ".blob")));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync("../../outside", CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.WriteAsync(new string('x', 32), bytes, CancellationToken.None));
            var publicStore = new LocalListingFileStorage(Options.Create(new FileUploadOptions { RootPath = Path.Combine(environment.WebRootPath, "uploads") }), environment);
            await Assert.ThrowsAsync<InvalidOperationException>(() => publicStore.WriteAsync(key, bytes, CancellationToken.None));
            await reopened.DeleteAsync(key, CancellationToken.None);
            Assert.Null(await storage.ReadAsync(key, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FileEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "FileStorageTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
