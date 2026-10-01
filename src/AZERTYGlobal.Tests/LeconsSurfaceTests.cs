using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;
using Engine = TypingEngine.Windows.Win32;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Leçons, surface de frappe unique (étape 2 du lot, décisions du 29/09) : Tab parcourt
/// surface → boutons → surface, une frappe ou un clic y rend le focus, le caret système est
/// placé devant la lettre à taper sans être affiché, et les soulignements gardent une largeur
/// minimale sous les lettres étroites.
/// </summary>
public class LeconsSurfaceTests
{
    private const uint GUI_CARETBLINKING = 0x0001; // posé seulement si le caret est visible
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const int OBJID_CARET = -8;
    private const uint PM_REMOVE = 0x0001;

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out Win32.MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

    [Theory]
    // Avec la surface (-1) et trois boutons : surface → 0 → 1 → 2 → surface, et l'inverse.
    [InlineData(-1, 3, 1, true, 0)]
    [InlineData(1, 3, 1, true, 2)]
    [InlineData(2, 3, 1, true, -1)]
    [InlineData(-1, 3, -1, true, 2)]
    [InlineData(0, 3, -1, true, -1)]
    [InlineData(-1, 0, 1, true, -1)]
    [InlineData(-1, 0, -1, true, -1)]
    // Sans surface (Paramètres, récapitulatif) : les boutons seuls, comme avant.
    [InlineData(-1, 3, 1, false, 0)]
    [InlineData(2, 3, 1, false, 0)]
    [InlineData(-1, 3, -1, false, 2)]
    [InlineData(0, 3, -1, false, 2)]
    [InlineData(-1, 0, 1, false, -1)]
    // Index périmé, quand le repeint compte moins de boutons : repart d'un bout.
    [InlineData(5, 3, 1, true, -1)]
    [InlineData(5, 3, -1, true, 2)]
    [InlineData(5, 3, 1, false, 0)]
    public void TabParcourtLaSurfacePuisLesBoutons(int current, int count, int direction, bool surface, int expected)
    {
        Assert.Equal(expected, LessonsWindow.NextFocusIndex(current, count, direction, surface));
    }

