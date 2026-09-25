using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, L-06 : les infobulles des 62 touches du clavier des Leçons ne sont plus
/// construites à chaque repeint, mais au survol, pour la seule touche visée. Ce témoin vérifie
/// que le texte et l'ancre obtenus au survol sont ceux que le repeint déclarait : une zone par
/// touche au texte non vide, dans l'ordre des touches, la première qui contient le point.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette : la tenue des zones par la fenêtre
/// (repeint, OnMouseMove) et l'affichage de l'infobulle.
/// </summary>
public class LessonKeyboardTooltipTests : IDisposable
{
    public LessonKeyboardTooltipTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    private static readonly Win32.RECT Cadre = new() { left = 24, top = 310, right = 904, bottom = 610 };

    /// <summary>Les zones que le repeint déclarait avant L-06.</summary>
    private static List<(Win32.RECT Rect, string Tooltip)> ZonesDuRepeint(Layout layout, KeyboardRenderState state)
    {
        var zones = new List<(Win32.RECT, string)>();
        foreach (var hit in KeyboardRenderer.BuildHitTestRects(Cadre))
        {
            string texte = KeyboardRenderer.BuildTooltipText(layout, KeyboardRenderProfile.Lesson, state, hit.Scancode, hit.Label);
            if (!string.IsNullOrWhiteSpace(texte))
                zones.Add((hit.Rect, texte));
        }
        return zones;
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("dk_tilde", true)]  // touche morte active : bien des touches n'ont aucun texte
    public void LInfobulleAuSurvolEstCelleQueLeRepeintDeclarait(string? toucheMorte, bool invisibles)
    {
        var layout = LayoutLoader.LoadFromResource();
        var state = new KeyboardRenderState { ActiveDeadKey = toucheMorte, ShowInvisibleMarkers = invisibles };
        var zones = ZonesDuRepeint(layout, state);
        int points = 0, sansTexte = 0;
        foreach (var hit in KeyboardRenderer.BuildHitTestRects(Cadre))
        {
            var r = hit.Rect;
            int cx = (r.left + r.right) / 2, cy = (r.top + r.bottom) / 2;
            // Centre, quatre coins (bornes incluses), et un pixel de part et d'autre.
            foreach (var (x, y) in new[] { (cx, cy), (r.left, r.top), (r.right, r.top), (r.left, r.bottom),
                                           (r.right, r.bottom), (r.left - 1, cy), (r.right + 1, cy), (cx, r.bottom + 1) })
            {
                int i = zones.FindIndex(z => x >= z.Rect.left && x <= z.Rect.right && y >= z.Rect.top && y <= z.Rect.bottom);
                bool trouve = LessonsWindow.TryGetKeyboardTooltip(layout, Cadre, KeyboardRenderProfile.Lesson, state,
                    x, y, out var texte, out var ancre);
                Assert.Equal(i >= 0, trouve);
                if (i >= 0)
                {
                    Assert.Equal(zones[i].Tooltip, texte);
                    Assert.Equal(zones[i].Rect, ancre);
                }
                else sansTexte++;
                points++;
            }
        }
        Assert.True(points >= 62 * 8);
        Assert.True(sansTexte > 0);
    }
}
