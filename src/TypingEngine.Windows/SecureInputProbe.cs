namespace TypingEngine.Windows;

/// <summary>Un seul worker actif : une requête manquante ou échouée reste sécurisée
/// (choix le plus sûr quand on ne sait pas). Un worker occupé n'est pas multiplié, mais
/// il est ABANDONNÉ au bout de <c>abandonAfter</c> ms de blocage dans une requête : une
/// nouvelle génération de worker repart, et le retour tardif de l'ancien est ignoré
/// (il n'écrit ni le résultat ni la levée tardive). Au plus
/// <see cref="MaxAbandonedWorkers"/> workers abandonnés restent bloqués en même temps :
/// au-delà, plus aucun worker n'est lancé (sécurisé) tant qu'un d'eux ne revient pas.
/// Un worker terminé peut être relancé. À toute sortie du fil, abandon compris,
/// <c>workerExit</c> s'exécute sur ce fil : l'appelant y relâche ce qui lui est propre.</summary>
internal sealed class SecureInputProbe : IDisposable
{
    private sealed record Result(int Request, bool Secure);
    private sealed record Request(int Id, IntPtr Window, long Deadline);
    private readonly Func<bool> _initialize;
    private readonly Func<IntPtr, bool> _query;
    private readonly int _timeout;
    private readonly int _retryDelay;
    private readonly int _abandonAfter;
    private readonly Func<long> _clock;
    private readonly Action? _workerExit;
    private readonly object _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly AutoResetEvent _done = new(false);
    private Thread? _worker;
    private Request _requested = new(0, IntPtr.Zero, 0);
    private long _retryAfter;
    private int _generation;
    private long _busySince;
    private int _abandoned;
    private volatile bool _stopping;
    private Result _result = new(0, true);
    internal event Action? ResultChangedLate;

    /// <summary>Workers abandonnés encore bloqués dans leur requête : jamais plus de
    /// <see cref="MaxAbandonedWorkers"/>, donc jamais plus de 4 fils en tout.</summary>
    internal const int MaxAbandonedWorkers = 3;

    /// <summary>Délai de blocage d'une requête avant abandon du worker : 2 s, soit
    /// deux relances sécurisées de ForegroundMonitor (1 s) et plus de quatre fois la
    /// durée « plusieurs centaines de ms » d'un appel UIA lent mais vivant.</summary>
    internal const int DefaultAbandonAfter = 2000;

    internal SecureInputProbe(Func<bool> initialize, Func<IntPtr, bool> query,
        int timeout = 30, int retryDelay = 1000, int abandonAfter = DefaultAbandonAfter,
        Func<long>? clock = null, Action? workerExit = null)
    {
        _initialize = initialize;
        _query = query;
        _timeout = timeout;
        _retryDelay = retryDelay;
        _abandonAfter = abandonAfter;
        _clock = clock ?? (() => Environment.TickCount64);
        _workerExit = workerExit;
    }

    /// <summary>Workers abandonnés qui n'ont pas encore rendu la main (tests, diagnostic).</summary>
    internal int AbandonedWorkers => Volatile.Read(ref _abandoned);

    /// <summary>Identifiant de la requête du dernier résultat accepté (tests).</summary>
    internal int LastResultRequestId => Volatile.Read(ref _result).Request;

    internal bool Query(IntPtr window = default)
    {
        Request request;
        lock (_gate)
        {
            if (_stopping) return true;
            long now = _clock();
            request = new Request(_requested.Id + 1, window, now + _timeout);
            Volatile.Write(ref _requested, request);
            if (_worker?.IsAlive == true && _busySince != 0 && now - _busySince >= _abandonAfter
                && _abandoned < MaxAbandonedWorkers)
            {
                // Worker bloqué : on le laisse mourir seul, sa génération n'est plus la bonne.
                _generation++;
                _abandoned++;
                _busySince = 0;
                _worker = null;
            }
            if (_worker?.IsAlive != true && now >= _retryAfter)
            {
                int generation = _generation;
                _worker = new Thread(() => Run(generation)) { IsBackground = true, Name = "SecureInputDetector" };
                _worker.Start();
            }
        }
        _wake.Set();
        while (Volatile.Read(ref _result).Request < request.Id)
        {
            long remaining = request.Deadline - _clock();
            if (remaining <= 0 || !_done.WaitOne((int)remaining)) break;
        }
        var result = Volatile.Read(ref _result);
        return result.Request != request.Id || result.Secure;
    }

    private void Run(int generation)
    {
        try
        {
            if (!_initialize()) return;
            while (!_stopping)
            {
                _wake.WaitOne();
                if (_stopping) return;
                Request request;
                lock (_gate)
                {
                    if (generation != _generation) return;
                    request = Volatile.Read(ref _requested);
                    _busySince = _clock();
                }
                bool secure;
                try { secure = _query(request.Window); }
                catch { secure = true; }
                bool changedLate;
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        // Worker abandonné : son retour tardif n'écrase rien et ne signale rien.
                        _abandoned--;
                        return;
                    }
                    _busySince = 0;
                    var previous = Volatile.Read(ref _result);
                    // Le retard se mesure à la publication, avant de réveiller l'appelant : mesuré
                    // après _done.Set(), un fil préempté à cet endroit signalait comme tardif un
                    // résultat que l'appelant avait reçu dans le délai.
                    changedLate = _clock() > request.Deadline && previous.Secure != secure;
                    Volatile.Write(ref _result, new Result(request.Id, secure));
                }
                _done.Set();
                if (changedLate)
                {
                    try { ResultChangedLate?.Invoke(); } catch { }
                }
            }
        }
        catch { /* Initialisation indisponible : le délai impose le mode sécurisé. */ }
        finally
        {
            lock (_gate)
            {
                if (generation == _generation)
                    Volatile.Write(ref _retryAfter, _clock() + _retryDelay);
            }
            // Toute sortie du fil (arrêt, abandon, initialisation en échec, exception) relâche ce
            // qui est propre à ce fil : l'instance UIA de l'appelant est [ThreadStatic], donc ni
            // un autre fil ni la sonde ne peuvent la relâcher à sa place.
            try { _workerExit?.Invoke(); } catch { }
        }
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
