using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace BulkExtensionChanger.Services;

public static class SingleInstanceIpc
{
    private static readonly int SessionId = Process.GetCurrentProcess().SessionId;
    public static string MutexName => $"Local\\BulkExtensionChanger_SingleInstance_S{SessionId}";
    public static string PipeName => $"BulkExtensionChanger_Pipe_S{SessionId}";

    public static bool TryAcquirePrimary(out Mutex mutex)
    {
        mutex = new Mutex(initiallyOwned: false, MutexName, out _);
        try
        {
            return mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    public static void StartPipeServer(Action<IReadOnlyList<string>> onMessageReceived, CancellationToken cancellationToken)
    {
        // Run multiple concurrent accept loops so bursts of 20+ Explorer launches connect immediately
        for (int i = 0; i < 4; i++)
        {
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
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

                        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                        var connectedServer = server;
                        server = null;
                        _ = Task.Run(() => ReadClientPathsAsync(connectedServer, onMessageReceived), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        server?.Dispose();
                        break;
                    }
                    catch
                    {
                        server?.Dispose();
                        await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                    }
                }
            }, cancellationToken);
        }
    }

    private static async Task ReadClientPathsAsync(
        NamedPipeServerStream server,
        Action<IReadOnlyList<string>> onMessageReceived)
    {
        using (server)
        {
            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var paths = new List<string>();
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        paths.Add(line);
                }

                onMessageReceived(paths);
            }
            catch
            {
                // Ignore broken pipe client
            }
        }
    }

    public static bool SendPathsToPrimary(IReadOnlyList<string> paths, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(150);

                using (var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true))
                {
                    foreach (var path in paths)
                    {
                        if (!string.IsNullOrWhiteSpace(path))
                            writer.WriteLine(path);
                    }
                    writer.Flush();
                }
                return true;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(15);
            }
            catch (IOException)
            {
                Thread.Sleep(15);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
