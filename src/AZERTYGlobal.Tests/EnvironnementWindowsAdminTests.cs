using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;
using Xunit;
using Xunit.Abstractions;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit de l'environnement Windows (docs/audit-environnement-windows.md), constat EW-7 :
/// la recherche de caractère insérait par SendInput dans une fenêtre lancée en
/// administrateur, que Windows (UIPI) refuse sans le signaler de façon fiable. Le correctif
/// compare le niveau d'intégrité de la cible au nôtre avant d'émettre ; au-dessus, rien
/// n'est émis, le caractère est copié et une bulle dit pourquoi.
///
/// Ce qu'ils prouvent : la décision, le chemin de la recherche jusqu'à la bulle, et la
/// sonde réelle sur des fenêtres de même niveau (pas de faux « au-dessus »). Le cas d'une
/// vraie fenêtre administrateur s'établit par l'essai à la main E4.
/// </summary>
public sealed class EnvironnementWindowsAdminTests
{
    private const uint Low = 0x1000, Medium = 0x2000, MediumPlus = 0x2100, High = 0x3000, System = 0x4000;

    private readonly ITestOutputHelper _sortie;

    public EnvironnementWindowsAdminTests(ITestOutputHelper sortie) => _sortie = sortie;

    // ── Décision pure ────────────────────────────────────────────────

    [Theory]
    [InlineData(true, High, Medium, true)]
    [InlineData(true, System, Medium, true)]
    [InlineData(true, System, High, true)]
    [InlineData(true, Medium, Medium, false)]
    [InlineData(true, MediumPlus, Medium, true)]
    [InlineData(true, Low, Medium, false)]
    [InlineData(true, High, High, false)]
    [InlineData(true, Medium, High, false)]
    public void EW7_Decision_ComparerLesNiveaux(bool ouvert, uint cible, uint nous, bool attendu) =>
        Assert.Equal(attendu, TargetIntegrity.IsAbove(ouvert, cible, nous));

    /// <summary>Jeton illisible alors que le processus s'ouvre (cas d'un processus élevé vu
    /// d'un processus ordinaire), ou processus fermé : la copie, qui ne perd rien.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EW7_Decision_DansLeDoute_Copier(bool ouvert) =>
        Assert.True(TargetIntegrity.IsAbove(ouvert, null, Medium));

    // ── Service d'insertion ──────────────────────────────────────────

    [Fact]
    public void EW7_CibleAuDessus_RienNEstEmis()
    {
        var appels = new List<string>();
        var service = new TextInsertionService(
            texte => { appels.Add("émission"); return new(2, 2); },
            activateTarget: _ => { appels.Add("activation"); return true; },
            canInsert: () => true,
            refreshTarget: () => appels.Add("relecture"),
            targetAboveUs: hwnd => { appels.Add("niveau:" + hwnd); return true; });

        Assert.Equal(TextInsertionOutcome.ElevatedTarget, service.Insert((IntPtr)42, "α"));
        Assert.Equal(new[] { "activation", "relecture", "niveau:42" }, appels);
    }

    [Fact]
    public void EW7_Temoin_CibleMemeNiveau_Emet()
    {
        bool émis = false;
        var service = new TextInsertionService(_ => { émis = true; return new(2, 2); },
            activateTarget: _ => true, targetAboveUs: _ => false);

        Assert.Equal(TextInsertionOutcome.Inserted, service.Insert((IntPtr)42, "α"));
        Assert.True(émis);
    }

    /// <summary>
    /// Sur une vraie fenêtre administrateur, la sonde du champ mot de passe (UIA) échoue et
    /// répond « protégé » (échec fermé de SecureInputDetector) : la seconde lecture dirait
    /// « bloqué ». La fenêtre administrateur passe avant : copie et bulle, rien d'émis.
    /// </summary>
    [Fact]
    public void EW7_SondeFermeeApresActivation_NeMasquePasLaFenetreAdministrateur()
    {
        int lectures = 0;
        bool émis = false;
        var service = new TextInsertionService(_ => { émis = true; return new(2, 2); },
            activateTarget: _ => true, canInsert: () => ++lectures < 2, targetAboveUs: _ => true);

        Assert.Equal(TextInsertionOutcome.ElevatedTarget, service.Insert((IntPtr)42, "α"));
        Assert.False(émis);
    }

