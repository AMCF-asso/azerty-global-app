using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins de la saisie positionnelle, commune au tutoriel et aux Leçons depuis le lot 9
/// (audit du 25/09, L-04) : au keydown, le texte qu'AZERTY Global va produire pour la touche
/// physique, que le WM_CHAR reçu remplace ensuite (un Windows en QWERTY US peut laisser passer
/// un caractère natif). Disposition réelle, touches mortes comprises.
/// </summary>
public class SaisiePositionnelleTests
{
    private const uint SC_E = 0x12;     // e / E / €
    private const uint SC_POINT = 0x33; // . / ; / >
    private const uint SC_C11 = 0x28;   // accent aigu / accent grave (touches mortes)
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    private string? Attendu(uint scancode, bool maj = false, string? toucheMorte = null) =>
        PhysicalTextInputBuffer.ExpectedText(_layout, scancode, maj, altGr: false, capsLock: false, toucheMorte);

    [Fact]
    public void Une_touche_donne_la_sortie_de_sa_couche()
    {
        Assert.Equal("e", Attendu(SC_E));
        Assert.Equal("E", Attendu(SC_E, maj: true));
        Assert.Null(Attendu(0x01)); // Échap : hors disposition
    }

    [Fact]
    public void Une_touche_morte_armee_transforme_la_frappe_ou_sort_seule_devant()
    {
        Assert.Equal("é", Attendu(SC_E, toucheMorte: "dk_acute"));
        Assert.Equal("´.", Attendu(SC_POINT, toucheMorte: "dk_acute"));
    }

    [Fact]
    public void Une_touche_morte_ne_produit_rien_sauf_si_elle_en_remplace_une_autre()
    {
        Assert.Null(Attendu(SC_C11));
        // Maj + C11 (accent grave) pendant que l'aigu attend : l'aigu sort seul.
        Assert.Equal("´", Attendu(SC_C11, maj: true, toucheMorte: "dk_acute"));
    }

    [Fact]
    public void La_frappe_capturee_remplace_le_caractere_recu()
    {
        var mapper = new KeyMapper(_layout, new MockWin32Api());
        mapper.ProcessKey(0, SC_C11, 0, true); // accent aigu armé
        var file = new PhysicalTextInputBuffer(1000);

        file.CaptureKey(_layout, SC_E, mapper);

        Assert.Equal('é', file.Resolve('e'));
        Assert.Equal('x', file.Resolve('x')); // file vide : le caractère reçu passe
    }
}
