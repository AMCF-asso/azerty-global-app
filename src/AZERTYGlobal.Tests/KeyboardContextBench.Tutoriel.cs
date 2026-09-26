using System;
using System.Collections.Generic;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le tutoriel dans les états qui changent son clavier : chaque exercice, les trois genres de
/// surlignage (direct, étape 1, étape 2 avec pastille), Verr. Maj. gardée des exercices 1 et 2,
/// modificateurs tenus, touche enfoncée, touche morte active, Retour arrière demandé, erreur,
/// pages de choix et finale, voile « Cliquez pour reprendre ». Et, pour les Leçons, l'indice
/// quand la touche morte est armée, la bonne ou une autre. Préalable du lot 9 (audit du 25/09,
/// L-01) : le tutoriel passe sur <see cref="KeyboardRenderer"/>, et ces images en sont la
/// référence au pixel.
///
/// Même porte que le reste du banc (<c>AZERTY_CONTEXTE</c>), même isolation, même mapper sur
/// <see cref="MockWin32Api"/>. Les états sont posés comme la fenêtre les pose : frappes par
/// <c>OnChar</c>, touches mortes et Verr. Maj. par le mapper. Seuls l'exercice courant, la page
/// finale, le voile et la touche enfoncée sont écrits par réflexion : les atteindre par la
/// fenêtre prendrait le premier plan (<c>TakeFocus</c>) ou une minuterie.
/// </summary>
public partial class KeyboardContextBench
{
    private const int VK_CAPITAL = 0x14;
    private static readonly short PressedBit = unchecked((short)0x8000);

    private sealed record TutorialState(string Slug, Action<LearningModule, MockWin32Api, KeyMapper> Setup);

