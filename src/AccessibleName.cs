using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// N10 (accessibilité 1.3.0) — nom accessible d'un contrôle dont le texte de fenêtre n'en est
/// pas un : les boutons ▲▼ de la Pause s'annonçaient « ▲ » et « ▼ ».
///
/// Dynamic Annotation : <c>IAccPropServices::SetHwndPropStr</c> rattache
/// <c>PROPID_ACC_NAME</c> au HWND, et MSAA comme UI Automation le lisent (mesuré le 2026-09-23
/// sur Windows 11 26200 par une sonde hors dépôt : get_accName et CurrentName rendent le nom
/// posé). Le texte affiché ne change pas.
///
/// Appel par la table virtuelle et <c>Marshal.GetDelegateForFunctionPointer</c>, comme
/// <c>AutoStart</c> : aucun bloc unsafe dans l'application (voir le commentaire
/// d'AllowUnsafeBlocks dans le csproj), aucune interop générée, compatible NativeAOT.
///
/// Un EDIT ou une LISTBOX se nomment, eux, par l'étiquette STATIC qui les précède dans l'ordre
/// Z (N8) : pas besoin de COM pour eux.
///
/// À appeler sur le thread d'interface, en STA (<c>[STAThread]</c>). L'annotation vit avec le
/// HWND : l'exemple de Microsoft (« Using Direct Annotation ») ne la retire pas non plus.
/// </summary>
static class AccessibleName
{
    // oleacc.h : CLSID_AccPropServices, IID_IAccPropServices, PROPID_ACC_NAME.
    private static readonly Guid ClsidAccPropServices = new("B5F8350B-0548-48B1-A6EE-88BD00B4A5E7");
    private static readonly Guid IidIAccPropServices = new("6E26E776-04F0-495D-80E4-3330352E3169");
    private static readonly Guid PropIdAccName = new("608D3DF8-8128-4AA7-A428-F55E49267291");

    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private const uint OBJID_CLIENT = 0xFFFFFFFC;
    private const uint CHILDID_SELF = 0;

    // IAccPropServices (hérite IUnknown : 0 = QueryInterface, 1 = AddRef, 2 = Release) :
    // 3 = SetPropValue, 4 = SetPropServer, 5 = ClearProps, 6 = SetHwndProp, 7 = SetHwndPropStr.
    private const int VT_Release = 2;
    private const int VT_SetHwndPropStr = 7;

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SetHwndPropStrDelegate(IntPtr self, IntPtr hwnd, uint idObject, uint idChild,
        Guid idProp, string str);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseDelegate(IntPtr self);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext,
        ref Guid riid, out IntPtr ppv);

    /// <summary>
    /// Pose le nom accessible de <paramref name="hwnd"/>. Rend faux, sans lever ni journaliser,
    /// si COM ou oleacc refusent : le contrôle garde alors son texte pour nom, l'état d'avant.
    /// </summary>
    public static bool TrySet(IntPtr hwnd, string name)
    {
        if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(name)) return false;
        IntPtr services = IntPtr.Zero;
        try
        {
            Guid clsid = ClsidAccPropServices;
            Guid iid = IidIAccPropServices;
            if (CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, ref iid, out services) != 0
                || services == IntPtr.Zero)
                return false;
            var setHwndPropStr = Marshal.GetDelegateForFunctionPointer<SetHwndPropStrDelegate>(
                VTableSlot(services, VT_SetHwndPropStr));
            return setHwndPropStr(services, hwnd, OBJID_CLIENT, CHILDID_SELF, PropIdAccName, name) == 0;
        }
        catch (Exception ex) when (ex is ExternalException or MarshalDirectiveException
            or EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
        finally
        {
            if (services != IntPtr.Zero)
                Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(VTableSlot(services, VT_Release))(services);
        }
    }

    private static IntPtr VTableSlot(IntPtr instance, int slot) =>
        Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size);
}
