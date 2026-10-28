using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace Faunex.Api.Files;

public sealed class ClamAvUploadScanner(IOptions<FileUploadOptions> options) : IUploadScanner
{
    public async Task ScanAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ScannerHost)) throw new FileServiceUnavailableException("File scanning is not configured. Uploads are unavailable.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(options.Value.ScannerHost, options.Value.ScannerPort, timeout.Token);
            await using var stream = tcp.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), timeout.Token);
            var size = new byte[4];
            for (var offset = 0; offset < bytes.Length; offset += 65536)
            {
                var length = Math.Min(65536, bytes.Length - offset);
                BinaryPrimitives.WriteInt32BigEndian(size, length);
                await stream.WriteAsync(size, timeout.Token);
                await stream.WriteAsync(bytes.AsMemory(offset, length), timeout.Token);
            }
            await stream.WriteAsync(new byte[4], timeout.Token);
            var response = new List<byte>();
            var next = new byte[1];
            var terminated = false;
            while (response.Count < 1024)
            {
                if (await stream.ReadAsync(next, timeout.Token) == 0) break;
                if (next[0] == 0) { terminated = true; break; }
                response.Add(next[0]);
            }
            var verdict = Encoding.UTF8.GetString(response.ToArray()).Trim();
            if (terminated && verdict == "stream: OK") return;
            if (verdict.EndsWith(" FOUND", StringComparison.Ordinal)) throw new FileSecurityException("This file did not pass the security scan.");
            throw new FileServiceUnavailableException("The file scanner could not verify this file. Try again later.");
        }
        catch (Exception ex) when (ex is SocketException or IOException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        { throw new FileServiceUnavailableException("The file scanner is starting or unavailable. Please try again shortly."); }
    }
}
