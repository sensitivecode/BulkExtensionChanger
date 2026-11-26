using BulkExtensionChanger.Services;
using BulkExtensionChanger.Shell;

namespace BulkExtensionChanger;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase)))
        {
            ShellRegistrationService.Install();
            return 0;
        }

        if (args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            ShellRegistrationService.Uninstall();
            return 0;
        }

        var isComServer = args.Any(a =>
            string.Equals(a, "--com-server", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-Embedding", StringComparison.OrdinalIgnoreCase));

        var initialPaths = args.Where(a =>
            !string.Equals(a, "--com-server", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "-Embedding", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)).ToList();

        if (!SingleInstanceIpc.TryAcquirePrimary(out var primaryMutex))
        {
            primaryMutex.Dispose();

            if (isComServer)
            {
                return RunSecondaryComForwarder();
            }

            SingleInstanceIpc.SendPathsToPrimary(initialPaths);
            return 0;
        }

        try
        {
            using var cts = new CancellationTokenSource();
            var app = new App(initialPaths, isComServer);
            SingleInstanceIpc.StartPipeServer(app.OnIpcPathsReceived, cts.Token);
            app.InitializeComponent();
            var exitCode = app.Run();
            cts.Cancel();
            return exitCode;
        }
        finally
        {
            try { primaryMutex.ReleaseMutex(); } catch { }
            primaryMutex.Dispose();
        }
    }

    private static int RunSecondaryComForwarder()
    {
        // Minimal Win32 STA pump just to receive one Drop() and forward paths to the primary window
        using var done = new ManualResetEventSlim(false);
        using var comServer = new ComDropServer();

        IReadOnlyList<string>? dropped = null;
        comServer.Start(paths =>
        {
            dropped = paths;
            done.Set();
        });

        var deadline = Environment.TickCount64 + 10000;
        while (!done.IsSet && Environment.TickCount64 < deadline)
        {
            PumpPendingMessages();
            Thread.Sleep(10);
        }

        if (dropped is not null)
        {
            SingleInstanceIpc.SendPathsToPrimary(dropped);
        }

        return 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMsg lpMsg);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMsg lpMsg);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeMsg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    private static void PumpPendingMessages()
    {
        while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }
}
