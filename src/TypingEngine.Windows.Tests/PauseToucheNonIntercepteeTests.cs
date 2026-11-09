using System.Reflection;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit UX du 09/11, E26 (« en pause, Échap et Tab sont avalés »). Côté moteur, rien n'est avalé : en
/// pause, le <c>KeyMapper</c> laisse passer toute touche, Échap et Tab compris, et ne touche pas à la
/// touche morte en attente (le hook, lui, n'y appelle <c>ProcessKey</c> que si <c>Enabled</c>). Ces
/// témoins verrouillent ce comportement ; l'avalement venait des fenêtres de l'application (Recherche,
/// Leçons), pas du moteur.
/// </summary>
public class PauseToucheNonIntercepteeTests
{
    private const uint SC_CIRCONFLEXE = 0x1A;
    private const uint VK_CIRCONFLEXE = 0xDD;
    private const uint VK_ESCAPE = 0x1B, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_BACK = 0x08, VK_SPACE = 0x20;
    private const uint SC_ESCAPE = 0x01, SC_TAB = 0x0F, SC_RETURN = 0x1C, SC_BACK = 0x0E, SC_SPACE = 0x39;

    private static KeyMapper MapperEnPauseAvecToucheMorte()
    {
        var layout = new Layout();
        layout.Keys[SC_CIRCONFLEXE] = new KeyDefinition
        {
            Scancode = (ushort)SC_CIRCONFLEXE,
            Position = "D11",
            Base = "dk_circumflex"
        };
        var dk = new DeadKeyDefinition { Name = "dk_circumflex", Description = "circonflexe" };
        dk.Table["a"] = "â";
        layout.DeadKeys["dk_circumflex"] = dk;

        var km = new KeyMapper(layout, new MockWin32Api());
        km.ProcessKey(VK_CIRCONFLEXE, SC_CIRCONFLEXE, 0, isKeyDown: true);
        km.ProcessKey(VK_CIRCONFLEXE, SC_CIRCONFLEXE, 0, isKeyDown: false);
        km.EmissionPaused = true;
        return km;
    }

    private static string? ToucheMorteEnAttente(KeyMapper km)
    {
        var champ = typeof(KeyMapper).GetField("_composition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((CompositionEngine)champ.GetValue(km)!).ActiveDeadKey;
    }

    [Theory]
    [InlineData(VK_ESCAPE, SC_ESCAPE)]
    [InlineData(VK_TAB, SC_TAB)]
    [InlineData(VK_RETURN, SC_RETURN)]
    [InlineData(VK_BACK, SC_BACK)]
    [InlineData(VK_SPACE, SC_SPACE)]
    public void EnPause_AucuneTouche_NEstAvalée_NiNAnnuleLaToucheMorte(uint vk, uint scan)
    {
        var km = MapperEnPauseAvecToucheMorte();
        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));

        Assert.False(km.ProcessKey(vk, scan, 0, isKeyDown: true));
        Assert.False(km.ProcessKey(vk, scan, 0, isKeyDown: false));

        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));
    }
}