    private static readonly TutorialState[] TutorialStates =
    {
        // Exercice 1 : Verr. Maj. activée, pleine en vert, et la touche É en contour.
        new("tuto-ex1-verrmaj", (m, api, mapper) => { CapsLockOn(api, mapper); }),
        // Exercice 2 : Verr. Maj. gardée ; l'espace est dans la couche Caps.
        new("tuto-ex2-espace", (m, api, mapper) => { GoTo(m, 1); CapsLockOn(api, mapper); Type(m, "GRÂCE"); }),
        // Exercice 2 : Â par touche morte, étape 1 et pastille ; Verr. Maj. reste « direct ».
        new("tuto-ex2-etape1", (m, api, mapper) => { GoTo(m, 1); CapsLockOn(api, mapper); Type(m, "GR"); }),
        // Exercice 3 : premier caractère, et la même page avec Maj tenue.
        new("tuto-ex3", (m, api, mapper) => GoTo(m, 2)),
        new("tuto-ex3-maj-tenue", (m, api, mapper) => { GoTo(m, 2); Hold(api, mapper, VK_LSHIFT, 0x2A, 0); }),
        // Exercice 3 : une faute, le caractère courant en rouge.
        new("tuto-ex3-erreur", (m, api, mapper) => { GoTo(m, 2); Type(m, "x"); }),
        // Exercice 3 : la touche surlignée enfoncée.
        new("tuto-ex3-enfoncee", (m, api, mapper) => { GoTo(m, 2); BancCapture.SetField(m, "_pressedScancode", 0x24u); }),
        // Exercice 4 : L demande Maj ; æ demande AltGr, puis AltGr tenue (pleine en vert).
        new("tuto-ex4-maj", (m, api, mapper) => GoTo(m, 3)),
        new("tuto-ex4-altgr", (m, api, mapper) => { GoTo(m, 3); Type(m, "L"); }),
        new("tuto-ex4-altgr-tenue", (m, api, mapper) => { GoTo(m, 3); Type(m, "L"); Hold(api, mapper, VK_RMENU, 0x38, LLKHF_EXTENDED); }),
        // Exercice 5, facultatif : titre « (Bonus) », bouton « Passer », accolade par AltGr.
        new("tuto-ex5", (m, api, mapper) => { GoTo(m, 4); Type(m, "type Config = "); }),
        // Exercice 6 : aides des langues (◌/, ¿, ¡), ã par touche morte, étape 1.
        new("tuto-ex6-etape1", (m, api, mapper) => { GoTo(m, 5); Type(m, "S"); }),
        // Exercice 6 : ó, la bonne touche morte armée : étape 2 et clavier des résultats.
        new("tuto-ex6-etape2", (m, api, mapper) => { GoTo(m, 5); Type(m, "São Paulo, C"); ActivateDeadKey(api, mapper, "dk_acute"); }),
        // Exercice 6 : ã attendu, une autre touche morte armée : Retour arrière demandé.
        new("tuto-ex6-retour", (m, api, mapper) => { GoTo(m, 5); Type(m, "S"); ActivateDeadKey(api, mapper, "dk_acute"); }),
        // Page de choix après le premier succès, page finale, voile de perte du focus.
        new("tuto-choix", (m, api, mapper) => Type(m, "É")),
        new("tuto-final", (m, api, mapper) => ShowFinalPage(m)),
        new("tuto-pause", (m, api, mapper) => BancCapture.SetField(m, "_focusLostConfirmed", true)),
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
                        attempts.Add((state.Slug, () => CaptureTutorialState(target, state, layout, api, mapper, hook)));
                    attempts.Add(("lecons-indice-etape2", () => CaptureLessonsDeadKeyHint(target, "lecons-indice-etape2", layout, api, mapper, hook, sameDeadKey: true)));
                    attempts.Add(("lecons-indice-autre-touche-morte", () => CaptureLessonsDeadKeyHint(target, "lecons-indice-autre-touche-morte", layout, api, mapper, hook, sameDeadKey: false)));

                    foreach (var (name, run) in attempts)
                    {
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

    /// <summary>Même ouverture que <see cref="CaptureLearningModule"/> ; l'état est posé après
    /// la mise à l'échelle, comme en cours d'exercice.</summary>
    private static void CaptureTutorialState(BancCapture.Target target, TutorialState state, Layout layout,
        MockWin32Api api, KeyMapper mapper, KeyboardHook hook)
    {
        var module = new LearningModule(IntPtr.Zero, mapper, hook, layout);
        try
        {
            IntPtr hwnd = BancCapture.Handle(module);
            Win32.ShowWindow(hwnd, 4); // SW_SHOWNOACTIVATE
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            state.Setup(module, api, mapper);
            Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, state.Slug, windowDpi);
        }
        finally
        {
            BancCapture.Teardown(module);
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
            BancCapture.Call(window, "ShowHintCore", true);

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

    /// <summary>Exercice <paramref name="step"/> (0 à 5), curseur au début, comme après
    /// « Exercice suivant », sans reprendre le premier plan.</summary>
    private static void GoTo(LearningModule module, int step)
    {
        BancCapture.SetField(module, "_currentStep", step);
        BancCapture.Call(module, "UpdateHighlight");
        BancCapture.Call(module, "UpdateControlVisibility");
        BancCapture.Call(module, "RepositionControls");
    }

    private static void ShowFinalPage(LearningModule module)
    {
        BancCapture.SetField(module, "_currentStep", 6);
        BancCapture.SetField(module, "_completed", true);
        BancCapture.Call(module, "ClearHighlight");
        BancCapture.Call(module, "UpdateControlVisibility");
        BancCapture.Call(module, "RepositionControls");
    }

    /// <summary>Frappes reçues par la fenêtre, telles que WM_CHAR les livre.</summary>
    private static void Type(LearningModule module, string text)
    {
        foreach (char c in text)
            BancCapture.Call(module, "OnChar", c);
    }

    /// <summary>Verr. Maj. allumée côté Windows, puis relue par le mapper.</summary>
    private static void CapsLockOn(MockWin32Api api, KeyMapper mapper)
    {
        api.KeyStateScript[VK_CAPITAL] = 1;
        mapper.SyncState();
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
