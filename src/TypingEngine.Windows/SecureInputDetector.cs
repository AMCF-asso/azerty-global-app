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
/// protection des fonctions avancées. Le chemin ES_PASSWORD
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
    private const int GetCurrentNativeWindowHandleSlot = 36;
    private const int GetRawViewWalkerSlot = 16;
    private const int GetParentElementSlot = 3;
    private const int ReleaseSlot = 2;

    // CUIAutomation / IUIAutomation, définis par UIAutomationClient.h.
    private static readonly Guid ClsidCuiAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");
    private static readonly Guid IidIUiAutomation = new("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE");

    private static readonly SecureInputProbe Probe = new(
        () => CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED) >= 0,
        QueryFocusedElementIsPassword, QueryTimeoutMilliseconds);

    // Instance IUIAutomation mise en cache sur le worker (CoCreateInstance à chaque
    // focus serait du gaspillage) ; recréée après tout échec.
    private static IntPtr _automation;

    /// <summary>
    /// Déclenché depuis le thread de travail quand une requête terminée après le
    /// timeout de l'appelant change la valeur : le snapshot du ForegroundMonitor
    /// est alors périmé et mérite un Recompute. Peut arriver sur n'importe quel
    /// thread — l'abonné doit se contenter d'un PostMessage.
    /// </summary>
    public static event Action? ResultChangedLate
    {
        add => Probe.ResultChangedLate += value;
        remove => Probe.ResultChangedLate -= value;
    }

    /// <summary>Retour frais dans le budget ; sinon protection des fonctions avancées.</summary>
    public static bool IsFocusedElementPassword(IntPtr window) => Probe.Query(window);

    private static bool QueryFocusedElementIsPassword(IntPtr window)
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
                    return true;
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
                return true;
            }
            element = focused;

            var getIsPassword = (delegate* unmanaged[Stdcall]<IntPtr, int*, int>)
                VTableSlot(element, GetCurrentIsPasswordSlot);
            int isPassword;
            int hrPassword = getIsPassword(element, &isPassword);
            return hrPassword < 0 || isPassword != 0 || !BelongsToWindow(element, window);
        }
        catch
        {
            // Best-effort : ne jamais tuer le worker, ne rien logger par focus
            // (bruit + contexte d'utilisation sensible).
            ReleaseAutomation();
            return true;
        }
        finally
        {
            ReleaseComPointer(element);
        }
    }

    // Un résultat « non sécurisé » doit appartenir à la fenêtre attendue. Le PID
    // seul ne suffit pas : plusieurs fenêtres d'un navigateur partagent un processus.
    // Slots vérifiés dans UIAutomationClient.h (SDK 10.0.26100.0). Aucun texte lu.
    private static bool BelongsToWindow(IntPtr element, IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        IntPtr walker = IntPtr.Zero;
        IntPtr ownedParent = IntPtr.Zero;
        try
        {
            var getWalker = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)
                VTableSlot(_automation, GetRawViewWalkerSlot);
            if (getWalker(_automation, &walker) < 0 || walker == IntPtr.Zero) return false;
            IntPtr current = element;
            // Borne la traversée même face à un fournisseur UIA défectueux.
            for (int depth = 0; depth < 64 && current != IntPtr.Zero; depth++)
            {
                var getWindow = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)
                    VTableSlot(current, GetCurrentNativeWindowHandleSlot);
                IntPtr nativeWindow = IntPtr.Zero;
                if (getWindow(current, &nativeWindow) < 0) return false;
                if (nativeWindow == window) return true;
                var getParent = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>)
                    VTableSlot(walker, GetParentElementSlot);
                IntPtr parent = IntPtr.Zero;
                int hr = getParent(walker, current, &parent);
                ReleaseComPointer(ownedParent);
                ownedParent = parent;
                if (hr < 0) return false;
                current = parent;
            }
            return false;
        }
        finally { ReleaseComPointer(ownedParent); ReleaseComPointer(walker); }
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
