using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le sous-titre des modes de compatibilité est peint sous le bouton radio : rien ne l'y
/// rattachait pour un lecteur d'écran. AccessibleName.TrySetDescription le pose en description
/// accessible. Lu ici comme le lit le Narrateur, par le proxy MSAA (get_accDescription), sur
/// un fil STA comme celui de l'application.
/// </summary>
public class AccessibleDescriptionTests
{
    [Fact]
    public void BoutonRadio_AnnonceSaDescription()
    {
        string? lue = null;
        Exception? échec = null;
        var fil = new Thread(() =>
        {
            IntPtr bouton = IntPtr.Zero;
            try
            {
                bouton = Win32.CreateWindowExW(0, "BUTTON", "Auto", Win32.WS_POPUP | Win32.BS_AUTORADIOBUTTON,
                    0, 0, 100, 30, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                Assert.NotEqual(IntPtr.Zero, bouton);
                Assert.True(AccessibleName.TrySetDescription(bouton, L.SettingsPaper_CompatHintAuto));
                lue = Description(bouton);
            }
            catch (Exception ex) { échec = ex; }
            finally { if (bouton != IntPtr.Zero) Win32.DestroyWindow(bouton); }
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();

        if (échec != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(échec).Throw();
        Assert.Equal(L.SettingsPaper_CompatHintAuto, lue);
    }

    // IAccessible par sa table virtuelle (IUnknown 0 à 2, IDispatch 3 à 6, puis
    // get_accParent 7 … get_accName 10, get_accValue 11, get_accDescription 12).
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct VariantI4 { public ushort Vt; public ushort R1, R2, R3; public IntPtr Valeur; public IntPtr Reste; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ProprieteTexte(IntPtr self, VariantI4 child, out IntPtr bstr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Libérer(IntPtr self);

    private static string? Description(IntPtr hwnd)
    {
        Guid iidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
        Assert.Equal(0, AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref iidIAccessible, out IntPtr acc)); // OBJID_CLIENT
        try
        {
            IntPtr Slot(int i) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), i * IntPtr.Size);
            var soi = new VariantI4 { Vt = 3, Valeur = IntPtr.Zero }; // VT_I4, CHILDID_SELF
            Assert.Equal(0, Marshal.GetDelegateForFunctionPointer<ProprieteTexte>(Slot(12))(acc, soi, out IntPtr bstr));
            try { return Marshal.PtrToStringBSTR(bstr); }
            finally { Marshal.FreeBSTR(bstr); }
        }
        finally
        {
            Marshal.GetDelegateForFunctionPointer<Libérer>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), 2 * IntPtr.Size))(acc);
        }
    }
}
