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
