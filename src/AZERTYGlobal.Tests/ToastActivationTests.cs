using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de la partie testable hors runtime COM/WinRT de l'activateur de toast (v1.2.0) :
/// échappement XML du contenu injecté dans le template ToastGeneric, et cohérence du CLSID
/// avec le manifest MSIX. Le trajet COM complet (CoRegisterClassObject, activation) relève
/// du smoke test packagé.
/// </summary>
public class ToastActivationTests
{
    [Fact]
    public void EscapeXml_NeutralizesMarkupCharacters()
    {
        Assert.Equal("a&amp;b &lt;t&gt; &quot;q&quot; &apos;s&apos;",
            ToastActivation.EscapeXml("a&b <t> \"q\" 's'"));
    }

    [Fact]
    public void EscapeXml_LeavesPlainTextUntouched()
    {
        Assert.Equal("Vous aimez AZERTY Global ?", ToastActivation.EscapeXml("Vous aimez AZERTY Global ?"));
    }

    [Fact]
    public void ActivatorClsid_MatchesAppxManifestDeclaration()
    {
        // Le CLSID du code doit rester aligné sur msix/AppxManifest.xml
        // (com:Class Id + ToastActivatorCLSID). Le manifest n'est pas lisible depuis les
        // tests (hors arborescence src) : on fige la valeur ici — toute divergence
        // volontaire doit être répercutée aux deux endroits.
        Assert.Equal("126A58B4-3200-43A6-9018-612C108F4A94", ToastActivation.ActivatorClsidString);
    }
}
