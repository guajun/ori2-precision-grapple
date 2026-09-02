using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal sealed class PipeMonitorClient
{
    public async Task RunAsync(
        Action<DiagnosticFrame> onFrame,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                onStatus("Waiting for Ori...");
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    DiagnosticProtocol.PipeName,
                    PipeDirection.In,
                    PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);
                onStatus("Connected");

                using var reader = new StreamReader(
                    pipe,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096,
                    leaveOpen: true);

                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }

                    var frame = JsonSerializer.Deserialize<DiagnosticFrame>(line);
                    if (frame is not null)
                    {
                        onFrame(frame);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (TimeoutException)
            {
                // Normal while the game is not running.
            }
            catch (IOException)
            {
                onStatus("Disconnected");
            }
            catch (JsonException exception)
            {
                onStatus($"Frame error: {exception.Message}");
            }

            try
            {
                await Task.Delay(750, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
