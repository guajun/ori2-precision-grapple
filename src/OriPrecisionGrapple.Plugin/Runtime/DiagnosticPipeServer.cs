using System.IO.Pipes;
using System.Text.Json;
using BepInEx.Logging;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Runtime;

internal sealed class DiagnosticPipeServer : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _frameReady = new(0, 1);
    private readonly Task _worker;
    private DiagnosticFrame? _latestFrame;
    private long _latestSequence;
    private bool _connectedLogged;

    public DiagnosticPipeServer(ManualLogSource log, string? pipeName = null)
    {
        _log = log;
        _pipeName = pipeName ?? DiagnosticProtocol.PipeName;
        _worker = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public void Publish(DiagnosticFrame frame)
    {
        frame.Sequence = Interlocked.Increment(ref _latestSequence);
        frame.TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Volatile.Write(ref _latestFrame, frame);
        if (_frameReady.CurrentCount == 0)
        {
            _frameReady.Release();
        }
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
        _frameReady.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                if (!_connectedLogged)
                {
                    _connectedLogged = true;
                    _log.LogInfo($"External diagnostics monitor connected to pipe '{_pipeName}'.");
                }

                await using var writer = new StreamWriter(pipe, leaveOpen: true)
                {
                    AutoFlush = true,
                };

                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    await _frameReady.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var frame = Volatile.Read(ref _latestFrame);
                    if (frame is not null)
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(frame)).ConfigureAwait(false);
                    }
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
