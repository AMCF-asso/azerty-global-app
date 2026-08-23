using System.Runtime.InteropServices;

namespace TypingEngine.Windows;

/// <summary>
/// Détection UI Automation des champs mot de passe que le style natif ES_PASSWORD
/// ne couvre pas (Chromium, Firefox, applications modernes sans HWND enfant).
///
/// Contrainte centrale : GetFocusedElement est un appel COM cross-process qui peut
/// bloquer plusieurs centaines de ms face à une application occupée, et le thread
/// principal héberge le hook WH_KEYBOARD_LL — le bloquer ferait retarder ou perdre
/// des frappes pour tout le système. Toutes les requêtes UIA partent donc d'un
/// thread de travail dédié (MTA, recommandé pour les clients UIA) ; l'appelant
/// attend au plus <see cref="QueryTimeoutMilliseconds"/> puis retombe sur la
/// dernière valeur connue. Détection best-effort assumée : le chemin ES_PASSWORD
/// de RealWin32Api reste la source sûre pour les contrôles classiques, et un
/// résultat tardif qui change la donne est signalé via <see cref="ResultChangedLate"/>.
///
/// Interop par vtable COM brute (function pointers unmanaged : AOT-safe, aucune
/// génération de stub au runtime, pas de dépendance WPF/WinForms). Slots vérifiés
/// contre UIAutomationClient.h du SDK Windows local (10.0.26100.0) :
/// IUnknown = QueryInterface 0, AddRef 1, Release 2 ;
/// IUIAutomation::GetFocusedElement = slot 8 ;
/// IUIAutomationElement::get_CurrentIsPassword = slot 35.
/// </summary>
public static unsafe class SecureInputDetector
{
    private const int QueryTimeoutMilliseconds = 30;
    private const uint COINIT_MULTITHREADED = 0x0;
    private const uint CLSCTX_INPROC_SERVER = 0x1;

    private const int GetFocusedElementSlot = 8;
    private const int GetCurrentIsPasswordSlot = 35;
    private const int ReleaseSlot = 2;

    // CUIAutomation / IUIAutomation, définis par UIAutomationClient.h.
    private static readonly Guid ClsidCuiAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");
    private static readonly Guid IidIUiAutomation = new("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE");

    private static readonly object _startGate = new();
    private static readonly AutoResetEvent _wake = new(false);
    private static readonly AutoResetEvent _done = new(false);
    private static Thread? _worker;

    private static int _requestId;
    private static int _completedId;
    private static long _callerDeadline;
    private static bool _lastIsPassword;

    // Instance IUIAutomation mise en cache sur le worker (CoCreateInstance à chaque
    // focus serait du gaspillage) ; recréée après tout échec.
    private static IntPtr _automation;

    /// <summary>
    /// Déclenché depuis le thread de travail quand une requête terminée après le
    /// timeout de l'appelant change la valeur : le snapshot du ForegroundMonitor
    /// est alors périmé et mérite un Recompute. Peut arriver sur n'importe quel
    /// thread — l'abonné doit se contenter d'un PostMessage.
    /// </summary>
    public static event Action? ResultChangedLate;

    /// <summary>
    /// Interroge UIA depuis le thread de travail et retourne le résultat s'il
    /// arrive dans le budget, sinon la dernière valeur connue. Jamais bloquant
    /// au-delà de <see cref="QueryTimeoutMilliseconds"/>.
    /// </summary>
    public static bool IsFocusedElementPassword()
    {
        EnsureWorker();

        int id = Interlocked.Increment(ref _requestId);
        long deadline = Environment.TickCount64 + QueryTimeoutMilliseconds;
        Volatile.Write(ref _callerDeadline, deadline);
        _wake.Set();

        // Le worker peut signaler _done pour une requête antérieure : re-attendre
        // jusqu'à ce que NOTRE requête soit traitée ou que le budget soit épuisé.
        while (Volatile.Read(ref _completedId) < id)
        {
            long remaining = deadline - Environment.TickCount64;
            if (remaining <= 0 || !_done.WaitOne((int)remaining))
                break;
        }

        return Volatile.Read(ref _lastIsPassword);
    }

