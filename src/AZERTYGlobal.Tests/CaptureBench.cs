using System;
using System.Collections.Generic;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc de captures des fenêtres à contrôles : À propos, Durée de pause, Conflit de
/// disposition (au démarrage et après une bascule), Mes statistiques, Paramètres (les trois
/// onglets), Accueil (les quatre étapes) et Couches maintenables. Une image par fenêtre, par
/// langue et par échelle.
///
/// Il existe parce que le contrôle visuel ne passe pas par le lancement de l'application :
/// Smart App Control refuse un exécutable fraîchement compilé, et une session de travail ne
/// lance de toute façon pas l'application. Le processus de test, lui, charge la DLL et ouvre
/// les fenêtres. Porté du banc de main (<c>5ee46c8</c>) sans la charte, et sans aucun crochet
/// dans <c>src/</c> : voir <see cref="BancCapture"/>.
///
/// ⚠️ Ce n'est pas un test : il n'affirme rien qu'un humain ne doive regarder. Sans la
/// variable <c>AZERTY_CAPTURE</c>, il sort tout de suite, et compte comme réussi.
///
/// PowerShell :
/// <code>
/// $env:AZERTY_CAPTURE = "1"
/// $env:AZERTY_CAPTURE_DIR = "D:\captures\avant"
/// dotnet test src\AZERTYGlobal.Tests\AZERTYGlobal.Tests.csproj -c Release --filter FullyQualifiedName~CaptureBench
/// </code>
/// </summary>
public class CaptureBench
{
    private const string GateVariable = "AZERTY_CAPTURE";

    /// <summary>Les onglets de Paramètres, dans l'ordre de l'énumération privée <c>SettingsTab</c>.</summary>
    private static readonly string[] SettingsTabs = { "general", "applications", "langue-maintenance", "lecons" };

    /// <summary>Un message que laisse un geste de chaque onglet, dans le même ordre. L'onglet
    /// Leçons n'en produit pas : un message d'un autre onglet y vérifie seulement la mise en page.</summary>
    private static readonly Func<string>[] SettingsMessages =
    {
        () => L.Settings_ShortcutsReset,
        () => L.Settings_CompatAdded("notepad.exe"),
        () => L.Settings_VirtualKeyboardWindowReset,
        () => L.Settings_VirtualKeyboardWindowReset,
    };

    /// <summary>Les étapes de l'accueil. <c>_currentStep</c> va de 0 à 2.</summary>
    private const int OnboardingSteps = OnboardingStep.Count;

