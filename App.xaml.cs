using System.Windows;
using System.Windows.Threading;
using BulkExtensionChanger.Services;
using BulkExtensionChanger.Shell;

namespace BulkExtensionChanger;

public partial class App : Application
{
    private readonly List<string> _initialPaths;
    private readonly bool _startAsComServer;
    private readonly object _pendingLock = new();
    private readonly List<string> _pendingPipePaths = [];

    private ComDropServer? _comDropServer;
    private DispatcherTimer? _comIdleTimer;
    private MainWindow? _mainWindow;

    public App() : this([], false)
    {
    }

    public App(IReadOnlyList<string> initialPaths, bool startAsComServer)
    {
        _initialPaths = [.. initialPaths];
        _startAsComServer = startAsComServer;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (_startAsComServer)
        {
            StartAsComDropServer();
            return;
        }

        ShowOrUpdateMainWindow(_initialPaths);
    }

    public void OnIpcPathsReceived(IReadOnlyList<string> paths)
    {
        lock (_pendingLock)
        {
            if (_mainWindow is null)
            {
                _pendingPipePaths.AddRange(paths);
                return;
            }
        }

        Dispatcher.BeginInvoke(() =>
        {
            _mainWindow?.AddPathsFromExternal(paths);
        });
    }

    private void StartAsComDropServer()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _comDropServer = new ComDropServer();
        _comDropServer.Start(droppedPaths =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _comIdleTimer?.Stop();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                ShowOrUpdateMainWindow(droppedPaths);
            });
        });

        _comIdleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _comIdleTimer.Tick += (_, _) =>
        {
            _comIdleTimer.Stop();
            if (_mainWindow is null)
            {
                _comDropServer?.Dispose();
                _comDropServer = null;
                Shutdown(0);
            }
        };
        _comIdleTimer.Start();
    }

    private void ShowOrUpdateMainWindow(IReadOnlyList<string> paths)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AddPathsFromExternal(paths);
            return;
        }

        List<string> combined;
        lock (_pendingLock)
        {
            combined = new List<string>(paths.Count + _pendingPipePaths.Count);
            combined.AddRange(paths);
            combined.AddRange(_pendingPipePaths);
            _pendingPipePaths.Clear();

            _mainWindow = new MainWindow(combined);
        }

        MainWindow = _mainWindow;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _comIdleTimer?.Stop();
        _comDropServer?.Dispose();
        base.OnExit(e);
    }
}
