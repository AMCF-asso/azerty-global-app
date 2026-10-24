using System.Linq;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using TypingEngine.Core;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Défauts vus par Antoine le 24/10 dans Leçons › Initiation : une touche à taper était pleine
/// bleue, comme une touche tenue ou allumée, donc l'appui et Verr. Maj. allumée ne se voyaient
/// pas ; et la carte complète s'affichait dès l'Initiation. Le dessin se lit sur un bitmap en
/// mémoire (aucune fenêtre ouverte).
/// </summary>
public class PaperKeyboardTargetTests
{
    private const uint ScanQ = 0x1E;
    private const uint ScanCaps = 0x3A;
    private const float Unit = 52f;

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int x, int y);

    private static readonly Layout Disposition = LayoutLoader.LoadFromResource();

    /// <summary>Un point du fond de la touche, loin de ses légendes et de son contour.</summary>
    private static uint Fond(PaperKeyboardMode mode, PaperKeyboardState state, Func<VirtualKeyboard.VisualKey, bool> touche)
    {
        var placement = PaperKeyboard.PlaceAt(10, 10, Unit);
        var key = KeyboardRenderer.VisualKeys.First(touche);
        var rect = PaperKeyboard.ToRect(key, placement);
        var (width, height) = PaperKeyboard.Size(Unit);
        uint pixel = 0;
        IntPtr screen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(screen);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(screen, width + 20, height + 20);
        IntPtr old = Win32.SelectObject(hdc, bitmap);
        try
        {
            PaperDraw.Fill(hdc, 0, 0, width + 20, height + 20, PaperTheme.Paper);
            PaperKeyboard.Draw(hdc, placement, Disposition, mode, state);
            // En bas à gauche : aucune légende de lettre ni nom de touche de fonction, centré.
            pixel = GetPixel(hdc, rect.left + 5, rect.bottom - 5);
        }
        finally
        {
            Win32.SelectObject(hdc, old);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(hdc);
            Win32.ReleaseDC(IntPtr.Zero, screen);
        }
        return pixel;
    }

    private static bool Q(VirtualKeyboard.VisualKey k) => k.Scancode == ScanQ;
    private static bool Caps(VirtualKeyboard.VisualKey k) => k.Scancode == ScanCaps;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToucheATaper_CerneeSurFondClair_PasPleineBleue(bool lecons)
    {
        var state = new PaperKeyboardState();
        state.TargetScancodes.Add(ScanQ);
        var mode = lecons ? PaperKeyboardMode.Map : PaperKeyboardMode.ActiveLayer;
        Assert.Equal(PaperTheme.Surface, Fond(mode, state, Q));
    }

    [Fact]
    public void ToucheATaper_AppuyeeEnLecons_FondTeinte()
    {
        var state = new PaperKeyboardState { PressedScancode = ScanQ };
        state.TargetScancodes.Add(ScanQ);
        Assert.Equal(PaperTheme.ActionTint, Fond(PaperKeyboardMode.Map, state, Q));
    }

    [Fact]
    public void VerrMajATaper_PleineBleueSeulementUneFoisAllumee()
    {
        var eteinte = new PaperKeyboardState();
        eteinte.TargetScancodes.Add(ScanCaps);
        var allumee = new PaperKeyboardState { CapsLock = true };
        allumee.TargetScancodes.Add(ScanCaps);

        uint avant = Fond(PaperKeyboardMode.Map, eteinte, Caps);
        uint apres = Fond(PaperKeyboardMode.Map, allumee, Caps);

        Assert.NotEqual(PaperTheme.Action, avant);
        Assert.Equal(PaperTheme.Action, apres);
    }

    [Fact]
    public void Initiation_ClavierSimplifie_ChaqueLecon()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        var initiation = catalog.Modules.Single(m => m.Id == LessonCatalogLoader.InitiationModuleId);
        Assert.NotEmpty(initiation.Lessons);
        foreach (var lesson in initiation.Lessons)
            Assert.Equal(KeyboardRenderProfile.Lesson, LessonsWindow.KeyboardProfile(initiation, lesson));
    }

    [Fact]
    public void LeconSansCaracteresHorsInitiation_GardeLaCarteComplete()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        foreach (var module in catalog.Modules.Where(m => m.Id != LessonCatalogLoader.InitiationModuleId))
        foreach (var lesson in module.Lessons)
            Assert.Equal(lesson.Characters.Count > 0 ? KeyboardRenderProfile.Lesson : KeyboardRenderProfile.Full,
                LessonsWindow.KeyboardProfile(module, lesson));
    }
}
