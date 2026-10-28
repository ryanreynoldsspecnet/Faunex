using System.Collections.Concurrent;
using Faunex.Api.Files;
using Faunex.Application.Interfaces;

namespace Faunex.Tests;

internal sealed class TestFileStorage : IListingFileStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();
    public Task WriteAsync(string key, byte[] bytes, CancellationToken ct) { Assert.True(Files.TryAdd(key, bytes.ToArray())); return Task.CompletedTask; }
    public Task<byte[]?> ReadAsync(string key, CancellationToken ct) => Task.FromResult(Files.TryGetValue(key, out var bytes) ? bytes.ToArray() : null);
    public Task DeleteAsync(string key, CancellationToken ct) { Files.TryRemove(key, out _); return Task.CompletedTask; }
    public bool Exists(string key) => Files.ContainsKey(key);
}

internal sealed class TestUploadScanner : IUploadScanner
{
    public bool Reject { get; set; }
    public bool Unavailable { get; set; }
    public Func<Task>? Handler { get; set; }
    public async Task ScanAsync(byte[] bytes, CancellationToken ct)
    {
        if (Handler is not null) await Handler();
        if (Reject) throw new FileSecurityException("This file did not pass the security scan.");
        if (Unavailable) throw new FileServiceUnavailableException("Scanner unavailable.");
    }
}
