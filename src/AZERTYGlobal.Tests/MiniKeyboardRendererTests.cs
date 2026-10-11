using System.Linq;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Mini-clavier des Leçons (notations du 11/10) : le plan (touches allumées, états, numéros,
/// cadres) se teste sans fenêtre ; le dessin, sur un bitmap en mémoire (aucune fenêtre ouverte).
/// </summary>
public class MiniKeyboardRendererTests : IDisposable
{
    private const uint ScanDigit6 = 0x07;  // la touche gravée « - »
    private const uint ScanDigit2 = 0x03;  // la touche gravée « é »
    private const uint ScanN = 0x31;
    private const uint ScanT = 0x14;
    private const uint ScanCaps = 0x3A;

    public MiniKeyboardRendererTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int x, int y);

    private static List<MiniKey> Allumees(IReadOnlyList<GestureStep> steps, int? seulement = null)
        => MiniKeyboardRenderer.Plan(0, 0, 15f, steps, seulement).Where(k => k.State != MiniKeyState.Off).ToList();

    private static MiniKey Touche(IEnumerable<MiniKey> keys, uint scancode)
        => keys.Single(k => k.Key.Scancode == scancode);

    private static void AssertEtat(MiniKey key, MiniKeyState state, params int[] numeros)
    {
        Assert.Equal(state, key.State);
        Assert.Equal(numeros, key.Numbers.ToArray());
    }

    private static MiniKey AltGr(IEnumerable<MiniKey> keys) => keys.Single(k => k.Key.Label == "AltGr");
    private static MiniKey Maj(IEnumerable<MiniKey> keys) => keys.Single(k => k.Key.ContextId == VirtualKeyboard.ContextShiftLeft);

    // ── Les gestes du mandat ───────────────────────────────────────────

    [Fact]
    public void ToucheMorte_EngAltGrSixPuisN()
    {
        var steps = Gesture.Of("ŋ");
        Assert.Equal(2, steps.Count);
        var keys = Allumees(steps);

        Assert.Equal(3, keys.Count);
        AssertEtat(AltGr(keys), MiniKeyState.Held, 1);
        AssertEtat(Touche(keys, ScanDigit6), MiniKeyState.Struck, 1);
        AssertEtat(Touche(keys, ScanN), MiniKeyState.Struck, 2);
        Assert.DoesNotContain(keys, k => k.Key.ContextId == VirtualKeyboard.ContextShiftLeft);
    }

    [Fact]
    public void TiretCadratin_MajAltGrEtTAuTempsUn()
    {
        var steps = Gesture.Of("—");
        Assert.Single(steps);
        var keys = Allumees(steps);

        Assert.Equal(3, keys.Count);
        Assert.All(keys, k => Assert.Equal(new[] { 1 }, k.Numbers));
        Assert.Equal(MiniKeyState.Held, Maj(keys).State);
        Assert.Equal(MiniKeyState.Held, AltGr(keys).State);
        Assert.Equal(MiniKeyState.Struck, Touche(keys, ScanT).State);
    }

    [Fact]
    public void MajusculeAccentuee_CeQueGestureOfDonneReellement()
    {
        // É par sa méthode Caps (la recommandée est ^ puis é depuis le 12/10) : couche Caps de la touche 2,
        // deux temps, Verr. Maj. puis é. Le temps « éteint » (refrapper Verr. Maj.) de la maquette n'existe
        // pas dans GestureStep : c'est à la fenêtre Leçons de l'ajouter, s'il le faut.
        var steps = Gesture.Of(CharacterIndex.Shared.ByCharacter["É"].Caps!);
        Assert.Equal(2, steps.Count);
        Assert.Equal((Gesture.CapsLockCode, "Digit2"), (steps[0].KeyCode, steps[1].KeyCode));
        Assert.All(steps, s => Assert.False(s.Shift || s.AltGr));

        var keys = Allumees(steps);
        Assert.Equal(2, keys.Count);
        var caps = Touche(keys, ScanCaps);
        AssertEtat(caps, MiniKeyState.Struck, 1);
        Assert.Equal("VM", caps.Label);
        var deux = Touche(keys, ScanDigit2);
        AssertEtat(deux, MiniKeyState.Struck, 2);
        Assert.Equal("é", deux.Label);
    }

    [Fact]
    public void UneMemeToucheADeuxTemps_PorteDeuxNumeros()
    {
        // Aucun caractère réel ne frappe deux fois la même touche : gestes écrits à la main.
        var steps = new[]
        {
            new GestureStep(false, false, "KeyN", "N"),
            new GestureStep(true, false, "KeyN", "N"),
        };
        var keys = Allumees(steps);

        var n = Touche(keys, ScanN);
        Assert.Equal(new[] { 1, 2 }, n.Numbers);
        Assert.Equal(MiniKeyState.Struck, n.State);
        Assert.Equal(new[] { 2 }, Maj(keys).Numbers);

        // Deux pastilles distinctes, côte à côte, dans la carte.
        var a = MiniKeyboardRenderer.BadgeRect(n.Rect, 15f, 0);
        var b = MiniKeyboardRenderer.BadgeRect(n.Rect, 15f, 1);
        Assert.True(b.right <= a.left + 1, "la seconde pastille est à gauche de la première");
    }

    [Fact]
    public void Vignette_NeGardeQueLesTouchesDeSonTemps()
    {
        var steps = Gesture.Of("ŋ");

        var premier = Allumees(steps, 0);
        Assert.Equal(2, premier.Count); // AltGr et la touche 6
        Assert.DoesNotContain(premier, k => k.Key.Scancode == ScanN);
        var second = Allumees(steps, 1);
        Assert.Single(second);          // N seule : AltGr n'est plus tenue au second temps
        Assert.Equal(ScanN, second[0].Key.Scancode);
    }

    // ── Gravures et noms ───────────────────────────────────────────────

    [Fact]
    public void ToucheEteinte_PorteLaGravureAzertyEtLesFonctionsSontMuettes()
    {
        var tout = MiniKeyboardRenderer.Plan(0, 0, 15f, Array.Empty<GestureStep>());
        Assert.All(tout, k => Assert.Equal(MiniKeyState.Off, k.State));
        Assert.Equal("é", tout.Single(k => k.Key.Scancode == ScanDigit2).Label);
        Assert.Equal("W", tout.Single(k => k.Key.Scancode == 0x2C).Label);   // la touche gravée W est KeyZ
        Assert.Equal("ù", tout.Single(k => k.Key.Scancode == 0x28).Label);
        Assert.Equal("²", tout.Single(k => k.Key.Scancode == 0x29).Label);
        Assert.All(tout.Where(k => k.IsFunction), k => Assert.Equal("", k.Label));
        Assert.Equal("", tout.Single(k => k.Key.Scancode == 0x39).Label);   // la barre d'espace
    }

    [Fact]
    public void ToucheAllumee_PorteLaGravureDuGeste()
    {
        var keys = Allumees(Gesture.Of("ŋ"));
        Assert.Equal("-", Touche(keys, ScanDigit6).Label);
        Assert.Equal("N", Touche(keys, ScanN).Label);
    }

    [Fact]
    public void NomsCourtsDesFonctions_FrancaisEtAnglais()
    {
        var tenues = new[] { new GestureStep(true, true, "KeyT", "T") };
        var caps = new[] { new GestureStep(false, false, Gesture.CapsLockCode, Gesture.KeyLabel(Gesture.CapsLockCode)) };

        L.Language = "fr";
        var fr = Allumees(tenues);
        Assert.Equal("Maj", Maj(fr).Label);
        Assert.Equal("AltGr", AltGr(fr).Label);
        Assert.Equal("T", Touche(fr, ScanT).Label);
        Assert.Equal("VM", Allumees(caps)[0].Label);

        L.Language = "en";
        var en = Allumees(tenues);
        Assert.Equal("Shift", Maj(en).Label);
        Assert.Equal("AltGr", AltGr(en).Label);
        Assert.Equal("Caps", Allumees(caps)[0].Label);
    }

    [Fact]
    public void Legendes_FrancaisEtAnglais()
    {
        L.Language = "fr";
        Assert.Equal(("frappée", "tenue", "tout en même temps", "1, puis 2, puis 3"),
            (L.MiniKeyboard_Struck, L.MiniKeyboard_Held, L.MiniKeyboard_AllAtOnce, L.MiniKeyboard_Order(3)));
        Assert.Equal(("d’abord", "puis", "ensemble, en un temps"),
            (L.MiniKeyboard_First, L.MiniKeyboard_Next, L.MiniKeyboard_Together));

        L.Language = "en";
        Assert.Equal(("pressed", "held", "all at once", "1, then 2"),
            (L.MiniKeyboard_Struck, L.MiniKeyboard_Held, L.MiniKeyboard_AllAtOnce, L.MiniKeyboard_Order(2)));
        Assert.Equal(("first", "then", "together, in one step"),
            (L.MiniKeyboard_First, L.MiniKeyboard_Next, L.MiniKeyboard_Together));
    }

    // ── Géométrie ──────────────────────────────────────────────────────

    [Fact]
    public void Tailles_ProportionnellesALUnite_EtMesureesPourLesUnitesDesMaquettes()
    {
        Assert.Equal((260, 97), MiniKeyboardRenderer.Size(15f));
        Assert.Equal((415, 153), MiniKeyboardRenderer.Size(24f));
        // Le clavier lui-même est celui du grand : 16,3 unités sur 5,4.
        var (w, h) = PaperKeyboard.Size(24f);
        Assert.Equal((391, 129), (w, h));

        foreach (float unit in new[] { 13f, 15f, 17f, 24f })
        {
            var one = MiniKeyboardRenderer.Size(unit);
            var two = MiniKeyboardRenderer.Size(unit * 2);
            Assert.InRange(two.Width, 2 * one.Width - 2, 2 * one.Width + 2);
            Assert.InRange(two.Height, 2 * one.Height - 2, 2 * one.Height + 2);
        }
    }

    [Fact]
    public void Touches_ToutesDansLaCarte_PastillesComprises()
    {
        foreach (float unit in new[] { 13f, 15f, 17f, 24f })
        {
            var (w, h) = MiniKeyboardRenderer.Size(unit);
            var steps = new[]
            {
                new GestureStep(true, true, "Backquote", "²"),     // coin haut gauche
                new GestureStep(false, false, "Equal", "="),       // bord haut droit (Retour arrière est à côté)
                new GestureStep(false, false, "Equal", "="),
            };
            var keys = MiniKeyboardRenderer.Plan(100, 50, unit, steps);
            foreach (var key in keys)
            {
                Assert.True(key.Rect.left >= 100 && key.Rect.top >= 50 && key.Rect.right < 100 + w && key.Rect.bottom < 50 + h,
                    $"touche {key.Key.Label} hors de la carte à l'unité {unit}");
                for (int i = 0; i < key.Numbers.Count; i++)
                {
                    var badge = MiniKeyboardRenderer.BadgeRect(key.Rect, unit, i);
                    Assert.True(badge.left >= 100 && badge.top >= 50 && badge.right <= 100 + w && badge.bottom <= 50 + h,
                        $"pastille {key.Key.Label}/{i} hors de la carte à l'unité {unit}");
                }
            }
        }
    }

    // ── Dessin (bitmap en mémoire, aucune fenêtre) ─────────────────────

    private static void SurBitmap(int width, int height, Action<IntPtr> draw)
    {
        IntPtr screen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(screen);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(screen, width, height);
        IntPtr old = Win32.SelectObject(hdc, bitmap);
        try
        {
            PaperDraw.Fill(hdc, 0, 0, width, height, PaperTheme.Paper);
            draw(hdc);
        }
        finally
        {
            Win32.SelectObject(hdc, old);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(hdc);
            Win32.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [Fact]
    public void Numerote_DessineLaCarteEtLaLegende_ToucheFrappeePleineEtTenueTeintee()
    {
        var steps = Gesture.Of("—");
        var plan = MiniKeyboardRenderer.Plan(10, 10, 24f, steps);
        var t = plan.Single(k => k.Key.Scancode == ScanT);
        var maj = Maj(plan);
        int height = 0;
        uint struck = 0, held = 0, off = 0;
        var (width, expected) = (0, 0);

        SurBitmap(600, 260, hdc =>
        {
            (width, expected) = MiniKeyboardRenderer.MeasureNumbered(hdc, 24f, steps, 1f);
            height = MiniKeyboardRenderer.DrawNumbered(hdc, 10, 10, 24f, steps, 1f);
            // Un point du fond de la touche T (loin de sa gravure et de sa pastille), puis de Maj, puis d'une touche éteinte.
            struck = GetPixel(hdc, t.Rect.left + 3, t.Rect.bottom - 3);
            held = GetPixel(hdc, maj.Rect.left + 4, maj.Rect.bottom - 4);
            var e = plan.Single(k => k.Key.Scancode == 0x10);
            off = GetPixel(hdc, e.Rect.left + 3, e.Rect.bottom - 3);
        });

        Assert.Equal(expected, height);
        Assert.True(width >= MiniKeyboardRenderer.Size(24f).Width);
        Assert.True(height > MiniKeyboardRenderer.Size(24f).Height, "la légende s'ajoute sous la carte");
        if (struck == 0 && held == 0 && off == 0) return; // pas de pixels lisibles sur ce poste
        Assert.Equal(PaperTheme.Action, struck);
        Assert.Equal(PaperDraw.Blend(PaperTheme.Action, PaperTheme.Surface, 0.15f), held);
        Assert.Equal(PaperTheme.Surface, off);
    }

    [Fact]
    public void Vignettes_UneParTemps_PlusEtroitesQueLeursEntetesQuandIlEnFautDeux()
    {
        var un = Gesture.Of("—");
        var deux = Gesture.Of("ŋ");
        (int Width, int Height) m1 = default, m2 = default, m2x = default;
        int h1 = 0, h2 = 0;

        SurBitmap(900, 260, hdc =>
        {
            m1 = MiniKeyboardRenderer.MeasureFrames(hdc, 15f, un, 1f);
            m2 = MiniKeyboardRenderer.MeasureFrames(hdc, 15f, deux, 1f);
            m2x = MiniKeyboardRenderer.MeasureFrames(hdc, 15f, deux, 2f);
            h1 = MiniKeyboardRenderer.DrawFrames(hdc, 4, 4, 15f, un, 1f);
            h2 = MiniKeyboardRenderer.DrawFrames(hdc, 4, 4, 15f, deux, 1f);
        });

        var mini = MiniKeyboardRenderer.Size(15f);
        Assert.Equal(m1.Height, h1);
        Assert.Equal(m2.Height, h2);
        Assert.Equal(m1.Height, m2.Height);
        Assert.True(m1.Height > mini.Height, "l'entête s'ajoute au-dessus du clavier");
        Assert.True(m1.Width >= mini.Width);
        Assert.True(m2.Width >= 2 * mini.Width, "deux vignettes et une flèche");
        // Les rondes et les textes suivent l'échelle (le clavier, lui, suit l'unité) : plus haut, plus large.
        Assert.True(m2x.Height > m2.Height && m2x.Width > m2.Width);
    }

    [Fact]
    public void GesteVide_RienADessiner()
    {
        int a = -1, b = -1;
        (int Width, int Height) m = default;
        SurBitmap(100, 100, hdc =>
        {
            a = MiniKeyboardRenderer.DrawNumbered(hdc, 0, 0, 15f, Array.Empty<GestureStep>(), 1f);
            b = MiniKeyboardRenderer.DrawFrames(hdc, 0, 0, 15f, Array.Empty<GestureStep>(), 1f);
            m = MiniKeyboardRenderer.MeasureFrames(hdc, 15f, Array.Empty<GestureStep>(), 1f);
        });
        Assert.Equal((0, 0, 0, 0), (a, b, m.Width, m.Height));
    }
}
