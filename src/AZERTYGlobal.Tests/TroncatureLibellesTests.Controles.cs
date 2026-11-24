using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit de régression 1.3.0, RA16 : ce que la couture du lot 8 ne voyait pas.
/// <list type="bullet">
/// <item>les contrôles natifs (BUTTON, STATIC, COMBOBOX) : Windows coupe leur texte au bord,
/// sans « … ». Leur texte est mesuré dans leur police contre leur zone cliente ;</item>
/// <item>les DPI autres que celui du poste : un WM_DPICHANGED synthétique porte chaque fenêtre
/// à 120, 144 et 192 DPI, sur une zone de travail simulée (<see cref="ÉcranPour"/>) ;</item>
/// <item>des états jamais visités : recherche sans résultat et par langue, refus et
/// confirmations des Paramètres, Statistiques éteintes, notification de bascule, indicateur de
/// couche ;</item>
/// <item>les bulles de la zone de notification, que Windows coupe à 63 et 255 caractères.</item>
/// </list>
/// </summary>
public sealed partial class TroncatureLibellesTests
{
    // ── DPI simulé ───────────────────────────────────────────────────

    /// <summary>DPI auquel les fenêtres du passage en cours sont portées ; 0 : celui du poste.
    /// Statique : les fonctions de parcours créent leurs fenêtres sans paramètre, et les tests
    /// d'une même classe ne tournent pas en parallèle.</summary>
    private static int _dpiSimulé;

    /// <summary>Échelle lue dans chaque fenêtre après la mise à l'échelle (champ
    /// <c>_dpiScale</c>, ou <c>_dpi</c> / 96) : preuve que le DPI demandé a été pris.</summary>
    private static readonly Dictionary<string, float> _échelles = new(StringComparer.Ordinal);

    /// <summary>
    /// Zone de travail d'un écran courant à ce DPI, barre des tâches (48 px à 100 %) retirée :
    /// 1920 × 1080 à 125 % et 150 % (portables), 3840 × 2160 à 200 %. Sans elle, le verdict
    /// dépendrait de l'écran du poste : à 200 % sur un écran de 1440 px de haut, les fenêtres
    /// plafonnent comme sur aucun écran vendu à cette échelle.
    /// </summary>
    private static Win32.RECT ÉcranPour(int dpi)
    {
        var (largeur, hauteur) = dpi >= 192 ? (3840, 2160) : (1920, 1080);
        return new Win32.RECT { left = 0, top = 0, right = largeur, bottom = hauteur - 48 * dpi / 96 };
    }

    /// <summary>Porte la fenêtre au DPI simulé (WM_DPICHANGED synthétique, voir
    /// <see cref="BancCapture.ApplyDpi"/>) et note l'échelle qu'elle en retient. Sans DPI
    /// simulé, ne fait que noter.</summary>
    private static void MettreÀLÉchelle(object fenêtre, IntPtr hwnd)
    {
        if (_dpiSimulé > 0)
            BancCapture.ApplyDpi(hwnd, _dpiSimulé);

        const BindingFlags champs = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = fenêtre.GetType();
        if (type.GetField("_dpiScale", champs)?.GetValue(fenêtre) is float échelle)
            _échelles[type.Name] = échelle;
        else if (type.GetField("_dpi", champs)?.GetValue(fenêtre) is int dpi)
            _échelles[type.Name] = dpi / 96f;
    }

