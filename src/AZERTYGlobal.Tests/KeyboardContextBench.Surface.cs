using System;
using System.Collections.Generic;
using System.Reflection;
using AZERTYGlobal;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Leçons, surface de frappe unique (lot Leçons 1.3.0, V2 retenue le 29/09) : texte en cours,
/// faute en mode strict, fautes en mode souple, lettre étroite à taper, la même saisie sans le
/// focus puis avec le focus sur un bouton, et le mode Libre dans la police de la ligne. Les
/// frappes passent par <c>OnChar</c>, comme au clavier.
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

    private const string FreeTyped = "María et Niccolò arrivent à São Paulo.";

    private sealed record SurfaceState(string Slug, string Module, string Lesson, int Exercise, string Typed,
        bool Focus, int FocusedAction = -1, bool Free = false);

    private static readonly SurfaceState[] SurfaceStates =
    {
        new("saisie", StrictModule, StrictLesson, StrictExercise, StrictTyped, Focus: true),
        new("faute-stricte", StrictModule, StrictLesson, StrictExercise, StrictTyped + "u", Focus: true),
        new("faute-souple", FlexibleModule, FlexibleLesson, 0, FlexibleTyped, Focus: true),
        // L'apostrophe est la lettre à taper : son soulignement garde sa largeur minimale.
        new("etroite", StrictModule, StrictLesson, StrictExercise, "Lætitia demande « d", Focus: true),
        new("sans-focus", StrictModule, StrictLesson, StrictExercise, StrictTyped, Focus: false),
        // Tab a quitté la surface : son cadre s'efface, le premier bouton porte le focus.
        new("focus-bouton", StrictModule, StrictLesson, StrictExercise, StrictTyped, Focus: true, FocusedAction: 0),
        new("libre", StrictModule, StrictLesson, StrictExercise, FreeTyped, Focus: true, Free: true),
    };

    private static void AddLessonSurfaceStates(List<(string Name, Action Run)> attempts, BancCapture.Target target,
        Layout layout, KeyMapper mapper, KeyboardHook hook)
    {
        foreach (var state in SurfaceStates)
        {
            string slug = $"lecons-surface-{state.Slug}";
            attempts.Add((slug, () => CaptureLessonSurface(target, slug, layout, mapper, hook, state)));
        }
    }

    private static void CaptureLessonSurface(BancCapture.Target target, string slug, Layout layout, KeyMapper mapper,
        KeyboardHook hook, SurfaceState state)
    {
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var window = new LessonsWindow(layout, mapper, hook);
        try
        {
            if (!(bool)BancCapture.Call(window, "SelectExercise", state.Module, state.Lesson, state.Exercise, false)!)
                throw new InvalidOperationException($"exercice {state.Module}/{state.Lesson}/{state.Exercise} introuvable");
            if (state.Free)
            {
                var modeType = typeof(LessonsWindow).GetNestedType("WindowMode", BindingFlags.NonPublic)!;
                BancCapture.Call(window, "SwitchMode", Enum.Parse(modeType, "Free"));
            }
            foreach (char c in state.Typed)
                BancCapture.Call(window, "OnChar", c);
            if (state.Free)
                BancCapture.SetField(window, "_freeStartedAt", null); // statistiques sans horloge
            BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 1);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.SetField(window, "_visible", true);
            BancCapture.SetField(window, "_hasFocus", state.Focus);
            BancCapture.SetField(window, "_focusedActionIndex", state.FocusedAction);
            Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, slug, windowDpi);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }
}