    [Fact]
    public void UnTourDeTabPasseParChaqueCibleUneFois()
    {
        var visited = new List<int>();
        int focus = -1;
        for (int i = 0; i < 5; i++)
        {
            focus = LessonsWindow.NextFocusIndex(focus, 4, 1, hasSurface: true);
            visited.Add(focus);
        }
        Assert.Equal(new[] { 0, 1, 2, 3, -1 }, visited);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 105)]
    [InlineData(2, 115)]
    [InlineData(3, 122)] // fin de ligne : après la dernière case
    [InlineData(9, 122)] // au-delà : borné à la fin
    [InlineData(-1, 100)]
    public void LeCurseurSuitLaLargeurDesCasesQuiLePrecedent(int cursor, int expected)
    {
        Assert.Equal(expected, LessonsWindow.SurfaceCaretX(new[] { 5, 10, 7 }, cursor, 100));
    }

    [Fact]
    public void LeSoulignementDUneLettreEtroiteGardeSixPixels()
    {
        var (left, right) = LessonsWindow.UnderlineSpan(100, 104, 2, 6);
        Assert.Equal(6, right - left);
        Assert.Equal(204, left + right); // centré sur la case
    }

    [Fact]
    public void LeSoulignementDUneLettreLargeResteLaCaseMoinsSesRetraits()
    {
        Assert.Equal((102, 118), LessonsWindow.UnderlineSpan(100, 120, 2, 6));
    }

    [Fact]
    public void UneFrappeRendLeFocusALaSurface_PasLeCaractereDeTab()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        try
        {
            Paint(window);
            BancCapture.Call(window, "OnKeyDown", 0x09);
            Assert.Equal(0, BancCapture.Field<int>(window, "_focusedActionIndex"));

            BancCapture.Call(window, "OnChar", '\t'); // le WM_CHAR qui suit Tab
            Assert.Equal(0, BancCapture.Field<int>(window, "_focusedActionIndex"));

            BancCapture.Call(window, "OnChar", 'L');
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void UnRetourArriereRendLeFocusALaSurface()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        try
        {
            BancCapture.Call(window, "OnChar", 'L');
            Paint(window);
            BancCapture.SetField(window, "_focusedActionIndex", 1);
            BancCapture.Call(window, "OnKeyDown", 0x08);
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void UnClicRendLeFocusALaSurface()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        try
        {
            Paint(window);
            BancCapture.SetField(window, "_focusedActionIndex", 1);
            BancCapture.Call(window, "OnClick", IntPtr.Zero); // (0, 0) : hors de tout bouton
            Assert.Equal(-1, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void DansLesParametres_TabEtLeClicLaissentLaSurfaceHorsDuCycle()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        try
        {
            BancCapture.SetField(window, "_settingsOpen", true);
            Paint(window);
            int count = BancCapture.Field<List<(Win32.RECT Rect, Action Action)>>(window, "_clickActions").Count;
            Assert.True(count > 1);

            BancCapture.SetField(window, "_focusedActionIndex", count - 1);
            BancCapture.Call(window, "OnKeyDown", 0x09);
            Assert.Equal(0, BancCapture.Field<int>(window, "_focusedActionIndex"));

            BancCapture.Call(window, "OnClick", IntPtr.Zero);
            Assert.Equal(0, BancCapture.Field<int>(window, "_focusedActionIndex"));
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void LeCaretSystemeEstDevantLaLettreATaper_SansEtreAffiche()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        IntPtr hwnd = BancCapture.Handle(window);
        try
        {
            BancCapture.SetField(window, "_hasFocus", true);
            Paint(window);
            var first = CaretInfo();
            var expected = BancCapture.Field<Win32.RECT?>(window, "_systemCaretRect")!.Value;
            Assert.Equal(hwnd, first.hwndCaret);
            Assert.Equal(expected.left, first.rcCaret.left);
            Assert.Equal(expected.top, first.rcCaret.top);
            Assert.Equal(0u, first.flags & GUI_CARETBLINKING);

            BancCapture.Call(window, "OnChar", 'L');
            Paint(window);
            var second = CaretInfo();
            Assert.True(second.rcCaret.left > first.rcCaret.left, "le caret avance avec la frappe");
            Assert.Equal(0u, second.flags & GUI_CARETBLINKING);

            Win32.SendMessageW(hwnd, Win32.WM_KILLFOCUS, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(IntPtr.Zero, CaretInfo().hwndCaret);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void LeCaretPrendLaTailleDuModeLibre()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        try
        {
            BancCapture.SetField(window, "_hasFocus", true);
            Paint(window);
            var lesson = CaretInfo().rcCaret;

            var modeType = typeof(LessonsWindow).GetNestedType("WindowMode", System.Reflection.BindingFlags.NonPublic)!;
            BancCapture.Call(window, "SwitchMode", Enum.Parse(modeType, "Free"));
            Paint(window);
            var free = CaretInfo().rcCaret;
            var expected = BancCapture.Field<Win32.RECT?>(window, "_systemCaretRect")!.Value;

            Assert.NotEqual(lesson.bottom - lesson.top, free.bottom - free.top);
            Assert.Equal(expected.right - expected.left, free.right - free.left);
            Assert.Equal(expected.bottom - expected.top, free.bottom - free.top);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    /// <summary>
    /// Ce que la loupe et les lecteurs d'écran écoutent hors UI Automation : le déplacement du
    /// caret (EVENT_OBJECT_LOCATIONCHANGE, OBJID_CARET). Mesure : le caret masqué l'émet-il ?
    /// </summary>
    [Fact]
    public void LeCaretMasqueSignaleSesDeplacements()
    {
        using var isolation = new BancCapture.Isolation();
        var window = OpenStrictExercise();
        IntPtr hwnd = BancCapture.Handle(window);
        var moves = new List<IntPtr>();
        Engine.WinEventDelegate callback = (_, _, eventHwnd, idObject, _, _, _) =>
        {
            if (idObject == OBJID_CARET)
                moves.Add(eventHwnd);
        };
        IntPtr hook = Engine.SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero,
            callback, (uint)Environment.ProcessId, Win32.GetCurrentThreadId(), Engine.WINEVENT_OUTOFCONTEXT);
        Assert.NotEqual(IntPtr.Zero, hook);
        try
        {
            BancCapture.SetField(window, "_hasFocus", true);
            Paint(window);
            Pump();
            moves.Clear();

            BancCapture.Call(window, "OnChar", 'L');
            Paint(window);
            Pump();
            Assert.Contains(hwnd, moves);
        }
        finally
        {
            Engine.UnhookWinEvent(hook);
            GC.KeepAlive(callback);
            BancCapture.Teardown(window);
        }
    }

    private static LessonsWindow OpenStrictExercise()
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

    /// <summary>Repeint hors écran, comme OnPaint : rien ne s'affiche pendant les tests.</summary>
    private static void Paint(LessonsWindow window)
    {
        IntPtr hwnd = BancCapture.Handle(window);
        Win32.GetClientRect(hwnd, out var rc);
        IntPtr screen = Win32.GetDC(hwnd);
        IntPtr dc = Win32.CreateCompatibleDC(screen);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(screen, Math.Max(1, rc.right), Math.Max(1, rc.bottom));
        IntPtr previous = Win32.SelectObject(dc, bitmap);
        try
        {
            BancCapture.Call(window, "DrawWindowContents", dc, rc);
        }
        finally
        {
            Win32.SelectObject(dc, previous);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(dc);
            Win32.ReleaseDC(hwnd, screen);
        }
    }

    private static Engine.GUITHREADINFO CaretInfo()
    {
        var info = new Engine.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<Engine.GUITHREADINFO>() };
        Assert.True(Engine.GetGUIThreadInfo(Win32.GetCurrentThreadId(), ref info));
        return info;
    }

    private static void Pump()
    {
        var until = Environment.TickCount64 + 200;
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessageW(ref msg);
            }
            System.Threading.Thread.Sleep(10);
        }
    }
}
