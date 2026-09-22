namespace TypingEngine.Windows;

/// <summary>Un seul worker : une requête manquante ou échouée reste sécurisée.
/// Un worker bloqué n'est jamais multiplié ; un worker terminé peut être relancé.</summary>
internal sealed class SecureInputProbe : IDisposable
{
    private sealed record Result(int Request, bool Secure);
    private sealed record Request(int Id, IntPtr Window, long Deadline);
    private readonly Func<bool> _initialize;
    private readonly Func<IntPtr, bool> _query;
    private readonly int _timeout;
    private readonly int _retryDelay;
    private readonly object _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly AutoResetEvent _done = new(false);
    private Thread? _worker;
    private Request _requested = new(0, IntPtr.Zero, 0);
    private long _retryAfter;
    private volatile bool _stopping;
    private Result _result = new(0, true);
    internal event Action? ResultChangedLate;

    internal SecureInputProbe(Func<bool> initialize, Func<IntPtr, bool> query,
        int timeout = 30, int retryDelay = 1000)
    {
        _initialize = initialize;
        _query = query;
        _timeout = timeout;
        _retryDelay = retryDelay;
    }

    internal bool Query(IntPtr window = default)
    {
        Request request;
        lock (_gate)
        {
            if (_stopping) return true;
            request = new Request(_requested.Id + 1, window, Environment.TickCount64 + _timeout);
            Volatile.Write(ref _requested, request);
            if (_worker?.IsAlive != true && Environment.TickCount64 >= _retryAfter)
            {
                _worker = new Thread(Run) { IsBackground = true, Name = "SecureInputDetector" };
                _worker.Start();
            }
        }
        _wake.Set();
        while (Volatile.Read(ref _result).Request < request.Id)
        {
            long remaining = request.Deadline - Environment.TickCount64;
            if (remaining <= 0 || !_done.WaitOne((int)remaining)) break;
        }
        var result = Volatile.Read(ref _result);
        return result.Request != request.Id || result.Secure;
    }

    private void Run()
    {
        try
        {
            if (!_initialize()) return;
            while (!_stopping)
            {
                _wake.WaitOne();
                if (_stopping) return;
                var request = Volatile.Read(ref _requested);
                bool secure;
                try { secure = _query(request.Window); }
                catch { secure = true; }
                var previous = Volatile.Read(ref _result);
                Volatile.Write(ref _result, new Result(request.Id, secure));
                _done.Set();
                if (Environment.TickCount64 > request.Deadline && previous.Secure != secure)
                {
                    try { ResultChangedLate?.Invoke(); } catch { }
                }
            }
        }
        catch { /* Initialisation indisponible : le délai impose le mode sécurisé. */ }
        finally { Volatile.Write(ref _retryAfter, Environment.TickCount64 + _retryDelay); }
    }

    public void Dispose()
    {
        lock (_gate) { _stopping = true; }
        _wake.Set();
        // Un fournisseur COM peut rester bloqué. Ne pas attendre sa terminaison et
        // ne pas libérer ses événements tant qu'il peut encore les utiliser.
        if (_worker == null || _worker.Join(100)) { _wake.Dispose(); _done.Dispose(); }
    }
}
