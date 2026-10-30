using System;
using System.Collections.Generic;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le parcours de l'accueil, ouvert dans Leçons › Initiation (audit 1.3.0, B2 voie (b) : la
/// fenêtre d'exercices sombre a disparu) : écran des usages, premier exercice avec sa ligne de
/// parcours, exercice bonus avec « Passer cet exercice », écran final. Et, pour les Leçons,
/// l'indice quand la touche morte est armée, la bonne ou une autre.
///
/// Même porte que le reste du banc (<c>AZERTY_CONTEXTE</c>), même isolation, même mapper sur
/// <see cref="MockWin32Api"/>. Le parcours est posé par ses méthodes (<c>BeginTutorial</c>,
/// « Continuer », « Suivant ») sans <c>Show()</c>, qui prendrait le premier plan.
/// </summary>
public partial class KeyboardContextBench
{
    private const int VK_CAPITAL = 0x14;
    private static readonly short PressedBit = unchecked((short)0x8000);

    private sealed record TutorialState(string Slug, Action<LessonsWindow> Setup);

    private static readonly TutorialState[] TutorialStates =
    {
        // 1re page : le choix des usages, une case cochée.
        new("tuto-usages", w => BancCapture.Call(w, "ToggleTutorialUsage", 1)),
        // Exercice 1 du parcours Documents : « É », la ligne du parcours sous la consigne.
        new("tuto-parcours", w => BancCapture.Call(w, "StartTutorialRoute")),
        // Afrique puis Code : l'exercice Code arrive en bonus, avec « Passer cet exercice ».
        new("tuto-bonus", w =>
        {
            BancCapture.Call(w, "ToggleTutorialUsage", 2);
            BancCapture.Call(w, "ToggleTutorialUsage", 4);
            BancCapture.Call(w, "StartTutorialRoute");
            for (int i = 0; i < 4; i++) BancCapture.Call(w, "AdvanceTutorialRoute");
        }),
        // Écran final, après les quatre exercices communs réussis : sa liste de ce qui a été tapé.
        new("tuto-final", w =>
        {
            BancCapture.Call(w, "StartTutorialRoute");
            for (int i = 0; i < 4; i++)
            {
                ConfigManager.RecordLearningExerciseCompleted(i);
                BancCapture.Call(w, "AdvanceTutorialRoute");
            }
        }),
    };

