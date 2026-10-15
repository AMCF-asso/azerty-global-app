using System;
using System.IO;
using AZERTYGlobal;
using TypingEngine.Windows.Testing;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le banc de capture ne touche plus à la vraie souris (choix d'Antoine du 15/10 : « simuler le
/// curseur »). Avant, il la garait en (0, 0) avant chaque cliché, la remettait à la fin, et la
/// déplaçait donc sous les doigts d'Antoine à chaque rendu. Ces témoins ne sont pas derrière la
/// porte AZERTY_CAPTURE : ils font un vrai cliché d'une vraie fenêtre.
///
/// Limite : si l'utilisateur bouge la souris pendant le test, la comparaison peut échouer à
/// tort ; elle ne peut pas réussir à tort, sauf souris déjà posée en (0, 0).
/// </summary>
public class CurseurSimuleTests
{
    private static Win32.POINT RealCursor()
    {
        Assert.True(Win32.GetCursorPos(out var point));
        return point;
    }

    private static LessonsWindow OpenLessons()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var window = new LessonsWindow(layout, mapper, hook);
        if (!(bool)BancCapture.Call(window, "SelectExercise", LessonCatalogLoader.InitiationModuleId,
                LessonCatalogLoader.InitiationLessonId, 3, false)!)
            throw new InvalidOperationException("exercice d'initiation introuvable");
        return window;
    }

    [Fact]
    public void UnClicheLaisseLaVraieSourisOuElleEtait()
    {
        var before = RealCursor();
        using var isolation = new BancCapture.Isolation();
        var window = OpenLessons();
        string dir = Path.Combine(Path.GetTempPath(), "AZGCurseur_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 1);
            BancCapture.MarkActive(hwnd);

            BancCapture.Capture(hwnd, Path.Combine(dir, "curseur.png"));

            // Mesuré tant que la fenêtre existe et que l'isolation tient : l'ancien banc avait
            // garé la souris en (0, 0) à cet instant, et ne la rendait qu'au Dispose.
            var during = RealCursor();
            Assert.Equal((before.x, before.y), (during.x, during.y));
        }
        finally
        {
            BancCapture.Teardown(window);
            Directory.Delete(dir, recursive: true);
        }

        var after = RealCursor();
        Assert.Equal((before.x, before.y), (after.x, after.y));
    }

    [Fact]
    public void LesLeconsLisentLeCurseurParLaCoucheWin32()
    {
        var before = RealCursor();
        using var isolation = new BancCapture.Isolation();
        var spy = new MockWin32Api { CursorPosition = (-123, -456) };
        using (CursorSource.OverrideForTests(spy))
        {
            // La lecture directe passe par la couche : position scriptée, jamais la vraie.
            Assert.True(CursorSource.TryGet(out var point));
            Assert.Equal((-123, -456), (point.x, point.y));
            Assert.Equal(1, spy.CursorReads);

            // Et la fenêtre qui se place sur l'écran du curseur la lit aussi.
            var window = OpenLessons();
            try
            {
                BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
                Win32.ShowWindow(window.Handle, 1);
                Win32.SendMessageW(window.Handle, Win32.WM_SETCURSOR, window.Handle, (IntPtr)1 /* HTCLIENT */);
            }
            finally
            {
                BancCapture.Teardown(window);
            }
            Assert.True(spy.CursorReads > 1, $"les Leçons n'ont pas lu le curseur par la couche ({spy.CursorReads} lecture)");
        }

        var after = RealCursor();
        Assert.Equal((before.x, before.y), (after.x, after.y));
    }

    [Fact]
    public void LIsolationRendUnCurseurSimuleEtLeRetire()
    {
        using (new BancCapture.Isolation())
        {
            Assert.True(CursorSource.TryGet(out var simulated));
            Assert.Equal((0, 0), (simulated.x, simulated.y));
        }

        // Isolation fermée : la couche rend de nouveau la vraie souris.
        Assert.True(CursorSource.TryGet(out var real));
        var truth = RealCursor();
        Assert.Equal((truth.x, truth.y), (real.x, real.y));
    }
}