    /// <summary>Témoin : même sonde fermée vers une fenêtre de même niveau, le blocage reste.</summary>
    [Fact]
    public void EW7_Temoin_SondeFermeeMemeNiveau_Bloque()
    {
        int lectures = 0;
        var service = new TextInsertionService(_ => new(2, 2),
            activateTarget: _ => true, canInsert: () => ++lectures < 2, targetAboveUs: _ => false);

        Assert.Equal(TextInsertionOutcome.Blocked, service.Insert((IntPtr)42, "α"));
    }

    /// <summary>A21 : jeu anti-triche ou client distant révélé par la relecture : bloqué, et
    /// son jeton n'est jamais demandé (aucune requête de plus vers ce processus).</summary>
    [Fact]
    public void EW7_SuspensionImposee_PrimeSansSonderLeNiveau()
    {
        bool sondé = false, émis = false;
        var service = new TextInsertionService(_ => { émis = true; return new(2, 2); },
            activateTarget: _ => true, canInsert: () => true,
            targetAboveUs: _ => sondé = true, imposedSuspension: () => true);

        Assert.Equal(TextInsertionOutcome.Blocked, service.Insert((IntPtr)42, "α"));
        Assert.False(sondé);
        Assert.False(émis);
    }

    /// <summary>Pause ou consentement absent (lecture d'avant activation) : rien, pas même
    /// la sonde de niveau ni l'activation.</summary>
    [Fact]
    public void EW7_PauseAvantActivation_Prime()
    {
        bool sondé = false, activé = false;
        var service = new TextInsertionService(_ => new(2, 2),
            activateTarget: _ => activé = true, canInsert: () => false,
            targetAboveUs: _ => sondé = true);

        Assert.Equal(TextInsertionOutcome.Blocked, service.Insert((IntPtr)42, "α"));
        Assert.False(activé);
        Assert.False(sondé);
    }

    // ── Recherche de caractère jusqu'à la bulle ──────────────────────

    private static CharacterSearch RechercheVersFenetreAdministrateur(Action onEmit) =>
        new(new TextInsertionService(_ => { onEmit(); return new(2, 2); },
            activateTarget: _ => true, canInsert: () => true, targetAboveUs: _ => true));

    private static void EntreeSurLePremierResultat(CharacterSearch search)
    {
        search.Show();
        var edit = BancCapture.Field<IntPtr>(search, "_hEdit");
        Win32.SetWindowTextW(edit, "e");
        Assert.True(BancCapture.Field<System.Collections.IList>(search, "_filteredResults").Count > 0);
        BancCapture.SetField(search, "_selectedIndex", 0);
        BancCapture.SetField(search, "_targetWindow", (IntPtr)42);
        BancCapture.Call(search, "InsertSelectedCharacter");
    }

    [Fact]
    public void EW7_Recherche_FenetreAdministrateur_CopieEtBulleDediee()
    {
        using var isolation = new BancCapture.Isolation();
        int émissions = 0;
        var search = RechercheVersFenetreAdministrateur(() => émissions++);
        try
        {
            var dédiées = new List<string>();
            var génériques = new List<string>();
            var échecs = new List<string>();
            var copié = new List<string>();
            search.ElevatedTargetCopied += dédiées.Add;
            search.FallbackCopied += génériques.Add;
            search.FallbackCopyFailed += échecs.Add;
            search.ClipboardWriter = (_, texte) => { copié.Add(texte); return true; };

            EntreeSurLePremierResultat(search);

            Assert.Equal(0, émissions);
            Assert.Single(copié);
            Assert.Equal(copié, dédiées);
            Assert.Empty(génériques);
            Assert.Empty(échecs);
        }
        finally { BancCapture.Teardown(search); }
    }

    [Fact]
    public void EW7_Recherche_FenetreAdministrateur_CopieRefusee_EstAnnoncee()
    {
        using var isolation = new BancCapture.Isolation();
        var search = RechercheVersFenetreAdministrateur(() => { });
        try
        {
            var dédiées = new List<string>();
            var échecs = new List<string>();
            search.ElevatedTargetCopied += dédiées.Add;
            search.FallbackCopyFailed += échecs.Add;
            search.ClipboardWriter = (_, _) => false;

            EntreeSurLePremierResultat(search);

            Assert.Single(échecs);
            Assert.Empty(dédiées);
        }
        finally { BancCapture.Teardown(search); }
    }

