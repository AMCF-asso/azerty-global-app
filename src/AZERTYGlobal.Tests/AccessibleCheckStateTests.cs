using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Les boutons d'usage du tutoriel sont des boutons owner-draw : sans annotation, le lecteur
/// d'écran annonçait « bouton » sans dire s'ils étaient cochés. AccessibleName.TrySetCheckState
/// les présente comme des cases à cocher. Lu ici comme le lit le Narrateur, par le proxy MSAA
/// du contrôle (AccessibleObjectFromWindow), sur un fil STA comme celui de l'application.
/// </summary>
public class AccessibleCheckStateTests
{
    private const int ROLE_SYSTEM_CHECKBUTTON = 0x2C;
    private const int STATE_SYSTEM_CHECKED = 0x10;
    private const int STATE_SYSTEM_FOCUSED = 0x4;

    [Fact]
    public void BoutonOwnerDraw_AnnonceRôleEtÉtatCoché()
    {
        (int rôle, int état)[]? lus = null;
        Exception? échec = null;
        var fil = new Thread(() =>
        {
            IntPtr bouton = IntPtr.Zero;
            try
            {
                bouton = Win32.CreateWindowExW(0, "BUTTON", "Email", Win32.WS_POPUP | Win32.BS_OWNERDRAW,
                    0, 0, 100, 30, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                Assert.NotEqual(IntPtr.Zero, bouton);
                Assert.True(AccessibleName.TrySetCheckState(bouton, isChecked: true, focused: false, announce: false));
                var coché = RôleEtÉtat(bouton);
                Assert.True(AccessibleName.TrySetCheckState(bouton, isChecked: false, focused: true, announce: true));
                lus = new[] { coché, RôleEtÉtat(bouton) };
            }
            catch (Exception ex) { échec = ex; }
            finally { if (bouton != IntPtr.Zero) Win32.DestroyWindow(bouton); }
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();

        if (échec != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(échec).Throw();
        Assert.Equal(ROLE_SYSTEM_CHECKBUTTON, lus![0].rôle);
        Assert.Equal(STATE_SYSTEM_CHECKED, lus[0].état & (STATE_SYSTEM_CHECKED | STATE_SYSTEM_FOCUSED));
        Assert.Equal(ROLE_SYSTEM_CHECKBUTTON, lus[1].rôle);
        Assert.Equal(STATE_SYSTEM_FOCUSED, lus[1].état & (STATE_SYSTEM_CHECKED | STATE_SYSTEM_FOCUSED));
    }

    // IAccessible par sa table virtuelle (IUnknown 0 à 2, IDispatch 3 à 6, puis
    // get_accParent 7 … get_accRole 13, get_accState 14).
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct VariantI4 { public ushort Vt; public ushort R1, R2, R3; public IntPtr Valeur; public IntPtr Reste; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ProprieteVariant(IntPtr self, VariantI4 child, out VariantI4 valeur);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Libérer(IntPtr self);

    private static (int rôle, int état) RôleEtÉtat(IntPtr hwnd)
    {
        Guid iidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
        Assert.Equal(0, AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref iidIAccessible, out IntPtr acc)); // OBJID_CLIENT
        try
        {
            IntPtr Slot(int i) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), i * IntPtr.Size);
            var soi = new VariantI4 { Vt = 3, Valeur = IntPtr.Zero }; // VT_I4, CHILDID_SELF
            Assert.Equal(0, Marshal.GetDelegateForFunctionPointer<ProprieteVariant>(Slot(13))(acc, soi, out VariantI4 rôle));
            Assert.Equal(0, Marshal.GetDelegateForFunctionPointer<ProprieteVariant>(Slot(14))(acc, soi, out VariantI4 état));
            return ((int)rôle.Valeur, (int)état.Valeur);
        }
        finally
        {
            Marshal.GetDelegateForFunctionPointer<Libérer>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), 2 * IntPtr.Size))(acc);
        }
    }
}
