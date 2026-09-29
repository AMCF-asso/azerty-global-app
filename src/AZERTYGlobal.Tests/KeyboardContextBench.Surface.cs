using System;
using System.Collections.Generic;
using AZERTYGlobal;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Leçons, surface de frappe unique (lot Leçons 1.3.0) : texte en cours, faute en mode strict,
/// fautes en mode souple, et la même saisie sans le focus, pour chacune des deux variantes
/// rendues pour le choix sur planches. Les frappes passent par <c>OnChar</c>, comme au clavier.
/// </summary>
public partial class KeyboardContextBench
{
    // Mode strict : seuls les exercices d'initiation le sont ; le 4e est une phrase longue.
    private const string StrictModule = LessonCatalogLoader.InitiationModuleId;
    private const string StrictLesson = LessonCatalogLoader.InitiationLessonId;
    private const int StrictExercise = 3;
    private const string StrictTyped = "Lætitia demande « d'o";

    // Mode souple : deux fautes classiques, É tapé E et À tapé A.
    private const string FlexibleModule = "majuscules-accentuees";
    private const string FlexibleLesson = "caps-all";
    private const string FlexibleTyped = "LES ELÈVES VONT A L'";

    private sealed record SurfaceState(string Slug, string Module, string Lesson, int Exercise, string Typed, bool Focus);

    private static readonly SurfaceState[] SurfaceStates =
    {
        new("saisie", StrictModule, StrictLesson, StrictExercise, StrictTyped, Focus: true),
        new("faute-stricte", StrictModule, StrictLesson, StrictExercise, StrictTyped + "u", Focus: true),
        new("faute-souple", FlexibleModule, FlexibleLesson, 0, FlexibleTyped, Focus: true),
        new("sans-focus", StrictModule, StrictLesson, StrictExercise, StrictTyped, Focus: false),
    };

    private static void AddLessonSurfaceStates(List<(string Name, Action Run)> attempts, BancCapture.Target target,
        Layout layout, KeyMapper mapper, KeyboardHook hook)
    {
        foreach (int variant in new[] { 1, 2 })
        {
            foreach (var state in SurfaceStates)
            {
                string slug = $"lecons-surface-v{variant}-{state.Slug}";
                attempts.Add((slug, () => CaptureLessonSurface(target, slug, layout, mapper, hook, variant, state)));
            }
        }
    }

    private static void CaptureLessonSurface(BancCapture.Target target, string slug, Layout layout, KeyMapper mapper,
        KeyboardHook hook, int variant, SurfaceState state)
    {
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        int previousVariant = LessonsWindow.SurfaceVariant;
        LessonsWindow.SurfaceVariant = variant;
        var window = new LessonsWindow(layout, mapper, hook);
        try
        {
            if (!(bool)BancCapture.Call(window, "SelectExercise", state.Module, state.Lesson, state.Exercise, false)!)
                throw new InvalidOperationException($"exercice {state.Module}/{state.Lesson}/{state.Exercise} introuvable");
            foreach (char c in state.Typed)
                BancCapture.Call(window, "OnChar", c);
            BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 1);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.SetField(window, "_visible", true);
            BancCapture.SetField(window, "_hasFocus", state.Focus);
            Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, slug, windowDpi);
        }
        finally
        {
            LessonsWindow.SurfaceVariant = previousVariant;
            BancCapture.Teardown(window);
        }
    }
}