    // ── Bulle ────────────────────────────────────────────────────────

    /// <summary>Le corps dépend du caractère : mesuré avec un caractère hors du plan de base
    /// (deux unités UTF-16), le pire cas de la recherche.</summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void EW7_Bulle_TientDansLesLimitesDeWindows(string langue)
    {
        string avant = L.Language;
        try
        {
            L.Language = langue;
            string titre = L.Layers_InsertElevatedTitle;
            string corps = L.Layers_InsertElevatedBody("𝕏");
            _sortie.WriteLine($"[{langue}] titre {titre.Length}/63, corps {corps.Length}/255");
            Assert.InRange(titre.Length, 1, 63);
            Assert.InRange(corps.Length, 1, 255);
            Assert.Contains("𝕏", corps);
            Assert.DoesNotContain("—", corps);
            Assert.DoesNotContain("'", corps);
        }
        finally { L.Language = avant; }
    }

    // ── Sonde réelle, lecture seule ──────────────────────────────────

    [Fact]
    public void EW7_Sonde_NotreNiveauSeLit()
    {
        using var nous = Process.GetCurrentProcess();
        Assert.True(TargetIntegrity.TryReadLevel(nous.Handle, out uint niveau));
        _sortie.WriteLine($"Niveau d'intégrité des tests : 0x{niveau:X4}");
        Assert.InRange(niveau, Low, System);
    }

    [Fact]
    public void EW7_Sonde_NosFenetres_PasAuDessus()
    {
        using var isolation = new BancCapture.Isolation();
        var search = new CharacterSearch(new TextInsertionService(_ => default, _ => false));
        try
        {
            search.Show();
            IntPtr hwnd = BancCapture.Field<IntPtr>(search, "_hWnd");
            Assert.NotEqual(IntPtr.Zero, hwnd);
            Assert.False(TargetIntegrity.IsWindowAboveUs(hwnd));
        }
        finally { BancCapture.Teardown(search); }
    }

    [Fact]
    public void EW7_Sonde_FenetrePerimee_PasAuDessus() =>
        Assert.False(TargetIntegrity.IsWindowAboveUs(new IntPtr(0x0BADF00D)));

    /// <summary>
    /// La barre des tâches (Explorateur, niveau moyen) n'est pas « au-dessus » d'un processus
    /// ordinaire : la sonde ne copie pas à tort vers les applications de même niveau. Les
    /// fenêtres visibles du bureau sont listées avec leur verdict, pour relecture (aucune
    /// assertion : leur liste dépend du poste).
    /// </summary>
    [Fact]
    public void EW7_Sonde_BarreDesTaches_PasAuDessus()
    {
        IntPtr barre = FindWindowW("Shell_TrayWnd", null);
        if (barre == IntPtr.Zero)
        {
            _sortie.WriteLine("Shell_TrayWnd absent (session sans Explorateur) : sonde non jouée.");
            return;
        }
        Assert.False(TargetIntegrity.IsWindowAboveUs(barre));

        var verdicts = new List<string>();
        EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;
            var titre = new StringBuilder(128);
            Win32.GetWindowTextW(hwnd, titre, titre.Capacity);
            if (titre.Length == 0) return true;
            TypingEngine.Windows.Win32.GetWindowThreadProcessIdOut(hwnd, out uint pid);
            string nom;
            try { using var p = Process.GetProcessById((int)pid); nom = p.ProcessName; }
            catch (ArgumentException) { nom = "?"; }
            catch (InvalidOperationException) { nom = "?"; }
            verdicts.Add($"{(TargetIntegrity.IsWindowAboveUs(hwnd) ? "AU-DESSUS" : "même/inférieur")} : {nom}");
            return true;
        }, IntPtr.Zero);
        foreach (var ligne in verdicts.Distinct().OrderBy(s => s, StringComparer.Ordinal))
            _sortie.WriteLine(ligne);
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? className, string? windowName);
}
