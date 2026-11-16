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
    private const uint SC_E = 0x12;
    private const uint VK_E = 0x45;
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

    // ── C1.1 ──

    // Constat C1.1
    // Un coup grec armé, puis la touche morte accent aigu (SC028) : la composition s'arme alors
    // que le coup reste armé. Invariant M1.2 I6 : un coup et une composition ne coexistent jamais.
    // Décision d'Antoine (QCM du 16 novembre 2026) : l'accent annule le grec, une seule attente
    // à la fois. Test repris de la copie en attente, renforcé : le coup est annulé et la
    // composition reste armée (le test d'origine n'excluait que la coexistence).
    // Modèle suivi : KeyMapperMaintainableLayerCoverageTests.OtherDeadKeys_KeepClassicBehavior.
    [Fact]
    public void UnCoupDeCouche_EtUneCompositionDeToucheMorte_NeCoexistentJamais()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true);
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, false);

            // ActiveDeadKey rend la composition en priorité : « dk_acute » = composition armée.
            bool compositionArmee = mapper.ActiveDeadKey == "dk_acute";
            bool coupArme = mapper.MaintainableLayerState.Mode == MaintainableLayerMode.OneShot;
            Assert.False(compositionArmee && coupArme,
                "un coup de couche et une composition de touche morte sont armés en même temps");
            Assert.True(compositionArmee, "la touche morte reste armée : seul l'accent compte");
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    // Constat C1.1
    // Après le grec d'un coup puis l'accent aigu, la lettre suivante est une lettre française
    // accentuée (é), jamais une lettre grecque ; la frappe d'après est une lettre française
    // ordinaire (le coup n'est pas revenu).
    [Fact]
    public void UnCoupDeCouche_PuisAccent_LaLettreSuivanteEstFrancaiseAccentuee()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true);
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, false);
            mock.SendInputCalls.Clear();

            mapper.ProcessKey(VK_E, SC_E, 0, true);
            mapper.ProcessKey(VK_E, SC_E, 0, false);
            Assert.Equal('é', DernierCaractereEmis(mock));

            // Un « a » ordinaire passe au système tel quel : rien n'est émis par le moteur (pas d'alpha).
            mock.SendInputCalls.Clear();
            bool absorbee = mapper.ProcessKey(VK_A, SC_A, 0, true);
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            Assert.False(absorbee);
            Assert.Empty(mock.SendInputCalls);
        }
    }

    // Constat C1.1
    // L'indicateur de couche suit l'annulation : l'interface relit MaintainableLayerState et
    // DisplayDeadKey sur l'événement StateChanged du moteur. Après l'accent, l'événement a été
    // levé, la couche est inactive et la touche morte à visualiser est l'accent, plus le grec.
    [Fact]
    public void UnCoupDeCouche_PuisAccent_LIndicateurSuitLAnnulation()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal("dk_greek", mapper.DisplayDeadKey);

            var couchesVues = new List<MaintainableLayerMode>();
            var touchesMortesVues = new List<string?>();
            mapper.StateChanged += () =>
            {
                couchesVues.Add(mapper.MaintainableLayerState.Mode);
                touchesMortesVues.Add(mapper.DisplayDeadKey);
            };

            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true);

            Assert.NotEmpty(couchesVues);
            Assert.Equal(MaintainableLayerMode.Inactive, couchesVues[^1]);
            Assert.Equal("dk_acute", touchesMortesVues[^1]);
            Assert.Equal("dk_acute", mapper.DisplayDeadKey);
        }
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

    // Constat C1.2 (suite ME-06 : lot SendInput partiel)
    // Le moteur émet alpha, mais Windows n'accepte que le premier des deux événements (appui sans
    // relâchement) : le caractère a pu partir. Annoncé « rien émis », la file retirait son entrée
    // provisoire et l'exercice voyait un alpha sans attendu. La file garde l'attendu des caractères
    // dont au moins un événement est parti.
    // Modèle suivi : FileDesLecons_AccordTenu_AttendCeQueLeMoteurEmet (émetteur simulé du moteur).
    [Fact]
    public void FileDesLecons_LotPartiel_GardeUnAttenduCoherentAvecCeQuiAPuPartir()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);
            KeyDecision? decision = null;
            mapper.KeyDecided += d => decision = d;

            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true); // déclencheur tenu
            mock.SendInputCalls.Clear();
            mock.SendInputResult = 1; // chaque lot n'est accepté qu'à hauteur d'un événement

            buffer.CaptureKey(layout, SC_A, mapper);
            mapper.ProcessKey(VK_A, SC_A, 0, true);

            char emis = DernierCaractereEmis(mock);
            Assert.Equal('α', emis);
            Assert.Equal("α", decision!.Value.EmittedText);
            Assert.True(buffer.HasPending,
                "lot partiel annoncé « rien émis » : l'entrée provisoire a été retirée alors que le caractère a pu partir");
            Assert.Equal(emis, buffer.Resolve(emis));
            Assert.False(buffer.HasPending);
        }
    }

    // Constat C1.2 (suite ME-06 : rien n'est parti)
    // Aucun événement accepté : rien n'a été écrit, la file ne garde pas d'attendu qui ne viendra pas.
    [Fact]
    public void FileDesLecons_LotRefuStrictement_NAttendRien()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);

            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true);
            mock.SendInputCalls.Clear();
            mock.SendInputResult = 0;

            buffer.CaptureKey(layout, SC_A, mapper);
            mapper.ProcessKey(VK_A, SC_A, 0, true);

            Assert.False(buffer.HasPending);
        }
    }

    // Constat C1.2 (suite ME-06 : texte de plusieurs caractères coupé)
    // Touche morte accent aigu, puis « h » : le moteur émet « ´h », quatre événements (appui et
    // relâchement de chacun des deux caractères). Windows n'en accepte que les `acceptes` premiers :
    // la décision annonce les seuls caractères dont un événement au moins est parti, ni plus (le
    // second caractère n'a pas commencé) ni moins (le premier a au moins son appui).
    // Cas : 1 = coupure entre l'appui et le relâchement du premier caractère ; 2 = coupure entre les
    // deux caractères ; 3 = coupure entre l'appui et le relâchement du second ; 4 = lot complet.
    [Theory]
    [InlineData(1u, "´")]
    [InlineData(2u, "´")]
    [InlineData(3u, "´h")]
    [InlineData(4u, "´h")]
    public void FileDesLecons_TexteDePlusieursCaracteresCoupe_AnnonceLesCaracteresCommences(uint acceptes, string attendu)
    {
        const uint SC_H = 0x23;
        const uint VK_H = 0x48;
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var buffer = FileDesLeconsSuivant(mapper);
            KeyDecision? decision = null;
            mapper.KeyDecided += d => decision = d;

            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true); // touche morte armée, rien émis
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, false);
            mock.SendInputCalls.Clear();
            mock.SendInputResult = acceptes;

            buffer.CaptureKey(layout, SC_H, mapper);
            mapper.ProcessKey(VK_H, SC_H, 0, true);

            Assert.Equal(4, mock.SendInputCalls[0].Length); // témoin : « ´h » en quatre événements
            Assert.Equal(attendu, decision!.Value.EmittedText);
            Assert.True(buffer.HasPending, "le caractère parti n'a plus d'attendu dans la file");
            foreach (char c in attendu)
                Assert.Equal(c, buffer.Resolve(c));
            Assert.False(buffer.HasPending, "un attendu reste en file pour un caractère qui n'a pas commencé à partir");
        }
    }

    // Constat C1.5 (suite ME-12 : Entrée et Tab sortent avant la promotion d'un déclencheur en attente)
    // Entrée ou Tab, qui annule un coup de couche, sort de ProcessKeyCore par `return false` avant
    // l'étape « déclencheur en attente promu en coup », comme le faisait déjà Retour arrière. La
    // sortie ne change aucun résultat : un coup et un déclencheur en attente non promu ne coexistent
    // jamais (BeginTrigger efface le coup ; seuls EndTrigger, qui vide le déclencheur, et la promotion,
    // qui le marque « accord », arment un coup), et PromotePendingTriggerToOneShot ne fait rien sur un
    // accord déjà promu. Ces deux tests le caractérisent, et comparent chaque fois Entrée et Tab à
    // Retour arrière (comportement d'avant la branche).
    private const uint SC_ENTREE = 0x1C, VK_ENTREE = 0x0D;
    private const uint SC_TAB = 0x0F, VK_TAB = 0x09;
    private const uint SC_RETOUR = 0x0E, VK_RETOUR = 0x08;
    private const uint SC_FLECHE_GAUCHE = 0x4B, VK_FLECHE_GAUCHE = 0x25;
    private const uint LLKHF_EXTENDED = 0x01;

    // Coup armé, puis le déclencheur tenu de nouveau (le second appui remplace le coup : indicateur
    // inactif), puis la touche : aucun coup à annuler, le déclencheur en attente est promu en coup et
    // la lettre sort en grec, quelle que soit la touche. Tout relâché, rien ne reste armé.
    [Theory]
    [InlineData(VK_ENTREE, SC_ENTREE)]
    [InlineData(VK_TAB, SC_TAB)]
    [InlineData(VK_RETOUR, SC_RETOUR)]
    public void UnCoupDeCouche_SecondDeclencheurTenuPuisEntreeTabOuRetourArriere_LAccordSePromeutEtLaLettreSortEnGrec(uint vk, uint sc)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            KeyDecision? decision = null;
            mapper.KeyDecided += d => decision = d;

            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true); // second appui, tenu
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            Assert.False(mapper.ProcessKey(vk, sc, 0, true)); // la touche passe, l'accord est promu
            mapper.ProcessKey(vk, sc, 0, false);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
            Assert.Equal("dk_greek", mapper.MaintainableLayerState.LayerId);
            mock.SendInputCalls.Clear();

            mapper.ProcessKey(VK_A, SC_A, 0, true);
            Assert.Equal("α", decision!.Value.EmittedText);
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, false);
            MajRelachee(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    // Accord déjà promu par une touche qui ne le consomme pas (flèche, touche étendue) : le coup existe
    // alors que le déclencheur est encore tenu. Entrée, Tab et Retour arrière l'annulent de la même
    // façon (sortie anticipée comprise) : l'indicateur redevient inactif, la lettre suivante sort en
    // français, et le relâchement du déclencheur ne réarme rien.
    [Theory]
    [InlineData(VK_ENTREE, SC_ENTREE)]
    [InlineData(VK_TAB, SC_TAB)]
    [InlineData(VK_RETOUR, SC_RETOUR)]
    public void UnCoupDeCouche_AccordDejaPromuPuisEntreeTabOuRetourArriere_LeCoupEstAnnuleEtRienNeReste(uint vk, uint sc)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            KeyDecision? decision = null;
            mapper.KeyDecided += d => decision = d;

            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true);   // déclencheur tenu
            mapper.ProcessKey(VK_FLECHE_GAUCHE, SC_FLECHE_GAUCHE, LLKHF_EXTENDED, true); // accord promu en coup
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            Assert.False(mapper.ProcessKey(vk, sc, 0, true));
            mapper.ProcessKey(vk, sc, 0, false);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
            mock.SendInputCalls.Clear();

            mapper.ProcessKey(VK_A, SC_A, 0, true);
            Assert.Equal("a", decision!.Value.EmittedText);
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, false);
            MajRelachee(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
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

    // ── C1.5 ──

    // Constat C1.5
    // Appui bref du déclencheur (un coup grec), puis Entrée ou Tab : le coup n'a ni minuterie ni
    // annulation et la prochaine lettre sortirait en grec. Invariant M1.2 I5.
    // Décision d'Antoine (QCM du 16 novembre 2026) : Entrée et Tab annulent l'attente, la lettre
    // suivante sort en français. Test repris de la copie en attente ; ajouts : Entrée du pavé
    // numérique, la touche passe (le moteur ne l'absorbe pas), la lettre suivante est française.
    // Modèle suivi : KeyMapperMaintainableLayerTests (retour arrière) ; helpers de
    // KeyMapperMaintainableLayerCoverageTests.
    [Theory]
    [InlineData(0x0Du, 0x1Cu, 0u)] // Entrée
    [InlineData(0x09u, 0x0Fu, 0u)] // Tab
    [InlineData(0x0Du, 0x1Cu, 1u)] // Entrée du pavé numérique (touche étendue)
    public void UnCoupDeCouche_EstAnnuleParEntreeEtTab(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            bool absorbee = mapper.ProcessKey(vk, scanCode, flags, true);
            mapper.ProcessKey(vk, scanCode, flags, false);

            Assert.False(absorbee); // la touche passe à l'application
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            // Plus de grec : un « a » ordinaire passe au système tel quel, rien n'est émis par le moteur.
            mock.SendInputCalls.Clear();
            bool lettreAbsorbee = mapper.ProcessKey(VK_A, SC_A, 0, true);
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            Assert.False(lettreAbsorbee);
            Assert.Empty(mock.SendInputCalls);
        }
    }

    // Constat C1.5
    // L'indicateur de couche suit l'annulation par Entrée : événement StateChanged levé, couche
    // inactive et plus de touche morte à visualiser.
    [Theory]
    [InlineData(0x0Du, 0x1Cu)] // Entrée
    [InlineData(0x09u, 0x0Fu)] // Tab
    public void UnCoupDeCouche_AnnuleParEntreeOuTab_LIndicateurSuitLAnnulation(uint vk, uint scanCode)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal("dk_greek", mapper.DisplayDeadKey);

            var couchesVues = new List<MaintainableLayerMode>();
            mapper.StateChanged += () => couchesVues.Add(mapper.MaintainableLayerState.Mode);

            mapper.ProcessKey(vk, scanCode, 0, true);

            Assert.NotEmpty(couchesVues);
            Assert.Equal(MaintainableLayerMode.Inactive, couchesVues[^1]);
            Assert.Null(mapper.DisplayDeadKey);
        }
    }

    // Constat C1.5 (borne de la décision)
    // Les modificateurs seuls n'annulent jamais l'attente : il faut pouvoir taper la majuscule de
    // l'autre alphabet. Maj, Ctrl, Alt, AltGr et Verr. maj : appui puis relâchement, le coup reste armé.
    // Le relecteur avait noté « Maj ne devrait pas l'annuler : à vérifier » : vérifié ici.
    [Theory]
    [InlineData(0xA0u, 0x2Au, 0u)] // Maj gauche
    [InlineData(0xA1u, 0x36u, 0u)] // Maj droite
    [InlineData(0xA2u, 0x1Du, 0u)] // Ctrl gauche
    [InlineData(0xA3u, 0x1Du, 1u)] // Ctrl droite
    [InlineData(0xA4u, 0x38u, 0u)] // Alt gauche
    [InlineData(0xA5u, 0x38u, 1u)] // AltGr (Alt droite)
    [InlineData(0x14u, 0x3Au, 0u)] // Verr. maj
    [InlineData(0x90u, 0x45u, 0u)] // Verr. num
    public void UnCoupDeCouche_UnModificateurSeul_NAnnuleJamaisLAttente(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mock.AsyncKeyStateScript[(int)vk] = KeyDown;
            mapper.TrackModifiers(vk, scanCode, flags, true);
            mapper.ProcessKey(vk, scanCode, flags, true);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mock.AsyncKeyStateScript[(int)vk] = 0;
            mapper.TrackModifiers(vk, scanCode, flags, false);
            mapper.ProcessKey(vk, scanCode, flags, false);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
        }
    }

    // Constat C1.5 (borne de la décision)
    // Maj enfoncé après l'appui bref du déclencheur, puis une lettre : la majuscule de l'autre
    // alphabet (Α, grec) sort, et le coup est consommé.
    [Fact]
    public void UnCoupDeCouche_AvecMaj_LaMajusculeDeLAutreAlphabetSort()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);

            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_LSHIFT, SC_LSHIFT, 0, true);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
            mock.SendInputCalls.Clear();

            mapper.ProcessKey(VK_A, SC_A, 0, true);
            mapper.ProcessKey(VK_A, SC_A, 0, false);

            Assert.Equal('Α', DernierCaractereEmis(mock)); // U+0391, alpha majuscule grec
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    // Constat C1.5 (comportement antérieur, gardé par un test)
    // Retour arrière et Échap annulaient déjà l'attente avant la décision ; le test les garde.
    [Theory]
    [InlineData(0x08u, 0x0Eu)] // Retour arrière
    [InlineData(0x1Bu, 0x01u)] // Échap
    public void UnCoupDeCouche_EstAnnuleParRetourArriereEtEchap(uint vk, uint scanCode)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mapper.ProcessKey(vk, scanCode, 0, true);
            mapper.ProcessKey(vk, scanCode, 0, false);

            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    // Constat C1.5 (suite ME-12 : toute touche qui n'écrit pas de caractère annule l'attente)
    // Décision d'Antoine du 16 novembre 2026 (« Toutes annulent ») : après l'appui bref du déclencheur,
    // toute touche qui n'écrit aucun caractère annule le coup de couche, la lettre suivante sort en
    // français. Seuls Maj, Ctrl, Alt, AltGr, Verr. maj et Verr. num font exception (test précédent).
    // Règle du moteur : une touche étendue, ou absente de la disposition, annule le coup, sauf les six
    // exceptions ; les touches du pavé numérique qui écrivent un caractère (VK 0x60 à 0x6F) sont
    // dans la règle depuis la décision « Le chiffre annule » (tests du pavé numérique plus bas).
    // Chaque touche passe à l'application (jamais absorbée) et son relâchement ne réarme rien.
    private const uint E = LLKHF_EXTENDED;

    public static TheoryData<string, uint, uint, uint> ToucheSansCaractere() => new()
    {
        { "Flèche gauche", 0x25, 0x4B, E }, { "Flèche haut", 0x26, 0x48, E },
        { "Flèche droite", 0x27, 0x4D, E }, { "Flèche bas", 0x28, 0x50, E },
        { "Début", 0x24, 0x47, E }, { "Fin", 0x23, 0x4F, E },
        { "Page préc.", 0x21, 0x49, E }, { "Page suiv.", 0x22, 0x51, E },
        { "Inser", 0x2D, 0x52, E }, { "Suppr", 0x2E, 0x53, E },
        { "Début du pavé numérique (Verr. num éteint)", 0x24, 0x47, 0 },
        { "Applications", 0x5D, 0x5D, E }, { "Impr. écran", 0x2C, 0x37, E },
        { "F1", 0x70, 0x3B, 0 }, { "F2", 0x71, 0x3C, 0 }, { "F3", 0x72, 0x3D, 0 }, { "F4", 0x73, 0x3E, 0 },
        { "F5", 0x74, 0x3F, 0 }, { "F6", 0x75, 0x40, 0 }, { "F7", 0x76, 0x41, 0 }, { "F8", 0x77, 0x42, 0 },
        { "F9", 0x78, 0x43, 0 }, { "F10", 0x79, 0x44, 0 }, { "F11", 0x7A, 0x57, 0 }, { "F12", 0x7B, 0x58, 0 },
        { "F13", 0x7C, 0x64, 0 }, { "F18", 0x81, 0x69, 0 }, { "F24", 0x87, 0x76, 0 },
        { "Arrêt défil", 0x91, 0x46, 0 }, { "Pause", 0x13, 0x45, 0 },
        { "Win gauche", 0x5B, 0x5B, E }, { "Win droite", 0x5C, 0x5C, E },
        { "Volume muet", 0xAD, 0x20, E }, { "Volume moins", 0xAE, 0x2E, E }, { "Volume plus", 0xAF, 0x30, E },
        { "Piste suivante", 0xB0, 0x19, E }, { "Piste précédente", 0xB1, 0x10, E },
        { "Arrêt média", 0xB2, 0x24, E }, { "Lecture / pause", 0xB3, 0x22, E },
        { "Navigateur précédent", 0xA6, 0x6A, E }, { "Lancer la messagerie", 0xB4, 0x6C, E },
    };

    [Theory]
    [MemberData(nameof(ToucheSansCaractere))]
    public void UnCoupDeCouche_EstAnnuleParToutesLesTouchesSansCaractere(string nom, uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mock.AsyncKeyStateScript[(int)vk] = KeyDown;
            mapper.TrackModifiers(vk, scanCode, flags, true);
            bool absorbee = mapper.ProcessKey(vk, scanCode, flags, true);

            Assert.False(absorbee, nom + " : la touche doit passer à l'application");
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            mock.AsyncKeyStateScript[(int)vk] = 0;
            mapper.TrackModifiers(vk, scanCode, flags, false);
            mapper.ProcessKey(vk, scanCode, flags, false);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            // Plus de grec : un « a » ordinaire passe au système tel quel, rien n'est émis par le moteur.
            mock.SendInputCalls.Clear();
            Assert.False(mapper.ProcessKey(VK_A, SC_A, 0, true));
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            Assert.Empty(mock.SendInputCalls);
        }
    }

    // Constat C1.5 (suite ME-12 : l'indicateur suit l'annulation, comme pour Entrée)
    [Theory]
    [InlineData(0x27u, 0x4Du, 1u)]  // Flèche droite
    [InlineData(0x74u, 0x3Fu, 0u)]  // F5
    [InlineData(0x5Bu, 0x5Bu, 1u)]  // Win gauche
    public void UnCoupDeCouche_AnnuleParUneToucheSansCaractere_LIndicateurSuitLAnnulation(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal("dk_greek", mapper.DisplayDeadKey);

            var couchesVues = new List<MaintainableLayerMode>();
            mapper.StateChanged += () => couchesVues.Add(mapper.MaintainableLayerState.Mode);

            mapper.TrackModifiers(vk, scanCode, flags, true);
            mapper.ProcessKey(vk, scanCode, flags, true);

            Assert.NotEmpty(couchesVues);
            Assert.Equal(MaintainableLayerMode.Inactive, couchesVues[^1]);
            Assert.Null(mapper.DisplayDeadKey);
        }
    }

    // Constat C1.5 (suite ME-12 : bornes de la règle)
    // Le verrou de couche (double appui) n'est pas un coup : une touche sans caractère ne le défait
    // pas et la lettre suivante sort toujours en grec.
    [Theory]
    [InlineData(0x27u, 0x4Du, 1u)]  // Flèche droite
    [InlineData(0x74u, 0x3Fu, 0u)]  // F5
    [InlineData(0x5Bu, 0x5Bu, 1u)]  // Win gauche
    public void UneCoucheVerrouillee_NEstPasDefaiteParUneToucheSansCaractere(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            mapper.TrackModifiers(vk, scanCode, flags, true);
            mapper.ProcessKey(vk, scanCode, flags, true);
            mapper.TrackModifiers(vk, scanCode, flags, false);
            mapper.ProcessKey(vk, scanCode, flags, false);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            Assert.Equal('α', DernierCaractereEmis(mock));
        }
    }

    // Constat C1.5 (suite ME-12 : bornes de la règle)
    // L'attente d'une touche morte seule (sans coup de couche) garde son comportement : une flèche ou
    // F5 ne l'annule pas, l'accent reste armé pour la lettre suivante.
    [Theory]
    [InlineData(0x27u, 0x4Du, 1u)]  // Flèche droite
    [InlineData(0x74u, 0x3Fu, 0u)]  // F5
    public void UneToucheMorteSeule_N_EstPasAnnuleeParUneToucheSansCaractere(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true);
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, false);
            Assert.NotNull(mapper.DisplayDeadKey);

            mapper.ProcessKey(vk, scanCode, flags, true);
            mapper.ProcessKey(vk, scanCode, flags, false);

            Assert.NotNull(mapper.DisplayDeadKey);
        }
    }

    // Constat C1.5 (suite ME-12 : le chiffre annule)
    // Décision d'Antoine du 16 novembre 2026 (« Le chiffre annule ») : une touche du pavé numérique qui
    // écrit un caractère (Verr. num allumé : chiffres, + - * / et séparateur décimal, VK 0x60 à 0x6F)
    // sort son caractère normalement, le moteur ne l'absorbe ni ne la transforme, ET annule le coup de
    // couche : après la touche spéciale, seule la toute prochaine touche peut sortir dans l'autre alphabet.
    public static TheoryData<string, uint, uint, uint> ToucheDuPaveNumerique() => new()
    {
        { "0", 0x60, 0x52, 0 }, { "1", 0x61, 0x4F, 0 }, { "2", 0x62, 0x50, 0 }, { "3", 0x63, 0x51, 0 },
        { "4", 0x64, 0x4B, 0 }, { "5", 0x65, 0x4C, 0 }, { "6", 0x66, 0x4D, 0 }, { "7", 0x67, 0x47, 0 },
        { "8", 0x68, 0x48, 0 }, { "9", 0x69, 0x49, 0 },
        { "* (multiplier)", 0x6A, 0x37, 0 }, { "+ (ajouter)", 0x6B, 0x4E, 0 }, { "séparateur", 0x6C, 0x7E, 0 },
        { "- (soustraire)", 0x6D, 0x4A, 0 }, { ". (décimal)", 0x6E, 0x53, 0 }, { "/ (diviser)", 0x6F, 0x35, E },
    };

    [Theory]
    [MemberData(nameof(ToucheDuPaveNumerique))]
    public void UnCoupDeCouche_UneToucheDuPaveNumeriqueQuiEcritUnCaractere_SortNormalementEtAnnuleLAttente(
        string nom, uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
            mock.SendInputCalls.Clear();

            bool absorbee = mapper.ProcessKey(vk, scanCode, flags, true);

            Assert.False(absorbee, nom + " : le caractère sort normalement, le moteur ne l'absorbe pas");
            Assert.Empty(mock.SendInputCalls); // ni transformé ni remplacé par le moteur
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
            mapper.ProcessKey(vk, scanCode, flags, false);

            // Plus de grec : un « a » ordinaire passe au système tel quel, rien n'est émis par le moteur.
            Assert.False(mapper.ProcessKey(VK_A, SC_A, 0, true));
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            Assert.Empty(mock.SendInputCalls);
        }
    }

    // Constat C1.5 (suite ME-12 : le chiffre annule, l'indicateur suit)
    [Theory]
    [InlineData(0x61u, 0x4Fu, 0u)]  // 1
    [InlineData(0x6Bu, 0x4Eu, 0u)]  // +
    [InlineData(0x6Fu, 0x35u, 1u)]  // / (touche étendue)
    public void UnCoupDeCouche_AnnuleParUneToucheDuPaveNumerique_LIndicateurSuitLAnnulation(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal("dk_greek", mapper.DisplayDeadKey);

            var couchesVues = new List<MaintainableLayerMode>();
            mapper.StateChanged += () => couchesVues.Add(mapper.MaintainableLayerState.Mode);

            mapper.ProcessKey(vk, scanCode, flags, true);

            Assert.NotEmpty(couchesVues);
            Assert.Equal(MaintainableLayerMode.Inactive, couchesVues[^1]);
            Assert.Null(mapper.DisplayDeadKey);
        }
    }

    // Constat C1.5 (suite ME-12 : le chiffre annule, le verrou reste)
    [Theory]
    [InlineData(0x61u, 0x4Fu, 0u)]  // 1
    [InlineData(0x6Fu, 0x35u, 1u)]  // / (touche étendue)
    public void UneCoucheVerrouillee_NEstPasDefaiteParUneToucheDuPaveNumerique(uint vk, uint scanCode, uint flags)
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            AppuiBrefDeclencheur(mapper, mock);
            AppuiBrefDeclencheur(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            Assert.False(mapper.ProcessKey(vk, scanCode, flags, true));
            mapper.ProcessKey(vk, scanCode, flags, false);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            Assert.Equal('α', DernierCaractereEmis(mock));
        }
    }

    // Constat C1.5 (suite ME-12 : le chiffre annule, l'accent en attente est mesuré)
    // Un accent aigu armé (sans coup de couche), puis un chiffre du pavé numérique : le chiffre sort
    // normalement (non absorbé) et ne touche pas à l'attente de l'accent, comme les autres touches sans
    // caractère (test de la touche morte seule) ; la lettre suivante porte l'accent (é).
    [Fact]
    public void UneToucheMorteSeule_N_EstPasAnnuleeParUnChiffreDuPaveNumerique()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, true);
            mapper.ProcessKey(VK_ACUTE, SC_ACUTE, 0, false);
            Assert.NotNull(mapper.DisplayDeadKey);
            mock.SendInputCalls.Clear();

            Assert.False(mapper.ProcessKey(0x61, 0x4F, 0, true)); // 1 du pavé numérique
            mapper.ProcessKey(0x61, 0x4F, 0, false);
            Assert.NotNull(mapper.DisplayDeadKey);
            Assert.Empty(mock.SendInputCalls);

            Assert.True(mapper.ProcessKey(VK_E, SC_E, 0, true));
            Assert.Equal('é', DernierCaractereEmis(mock));
        }
    }

    // Constat C1.5 (suite ME-12 : bornes de la règle)
    // Appui maintenu : l'auto-répétition d'une touche sans caractère ne rejoue pas l'annulation. Accord
    // (déclencheur tenu) promu en coup par la flèche, la flèche reste enfoncée et se répète : le coup
    // promu n'est pas défait par la répétition, il l'est par un nouvel appui (comme avant la décision
    // pour Retour arrière, qui ne s'applique qu'au premier appui).
    [Fact]
    public void UneToucheSansCaractereQuiSeRepete_NAnnuleQueAuPremierAppui()
    {
        var (mapper, mock, monitor) = CreerMapper();
        using (monitor)
        {
            MajEnfoncee(mapper, mock);
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true);          // déclencheur tenu
            mapper.ProcessKey(VK_FLECHE_GAUCHE, SC_FLECHE_GAUCHE, LLKHF_EXTENDED, true);   // accord promu en coup
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mapper.ProcessKey(VK_FLECHE_GAUCHE, SC_FLECHE_GAUCHE, LLKHF_EXTENDED, true);   // répétition
            mapper.ProcessKey(VK_FLECHE_GAUCHE, SC_FLECHE_GAUCHE, LLKHF_EXTENDED, true);

            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);
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
