// Activateur de toast COM — v1.2.0 (Option 2 du TO-DO, décision 2026-07-17).
//
// Problème résolu : en app packagée MSIX, la balloon Shell_NotifyIcon de sollicitation
// d'avis est rendue en toast ; le clic active l'app par son AUMID et, faute d'activateur
// COM, Windows RELANÇAIT l'exécutable (seconde instance silencieuse, Option 1 en v1.1.0).
// Avec ce serveur COM enregistré (CoRegisterClassObject au démarrage), Windows se connecte
// au processus VIVANT et livre l'activation via INotificationActivationCallback — aucun
// second processus ne démarre.
//
// Interop : source-générée (.NET 8 [GeneratedComInterface]/[GeneratedComClass]),
// AOT-compatible, contrairement à [ComImport] classique — cohérent avec PublishAot.
// Le CLSID doit rester identique à celui du manifest (com:Class + ToastActivatorCLSID).
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace AZERTYGlobal;

/// <summary>
/// Callback COM standard des toasts Windows (shellapi : INotificationActivationCallback).
/// Windows l'appelle dans le processus vivant quand l'utilisateur clique un toast
/// dont l'app déclare un ToastActivatorCLSID.
/// </summary>
[GeneratedComInterface]
[Guid("53E31837-6600-4A81-9395-75CFFE746F94")]
internal partial interface INotificationActivationCallback
{
    void Activate(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string? invokedArgs,
        IntPtr data,   // NOTIFICATION_USER_INPUT_DATA* — non utilisé (pas de champ de saisie)
        uint count);
}

/// <summary>Fabrique de classe COM standard.</summary>
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, in Guid riid, out IntPtr ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

/// <summary>
/// Enregistrement du serveur COM d'activation de toast + émission des toasts.
/// Utilisé uniquement en contexte packagé (MSIX) : hors package, les balloons
/// Shell_NotifyIcon restent le canal de notification (comportement v1.1.0).
/// </summary>
internal static partial class ToastActivation
{
    /// <summary>CLSID déclaré dans msix/AppxManifest.xml (com:Class + ToastActivatorCLSID).</summary>
    internal const string ActivatorClsidString = "126A58B4-3200-43A6-9018-612C108F4A94";
    private static readonly Guid ActivatorClsid = new(ActivatorClsidString);

    private const uint CLSCTX_LOCAL_SERVER = 0x4;
    private const uint REGCLS_MULTIPLEUSE = 1;
    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    private const int E_NOINTERFACE = unchecked((int)0x80004002);

    private static readonly StrategyBasedComWrappers ComWrappers = new();
    private static uint _registrationCookie;
    private static IntPtr _factoryUnknown;

    /// <summary>
    /// Déclenché à chaque activation de toast, avec les arguments de lancement
    /// (« action=review »…). ⚠️ Appelé sur un thread RPC COM, pas le thread UI —
    /// l'abonné doit re-router (PostMessage) avant de toucher à l'UI.
    /// </summary>
    public static event Action<string>? Activated;

    [GeneratedComClass]
    internal sealed partial class NotificationActivator : INotificationActivationCallback
    {
        public void Activate(string appUserModelId, string? invokedArgs, IntPtr data, uint count)
        {
            try { Activated?.Invoke(invokedArgs ?? ""); }
            catch (Exception ex) { ConfigManager.Log("ToastActivation.Activate", ex); }
        }
    }

    [GeneratedComClass]
    internal sealed partial class ActivatorFactory : IClassFactory
    {
        public int CreateInstance(IntPtr pUnkOuter, in Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;
            if (pUnkOuter != IntPtr.Zero)
                return CLASS_E_NOAGGREGATION;

            IntPtr unknown = ComWrappers.GetOrCreateComInterfaceForObject(
                new NotificationActivator(), CreateComInterfaceFlags.None);
            try
            {
                Guid iid = riid;
                int hr = Marshal.QueryInterface(unknown, ref iid, out ppvObject);
                return hr != 0 ? E_NOINTERFACE : 0;
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        public int LockServer(bool fLock) => 0;
    }

    /// <summary>
    /// Enregistre la fabrique de classe pour que Windows livre les activations de toast
    /// au processus courant. À appeler une fois au démarrage (contexte packagé uniquement).
    /// Retourne false en cas d'échec (loggé) — l'app continue, le clic sur toast retombe
    /// alors sur la relance d'exécutable absorbée par le mutex single-instance.
    /// </summary>
    public static bool Register()
    {
        if (_registrationCookie != 0) return true;
        try
        {
            _factoryUnknown = ComWrappers.GetOrCreateComInterfaceForObject(
                new ActivatorFactory(), CreateComInterfaceFlags.None);
            int hr = Win32.CoRegisterClassObject(
                ActivatorClsid, _factoryUnknown, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE,
                out _registrationCookie);
            if (hr != 0)
            {
                ConfigManager.LogCompatEvent("ToastActivatorRegisterFailed", $"hr=0x{hr:X8}");
                ReleaseFactory();
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("ToastActivation.Register", ex);
            ReleaseFactory();
            return false;
        }
    }

    /// <summary>Révoque la fabrique à la fermeture de l'app.</summary>
    public static void Unregister()
    {
        if (_registrationCookie != 0)
        {
            try { Win32.CoRevokeClassObject(_registrationCookie); } catch { }
            _registrationCookie = 0;
        }
        ReleaseFactory();
    }

    private static void ReleaseFactory()
    {
        if (_factoryUnknown != IntPtr.Zero)
        {
            try { Marshal.Release(_factoryUnknown); } catch { }
            _factoryUnknown = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Corps destine au toast : la premiere ligne seulement. La seconde ligne d'un corps de
    /// notification n'existe que pour dire ce que le clic ouvre, et le bouton la remplace.
    /// Le canal balloon n'a pas de bouton et garde donc le corps entier — d'ou une seule
    /// chaine pour les deux canaux plutot qu'un doublon par notification.
    /// Public pour que <c>ToastBodyTests</c> puisse l'eprouver sans runtime WinRT.
    /// </summary>
    public static string ToastBody(string body)
    {
        int i = body.IndexOf('\n');
        return i < 0 ? body : body[..i];
    }

    /// <summary>
    /// Affiche un toast ToastGeneric (titre, corps, bouton) portant des arguments de lancement.
    /// Contexte packagé uniquement (CreateToastNotifier s'appuie sur l'identité MSIX).
    /// Retourne false en cas d'échec — l'appelant retombe sur la balloon classique.
    /// </summary>
    public static bool TryShowToast(string title, string body, string buttonLabel,
                                    string launchArgs)
    {
        try
        {
            string xml =
                $"<toast launch=\"{EscapeXml(launchArgs)}\" activationType=\"foreground\">" +
                "<visual><binding template=\"ToastGeneric\">" +
                $"<text>{EscapeXml(title)}</text>" +
                $"<text>{EscapeXml(ToastBody(body))}</text>" +
                "</binding></visual>" +
                "<actions>" +
                $"<action content=\"{EscapeXml(buttonLabel)}\" activationType=\"foreground\"" +
                $" arguments=\"{EscapeXml(launchArgs)}\" />" +
                // content vide + activationType system : Windows met son propre libelle
                // traduit, une chaine de moins a maintenir en deux langues.
                "<action content=\"\" activationType=\"system\" arguments=\"dismiss\" />" +
                "</actions></toast>";

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);
            return true;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("ToastActivation.TryShowToast", ex);
            return false;
        }
    }

    internal static string EscapeXml(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");
}
