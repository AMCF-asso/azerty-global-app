using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, A-08 : l'infobulle de la zone de notification n'est renvoyée à Explorer
/// (NIM_MODIFY, appel inter-processus) que si son texte change. Chaque appui de Maj, Ctrl ou
/// AltGr la renvoyait, alors que son texte n'en dépend pas.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette : l'infobulle réelle d'Explorer.
/// </summary>
public class TrayTooltipTests
{
    private readonly List<string> _envois = new();

    private Func<string, bool> Explorer(bool accepte) => tip =>
    {
        _envois.Add(tip);
        return accepte;
    };

    [Fact]
    public void LePremierTexteEstEnvoye()
    {
        var infobulle = new TrayApplication.TooltipPublisher();
        Assert.True(infobulle.Publish("AZERTY Global — Actif", Explorer(true)));
        Assert.Equal(new[] { "AZERTY Global — Actif" }, _envois);
    }

    [Fact]
    public void UnTexteInchangeNEstPasRenvoye()
    {
        // Appui puis relâchement de Maj : deux changements d'état, un même texte.
        var infobulle = new TrayApplication.TooltipPublisher();
        infobulle.Publish("AZERTY Global — Actif", Explorer(true));
        Assert.False(infobulle.Publish("AZERTY Global — Actif", Explorer(true)));
        Assert.False(infobulle.Publish("AZERTY Global — Actif", Explorer(true)));
        Assert.Single(_envois);
    }

    [Fact]
    public void UnTexteChangeEstEnvoye()
    {
        var infobulle = new TrayApplication.TooltipPublisher();
        infobulle.Publish("AZERTY Global — Actif", Explorer(true));
        Assert.True(infobulle.Publish("AZERTY Global — Actif — Verr. Maj.", Explorer(true)));
        Assert.True(infobulle.Publish("AZERTY Global — Actif", Explorer(true)));
        Assert.Equal(3, _envois.Count);
    }

    [Fact]
    public void UnEnvoiRefuseEstRetenteAuSuivant()
    {
        // Explorer pas encore prêt : le texte n'est pas retenu comme affiché, comme avant
        // la garde où chaque changement d'état le renvoyait.
        var infobulle = new TrayApplication.TooltipPublisher();
        infobulle.Publish("AZERTY Global — Actif", Explorer(false));
        Assert.True(infobulle.Publish("AZERTY Global — Actif", Explorer(true)));
        Assert.False(infobulle.Publish("AZERTY Global — Actif", Explorer(true)));
        Assert.Equal(2, _envois.Count);
    }
}
