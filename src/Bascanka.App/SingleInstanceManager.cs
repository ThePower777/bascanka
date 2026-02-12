using System.IO.Pipes;
using System.Text;

namespace Bascanka.App;

/// <summary>
/// Manages single-instance behavior using named pipes.
/// When files are passed as arguments and an existing instance is running,
/// the files are sent to the existing instance via a named pipe.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string PipeName = "Bascanka_Pipe";
    private const int ConnectTimeoutMs = 1000;

    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// Attempts to send file paths to an already-running Bascanka instance.
    /// Returns <c>true</c> if the files were sent successfully (caller should exit).
    /// Returns <c>false</c> if no instance is listening (caller should start normally).
    /// </summary>
    public static bool TrySendFiles(string[] files)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);

            string payload = string.Join("\n", files);
            byte[] data = Encoding.UTF8.GetBytes(payload);
            client.Write(data, 0, data.Length);
            client.Flush();
            return true;
        }
        catch
        {
            // No server listening or connection failed — no existing instance.
            return false;
        }
    }

    /// <summary>
    /// Starts a background named pipe server that listens for file paths
    /// from other Bascanka instances. The callback is invoked (on a background thread)
    /// with the received file paths.
    /// </summary>
    public void StartListening(Action<string[]> onFilesReceived)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _listenTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string content = await reader.ReadToEndAsync(token);

                    string[] files = content
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (files.Length > 0)
                        onFilesReceived(files);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Transient error; retry after a short delay.
                    try { await Task.Delay(100, token); }
                    catch (OperationCanceledException) { break; }
                }
                finally
                {
                    server?.Dispose();
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _cts?.Cancel();

        // Unblock WaitForConnectionAsync by connecting and immediately closing.
        try
        {
            using var dummy = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            dummy.Connect(100);
        }
        catch
        {
            // Ignore — server may have already stopped.
        }

        try { _listenTask?.Wait(500); }
        catch { /* ignore */ }

        _cts?.Dispose();
    }
}
