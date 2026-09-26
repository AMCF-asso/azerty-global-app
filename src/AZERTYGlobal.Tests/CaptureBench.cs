using System;
using System.Collections.Generic;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc de captures des fenêtres à contrôles : À propos, Durée de pause, Conflit de
/// disposition (au démarrage et après une bascule), Mes statistiques, Paramètres (les trois
/// onglets), Accueil (les trois étapes) et Couches maintenables. Une image par fenêtre, par
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
    private static readonly string[] SettingsTabs = { "general", "applications", "langue-maintenance" };

    /// <summary>Un message que laisse un geste de chaque onglet, dans le même ordre.</summary>
    private static readonly Func<string>[] SettingsMessages =
    {
        () => L.Settings_ShortcutsReset,
        () => L.Settings_CompatAdded("notepad.exe"),
        () => L.Settings_VirtualKeyboardWindowReset,
    };

    /// <summary>Les étapes de l'accueil. <c>_currentStep</c> va de 0 à 2.</summary>
    private const int OnboardingSteps = 3;

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

    private static void CaptureAbout(BancCapture.Target target)
    {
        var window = new AboutWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, "a-propos", windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
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
        }
        finally
        {
            BancCapture.Teardown(dialog);
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
            }
            finally
            {
                BancCapture.Teardown(window);
            }
        }
    }

    /// <summary>Statistiques d'un poste neuf : le fichier est vide, dans le dossier temporaire.</summary>
    private static void CaptureStats(BancCapture.Target target)
    {
        var window = new UsageStatsWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, "statistiques", windowDpi);
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
            window.Show();
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

                // Le même onglet après un geste qui laisse un message : la ligne de
                // validation doit se voir sur l'onglet qui l'a produit.
                BancCapture.Call(window, "SetValidationMessage", SettingsMessages[tab](), false);
                BancCapture.MarkActive(hwnd);
                target.Shoot(hwnd, $"parametres-{SettingsTabs[tab]}-message", windowDpi);

                // Un refus, « Forcer compatibilité » sur une app protégée : en rouge.
                if (SettingsTabs[tab] == "applications")
                {
                    BancCapture.Call(window, "SetRefusalMessage", L.Settings_CompatForceOnRefused);
                    BancCapture.MarkActive(hwnd);
                    target.Shoot(hwnd, "parametres-applications-refus", windowDpi);
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
    /// Les trois étapes, un fichier chacune, dans l'état d'un premier accueil (aucun exercice
    /// fait). L'étape est posée dans <c>_currentStep</c> puis <c>UpdateStepVisibility</c> la
    /// rend, comme le bouton Suivant, sans armer <c>_step3Reached</c> : la fermeture ne peut
    /// donc rien enregistrer, ni l'affichage au démarrage ni le lancement automatique.
    /// </summary>
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
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }
}
