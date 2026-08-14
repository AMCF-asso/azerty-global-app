using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests i18n du KeyboardRenderer (tooltips des leçons et du mini-tutoriel) :
/// le nom du caractère affiché doit suivre la langue de l'UI, comme le fait déjà
/// le clavier virtuel. Régression corrigée en v1.2.0 : le renderer chargeait
/// uniquement unicodeNameFr et affichait du français en interface anglaise.
/// </summary>
public class KeyboardRendererI18nTests : IDisposable
{
    // Scancode de la touche E02 (rangée numérique, « é » en Base sur AZERTY Global)
    private const uint SC_E02 = 0x03;

    public KeyboardRendererI18nTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    private static string Tooltip()
    {
        var layout = LayoutLoader.LoadFromResource();
        var state = new KeyboardRenderState();
        return KeyboardRenderer.BuildTooltipText(
            layout, KeyboardRenderProfile.Full, state, SC_E02, "");
    }

    [Fact]
    public void BuildTooltipText_French_UsesFrenchCharacterName()
    {
        L.Language = "fr";
        var tooltip = Tooltip();
        Assert.Contains("LETTRE E MINUSCULE AVEC ACCENT AIGU", tooltip);
        Assert.DoesNotContain("LATIN SMALL LETTER E WITH ACUTE", tooltip);
    }

    [Fact]
    public void BuildTooltipText_English_UsesEnglishCharacterName()
    {
        L.Language = "en";
        var tooltip = Tooltip();
        Assert.Contains("LATIN SMALL LETTER E WITH ACUTE", tooltip);
        Assert.DoesNotContain("LETTRE E MINUSCULE AVEC ACCENT AIGU", tooltip);
    }
}
