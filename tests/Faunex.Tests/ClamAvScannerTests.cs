using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Faunex.Api.Files;
using Microsoft.Extensions.Options;

namespace Faunex.Tests;

public sealed class ClamAvScannerTests
{
    [Theory]
    [InlineData("stream: OK\0", true)]
    [InlineData("stream: Eicar-Test-Signature FOUND\0", false)]
    [InlineData("stream: size limit exceeded ERROR\0", false)]
    [InlineData("stream: OK", false)]
    public async Task Protocol_streaming_accepts_only_a_complete_clean_result(string verdict, bool clean)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var bytes = Enumerable.Repeat((byte)42, 150000).ToArray();
        var server = Task.Run(async () =>
        {
            using var tcp = await listener.AcceptTcpClientAsync(); await using var stream = tcp.GetStream();
            var command = new byte[10]; await stream.ReadExactlyAsync(command);
            Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            using var received = new MemoryStream();
            var size = new byte[4];
            while (true)
            {
                await stream.ReadExactlyAsync(size); var count = BinaryPrimitives.ReadInt32BigEndian(size);
                if (count == 0) break;
                Assert.InRange(count, 1, 65536);
                var chunk = new byte[count]; await stream.ReadExactlyAsync(chunk); await received.WriteAsync(chunk);
            }
            Assert.Equal(bytes, received.ToArray());
            await stream.WriteAsync(Encoding.ASCII.GetBytes(verdict));
        });
        var scanner = new ClamAvUploadScanner(Options.Create(new FileUploadOptions { ScannerHost = "127.0.0.1", ScannerPort = port }));
        var error = await Record.ExceptionAsync(() => scanner.ScanAsync(bytes, CancellationToken.None));
        if (clean) Assert.Null(error); else Assert.True(error is FileSecurityException or FileServiceUnavailableException);
        await server;
    }

    [ClamAvFact]
    public async Task Official_scanner_accepts_clean_content_and_rejects_Eicar()
    {
        var parts = Environment.GetEnvironmentVariable("FAUNEX_TEST_CLAMAV")!.Split(':');
        var scanner = new ClamAvUploadScanner(Options.Create(new FileUploadOptions { ScannerHost = parts[0], ScannerPort = int.Parse(parts[1]) }));
        await scanner.ScanAsync("Faunex clean scanner integration fixture"u8.ToArray(), CancellationToken.None);
        var eicar = Encoding.ASCII.GetBytes(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        await Assert.ThrowsAsync<FileSecurityException>(() => scanner.ScanAsync(eicar, CancellationToken.None));
    }
}

internal sealed class ClamAvFactAttribute : FactAttribute
{
    public ClamAvFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FAUNEX_TEST_CLAMAV"))) Skip = "Real isolated ClamAV is required; configured in CI.";
    }
}
