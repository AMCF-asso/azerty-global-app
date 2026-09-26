using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Couleur de la ligne de validation des Paramètres (décision d'Antoine du 2026-09-26) : un
/// refus s'affiche en rouge, tout autre message en vert. Avant, la couleur ne suivait que la
/// validité des deux raccourcis : « Forcer compatibilité » refusé sortait en vert, et un
/// raccourci resté invalide teintait en rouge le message d'un autre geste.
/// </summary>
public class SettingsValidationColorTests
{
    // Couleurs COLORREF de SettingsWindow (CLR_VALID, CLR_INVALID).
    private const uint Vert = 0x00228B22;
    private const uint Rouge = 0x000000CC;

    [Fact]
    public void Un_refus_s_affiche_en_rouge_et_le_message_suivant_en_vert()
    {
        // Fenêtre jamais affichée : aucun handle, rien n'est dessiné ni redimensionné.
        using var window = new SettingsWindow();

        BancCapture.Call(window, "SetRefusalMessage", "refus");
        Assert.Equal(Rouge, SettingsWindow.ValidationTextColor(BancCapture.Field<bool>(window, "_validationRefused")));

        BancCapture.Call(window, "SetValidationMessage", "fait", false);
        Assert.Equal(Vert, SettingsWindow.ValidationTextColor(BancCapture.Field<bool>(window, "_validationRefused")));
    }

    [Fact]
    public void La_couleur_ne_depend_que_de_la_nature_du_message()
    {
        Assert.Equal(Rouge, SettingsWindow.ValidationTextColor(refused: true));
        Assert.Equal(Vert, SettingsWindow.ValidationTextColor(refused: false));
    }

    // Retour à la ligne (26/09) : un message neuf peut passer d'une à deux lignes, la
    // mise en page doit donc être refaite, même quand la ligne était déjà visible.
    [Theory]
    [InlineData("", "fait", true)]
    [InlineData("fait", "", true)]
    [InlineData("court", "un message bien plus long", true)]
    [InlineData("fait", "fait", false)]
    [InlineData("", "", false)]
    public void Un_message_neuf_refait_la_mise_en_page(string avant, string apres, bool attendu)
    {
        Assert.Equal(attendu, SettingsWindow.ValidationNeedsRelayout(avant, apres));
    }
}
