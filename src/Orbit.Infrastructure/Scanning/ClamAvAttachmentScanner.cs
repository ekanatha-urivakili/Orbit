using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Orbit.Application.Abstractions;

namespace Orbit.Infrastructure.Scanning;

/// <summary>
/// Malware-scans attachment bytes against a clamd daemon using its native INSTREAM protocol over a
/// plain TCP socket (clamd's documented "zINSTREAM" command - see clamd(8)): the command name is sent
/// null-terminated, then the file is streamed as a sequence of network-byte-order length-prefixed
/// chunks, closed by a zero-length chunk, and clamd replies with a single null-terminated line such
/// as <c>"stream: OK"</c>, <c>"stream: Eicar-Test-Signature FOUND"</c>, or an error message. No
/// client-side scanning library is used - clamd already implements the actual malware signatures.
/// </summary>
internal sealed class ClamAvAttachmentScanner(IOptions<AttachmentScanningOptions> options) : IAttachmentScanner
{
    private const int ChunkSize = 8192;

    public async Task<AttachmentScanResult> ScanAsync(
        Stream content, string fileName, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(settings.ClamAvHost, settings.ClamAvPort, ct);
            await using var stream = client.GetStream();

            var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
            await stream.WriteAsync(command, ct);

            var buffer = new byte[ChunkSize];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, ct)) > 0)
            {
                await WriteChunkAsync(stream, buffer, bytesRead, ct);
            }

            // Zero-length chunk terminates the stream, per clamd's INSTREAM protocol.
            await stream.WriteAsync(new byte[] { 0, 0, 0, 0 }, ct);

            var response = await ReadResponseAsync(stream, ct);
            return Interpret(response);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return AttachmentScanResult.Failed($"Scan of clamd at {settings.ClamAvHost}:{settings.ClamAvPort} timed out.");
        }
        catch (SocketException exception)
        {
            return AttachmentScanResult.Failed($"Could not reach clamd: {exception.Message}");
        }
    }

    private static async Task WriteChunkAsync(
        NetworkStream stream, byte[] buffer, int length, CancellationToken cancellationToken)
    {
        var sizePrefix = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(sizePrefix, (uint)length);
        await stream.WriteAsync(sizePrefix, cancellationToken);
        await stream.WriteAsync(buffer.AsMemory(0, length), cancellationToken);
    }

    private static async Task<string> ReadResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var responseBuffer = new MemoryStream();
        var readBuffer = new byte[256];
        while (true)
        {
            var read = await stream.ReadAsync(readBuffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var terminatorIndex = Array.IndexOf(readBuffer, (byte)0, 0, read);
            if (terminatorIndex >= 0)
            {
                responseBuffer.Write(readBuffer, 0, terminatorIndex);
                break;
            }

            responseBuffer.Write(readBuffer, 0, read);
        }

        return Encoding.ASCII.GetString(responseBuffer.ToArray());
    }

    private static AttachmentScanResult Interpret(string response)
    {
        if (response.Contains("FOUND", StringComparison.Ordinal))
        {
            return AttachmentScanResult.Infected(response.Trim());
        }

        if (response.Contains("OK", StringComparison.Ordinal))
        {
            return AttachmentScanResult.Clean();
        }

        return AttachmentScanResult.Failed(
            string.IsNullOrWhiteSpace(response) ? "clamd returned an empty response." : response.Trim());
    }
}
