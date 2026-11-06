using System.Windows;

namespace PerformanceOverlay;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
            LocalLog.Write("ERROR", $"Unhandled UI exception: {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LocalLog.Write("FATAL", $"Unhandled process exception: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LocalLog.Write("ERROR", $"Unobserved task exception: {args.Exception}");
            args.SetObserved();
        };
        LocalLog.Write("INFO", $"Application starting; version={GetType().Assembly.GetName().Version}; pid={Environment.ProcessId}");
        _instanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\PerformanceOverlay",
            createdNew: out _ownsInstanceMutex);

        if (!_ownsInstanceMutex)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstanceMutex && _instanceMutex is not null)
        {
            try { _instanceMutex.ReleaseMutex(); }
            catch { }
        }

        _instanceMutex?.Dispose();
        _instanceMutex = null;
        base.OnExit(e);
    }
}
