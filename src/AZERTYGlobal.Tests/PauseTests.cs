using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Pause refaite le 2026-09-28 (décisions d'Antoine) : sous-menu de durées, « Jusqu'à
/// demain » = le prochain 8 h, et une fenêtre « Personnaliser » en heures et minutes avec
/// les flèches Windows.
/// </summary>
public class PauseTests
{
    [Theory]
    [InlineData("2026-09-28 14:30", "2026-09-29 08:00")] // après 8 h : demain
    [InlineData("2026-09-28 08:00", "2026-09-29 08:00")] // à 8 h pile : demain, pas maintenant
    [InlineData("2026-09-28 07:59", "2026-09-28 08:00")] // avant 8 h : ce matin
    [InlineData("2026-09-28 00:10", "2026-09-28 08:00")]
    [InlineData("2026-12-31 23:00", "2027-01-01 08:00")]
    public void JusquADemain_EstLeProchain8h(string maintenant, string attendu)
    {
        Assert.Equal(DateTime.Parse(attendu), PauseSchedule.NextMorning(DateTime.Parse(maintenant)));
    }

    [Theory]
    [InlineData("fr", "2026-09-28 16:12", "16 h 12")]
    [InlineData("fr", "2026-09-28 08:00", "8 h")]
    [InlineData("fr", "2026-09-28 09:05", "9 h 05")]
    [InlineData("en", "2026-09-28 16:12", "4:12 PM")]
    [InlineData("en", "2026-09-28 08:00", "8:00 AM")]
    public void Heure_SuitLaTypographieDeLaLangue(string langue, string heure, string attendu)
    {
        string avant = L.Language;
        try
        {
            L.Language = langue;
            Assert.Equal(attendu, PauseSchedule.FormatClock(DateTime.Parse(heure)));
        }
        finally { L.Language = avant; }
    }

    [Theory]
    [InlineData("fr", "2026-09-28 23:30", "2026-09-29 08:00", "demain à 8 h")]
    [InlineData("fr", "2026-09-28 14:00", "2026-09-28 16:12", "à 16 h 12")]
    [InlineData("en", "2026-09-28 23:30", "2026-09-29 08:00", "tomorrow at 8:00 AM")]
    public void Reprise_DitDemainQuandLaDateChange(string langue, string maintenant, string fin, string attendu)
    {
        string avant = L.Language;
        try
        {
            L.Language = langue;
            Assert.Equal(attendu, PauseSchedule.DescribeResume(DateTime.Parse(maintenant), DateTime.Parse(fin)));
        }
        finally { L.Language = avant; }
    }

    [Theory]
    [InlineData(0, 0, null)]      // durée nulle : refusée
    [InlineData(0, 1, 1)]
    [InlineData(1, 30, 90)]
    [InlineData(0, 75, 75)]       // minutes au-delà de 59 à la frappe : 1 h 15
    [InlineData(23, 59, 1439)]
    [InlineData(23, 60, null)]    // au-delà de 23 h 59
    [InlineData(-1, 30, null)]
    public void Duree_BorneeDe1minA23h59(int heures, int minutes, int? attendu)
    {
        Assert.Equal(attendu, (int?)PauseDurationDialog.ParseDuration(heures, minutes)?.TotalMinutes);
    }

    [Fact]
    public void Personnaliser_ChaqueChampASesFlechesWindows()
    {
        using var dialogue = new PauseDurationDialog();
        typeof(PauseDurationDialog).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogue, new object[] { IntPtr.Zero });

        foreach (var (champ, fleches) in new[] { ("_hEditHours", "_hSpinHours"), ("_hEditMinutes", "_hSpinMinutes") })
        {
            IntPtr edit = Field(dialogue, champ);
            IntPtr spin = Field(dialogue, fleches);
            Assert.NotEqual(IntPtr.Zero, spin);
            // UDM_GETBUDDY : les flèches pilotent bien leur champ.
            Assert.Equal(edit, Win32.SendMessageW(spin, 0x046A, IntPtr.Zero, IntPtr.Zero));
        }
    }

    /// <summary>
    /// Les flèches n'avaient pas de nom à elles. MSAA et UI Automation voient chaque Up-down
    /// comme un « spinner » au nom vide, avec deux boutons enfants que Windows nomme « Plus »
    /// et « Moins » (« More », « Less ») : ni le champ ni le sens. Lu ici comme le lit le
    /// Narrateur, par le proxy MSAA du contrôle (AccessibleObjectFromWindow), sur un fil STA
    /// comme celui de l'application.
    /// </summary>
    [Theory]
    [InlineData("fr", "Heures", "Augmenter les heures", "Diminuer les heures",
        "Minutes", "Augmenter les minutes", "Diminuer les minutes")]
    [InlineData("en", "Hours", "Increase hours", "Decrease hours",
        "Minutes", "Increase minutes", "Decrease minutes")]
    public void Personnaliser_ChaqueFlècheDitSonChampEtSonSens(string langue,
        string heures, string plusDHeures, string moinsDHeures,
        string minutes, string plusDeMinutes, string moinsDeMinutes)
    {
        string?[]? noms = null;
        Exception? échec = null;
        var fil = new Thread(() =>
        {
            string avant = L.Language;
            try
            {
                L.Language = langue;
                using var dialogue = new PauseDurationDialog();
                typeof(PauseDurationDialog).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(dialogue, new object[] { IntPtr.Zero });
                noms = NomsMsaa(Field(dialogue, "_hSpinHours")).Concat(NomsMsaa(Field(dialogue, "_hSpinMinutes"))).ToArray();
            }
            catch (Exception ex) { échec = ex; }
            finally { L.Language = avant; }
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();

        if (échec != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(échec).Throw();
        // Pour chaque Up-down : le contrôle, puis sa flèche du haut, puis celle du bas.
        Assert.Equal(new string?[] { heures, plusDHeures, moinsDHeures, minutes, plusDeMinutes, moinsDeMinutes }, noms);
    }

    // IAccessible par sa table virtuelle (IUnknown 0 à 2, IDispatch 3 à 6, puis
    // get_accParent 7, get_accChildCount 8, get_accChild 9, get_accName 10).
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct VariantI4 { public ushort Vt; public ushort R1, R2, R3; public IntPtr Valeur; public IntPtr Reste; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NombreDEnfants(IntPtr self, out int count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NomAccessible(IntPtr self, VariantI4 child, out IntPtr bstr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Libérer(IntPtr self);

    /// <summary>Nom MSAA du contrôle (indice 0), puis de chacun de ses enfants.</summary>
    private static string?[] NomsMsaa(IntPtr hwnd)
    {
        Guid iidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
        Assert.Equal(0, AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref iidIAccessible, out IntPtr acc)); // OBJID_CLIENT
        try
        {
            IntPtr Slot(int i) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), i * IntPtr.Size);
            Assert.Equal(0, Marshal.GetDelegateForFunctionPointer<NombreDEnfants>(Slot(8))(acc, out int enfants));
            var nom = Marshal.GetDelegateForFunctionPointer<NomAccessible>(Slot(10));
            var noms = new string?[enfants + 1];
            for (int i = 0; i <= enfants; i++)
            {
                int hr = nom(acc, new VariantI4 { Vt = 3, Valeur = (IntPtr)i }, out IntPtr bstr); // VT_I4
                noms[i] = hr == 0 && bstr != IntPtr.Zero ? Marshal.PtrToStringBSTR(bstr) : null;
                if (bstr != IntPtr.Zero) Marshal.FreeBSTR(bstr);
            }
            return noms;
        }
        finally
        {
            Marshal.GetDelegateForFunctionPointer<Libérer>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), 2 * IntPtr.Size))(acc);
        }
    }

    private static IntPtr Field(object o, string name) =>
        (IntPtr)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
}
