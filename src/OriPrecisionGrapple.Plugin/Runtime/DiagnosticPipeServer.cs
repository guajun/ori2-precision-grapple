using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using BepInEx.Logging;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Runtime;

internal sealed class DiagnosticPipeServer : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;
    private string? _latestFrame;
    private long _latestSequence;
    private bool _connectedLogged;

    public DiagnosticPipeServer(ManualLogSource log)
    {
        _log = log;
        _worker = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public void Publish(DiagnosticFrame frame)
    {
        frame.Sequence = Interlocked.Increment(ref _latestSequence);
        frame.TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var json = JsonSerializer.Serialize(frame);
        Volatile.Write(ref _latestFrame, json);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _worker.Wait(1000);
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            // Cancellation is the normal shutdown path.
        }

        _cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    DiagnosticProtocol.PipeName,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                if (!_connectedLogged)
                {
                    _connectedLogged = true;
                    _log.LogInfo($"External diagnostics monitor connected to pipe '{DiagnosticProtocol.PipeName}'.");
                }

                await using var writer = new StreamWriter(
                    pipe,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 4096,
                    leaveOpen: true)
                {
                    AutoFlush = true,
                };

                long sentSequence = 0;
                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var sequence = Volatile.Read(ref _latestSequence);
                    var frame = Volatile.Read(ref _latestFrame);
                    if (frame is not null && sequence != sentSequence)
                    {
                        await writer.WriteLineAsync(frame).ConfigureAwait(false);
                        sentSequence = sequence;
                    }

                    await Task.Delay(DiagnosticProtocol.PublishIntervalMilliseconds, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                _connectedLogged = false;
            }
            catch (Exception exception)
            {
                _connectedLogged = false;
                _log.LogWarning($"Diagnostics pipe will retry after an error: {exception.Message}");
                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
