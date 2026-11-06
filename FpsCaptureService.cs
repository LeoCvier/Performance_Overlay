using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System.Diagnostics;

namespace PerformanceOverlay;

/// <summary>Reads DirectX present events directly from Windows ETW.</summary>
internal sealed class FpsCaptureService : IDisposable
{
    public const int DesktopCompositionProcessId = -1;
    private static readonly Guid DxgKrnlProvider = new("802EC45A-1E99-4B83-9920-87C98277BA9D");
    private const ulong PresentKeyword = 0x08000000;

    private readonly Action<int, double> _onFps;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private readonly Dictionary<int, Queue<double>> _frameTimes = new();
    private readonly Dictionary<int, double> _lastPublish = new();
    private readonly HashSet<int> _desktopCompositorPids = Process.GetProcessesByName("dwm")
        .Select(process => process.Id)
        .ToHashSet();
    private int _loggedEvents;
    private int _loggedDwmEvent;

    public FpsCaptureService(Action<int, double> onFps, Action<string> log)
    {
        _onFps = onFps;
        _log = log;
        _worker = Task.Factory.StartNew(Run, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void Run()
    {
        var sessionName = $"PerformanceOverlayFps-{Environment.ProcessId}-{Guid.NewGuid():N}";
        try
        {
            using var session = new TraceEventSession(sessionName, TraceEventSessionOptions.Create);
            session.StopOnDispose = true;
            session.Source.Dynamic.All += OnEvent;
            // TraceEvent returns true only when it had to restart an already
            // existing session. False is the normal result for a newly started
            // session; it does not mean provider startup failed.
            session.EnableProvider(DxgKrnlProvider, TraceEventLevel.Verbose, PresentKeyword);

            _log($"Native FPS ETW capture started; session={sessionName}; provider={DxgKrnlProvider}; keyword=0x{PresentKeyword:X}");
            using var registration = _stop.Token.Register(() =>
            {
                try { session.Stop(noThrow: true); } catch { }
            });
            session.Source.Process();
        }
        catch (Exception ex)
        {
            if (!_stop.IsCancellationRequested)
                _log($"Native FPS ETW capture failed; error={ex}");
        }
        finally
        {
            _log("Native FPS ETW capture stopped.");
        }
    }

    private void OnEvent(TraceEvent traceEvent)
    {
        if (traceEvent.ProviderGuid != DxgKrnlProvider || (int)traceEvent.ID != 0x00B8)
            return;

        var processId = traceEvent.ProcessID;
        var timestamp = traceEvent.TimeStampRelativeMSec;
        if (processId <= 0 || !double.IsFinite(timestamp) || timestamp < 0)
            return;

        var isDesktopCompositor = _desktopCompositorPids.Contains(processId);
        if (_loggedEvents++ < 12)
            _log($"Native FPS present event; pid={processId}; event={traceEvent.EventName}; timestampMs={timestamp:0.###}");
        if (isDesktopCompositor && Interlocked.Exchange(ref _loggedDwmEvent, 1) == 0)
            _log($"Native FPS DWM present event observed; pid={processId}; event={traceEvent.EventName}");

        if (!_frameTimes.TryGetValue(processId, out var frames))
        {
            frames = new Queue<double>();
            _frameTimes[processId] = frames;
        }

        frames.Enqueue(timestamp);
        while (frames.Count > 0 && timestamp - frames.Peek() > 1000)
            frames.Dequeue();

        if (_lastPublish.TryGetValue(processId, out var lastPublished) && timestamp - lastPublished < 250)
            return;
        _lastPublish[processId] = timestamp;

        if (frames.Count < 2)
            return;

        var spanMs = timestamp - frames.Peek();
        if (spanMs <= 0)
            return;

        var fps = (frames.Count - 1) * 1000.0 / spanMs;
        if (double.IsFinite(fps) && fps > 0 && fps < 10000)
        {
            _onFps(isDesktopCompositor ? DesktopCompositionProcessId : processId, fps);
        }
    }

    public void Dispose()
    {
        if (_stop.IsCancellationRequested)
            return;

        _stop.Cancel();
        try { _worker.Wait(TimeSpan.FromSeconds(3)); } catch { }
        _stop.Dispose();
    }
}