    /// <summary>
    /// Précondition du banc : sous un DPI simulé (zone de travail de 1920 ou 3840 px), l'Accueil a la
    /// zone cliente qu'il demande. Une fenêtre plafonnée par l'écran réel du poste ou du runner
    /// (CI du 16/11 : 1028 px au lieu de 1440 à 192 DPI) ferait sortir « coupés » des titres qui
    /// tiennent : l'échec dit alors que c'est le banc, pas un libellé.
    /// </summary>
    private static void VérifierTailleDemandée(OnboardingWindow accueil, IntPtr hwnd)
    {
        if (_dpiSimulé <= 0)
            return;

        const BindingFlags constantes = BindingFlags.Static | BindingFlags.NonPublic;
        int largeurDeBase = (int)typeof(OnboardingWindow).GetField("BASE_WIN_W", constantes)!.GetRawConstantValue()!;
        float échelleInterface = (float)typeof(OnboardingWindow).GetField("ONBOARDING_UI_SCALE", constantes)!.GetRawConstantValue()!;
        int voulue = (int)(largeurDeBase * BancCapture.Field<float>(accueil, "_dpiScale") * échelleInterface);
        Win32.GetClientRect(hwnd, out var zoneCliente);
        Assert.True(zoneCliente.right - zoneCliente.left >= voulue,
            $"Banc : l'Accueil à {_dpiSimulé} DPI a {zoneCliente.right - zoneCliente.left} px de zone cliente pour {voulue} voulus "
            + "(plafond de Windows lié à l'écran réel : voir BancCapture.AllowSimulatedScreenSize).");
    }

    // ── Contrôles natifs ─────────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder name, int max);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageTexte(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);

    private const uint WM_GETFONT = 0x0031;
    private const uint CB_GETCOUNT = 0x0146, CB_GETLBTEXT = 0x0148, CB_GETLBTEXTLEN = 0x0149;
    private const int SM_CXVSCROLL = 2;
    private const uint DT_EXPANDTABS = 0x40;

    private static string Classe(IntPtr h)
    {
        var nom = new StringBuilder(64);
        GetClassNameW(h, nom, nom.Capacity);
        return nom.ToString();
    }

    private static string Texte(IntPtr h)
    {
        int longueur = Win32.GetWindowTextLengthW(h);
        if (longueur <= 0) return "";
        var texte = new StringBuilder(longueur + 1);
        Win32.GetWindowTextW(h, texte, texte.Capacity);
        return texte.ToString();
    }

    /// <summary>Taille de <paramref name="texte"/> dans la police du contrôle ; sur plusieurs
    /// lignes quand <paramref name="largeurDeRetour"/> est donnée (coupe aux mots).</summary>
    private static (int Largeur, int Hauteur) TailleDansLeContrôle(IntPtr contrôle, string texte, bool préfixe, int largeurDeRetour = 0)
    {
        IntPtr police = Win32.SendMessageW(contrôle, WM_GETFONT, IntPtr.Zero, IntPtr.Zero);
        IntPtr hdc = Win32.GetDC(contrôle);
        IntPtr ancienne = police != IntPtr.Zero ? Win32.SelectObject(hdc, police) : IntPtr.Zero;
        try
        {
            var calcul = new Win32.RECT { left = 0, top = 0, right = largeurDeRetour, bottom = 0 };
            uint format = Win32.DT_CALCRECT | (préfixe ? 0 : Win32.DT_NOPREFIX)
                | (largeurDeRetour > 0 ? Win32.DT_WORDBREAK | DT_EXPANDTABS : Win32.DT_SINGLELINE);
            Win32.DrawTextW(hdc, texte, -1, ref calcul, format);
            return (calcul.right - calcul.left, calcul.bottom - calcul.top);
        }
        finally
        {
            if (ancienne != IntPtr.Zero) Win32.SelectObject(hdc, ancienne);
            Win32.ReleaseDC(contrôle, hdc);
        }
    }

