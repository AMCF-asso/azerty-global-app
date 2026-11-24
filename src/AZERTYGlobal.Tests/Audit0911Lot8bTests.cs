using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, lot 8b (mineurs) : E28 (dialogue de pause), E30 (liens de l'accueil), E26 (pause),
/// E13, E14, W22, W23, W24.
/// </summary>
public sealed class Audit0911Lot8bTests
{
    private static string TextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(Math.Max(16, Win32.GetWindowTextLengthW(hwnd) + 1));
        Win32.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    // ── E30 : cadre de focus des liens de l'accueil ─────────────────────────

    [Theory]
    [InlineData(false, true, true, true)]    // focus + clavier déjà utilisé : cadre
    [InlineData(false, true, false, false)]  // focus donné par un clic : pas de cadre
    [InlineData(false, false, true, false)]  // pas de focus
    [InlineData(true, true, true, false)]    // DC de l'appelant (capture) : jamais
    public void Liens_CadreDeFocus_SeulementAprèsUnUsageDuClavier(bool dcAppelant, bool focus, bool indices, bool attendu) =>
        Assert.Equal(attendu, GdiHelpers.ShowsFocusFrame(dcAppelant, focus, indices));

    [Theory]
    [InlineData(true, false, false, true)]   // le survol colore toujours
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]  // focus à la souris : ni couleur de focus ni cadre
    [InlineData(false, false, true, false)]
    public void Liens_CouleurDeFocus_SuitLaMêmeRègle(bool survol, bool focus, bool indices, bool attendu) =>
        Assert.Equal(attendu, LinkBehavior.IsActiveState(survol, focus, indices));

    [Fact]
    public void Liens_ÉtatDInterface_LevéParLeClavier_BaisséParLaSouris()
    {
        const uint WM_UPDATEUISTATE = 0x0128;
        const int UIS_SET = 1, UIS_CLEAR = 2, UISF_HIDEFOCUS = 1;
        string classe = "AZERTYGlobal.Tests.Lot8b." + Guid.NewGuid().ToString("N");
        Win32.WNDPROC proc = Win32.DefWindowProcW;
        Assert.True(NativeWindow.RegisterClass(classe, proc));
        IntPtr hwnd = Win32.CreateWindowExW(0, classe, string.Empty, 0, 0, 0, 0, 0,
            new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);
            Win32.SendMessageW(hwnd, WM_UPDATEUISTATE, (IntPtr)(UIS_SET | (UISF_HIDEFOCUS << 16)), IntPtr.Zero);
            Assert.False(PaperChoice.FocusCuesShown(hwnd));
            Win32.SendMessageW(hwnd, WM_UPDATEUISTATE, (IntPtr)(UIS_CLEAR | (UISF_HIDEFOCUS << 16)), IntPtr.Zero);
            Assert.True(PaperChoice.FocusCuesShown(hwnd));
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
            NativeWindow.UnregisterClass(classe);
            GC.KeepAlive(proc);
        }
    }

    // ── W22 : le clavier virtuel est un affichage ───────────────────────────

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct VariantI4 { public ushort Vt; public ushort R1, R2, R3; public IntPtr Valeur; public IntPtr Reste; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ProprieteVariant(IntPtr self, VariantI4 child, out VariantI4 valeur);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ProprieteTexte(IntPtr self, VariantI4 child, out IntPtr bstr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Libérer(IntPtr self);

    [DllImport("user32.dll")]
    private static extern IntPtr GetCursor();

    [Fact]
    public void ClavierVirtuel_SePrésenteCommeUnAffichage_RôleNomEtCurseur()
    {
        const int ROLE_SYSTEM_PUSHBUTTON = 0x2B, ROLE_SYSTEM_GRAPHIC = 0x28;
        int rôle = 0; string? nom = null, description = null; IntPtr curseur = IntPtr.Zero, flèche = IntPtr.Zero;
        IntPtr réponse = IntPtr.Zero, réponseBord = (IntPtr)(-1), curseurBord = IntPtr.Zero;
        Exception? échec = null;
        var fil = new Thread(() =>
        {
            try
            {
                using var isolation = new BancCapture.Isolation();
                var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
                try
                {
                    IntPtr hwnd = BancCapture.Handle(clavier);
                    Assert.NotEqual(IntPtr.Zero, hwnd);
                    Guid iid = new("618736E0-3C3D-11CF-810C-00AA00389B71");
                    Assert.Equal(0, AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref iid, out IntPtr acc));
                    try
                    {
                        IntPtr Slot(int i) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), i * IntPtr.Size);
                        var soi = new VariantI4 { Vt = 3 };
                        Assert.Equal(0, Marshal.GetDelegateForFunctionPointer<ProprieteVariant>(Slot(13))(acc, soi, out VariantI4 r));
                        rôle = (int)r.Valeur;
                        string? Texte(int slot)
                        {
                            Marshal.GetDelegateForFunctionPointer<ProprieteTexte>(Slot(slot))(acc, soi, out IntPtr bstr);
                            return bstr == IntPtr.Zero ? null : Marshal.PtrToStringBSTR(bstr);
                        }
                        nom = Texte(10);
                        description = Texte(12);
                    }
                    finally
                    {
                        Marshal.GetDelegateForFunctionPointer<Libérer>(Slot0(acc, 2))(acc);
                    }

                    // WM_SETCURSOR au-dessus de la zone client : la flèche, jamais la main.
                    flèche = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512);
                    Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649)); // la main, posée avant
                    réponse = Win32.SendMessageW(hwnd, Win32.WM_SETCURSOR, hwnd, (IntPtr)(1 | (0x0200 << 16)));
                    curseur = GetCursor();
                    // Sur un bord (HTLEFT = 10) : Windows décide, la fenêtre ne répond pas.
                    réponseBord = Win32.SendMessageW(hwnd, Win32.WM_SETCURSOR, hwnd, (IntPtr)(10 | (0x0200 << 16)));
                    curseurBord = GetCursor();
                }
                finally { BancCapture.Teardown(clavier); }
            }
            catch (Exception ex) { échec = ex; }
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();
        if (échec != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(échec).Throw();

        Assert.Equal(ROLE_SYSTEM_GRAPHIC, rôle);
        Assert.NotEqual(ROLE_SYSTEM_PUSHBUTTON, rôle);
        Assert.Equal(L.VirtualKeyboardPaper_Title, nom);
        Assert.Equal(L.VirtualKeyboardPaper_Description, description);
        Assert.Equal((IntPtr)1, réponse);
        Assert.Equal(flèche, curseur);
        Assert.NotEqual(flèche, curseurBord);
    }

    private static IntPtr Slot0(IntPtr acc, int i) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), i * IntPtr.Size);

    [Theory]
    [InlineData(1, true)]    // HTCLIENT
    [InlineData(10, false)]  // HTLEFT : curseur de redimensionnement de Windows
    [InlineData(2, false)]   // HTCAPTION
    public void ClavierVirtuel_FlècheSeulementSurLaZoneClient(int zone, bool attendu) =>
        Assert.Equal(attendu, VirtualKeyboard.ShowsArrowCursor(zone));

    // ── E14 : couches grecque et cyrillique dans la frappe des Leçons ───────

    private const uint SC_TRIGGER_ALPHABET = 0x2B, SC_LSHIFT = 0x2A, SC_A = 0x10, SC_SPACE = 0x39;
    private const uint VK_LSHIFT = 0xA0;
    private static readonly short KeyDown = unchecked((short)0x8000);

    private static (KeyMapper Mapper, MockWin32Api Mock, ForegroundMonitor Monitor) MapperAvecCouches()
    {
        var mock = new MockWin32Api
        {
            ScriptedProcessName = "notepad.exe",
            ScriptedFullPath = @"C:\Windows\notepad.exe",
            ScriptedModules = new[] { "kernel32.dll" },
            ScriptedPid = 4242,
            ScriptedProcessStartTime = 123456789
        };
        var monitor = new ForegroundMonitor(mock, IntPtr.Zero);
        var mapper = new KeyMapper(LayoutLoader.LoadFromResource(), mock);
        mapper.SetForegroundMonitor(monitor);
        mapper.ApplyMaintainableLayerSettings(true, new[] { "dk_greek", "dk_cyrillic", "dk_scientific" }, 500);
        return (mapper, mock, monitor);
    }

    private static void TapAlphabetTrigger(KeyMapper mapper, MockWin32Api mock)
    {
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
        mapper.ProcessKey(0xDC, SC_TRIGGER_ALPHABET, 0, true);
        mapper.ProcessKey(0xDC, SC_TRIGGER_ALPHABET, 0, false);
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = 0;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, false);
    }

    [Fact]
    public void Leçons_CoucheGrecqueVerrouillée_LaFrappeResteGrecque_PasReplaceeParLeLatin()
    {
        var (mapper, mock, monitor) = MapperAvecCouches();
        using (monitor)
        {
            TapAlphabetTrigger(mapper, mock);
            TapAlphabetTrigger(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            var layout = LayoutLoader.LoadFromResource();
            var buffer = new PhysicalTextInputBuffer(1000, () => 0);
            buffer.CaptureKey(layout, SC_A, mapper);
            // Le moteur émet « α » ; la file ne doit pas le remplacer par le « a » de la couche de base.
            Assert.Equal('α', buffer.Resolve('α'));
            Assert.False(buffer.HasPending);
            // Le témoin discrimine : sans la couche, la file attendait bien le latin.
            Assert.Equal("a", PhysicalTextInputBuffer.ExpectedText(layout, SC_A, false, false, false, null));
            Assert.Equal("α", PhysicalTextInputBuffer.ExpectedText(layout, SC_A, false, false, false, null, "dk_greek"));
        }
    }

    [Fact]
    public void Leçons_CoucheGrecqueVerrouillée_EspaceResteUnEspace()
    {
        var (mapper, mock, monitor) = MapperAvecCouches();
        using (monitor)
        {
            TapAlphabetTrigger(mapper, mock);
            TapAlphabetTrigger(mapper, mock);
            var layout = LayoutLoader.LoadFromResource();
            Assert.Equal(" ", PhysicalTextInputBuffer.ExpectedText(layout, SC_SPACE, false, false, false, null,
                mapper.MaintainableLayerState.Mode == MaintainableLayerMode.Locked ? mapper.MaintainableLayerState.LayerId : null));
        }
    }

    [Fact]
    public void Leçons_CoucheGrecqueUnCoup_LaFrappeResteGrecque()
    {
        var (mapper, mock, monitor) = MapperAvecCouches();
        using (monitor)
        {
            TapAlphabetTrigger(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
            var layout = LayoutLoader.LoadFromResource();
            var buffer = new PhysicalTextInputBuffer(1000, () => 0);
            buffer.CaptureKey(layout, SC_A, mapper);
            Assert.Equal('α', buffer.Resolve('α'));
        }
    }

    // ── E26 : la Recherche en pause ─────────────────────────────────────────

    [Theory]
    [InlineData(Win32.WM_KEYDOWN, 0x1B, false)]  // Échap ferme la fenêtre
    [InlineData(Win32.WM_KEYDOWN, 0x09, false)]  // Tab quitte le champ
    [InlineData(Win32.WM_KEYDOWN, 0x41, true)]   // une lettre : saisie refusée
    [InlineData(Win32.WM_KEYDOWN, 0x2E, true)]   // Suppr : modifie le texte
    [InlineData(Win32.WM_CHAR, 'a', true)]
    [InlineData(Win32.WM_PASTE, 0, true)]
    [InlineData(Win32.WM_CUT, 0, true)]
    [InlineData(Win32.WM_KEYUP, 0x1B, false)]
    public void RechercheEnPause_RefuseLaSaisie_PasÉchapNiTab(uint msg, int touche, bool bloqué) =>
        Assert.Equal(bloqué, CharacterSearch.BlockedWhilePaused(msg, touche));

    // ── E28 : dialogue de pause ─────────────────────────────────────────────

    [Theory]
    [InlineData("1a2b", "12")]
    [InlineData("abc", "")]
    [InlineData("12 mn", "12")]
    [InlineData("-5", "5")]
    public void Pause_ChampsNumériques_UnCollageNonNumériqueNeGardeQueLesChiffres(string collé, string attendu)
    {
        using var isolation = new BancCapture.Isolation();
        var dialog = new PauseDurationDialog();
        try
        {
            BancCapture.Call(dialog, "CreateWindow", IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, BancCapture.Handle(dialog));
            foreach (string champ in new[] { "_hEditHours", "_hEditMinutes" })
            {
                IntPtr edit = BancCapture.Field<IntPtr>(dialog, champ);
                // WM_SETTEXT contourne ES_NUMBER comme le fait un collage : EN_CHANGE suit.
                Win32.SetWindowTextW(edit, collé);
                Assert.Equal(attendu, TextOf(edit));
            }
        }
        finally { BancCapture.Teardown(dialog); }
    }

    [Fact]
    public void Pause_ChampsNumériques_LeCollageEstLimitéÀDeuxChiffres()
    {
        using var isolation = new BancCapture.Isolation();
        var dialog = new PauseDurationDialog();
        try
        {
            BancCapture.Call(dialog, "CreateWindow", IntPtr.Zero);
            IntPtr edit = BancCapture.Field<IntPtr>(dialog, "_hEditMinutes");
            Assert.Equal(2, (int)Win32.SendMessageW(edit, 0x00D5 /* EM_GETLIMITTEXT */, IntPtr.Zero, IntPtr.Zero));
        }
        finally { BancCapture.Teardown(dialog); }
    }
}
