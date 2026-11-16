using AZERTYGlobal;
using TypingEngine.Windows;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 1 de l'analyse des machines à états (indicateur de couche). Chaque
/// test affirme l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot1Tests
{
    private const uint SC_TRIGGER_ALPHABET = 0x2B;  // SC02B : Maj = grec, AltGr = cyrillique
    private const uint VK_TRIGGER_ALPHABET = 0xDC;
    private const uint SC_ACUTE = 0x28;             // SC028 : touche morte accent aigu
    private const uint VK_ACUTE = 0xC0;
    private const uint SC_A = 0x10;
    private const uint VK_A = 0x41;
    private const uint SC_LSHIFT = 0x2A;
    private const uint VK_LSHIFT = 0xA0;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private static readonly short KeyDown = unchecked((short)0x8000);

    private static (KeyMapper Mapper, MockWin32Api Mock, ForegroundMonitor Monitor) CreerMapper()
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
        mock.SendInputCalls.Clear();
        return (mapper, mock, monitor);
    }

    private static void MajEnfoncee(KeyMapper mapper, MockWin32Api mock)
    {
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
    }

    private static void MajRelachee(KeyMapper mapper, MockWin32Api mock)
    {
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = 0;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, false);
    }

    /// <summary>Appui bref du déclencheur Maj+SC02B, Maj relâché : un coup grec armé.</summary>
    private static void AppuiBrefDeclencheur(KeyMapper mapper, MockWin32Api mock)
    {
        MajEnfoncee(mapper, mock);
        mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true);
        mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, false);
        MajRelachee(mapper, mock);
    }

    /// <summary>La file des Leçons suit la décision du moteur, comme LessonsWindow.</summary>
    private static PhysicalTextInputBuffer FileDesLeconsSuivant(KeyMapper mapper)
    {
        var buffer = new PhysicalTextInputBuffer(1000, () => 0);
        mapper.KeyDecided += d => buffer.ApplyEngineDecision(d.Handled, d.EmittedText);
        return buffer;
    }

    private static char DernierCaractereEmis(MockWin32Api mock)
    {
        Assert.NotEmpty(mock.SendInputCalls);
        var ki = mock.SendInputCalls[0][0].u.ki;
        Assert.True((ki.dwFlags & KEYEVENTF_UNICODE) != 0);
        return (char)ki.wScan;
    }

    // Constat C1.7
    // Classe de fenêtre refusée : l'indicateur existe sans fenêtre (HWND nul). Une couche qui
    // devient active ne doit alors rien placer ni repeindre : InvalidateRect(NULL) invalide toutes
    // les fenêtres.
    // Modèle suivi : Accessibilite130Tests (LayerIndicatorWindow).
    [Fact]
    public void IndicateurDeCouche_SansFenetre_NeRepeintRien()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetMaintainableLayersEnabled(true);
        ConfigManager.SetMaintainableVisualFeedbackEnabled(true);
        var invalidations = new List<IntPtr>();
        using var indicateur = new LayerIndicatorWindow((_, _) => false) { InvalidateWindow = invalidations.Add };

        indicateur.Update(new MaintainableLayerState("dk_greek", MaintainableLayerMode.OneShot, default), secureInput: false);

        Assert.Empty(invalidations);
    }

    // ── C1.2 ──

    // Constat C1.2 (variante A)
    // Maj tenu, déclencheur SC02B tenu, puis a : le moteur émet alpha (accord, un coup consommé)
    // mais CaptureKey, appelé avant ProcessKey, attend « A ». Invariant M1.7 I4 : la file reflète
    // ce que le moteur émet réellement.
    // Modèle suivi : Audit0911Lot8bTests.Leçons_CoucheGrecqueUnCoup_LaFrappeResteGrecque.
    [Fact]
    public void FileDesLecons_AccordTenu_AttendCeQueLeMoteurEmet()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);

            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true); // déclencheur tenu

            buffer.CaptureKey(layout, SC_A, mapper);   // le hook capture avant le moteur
            mapper.ProcessKey(VK_A, SC_A, 0, true);

            char emis = DernierCaractereEmis(mock);
            Assert.Equal('α', emis);
            Assert.Equal(emis, buffer.Resolve(emis));
            Assert.False(buffer.HasPending);
        }
    }

    // Constat C1.2 (variante B)
    // Appui bref du déclencheur, Maj d'activation encore tenu, puis a : le moteur neutralise le
    // Maj d'activation et émet alpha ; CaptureKey calcule la majuscule Alpha.
    // Modèle suivi : Audit0911Lot8bTests.Leçons_CoucheGrecqueUnCoup_LaFrappeResteGrecque.
    [Fact]
    public void FileDesLecons_MajDActivationEncoreTenu_AttendCeQueLeMoteurEmet()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);

            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, false); // Maj reste tenu
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            buffer.CaptureKey(layout, SC_A, mapper);
            mapper.ProcessKey(VK_A, SC_A, 0, true);

            char emis = DernierCaractereEmis(mock);
            Assert.Equal('α', emis);
            Assert.Equal(emis, buffer.Resolve(emis));
            Assert.False(buffer.HasPending);
        }
    }

    // ── C1.3 ──

    // Constat C1.3 (variante déclencheur)
    // Un coup armé, second appui du déclencheur (qui verrouille la couche) : le moteur n'émet rien
    // mais CaptureKey met « µ » en file. Invariant M1.7 I5 : toute entrée correspond à un WM_CHAR à venir.
    // Modèle suivi : Audit0911Lot8bTests.Leçons_CoucheGrecqueVerrouillée_LaFrappeResteGrecque_PasReplaceeParLeLatin.
    [Fact]
    public void FileDesLecons_SecondAppuiDuDeclencheurSousUnCoup_NAttendAucunCaractere()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);

            AppuiBrefDeclencheur(mapper, mock);
            MajEnfoncee(mapper, mock);
            mock.SendInputCalls.Clear();

            buffer.CaptureKey(layout, SC_TRIGGER_ALPHABET, mapper);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true);

            Assert.Empty(mock.SendInputCalls); // le moteur n'émet aucun texte
            Assert.False(buffer.HasPending);
        }
    }

    // Constat C1.3 (variante touche morte)
    // Un coup armé, puis la touche morte accent aigu : le moteur n'émet rien (composition armée)
    // mais CaptureKey met l'accent isolé en file.
    // Modèle suivi : Audit0911Lot8bTests.Leçons_CoucheGrecqueUnCoup_LaFrappeResteGrecque.
    [Fact]
    public void FileDesLecons_ToucheMorteSousUnCoup_NAttendAucunCaractere()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);

            AppuiBrefDeclencheur(mapper, mock);
            mock.SendInputCalls.Clear();

            buffer.CaptureKey(layout, SC_ACUTE, mapper);
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true);

            Assert.Empty(mock.SendInputCalls);
            Assert.False(buffer.HasPending);
        }
    }

    // ── C1.4 ──

    // Constat C1.4
    // Maj tenu, déclencheur SC02B tenu, a (alpha, un coup consommé), puis un second a : le Maj
    // d'activation reste neutralisé, le moteur doit émettre « a ». CanPassThrough lit le Maj
    // physique et laisse passer la touche native, d'où « A ». Invariant M1.2 I7.
    // Modèle suivi : KeyMapperMaintainableLayerCoverageTests.ActivationShift_IsConsumedWhileHeld.
    [Fact]
    public void MajDActivationNeutralise_LaSecondeLettreNeFuitPasEnMajusculeNative()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            MajEnfoncee(mapper, mock);
            Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true));

            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            Assert.Equal('α', DernierCaractereEmis(mock));
            mapper.ProcessKey(VK_A, SC_A, 0, false);

            // Couche consommée, Maj d'activation toujours tenu.
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
            mock.SendInputCalls.Clear();

            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true),
                "la touche est passée au système avec Maj physique tenu : Windows produirait A");
            Assert.Equal('a', DernierCaractereEmis(mock));
        }
    }

    // ── C1.6 ──

    // Constat C1.6
    // Maj+SC02B tenu, Maj relâché, SC02B toujours enfoncé : l'auto-répétition rejoue les effets
    // de ProcessKeyCore (étoile émise, ou scancode périmé dans _passedThroughKeys). Invariant
    // M1.4 I1 : chaque répétition garde la décision de l'appui initial, effets compris.
    // Modèle suivi : KeyMapperMaintainableLayerCoverageTests.ActivationShift_IsConsumedWhileHeld
    // (ClearPassedThroughKeys envoie un keyup par scancode suivi : témoin sans réflexion).
    [Fact]
    public void RepetitionDuDeclencheur_ApresRelachementDeMaj_NEmetRienEtNeSuitRien()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            MajEnfoncee(mapper, mock);
            Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true));
            MajRelachee(mapper, mock);
            mock.SendInputCalls.Clear();

            // Auto-répétition système de la touche toujours enfoncée.
            Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true));
            Assert.Empty(mock.SendInputCalls); // pas d'étoile parasite

            // Aucun scancode périmé : la purge n'a aucun keyup à envoyer.
            mapper.ClearPassedThroughKeys();
            Assert.Empty(mock.SendInputCalls);
        }
    }
}