    [Fact]
    public void RendLeTutorielEtLesIndicesDeToucheMorte()
    {
        string? outDir = BancCapture.OutputDirectory(GateVariable);
        if (outDir == null)
            return;

        Directory.CreateDirectory(outDir);
        var journal = new BancCapture.Journal(outDir, "banc-tutoriel.tsv");
        var failures = new List<string>();

        using (var isolation = new BancCapture.Isolation())
        {
            var layout = LayoutLoader.LoadFromResource();
            var api = new MockWin32Api();
            var mapper = new KeyMapper(layout, api);
            var hook = new KeyboardHook(mapper); // jamais installé

            foreach (string language in BancCapture.Languages)
            {
                isolation.SetLanguage(language);
                foreach (int dpi in BancCapture.Dpis)
                {
                    var target = new BancCapture.Target(outDir, language, dpi, journal);
                    var attempts = new List<(string Name, Action Run)>();
                    foreach (var state in TutorialStates)
                        attempts.Add((state.Slug, () => CaptureTutorialState(target, state, layout, mapper, hook)));
                    attempts.Add(("lecons-indice-etape2", () => CaptureLessonsDeadKeyHint(target, "lecons-indice-etape2", layout, api, mapper, hook, sameDeadKey: true)));
                    attempts.Add(("lecons-indice-autre-touche-morte", () => CaptureLessonsDeadKeyHint(target, "lecons-indice-autre-touche-morte", layout, api, mapper, hook, sameDeadKey: false)));

                    foreach (var (name, run) in attempts)
                    {
                        if (!BancCapture.Wanted(name))
                            continue;
                        try
                        {
                            run();
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{name}-{language}-{BancCapture.Percent(dpi)} : {ex.GetType().Name} : {ex.Message}");
                        }
                        finally
                        {
                            ResetMapper(api, mapper);
                        }
                    }
                }
            }

            journal.Header = isolation.Describe();
        }

        journal.Write();
        Assert.True(failures.Count == 0,
            $"{journal.Count} capture(s) écrite(s), {failures.Count} échec(s) :" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>Leçons ouvertes sur le parcours de l'accueil, l'état posé après la mise à
    /// l'échelle, comme en cours de parcours.</summary>
    private static void CaptureTutorialState(BancCapture.Target target, TutorialState state, Layout layout,
        KeyMapper mapper, KeyboardHook hook)
    {
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        // « Continuer » enregistre les usages cochés : chaque état repart sans usage, sinon
        // l'état suivant (autre langue, autre échelle) hériterait des cases du précédent.
        ConfigManager.SetLearningUsages(Array.Empty<string>());
        var window = new LessonsWindow(layout, mapper, hook);
        try
        {
            window.BeginTutorial();
            BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 4); // SW_SHOWNOACTIVATE
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.SetField(window, "_visible", true);
            state.Setup(window);
            Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, state.Slug, windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>
    /// Leçons, indice d'un caractère par touche morte (le cas « étape 1 » du banc), puis la
    /// touche morte armée : la sienne (l'indice passe à la touche finale) ou une autre
    /// (l'indice revient à l'armement).
    /// </summary>
    private static void CaptureLessonsDeadKeyHint(BancCapture.Target target, string slug, Layout layout,
        MockWin32Api api, KeyMapper mapper, KeyboardHook hook, bool sameDeadKey)
    {
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var window = new LessonsWindow(layout, mapper, hook);
        try
        {
            if (!(bool)BancCapture.Call(window, "SelectExercise", DeadKeyModule, DeadKeyLesson, 0, false)!)
                throw new InvalidOperationException($"exercice {DeadKeyModule}/{DeadKeyLesson} introuvable");
            BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 1);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.SetField(window, "_visible", true);
            ShowHintFrozen(window);

            object method = BancCapture.Field<object>(window, "_hintMethod")
                ?? throw new InvalidOperationException("aucun indice");
            string deadKey = (string?)method.GetType().GetProperty("DeadKey")!.GetValue(method) ?? "";
            if (deadKey.Length == 0)
                throw new InvalidOperationException("l'indice n'est pas une touche morte");
            string armed = sameDeadKey ? deadKey : (deadKey == "dk_acute" ? "dk_grave" : "dk_acute");
            ActivateDeadKey(api, mapper, armed);

            Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, slug, windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>Modificateur tenu : enfoncé pour Windows et suivi par le mapper.</summary>
    private static void Hold(MockWin32Api api, KeyMapper mapper, uint vk, uint scancode, uint flags)
    {
        api.AsyncKeyStateScript[(int)vk] = PressedBit;
        mapper.TrackModifiers(vk, scancode, flags, isKeyDown: true);
    }

    /// <summary>Arme une touche morte par sa touche d'activation, modificateurs compris, puis
    /// les relâche : le mapper garde la touche morte en attente.</summary>
    private static void ActivateDeadKey(MockWin32Api api, KeyMapper mapper, string deadKey)
    {
        var (key, layer) = CharacterIndex.Shared.DeadKeyActivations[deadKey];
        uint scancode = VirtualKeyboard.KeyCodeToScancode[key];
        bool shift = layer.Contains("Shift", StringComparison.Ordinal);
        bool altGr = layer.Contains("AltGr", StringComparison.Ordinal);
        if (shift) Hold(api, mapper, VK_LSHIFT, 0x2A, 0);
        if (altGr) Hold(api, mapper, VK_RMENU, 0x38, LLKHF_EXTENDED);
        mapper.ProcessKey(0, scancode, 0, isKeyDown: true);
        mapper.ProcessKey(0, scancode, 0, isKeyDown: false);
        if (altGr) Release(api, mapper, VK_RMENU, 0x38, LLKHF_EXTENDED);
        if (shift) Release(api, mapper, VK_LSHIFT, 0x2A, 0);
        if (mapper.ActiveDeadKey != deadKey)
            throw new InvalidOperationException($"{deadKey} non armée ({mapper.ActiveDeadKey ?? "aucune"})");
    }

    private static void Release(MockWin32Api api, KeyMapper mapper, uint vk, uint scancode, uint flags)
    {
        api.AsyncKeyStateScript.Remove((int)vk);
        mapper.TrackModifiers(vk, scancode, flags, isKeyDown: false);
    }

    /// <summary>Retour à l'état neutre entre deux images : Verr. Maj. éteinte, rien de tenu,
    /// aucune touche morte (<c>SyncState</c> l'annule et relâche les modificateurs).</summary>
    private static void ResetMapper(MockWin32Api api, KeyMapper mapper)
    {
        api.KeyStateScript.Remove(VK_CAPITAL);
        api.AsyncKeyStateScript.Clear();
        mapper.SyncState();
    }
}
