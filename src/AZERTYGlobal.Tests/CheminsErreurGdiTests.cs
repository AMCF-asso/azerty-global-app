using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>Audit des chemins d'erreur, rapport-E S1 et S2 : PaperText face à un DC invalide.</summary>
public class CheminsErreurPaperTextTests
{
    /// <summary>Le cache de couverture, clé (famille, graisse, grappe) depuis l'audit des ressources
    /// (C1, C2) : la hauteur n'en fait plus partie.</summary>
    private static Dictionary<(string Family, int Weight, string Cluster), bool> Coverage()
    {
        var champ = typeof(PaperText).GetField("Coverage", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(champ);
        return (Dictionary<(string Family, int Weight, string Cluster), bool>)champ!.GetValue(null)!;
    }

    // rapport-E S1 (W769, W770)
    // Adaptation à l'intégration 1.3.0 : la version de la branche d'audit lisait une clé
    // (FontSpec, grappe) et prenait une hauteur rare pour la victime. Depuis que la hauteur est
    // sortie de la clé, le témoin remplissait la case de la victime et le test passait sans
    // éprouver la panne. La grappe est donc propre au test (aucune autre ne demande « ӂ »), la
    // panne vient en premier, et le témoin vient après, sur la même clé vidée.
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Covers_DcInvalide_NeMemorisePasUneCouvertureInventee()
    {
        Assert.True(AppFonts.LoadedCount > 0, "polices embarquées non chargées : le témoin n'a pas de sens");
        var cache = Coverage();
        const string Grappe = "ӂ"; // U+04C2, cyrillique : AG Texte (latin) ne l'a pas
        var police = new FontSpec(AppFonts.Text, 59);
        var cle = (police.Family, police.Weight, Grappe);
        Assert.False(cache.ContainsKey(cle), "la grappe du test est déjà en cache : le test n'éprouverait rien");

        // Panne : DC nul (GetDC d'une fenêtre détruite). GetGlyphIndicesW rend GDI_ERROR.
        PaperText.Width(IntPtr.Zero, Grappe, police);
        bool memorise = cache.TryGetValue(cle, out bool apresPanne);
        cache.Remove(cle);

        // Témoin : avec un vrai DC, la vraie couverture.
        IntPtr ecran = Win32.GetDC(IntPtr.Zero);
        try { PaperText.Width(ecran, Grappe, police); }
        finally { Win32.ReleaseDC(IntPtr.Zero, ecran); }
        Assert.True(cache.TryGetValue(cle, out bool verite));
        Assert.False(verite, "AG Texte ne devrait pas couvrir le cyrillique");

        Assert.False(memorise && apresPanne != verite,
            "un échec GDI a été mémorisé comme « AG Texte couvre ӂ » : carré vide jusqu'à la fin de la session");
    }

    // rapport-E S2 (W759)
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Metrics_DcInvalide_RendUneHauteurPasZero()
    {
        var police = new FontSpec(AppFonts.Text, 47);
        IntPtr ecran = Win32.GetDC(IntPtr.Zero);
        try
        {
            var (a0, d0) = PaperText.Metrics(ecran, police);
            Assert.True(a0 + d0 > 0, "témoin : un vrai DC donne une hauteur");
        }
        finally { Win32.ReleaseDC(IntPtr.Zero, ecran); }

        var (ascent, descent) = PaperText.Metrics(IntPtr.Zero, police);
        Assert.True(ascent + descent > 0,
            "Metrics rend (0, 0) sans signal : SettingsWindow._metrics et CharacterSearch.ResizeEditControl le gardent");
    }

    // rapport-E S2 (W767)
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Width_DcInvalide_RendUneLargeurPasZero()
    {
        var police = new FontSpec(AppFonts.Text, 43);
        IntPtr ecran = Win32.GetDC(IntPtr.Zero);
        try { Assert.True(PaperText.Width(ecran, "0", police) > 0, "témoin"); }
        finally { Win32.ReleaseDC(IntPtr.Zero, ecran); }

        Assert.True(PaperText.Width(IntPtr.Zero, "0", police) > 0,
            "Width rend 0 sans signal : LessonsWindow._freeWidths range ce 0 pour la session");
    }
}

/// <summary>Audit des chemins d'erreur, rapport-E S3 : le pinceau de fond de classe.</summary>
public class CheminsErreurClassBackgroundTests
{
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    private static int ObjetsGdi() => (int)GetGuiResources(GetCurrentProcess(), 0); // GR_GDIOBJECTS

    // rapport-E S3 (W530)
    // Adaptation : le test du rapport passait un HWND nul ; ApplyClassBackground sort alors tout de
    // suite (garde NativeWindow.cs:105-106, VERT : variante « HWND nul » réfutée). La panne décrite par
    // le rapport est aussi « fenêtre détruite » (SettingsWindow.Paper.cs:86, RefreshPalette) : le test
    // utilise donc la poignée d'une fenêtre détruite, pour laquelle SetClassLongPtrW rend 0.
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void ApplyClassBackground_FenetreDetruite_NeFuitPasLePinceau()
    {
        const int Tours = 40;
        string nom = "AZERTYGlobal.Tests.FondClasse." + Guid.NewGuid().ToString("N");
        Win32.WNDPROC procedure = (h, m, w, l) => Win32.DefWindowProcW(h, m, w, l);
        Assert.True(NativeWindow.RegisterClass(nom, procedure));
        IntPtr hwnd = Win32.CreateWindowExW(0, nom, string.Empty, 0x80000000 /* WS_POPUP */,
            0, 0, 64, 32, HWND_MESSAGE, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);

            // Témoin : chemin sain, chaque appel supprime le pinceau précédent de la classe.
            NativeWindow.ApplyClassBackground(hwnd, 0x00112233);
            int avant = ObjetsGdi();
            for (int i = 0; i < Tours; i++) NativeWindow.ApplyClassBackground(hwnd, (uint)(0x00112233 + i));
            Assert.InRange(ObjetsGdi() - avant, -2, 2);

            // Panne : la fenêtre est détruite, SetClassLongPtrW rend 0 sur sa poignée.
            Assert.True(Win32.DestroyWindow(hwnd));
            int avantPanne = ObjetsGdi();
            for (int i = 0; i < Tours; i++) NativeWindow.ApplyClassBackground(hwnd, (uint)(0x00445566 + i));
            int fuite = ObjetsGdi() - avantPanne;
            Assert.True(fuite <= 2, $"{fuite} pinceaux GDI fuités sur {Tours} appels sur une fenêtre détruite");
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
            NativeWindow.UnregisterClass(nom);
            GC.KeepAlive(procedure);
        }
    }
}