    private static void EnsureWorker()
    {
        if (_worker != null) return;
        lock (_startGate)
        {
            if (_worker != null) return;
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "AZERTYGlobal.SecureInputDetector"
            };
            thread.Start();
            _worker = thread;
        }
    }

    private static void WorkerLoop()
    {
        // MTA : recommandé pour les clients UI Automation, et sans pompe de
        // messages à entretenir. Jamais CoUninitialize : thread background,
        // le teardown du process s'en charge.
        int hr = CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
        if (hr < 0) return;

        while (true)
        {
            _wake.WaitOne();

            // Coalescence : traiter uniquement la requête la plus récente.
            int id = Volatile.Read(ref _requestId);
            bool result = QueryFocusedElementIsPassword();

            bool late = Environment.TickCount64 > Volatile.Read(ref _callerDeadline);
            bool changed = Volatile.Read(ref _lastIsPassword) != result;

            Volatile.Write(ref _lastIsPassword, result);
            Volatile.Write(ref _completedId, id);
            _done.Set();

            // L'appelant a dépassé son budget ET la valeur change : son snapshot
            // est faux, prévenir pour qu'un Recompute rattrape. Une notification
            // en trop est bénigne (le Recompute relit la valeur fraîche et
            // converge, aucune boucle possible puisque changed redevient false).
            if (late && changed)
                ResultChangedLate?.Invoke();
        }
    }

    private static bool QueryFocusedElementIsPassword()
    {
        IntPtr element = IntPtr.Zero;
        try
        {
            if (_automation == IntPtr.Zero)
            {
                Guid clsid = ClsidCuiAutomation;
                Guid iid = IidIUiAutomation;
                int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER,
                    ref iid, out _automation);
                if (hr < 0 || _automation == IntPtr.Zero)
                {
                    _automation = IntPtr.Zero;
                    return false;
                }
            }

            var getFocused = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)
                VTableSlot(_automation, GetFocusedElementSlot);
            IntPtr focused;
            int hrFocused = getFocused(_automation, &focused);
            if (hrFocused < 0 || focused == IntPtr.Zero)
            {
                // Échec possible durable (objet cassé après un crash serveur UIA) :
                // repartir d'une instance neuve à la prochaine requête.
                if (hrFocused < 0)
                    ReleaseAutomation();
                return false;
            }
            element = focused;

            var getIsPassword = (delegate* unmanaged[Stdcall]<IntPtr, int*, int>)
                VTableSlot(element, GetCurrentIsPasswordSlot);
            int isPassword;
            int hrPassword = getIsPassword(element, &isPassword);
            return hrPassword >= 0 && isPassword != 0;
        }
        catch
        {
            // Best-effort : ne jamais tuer le worker, ne rien logger par focus
            // (bruit + contexte d'utilisation sensible).
            ReleaseAutomation();
            return false;
        }
        finally
        {
            ReleaseComPointer(element);
        }
    }

    private static void ReleaseAutomation()
    {
        ReleaseComPointer(_automation);
        _automation = IntPtr.Zero;
    }

    private static IntPtr VTableSlot(IntPtr instance, int slot)
    {
        IntPtr vtable = *(IntPtr*)instance;
        return *(IntPtr*)(vtable + slot * IntPtr.Size);
    }

    private static void ReleaseComPointer(IntPtr instance)
    {
        if (instance == IntPtr.Zero) return;
        try
        {
            var release = (delegate* unmanaged[Stdcall]<IntPtr, uint>)
                VTableSlot(instance, ReleaseSlot);
            release(instance);
        }
        catch { }
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context,
        ref Guid iid, out IntPtr instance);
}