    [Fact]
    public void RendLesFenetresAuxEchellesDemandees()
    {
        string? outDir = BancCapture.OutputDirectory(GateVariable);
        if (outDir == null)
            return;

        Directory.CreateDirectory(outDir);
        var journal = new BancCapture.Journal(outDir, "banc-fenetres.tsv");
        var failures = new List<string>();

        using (var isolation = new BancCapture.Isolation())
        {
            foreach (string language in BancCapture.Languages)
            {
                isolation.SetLanguage(language);
                foreach (int dpi in BancCapture.Dpis)
                {
                    var target = new BancCapture.Target(outDir, language, dpi, journal);
                    foreach (var (name, run) in new (string, Action<BancCapture.Target>)[]
                             {
                                 ("a-propos", CaptureAbout),
                                 ("duree-de-pause", CapturePause),
                                 ("conflit", CaptureConflict),
                                 ("statistiques", CaptureStats),
                                 ("parametres", CaptureSettings),
                                 ("accueil", CaptureOnboarding),
                                 ("couches", CaptureLayers),
                             })
                    {
                        if (!BancCapture.Wanted(name))
                            continue;
                        try
                        {
                            run(target);
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{name}-{language}-{BancCapture.Percent(dpi)} : {ex.GetType().Name} : {ex.Message}");
                        }
                    }
                }
            }

            // En fin de banc : comctl32 et les écrans ont alors servi.
            journal.Header = isolation.Describe();
        }

        journal.Write();
        Assert.True(failures.Count == 0,
            $"{journal.Count} capture(s) écrite(s), {failures.Count} échec(s) :" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>Canal Store, puis canal AMCF (S1-1 et S1-2) : le nom de l'association sans lien
    /// HelloAsso, et le canal après la version.</summary>
    private static void CaptureAbout(BancCapture.Target target)
    {
        CaptureOnChannel(DistributionChannel.Store, () => CaptureAboutOnce(target, "a-propos"));
        CaptureOnChannel(DistributionChannel.Amcf, () => CaptureAboutOnce(target, "a-propos-amcf"));
    }

    private static void CaptureAboutOnce(BancCapture.Target target, string slug)
    {
        var window = new AboutWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, slug, windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>Le canal de l'isolation (Store) ou un autre, le temps d'une capture.</summary>
    private static void CaptureOnChannel(DistributionChannel channel, Action capture)
    {
        using (AppChannel.OverrideForTests(channel))
            capture();
    }

    /// <summary>
    /// Durée de pause n'a qu'une porte publique, <c>Show(owner)</c>, qui pompe les messages
    /// jusqu'à la fermeture : le banc n'en reviendrait jamais. Il refait donc ce que fait
    /// <c>ShowModal</c> avant sa boucle, par réflexion : création, affichage, premier plan,
    /// focus sur les minutes.
    /// </summary>
    private static void CapturePause(BancCapture.Target target)
    {
        var dialog = new PauseDurationDialog();
        var horloge = PauseDurationDialog.Clock;
        // Heure figée : la ligne « Reprise à … » la montre.
        PauseDurationDialog.Clock = () => new DateTime(2026, 9, 28, 14, 30, 0);
        try
        {
            BancCapture.Call(dialog, "CreateWindow", IntPtr.Zero);
            IntPtr hwnd = BancCapture.Handle(dialog);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("CreateWindow n'a pas créé de fenêtre");
            Win32.ShowWindow(hwnd, 1);
            Win32.SetForegroundWindow(hwnd);
            Win32.SetFocus(BancCapture.Field<IntPtr>(dialog, "_hEditMinutes"));
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, "duree-de-pause", windowDpi);

            // Durée nulle : ligne de reprise en rouge, bouton grisé. EN_CHANGE met la ligne à
            // jour ; les messages postés passent avant la prise.
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(dialog, "_hEditMinutes"), "0");
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, "duree-de-pause-invalide", windowDpi);
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(dialog, "_hEditMinutes"), "5");

            // Le même état sur une surface noircie : ce qu'une fenêtre n'efface pas n'a
            // pas de couleur garantie. Sous DWM il sort en noir (vu par Antoine le 26/09 sur
            // cette fenêtre), le runner le rend blanc. Voir BancCapture.BlackenClient.
            BancCapture.BlackenClient(hwnd);
            target.Shoot(hwnd, "duree-de-pause-fond", windowDpi);
        }
        finally
        {
            BancCapture.Teardown(dialog);
            PauseDurationDialog.Clock = horloge;
        }
    }

    /// <summary>Les deux textes d'introduction : au démarrage, et après une bascule vers une
    /// autre disposition. Les deux actions ne sont jamais déclenchées : le banc ne clique pas.</summary>
    private static void CaptureConflict(BancCapture.Target target)
    {
        foreach (var (atStartup, slug) in new[] { (true, "conflit-demarrage"), (false, "conflit-bascule") })
        {
            var window = new LayoutConflictWindow(atStartup, () => { }, () => { });
            try
            {
                window.Show();
                IntPtr hwnd = BancCapture.Handle(window);
                int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
                BancCapture.MarkActive(hwnd);
                target.Shoot(hwnd, slug, windowDpi);
                ShootScrolledToEnd(target, window, hwnd, slug, windowDpi);
            }
            finally
            {
                BancCapture.Teardown(window);
            }
        }
    }

    /// <summary>Statistiques d'un poste neuf : le fichier est vide, dans le dossier temporaire.
    /// Puis sur le canal AMCF, collecte éteinte sans politique (S1-3) : la ligne qui dit qu'une
    /// stratégie de groupe peut la rallumer.</summary>
    private static void CaptureStats(BancCapture.Target target)
    {
        CaptureOnChannel(DistributionChannel.Store, () => CaptureStatsOnce(target, "statistiques"));
        CaptureOnChannel(DistributionChannel.Amcf, () => CaptureStatsOnce(target, "statistiques-amcf"));
    }

    private static void CaptureStatsOnce(BancCapture.Target target, string slug)
    {
        var window = new UsageStatsWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, slug, windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>
    /// Les trois onglets, un fichier chacun. <c>SetActiveTab</c> est privé et ne touche pas la
    /// barre d'onglets, que l'utilisateur a déjà cliquée quand il est appelé : le banc la
    /// sélectionne d'abord, sans notification, comme le fait l'ouverture de la fenêtre.
    /// </summary>
    private static void CaptureSettings(BancCapture.Target target)
    {
        var window = new SettingsWindow();
        try
        {
            // Liste des applications de la maquette (config isolée par le banc) : un jeu, un
            // éditeur, et la connexion à distance choisie, dont le mode s'écrit au-dessus des
            // boutons radio.
            ConfigManager.SetCompatibilityOverride("eldenring.exe", "forceOn");
            ConfigManager.SetCompatibilityOverride("Code.exe", "forceOff");
            ConfigManager.SetCompatibilityOverride("mstsc.exe", "forceOff");
            window.Show();
            BancCapture.Call(window, "RefreshCompatList", "mstsc.exe");
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            IntPtr tabStrip = BancCapture.Field<IntPtr>(window, "_hWndTabStrip");
            var tabType = typeof(SettingsWindow).GetNestedType("SettingsTab", System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingMemberException(nameof(SettingsWindow), "SettingsTab");

            for (int tab = 0; tab < SettingsTabs.Length; tab++)
            {
                Win32.SendMessageW(tabStrip, Win32.TCM_SETCURSEL, (IntPtr)tab, IntPtr.Zero);
                BancCapture.Call(window, "SetActiveTab", Enum.ToObject(tabType, tab));
                BancCapture.MarkActive(hwnd);
                target.Shoot(hwnd, $"parametres-{SettingsTabs[tab]}", windowDpi);

                // Capture d'un raccourci : la case Recherche attend sa touche, l'invite
                // s'écrit sous les raccourcis.
                if (SettingsTabs[tab] == "general")
                {
                    IntPtr search = BancCapture.Field<IntPtr>(window, "_hWndEditSearch");
                    Win32.SetFocus(search);
                    BancCapture.MarkActive(hwnd);
                    target.Shoot(hwnd, "parametres-general-capture", windowDpi);

                    // Une lettre réservée (T) : le refus dit ce qu'elle fait déjà, à la
                    // place de l'invite (lot « clarté »).
                    BancCapture.Call(window, "ApplyCapturedShortcut", search, (uint)'T');
                    BancCapture.MarkActive(hwnd);
                    target.Shoot(hwnd, "parametres-general-refus", windowDpi);
                    BancCapture.Call(window, "CancelShortcutCapture", search);
                    Win32.SetFocus(hwnd);
                }

                // Le même onglet après un geste qui laisse un message : la ligne de
                // validation doit se voir sur l'onglet qui l'a produit.
                BancCapture.Call(window, "SetValidationMessage", SettingsMessages[tab](), false);
                BancCapture.MarkActive(hwnd);
                target.Shoot(hwnd, $"parametres-{SettingsTabs[tab]}-message", windowDpi);
                RecordMessagePosition(target, window, $"parametres-{SettingsTabs[tab]}-message");

                // Un refus, « Compatibilité jeu » sur la connexion à distance choisie (mstsc),
                // puis sur un jeu à anti-triche : en rouge, avec la raison et ce qui se passe à
                // la place. Le vrai chemin du bouton radio, pas un texte posé à la main.
                if (SettingsTabs[tab] == "applications")
                {
                    BancCapture.Call(window, "ApplyCompatModeToSelection", "forceOn");
                    BancCapture.MarkActive(hwnd);
                    target.Shoot(hwnd, "parametres-applications-refus", windowDpi);

                    ConfigManager.SetCompatibilityOverride("valorant.exe", "forceOff");
                    BancCapture.Call(window, "RefreshCompatList", "valorant.exe");
                    BancCapture.Call(window, "ApplyCompatModeToSelection", "forceOn");
                    BancCapture.MarkActive(hwnd);
                    target.Shoot(hwnd, "parametres-applications-refus-jeu", windowDpi);
                    ConfigManager.SetCompatibilityOverride("valorant.exe", null);
                    BancCapture.Call(window, "RefreshCompatList", "mstsc.exe");
                }
                BancCapture.Call(window, "SetValidationMessage", string.Empty, false);
            }
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>
    /// Les quatre étapes, un fichier chacune, dans l'état d'un premier accueil (aucun exercice
    /// fait). L'étape est posée dans <c>_currentStep</c> puis <c>UpdateStepVisibility</c> la
    /// rend, comme le bouton Suivant, sans armer <c>_step3Reached</c> : la fermeture ne peut
    /// donc rien enregistrer, ni l'affichage au démarrage ni le lancement automatique.
    /// </summary>
    /// <summary>Position du message de validation dans la zone cliente, en pixels : le haut et le
    /// bas du contrôle tel qu'il est placé, contre la hauteur de la zone visible. Le
    /// message se voit sans défilement quand son bas ne dépasse pas la zone. Écrit
    /// <c>position-message.tsv</c> à côté des images.</summary>
    private static void RecordMessagePosition(BancCapture.Target target, SettingsWindow window, string slug)
    {
        int viewport = BancCapture.Field<int>(window, "_viewportHeight");
        int content = BancCapture.Field<int>(window, "_contentHeight");
        int scroll = BancCapture.Field<int>(window, "_scrollY");
        // Position réelle du contrôle dans la zone cliente, après défilement.
        IntPtr control = BancCapture.Field<IntPtr>(window, "_hWndValidation");
        Win32.GetWindowRect(control, out var screen);
        var topLeft = new Win32.POINT { x = screen.left, y = screen.top };
        var bottomRight = new Win32.POINT { x = screen.right, y = screen.bottom };
        IntPtr hwnd = BancCapture.Handle(window);
        Win32.ScreenToClient(hwnd, ref topLeft);
        Win32.ScreenToClient(hwnd, ref bottomRight);
        var rect = new Win32.RECT { top = topLeft.y + scroll, bottom = bottomRight.y + scroll };
        string path = Path.Combine(target.OutDir, "position-message.tsv");
        if (!File.Exists(path))
            File.WriteAllText(path, "capture\tdpi\tmessage_haut\tmessage_bas\thauteur_visible\thauteur_contenu\tdefilement\tvisible\n");
        File.AppendAllText(path, string.Join('\t', $"{slug}-{target.Language}-{BancCapture.Percent(target.Dpi)}", target.Dpi,
            rect.top - scroll, rect.bottom - scroll, viewport, content, scroll,
            rect.bottom - scroll <= viewport ? "oui" : "NON") + "\n");
    }

    private static void CaptureOnboarding(BancCapture.Target target)
    {
        var window = new OnboardingWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            for (int step = 0; step < OnboardingSteps; step++)
            {
                BancCapture.SetField(window, "_currentStep", step);
                BancCapture.Call(window, "UpdateStepVisibility");
                BancCapture.MarkActive(hwnd);
                target.Shoot(hwnd, $"accueil-etape{step + 1}", windowDpi);
            }

            // Étape 1 dans l'autre état de l'accord d'activation, en mémoire seulement : le
            // bouton dit « Activer et essayer » sans accord, « Essayer maintenant » avec.
            bool accepted = BancCapture.Field<bool>(window, "_activationAccepted");
            BancCapture.SetField(window, "_activationAccepted", !accepted);
            BancCapture.SetField(window, "_currentStep", 0);
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(window, "_hWndBtnTry"), (string)BancCapture.Call(window, "get_TryButtonText")!);
            BancCapture.Call(window, "UpdateStepVisibility");
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, accepted ? "accueil-etape1-sans-accord" : "accueil-etape1-avec-accord", windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    private static void CaptureLayers(BancCapture.Target target)
    {
        var window = new MaintainableLayersWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, "couches", windowDpi);
            ShootScrolledToEnd(target, window, hwnd, "couches", windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>
    /// Lot 130 A : une fenêtre qui défile cache son bas sur la première image. Quand elle défile,
    /// une image de plus la montre défilée jusqu'au bout (<c>{sujet}-bas</c>) : le bouton ou la
    /// carte du bas s'y voient entiers. Rien n'est ajouté quand tout tient.
    /// </summary>
    private static void ShootScrolledToEnd(BancCapture.Target target, object window, IntPtr hwnd, string slug, int windowDpi)
    {
        var scroll = BancCapture.Field<WindowScroll>(window, "_scroll");
        if (!scroll.Needed)
            return;

        scroll.ScrollTo(hwnd, int.MaxValue);
        BancCapture.MarkActive(hwnd);
        target.Shoot(hwnd, slug + "-bas", windowDpi);
    }
}
