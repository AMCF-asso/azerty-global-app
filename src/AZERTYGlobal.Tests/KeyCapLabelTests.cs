using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Texte dessiné sur les touches du clavier (v1.3.0) : traduit au rendu par
/// <c>L.Keyboard_KeyCap</c>, alors que le libellé interne des <c>VisualKey</c>, qui sert
/// d'identifiant (surlignage, modificateurs, tooltips), reste le même dans les deux langues.
/// </summary>
public class KeyCapLabelTests : IDisposable
{
    public KeyCapLabelTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    [Theory]
    [InlineData("Entrée", "Enter")]
    [InlineData("Verr. Maj.", "Caps Lock")]
    [InlineData("Maj ⇧", "Shift ⇧")]
    [InlineData("Espace", "Space")]
    public void Libelle_traduit_en_anglais_et_inchange_en_francais(string internalLabel, string english)
    {
        L.Language = "fr";
        Assert.Equal(internalLabel, L.Keyboard_KeyCap(internalLabel));
        L.Language = "en";
        Assert.Equal(english, L.Keyboard_KeyCap(internalLabel));
    }

    [Theory]
    [InlineData("Tab")]
    [InlineData("⌫")]
    [InlineData("Ctrl")]
    [InlineData("Win")]
    [InlineData("Alt")]
    [InlineData("AltGr")]
    [InlineData("Menu")]
    [InlineData("A")]
    [InlineData("ù")]
    public void Libelles_communs_aux_deux_langues_passent_tels_quels(string label)
    {
        L.Language = "en";
        Assert.Equal(label, L.Keyboard_KeyCap(label));
    }

    [Fact]
    public void Identifiants_internes_des_touches_ne_changent_pas_avec_la_langue()
    {
        L.Language = "fr";
        var fr = VirtualKeyboard.BuildKeyLayout().Select(k => k.Label).ToArray();
        L.Language = "en";
        var en = VirtualKeyboard.BuildKeyLayout().Select(k => k.Label).ToArray();

        Assert.Equal(fr, en);
        // Identifiants comparés en dur dans VirtualKeyboard, KeyboardRenderer, LearningModule
        // et LessonsWindow : ils doivent rester présents tels quels.
        Assert.Contains("Entrée", en);
        Assert.Contains("Verr. Maj.", en);
        Assert.Equal(2, en.Count(l => l == "Maj ⇧"));
        Assert.Contains("Espace", en);
    }

    [Fact]
    public void Aucune_touche_ne_garde_un_libelle_francais_en_anglais()
    {
        L.Language = "en";
        string[] french = { "Entrée", "Verr. Maj.", "Maj ⇧", "Espace" };
        foreach (var key in VirtualKeyboard.BuildKeyLayout())
            Assert.DoesNotContain(L.Keyboard_KeyCap(key.Label), french);
    }

    [Fact]
    public void Tooltips_des_touches_contextuelles_restent_resolus_par_l_identifiant()
    {
        var layout = LayoutLoader.LoadFromResource();
        var state = new KeyboardRenderState();
        L.Language = "en";
        Assert.Equal(L.Keyboard_TooltipCapsLock,
            KeyboardRenderer.BuildTooltipText(layout, KeyboardRenderProfile.Full, state, 0, "Verr. Maj."));
        Assert.Equal(L.Keyboard_TooltipShift,
            KeyboardRenderer.BuildTooltipText(layout, KeyboardRenderProfile.Full, state, 0, "Maj ⇧"));
        Assert.Equal("Enter",
            KeyboardRenderer.BuildTooltipText(layout, KeyboardRenderProfile.Full, state, 0, "Entrée"));
    }
}