    /// <summary>
    /// Le texte du contrôle natif contre sa zone cliente. Rend les passages (un par texte, un
    /// par entrée de liste déroulante), ou rien pour un contrôle que le produit peint lui-même
    /// (BS_OWNERDRAW, SS_OWNERDRAW : la couture le voit), sans texte, de taille nulle, ou de
    /// saisie (EDIT : le contenu est à l'utilisateur). Les classes non mesurées sont notées
    /// dans <paramref name="nonMesurées"/>.
    ///
    /// Places que Windows prend sur la largeur, au DPI réel de la fenêtre (le thème dessine
    /// la case à ce DPI, même sous un DPI simulé) : 6 px de bord pour un bouton, 18 px de case
    /// et d'écart pour une case à cocher ou un bouton radio.
    /// </summary>
    private static IEnumerable<TruncationProbe.Passage> MesurerContrôle(IntPtr contrôle, HashSet<string> nonMesurées)
    {
        string classe = Classe(contrôle);
        Win32.GetClientRect(contrôle, out var client);
        int largeur = client.right - client.left, hauteur = client.bottom - client.top;
        if (largeur <= 0 || hauteur <= 0) yield break;

        int style = GetWindowLongW(contrôle, -16);
        int id = GetWindowLongW(contrôle, -12);
        int dpiRéel = Math.Max(96, Win32.GetDpiForWindow(contrôle));
        int Réel(int px) => (int)Math.Ceiling(px * dpiRéel / 96.0);

        if (classe.Equals("ComboBox", StringComparison.OrdinalIgnoreCase))
        {
            int dispo = largeur - Win32.GetSystemMetrics(SM_CXVSCROLL) - Réel(8);
            int nombre = (int)Win32.SendMessageW(contrôle, CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
            for (int i = 0; i < nombre; i++)
            {
                int longueur = (int)Win32.SendMessageW(contrôle, CB_GETLBTEXTLEN, (IntPtr)i, IntPtr.Zero);
                if (longueur <= 0) continue;
                var entrée = new StringBuilder(longueur + 1);
                SendMessageTexte(contrôle, CB_GETLBTEXT, (IntPtr)i, entrée);
                int besoin = TailleDansLeContrôle(contrôle, entrée.ToString(), préfixe: false).Largeur;
                yield return new TruncationProbe.Passage(entrée.ToString(), null, besoin, dispo, "natif ComboBox", id);
            }
            yield break;
        }

        string texte = Texte(contrôle);
        if (texte.Length == 0) yield break;

        if (classe.Equals("Button", StringComparison.OrdinalIgnoreCase))
        {
            int sorte = style & 0xF;
            bool plusieursLignes = (style & 0x2000) != 0; // BS_MULTILINE
            int place;
            string nom;
            switch (sorte)
            {
                case 0: case 1: // BS_PUSHBUTTON, BS_DEFPUSHBUTTON
                    place = Réel(6); nom = "natif Button"; break;
                case 2: case 3: case 4: case 5: case 6: case 9: // cases à cocher et boutons radio
                    place = Réel(18); nom = "natif Button (case)"; break;
                case 7: // BS_GROUPBOX
                    place = Réel(18); nom = "natif Button (groupe)"; plusieursLignes = false; break;
                default: // BS_OWNERDRAW et autres : peints par le produit
                    yield break;
            }
            int dispo = largeur - place;
            if (plusieursLignes)
            {
                var (l, h) = TailleDansLeContrôle(contrôle, texte, préfixe: true, largeurDeRetour: Math.Max(1, dispo));
                yield return l > dispo
                    ? new TruncationProbe.Passage(texte, null, l, dispo, nom, id)
                    : new TruncationProbe.Passage(texte, null, h, hauteur, nom + " (hauteur)", id);
            }
            else
                yield return new TruncationProbe.Passage(texte, null, TailleDansLeContrôle(contrôle, texte, préfixe: true).Largeur, dispo, nom, id);
            yield break;
        }

        if (classe.Equals("Static", StringComparison.OrdinalIgnoreCase))
        {
            int sorte = style & 0x1F;
            bool préfixe = (style & 0x80) == 0; // SS_NOPREFIX
            bool uneLigne = sorte is 0xB or 0xC   // SS_SIMPLE, SS_LEFTNOWORDWRAP
                || (style & 0xC000) != 0;         // SS_ENDELLIPSIS, SS_PATHELLIPSIS, SS_WORDELLIPSIS
            if (sorte is not (0 or 1 or 2 or 0xB or 0xC)) yield break; // icône, cadre, image, SS_OWNERDRAW
            if (uneLigne)
                yield return new TruncationProbe.Passage(texte, null, TailleDansLeContrôle(contrôle, texte, préfixe).Largeur, largeur, "natif Static", id);
            else
            {
                var (l, h) = TailleDansLeContrôle(contrôle, texte, préfixe, largeurDeRetour: largeur);
                yield return l > largeur
                    ? new TruncationProbe.Passage(texte, null, l, largeur, "natif Static", id)
                    : new TruncationProbe.Passage(texte, null, h, hauteur, "natif Static (hauteur)", id);
            }
            yield break;
        }

        if (!classe.Equals("Edit", StringComparison.OrdinalIgnoreCase))
            nonMesurées.Add(classe);
    }

    /// <summary>Les contrôles natifs affichés de la fenêtre, mesurés.</summary>
    private static List<TruncationProbe.Passage> MesurerContrôles(IntPtr hwnd, HashSet<string> nonMesurées)
    {
        var enfants = new List<IntPtr>();
        EnumChildWindows(hwnd, (h, _) => { enfants.Add(h); return true; }, IntPtr.Zero);
        return enfants.Where(e => Affiché(e, hwnd)).SelectMany(e => MesurerContrôle(e, nonMesurées)).ToList();
    }

    // ── États que le parcours ne visitait pas ────────────────────────

    /// <summary>Recherche : sans résultat, puis par nom de langue.</summary>
    private static void VisiterRechercheSansRésultat(Relevé relevé, CharacterSearch recherche, IntPtr hwnd)
    {
        IntPtr champ = BancCapture.Field<IntPtr>(recherche, "_hEdit");
        Win32.SetWindowTextW(champ, "zzqxzzqx");
        relevé.Visiter("Recherche · sans résultat", hwnd);
        Assert.Contains(relevé.Passages, x => x.Écran == "Recherche · sans résultat" && x.Passage.Text == L.Search_NoResults);

        Win32.SetWindowTextW(champ, "wolof");
        relevé.Visiter("Recherche · par langue", hwnd);
    }

    /// <summary>Paramètres : chaque refus et chaque confirmation, à sa place (sous les
    /// raccourcis, sous la liste des applications, au pied). Le message est un STATIC peint par
    /// le produit : sur une ligne il passe par la couture ; sur plusieurs, sa hauteur est
    /// comparée ici à celle du contrôle.</summary>
    private static void VisiterMessagesDesParamètres(Relevé relevé, SettingsWindow paramètres, IntPtr hwnd)
    {
        var type = typeof(SettingsWindow).GetNestedType("SettingsTab", BindingFlags.NonPublic)!;
        string touche = ConfigManager.GetShortcutDisplayName(0x7B); // F12
        var parOnglet = new (string Onglet, bool Refus, (string Nom, string Texte)[] Messages)[]
        {
            ("General", true, new[]
            {
                ("touche prise par la recherche", L.SettingsClarity_ShortcutTakenBySearch(touche)),
                ("touche prise par le clavier", L.SettingsClarity_ShortcutTakenByKeyboard(touche)),
                ("Ctrl seul", L.SettingsClarity_ShortcutCtrlOnly),
                ("lettre réservée", L.SettingsClarity_ShortcutReservedLetter('W')),
                ("pavé numérique", L.SettingsClarity_ShortcutKeypadDigit),
                ("autre touche", L.SettingsClarity_ShortcutOtherKey),
            }),
            ("Applications", true, new[]
            {
                ("anti-triche", L.SettingsClarity_CompatRefusedAntiCheat),
                ("prise en main à distance", L.SettingsClarity_CompatRefusedRemote),
            }),
            ("General", false, new[]
            {
                ("raccourcis rétablis", L.Settings_ShortcutsReset),
                ("fenêtre du clavier rétablie", L.Settings_VirtualKeyboardWindowReset),
                ("fenêtre des leçons rétablie", L.Settings_LessonsWindowReset),
            }),
        };

        IntPtr message = BancCapture.Field<IntPtr>(paramètres, "_hWndValidation");
        foreach (var (onglet, refus, messages) in parOnglet)
        {
            BancCapture.Call(paramètres, "SetActiveTab", Enum.Parse(type, onglet));
            foreach (var (nom, texte) in messages)
            {
                BancCapture.Call(paramètres, "ShowValidationMessage", texte, false, refus);
                string écran = $"Paramètres · {onglet} · {(refus ? "refus" : "confirmation")} : {nom}";
                relevé.Visiter(écran, hwnd);

                Win32.GetClientRect(message, out var cadre);
                Assert.True(cadre.right > 0 && cadre.bottom > 0, $"{écran} : le message « {texte} » n'a pas de cadre ({cadre.right} × {cadre.bottom}), place {BancCapture.Call(paramètres, "CurrentMessagePlace")}");
                int besoin = (int)BancCapture.Call(paramètres, "MessageHeight", texte, cadre.right)!;
                relevé.Ajouter(écran, new TruncationProbe.Passage(texte, null, besoin, cadre.bottom, "message des Paramètres (hauteur)", 0));
            }
        }
        BancCapture.Call(paramètres, "ShowValidationMessage", "", false, false);
    }

    /// <summary>Statistiques avec la collecte éteinte par la politique d'entreprise.</summary>
    private static void VisiterStatistiquesÉteintes(Relevé relevé)
    {
        using var politique = PolicyManager.OverrideForTests((_, valeur) => valeur == PolicyManager.ValueUsageStats ? 0 : null);
        Assert.False(UsageStats.CollectionEnabled);
        var statistiques = new UsageStatsWindow();
        try
        {
            MettreÀLÉchelle(statistiques, BancCapture.Handle(statistiques));
            BancCapture.Call(statistiques, "FitWindow");
            relevé.Visiter("Statistiques · collecte éteinte", BancCapture.Handle(statistiques));
        }
        finally { BancCapture.Teardown(statistiques); }
    }

    /// <summary>Notification de bascule (activé, désactivé), jamais montrée : peinte cachée.</summary>
    private static void VisiterBascule(Relevé relevé)
    {
        var bascule = new ToggleNotification();
        try
        {
            IntPtr hwnd = BancCapture.Handle(bascule);
            MettreÀLÉchelle(bascule, hwnd);
            foreach (bool activé in new[] { true, false })
            {
                BancCapture.SetField(bascule, "_currentActivated", activé);
                relevé.Visiter(activé ? "Bascule · activé" : "Bascule · désactivé", hwnd);
            }
        }
        finally { BancCapture.Teardown(bascule); }
    }

    /// <summary>Indicateur de couche : chaque libellé, seul, à un coup et verrouillé, dans la
    /// boîte que le produit lui donne (<see cref="LayerIndicatorWindow.IndicatorSize"/>). La
    /// fenêtre suit le curseur de saisie d'une autre application : le test peint sa boîte dans
    /// un DC en mémoire.</summary>
    private static void VisiterIndicateurDeCouche(Relevé relevé, int dpi)
    {
        IntPtr hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        IntPtr fond = Win32.CreateSolidBrush(0xFFFFFF);
        try
        {
            foreach (string couche in new[] { "dk_greek", "dk_cyrillic", "dk_scientific" })
            foreach (var (mode, suffixe) in new[]
                     {
                         (MaintainableLayerMode.Locked, L.Layers_IndicatorLockedSuffix),
                         (MaintainableLayerMode.OneShot, " · 1"),
                     })
            {
                string libellé = TrayApplication.GetMaintainableLayerLabel(couche) + suffixe;
                var (largeur, hauteur) = LayerIndicatorWindow.IndicatorSize(libellé.Length, dpi);
                relevé.Sous($"Indicateur de couche · {couche} {mode}", () => LayerIndicatorWindow.Paint(hdc,
                    new Win32.RECT { left = 0, top = 0, right = largeur, bottom = hauteur }, libellé, mode, fond, dpi));
            }
        }
        finally
        {
            Win32.DeleteObject(fond);
            Win32.DeleteDC(hdc);
        }
    }

    // ── Bulles de la zone de notification ────────────────────────────

    private static string RacineDesSources()
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier != null; dossier = dossier.Parent)
            if (File.Exists(Path.Combine(dossier.FullName, "src", "TrayApplication.cs")))
                return Path.Combine(dossier.FullName, "src");
        throw new DirectoryNotFoundException("src/TrayApplication.cs introuvable depuis les tests.");
    }

    /// <summary>
    /// Windows coupe sans rien dire le titre d'une bulle à 63 caractères et son corps à 255
    /// (<c>szInfoTitle[64]</c>, <c>szInfo[256]</c> de NOTIFYICONDATA). Chaque
    /// <c>ShowBalloon(L.Titre, L.Corps)</c> de TrayApplication est relu dans la source ; les
    /// chaînes sans paramètre sont mesurées dans les deux langues, les autres (nom de processus,
    /// caractère) sont listées : leur longueur dépend de l'exécution.
    /// </summary>
    [Fact]
    public void Les_bulles_tiennent_dans_les_limites_de_Windows()
    {
        string source = File.ReadAllText(Path.Combine(RacineDesSources(), "TrayApplication.cs"));
        var appels = Regex.Matches(source, @"ShowBalloon\(\s*(?<titre>[^,;]+?)\s*,\s*(?<corps>[^;]+?)\)\s*;", RegexOptions.Singleline)
            .Where(m => !m.Value.Contains("string title", StringComparison.Ordinal)).ToList();
        Assert.True(appels.Count >= 20, $"{appels.Count} appels de ShowBalloon lus : la lecture de la source a changé");

        var propriété = new Regex(@"^L\.(\w+)$");
        var titres = new HashSet<string>(StringComparer.Ordinal);
        var corps = new HashSet<string>(StringComparer.Ordinal);
        var nonMesurés = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match appel in appels)
        {
            foreach (var (groupe, noms) in new[] { ("titre", titres), ("corps", corps) })
            {
                // Une alternative « a ? L.X : L.Y » donne deux chaînes.
                foreach (string morceau in appel.Groups[groupe].Value.Split('?', ':').Select(s => s.Trim()))
                {
                    if (propriété.Match(morceau) is { Success: true } m)
                        noms.Add(m.Groups[1].Value);
                    else if (morceau.Contains("L.", StringComparison.Ordinal) || (!morceau.Contains(' ') && morceau.Length > 0 && char.IsLower(morceau[0])))
                        nonMesurés.Add($"{groupe} : {Regex.Replace(morceau, @"\s+", " ")}");
                }
            }
        }
        Assert.NotEmpty(titres);
        Assert.NotEmpty(corps);

        string avant = L.Language;
        var tropLongs = new List<string>();
        try
        {
            foreach (string langue in new[] { "fr", "en" })
            {
                L.Language = langue;
                foreach (var (noms, limite) in new[] { (titres, 63), (corps, 255) })
                foreach (string nom in noms)
                {
                    var p = typeof(L).GetProperty(nom, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    Assert.True(p != null, $"L.{nom} : lu dans TrayApplication.cs, absent de L");
                    string texte = (string)p!.GetValue(null)!;
                    if (texte.Length > limite)
                        tropLongs.Add($"L.{nom} [{langue}] : {texte.Length} caractères pour {limite}");
                }
            }
        }
        finally { L.Language = avant; }

        _sortie.WriteLine($"{titres.Count} titres et {corps.Count} corps de bulle mesurés dans les deux langues.");
        _sortie.WriteLine("Non mesurés (texte composé à l'exécution) : " + (nonMesurés.Count == 0 ? "aucun" : string.Join(" | ", nonMesurés)));
        Assert.True(tropLongs.Count == 0, "Bulles que Windows couperait :\n" + string.Join("\n", tropLongs));
    }

    // ── Témoins ──────────────────────────────────────────────────────

    /// <summary>Témoin : la couture voit un débordement de <see cref="PaperText.DrawLine"/>, et
    /// ne confond pas une ligne qui tient avec une ligne qui déborde.</summary>
    [Fact]
    public void La_couture_voit_une_ligne_qui_deborde_de_son_cadre()
    {
        var vus = new List<TruncationProbe.Passage>();
        TruncationProbe.Observer = vus.Add;
        IntPtr hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        try
        {
            var police = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f);
            int largeur = PaperText.Width(hdc, "Continuer les leçons", police);
            PaperText.DrawLine(hdc, "Continuer les leçons", police, new Win32.RECT { left = 5, top = 0, right = 5 + largeur, bottom = 30 }, 0);
            PaperText.DrawLine(hdc, "Continuer les leçons", police, new Win32.RECT { left = 5, top = 0, right = 5 + largeur - 1, bottom = 30 }, 0);

            Assert.Equal(2, vus.Count);
            Assert.All(vus, p => Assert.True(p.Overflow));
            Assert.False(vus[0].Cut, "une ligne qui tient au pixel près n'est pas un débordement");
            Assert.True(vus[1].Cut);
            Assert.Equal(largeur, vus[1].Needed);
            Assert.Equal(largeur - 1, vus[1].Available);
            Assert.EndsWith("TroncatureLibellesTests.Controles.cs", vus[1].File);
        }
        finally
        {
            TruncationProbe.Observer = null;
            Win32.DeleteDC(hdc);
        }
    }

    /// <summary>Témoin : la mesure des contrôles natifs voit un bouton, une case à cocher, un
    /// texte statique et une liste déroulante trop étroits, et pas les mêmes assez larges.</summary>
    [Fact]
    public void La_mesure_voit_un_controle_natif_trop_etroit()
    {
        using var isolation = new BancCapture.Isolation();
        const uint WS_CHILD = 0x40000000, WS_VISIBLE_ = 0x10000000;
        IntPtr instance = Win32.GetModuleHandleW(null);
        IntPtr parent = Win32.CreateWindowExW(0, "STATIC", "", 0x80000000 /* WS_POPUP */, 0, 0, 900, 400,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        try
        {
            const string texte = "Continuer les leçons maintenant";
            IntPtr Créer(string classe, uint style, int largeur, int hauteur)
            {
                IntPtr h = Win32.CreateWindowExW(0, classe, texte, WS_CHILD | WS_VISIBLE_ | style, 0, 0, largeur, hauteur,
                    parent, IntPtr.Zero, instance, IntPtr.Zero);
                Assert.NotEqual(IntPtr.Zero, h);
                return h;
            }
            var nonMesurées = new HashSet<string>();
            bool Coupé(IntPtr h) => MesurerContrôle(h, nonMesurées).Single().Cut;

            int juste = TailleDansLeContrôle(Créer("STATIC", 0xC, 600, 20), texte, préfixe: true).Largeur;
            Assert.True(juste > 60, $"largeur mesurée : {juste} px");

            Assert.False(Coupé(Créer("BUTTON", 0, juste + 40, 30)));
            Assert.True(Coupé(Créer("BUTTON", 0, juste, 30)), "bouton sans place pour ses bords");
            Assert.False(Coupé(Créer("BUTTON", 3, juste + 60, 20)));
            Assert.True(Coupé(Créer("BUTTON", 3, juste + 4, 20)), "case à cocher sans place pour sa case");
            Assert.False(Coupé(Créer("STATIC", 0xC, juste, 20)));
            Assert.True(Coupé(Créer("STATIC", 0xC, juste - 1, 20)), "texte statique sur une ligne");
            Assert.False(Coupé(Créer("STATIC", 0, juste / 2, 60)));
            Assert.True(Coupé(Créer("STATIC", 0, juste / 2, 14)), "texte statique replié, trop bas");

            IntPtr liste = Win32.CreateWindowExW(0, "COMBOBOX", "", WS_CHILD | WS_VISIBLE_ | 0x3 /* CBS_DROPDOWNLIST */ | 0x200 /* CBS_HASSTRINGS */,
                0, 0, juste, 200, parent, IntPtr.Zero, instance, IntPtr.Zero);
            SendMessageTexte(liste, 0x0143 /* CB_ADDSTRING */, IntPtr.Zero, new StringBuilder(texte));
            SendMessageTexte(liste, 0x0143, IntPtr.Zero, new StringBuilder("Oui"));
            var entrées = MesurerContrôle(liste, nonMesurées).ToList();
            Assert.Equal(2, entrées.Count);
            Assert.True(entrées[0].Cut, "entrée de liste plus large que la liste");
            Assert.False(entrées[1].Cut);
        }
        finally { Win32.DestroyWindow(parent); }
    }
}
