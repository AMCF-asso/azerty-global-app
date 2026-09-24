using AZERTYGlobal;
using Xunit;
using HeaderKeyAction = AZERTYGlobal.LearningModule.HeaderKeyAction;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 24/09 — régression K3 : un Tab accidentel met le focus sur « Quitter », puis la
/// première espace de l'exercice fermait le tutoriel.
///
/// Un BUTTON Win32 s'enfonce au WM_KEYDOWN d'espace et clique au WM_KEYUP, dans sa propre
/// procédure. Ce qui est éprouvé ici : la décision prise par la sous-classe des boutons
/// d'en-tête pour chaque message. Que Windows envoie bien ces messages, et que le caractère
/// rendu à l'exercice y compte comme frappe, reste à la recette.
/// </summary>
public class HeaderButtonKeyTests
{
    // Valeurs Win32 écrites en dur : le témoin ne s'appuie pas sur les constantes qu'il vérifie.
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_CHAR = 0x0102;
    private const uint WM_SYSCHAR = 0x0106;

    [Fact]
    public void Espace_NActiveJamaisLeBouton()
    {
        // ⛔ Le constat : avaler l'enfoncement ET le relâchement, sinon BUTTON clique.
        Assert.Equal(HeaderKeyAction.Swallow, LearningModule.ClassifyHeaderButtonKey(WM_KEYDOWN, 0x20));
        Assert.Equal(HeaderKeyAction.Swallow, LearningModule.ClassifyHeaderButtonKey(WM_KEYUP, 0x20));
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('a')]
    [InlineData('É')]
    [InlineData('’')]
    public void CaractèreImprimable_RetourneÀLExercice(char c)
    {
        Assert.Equal(HeaderKeyAction.ForwardToExercise, LearningModule.ClassifyHeaderButtonKey(WM_CHAR, c));
        Assert.Equal(HeaderKeyAction.ForwardToExercise, LearningModule.ClassifyHeaderButtonKey(WM_SYSCHAR, c));
    }

    [Fact]
    public void MoitiéDeSurrogate_RetourneÀLExercice()
    {
        // Un emoji arrive en deux WM_CHAR : chaque unité UTF-16 suit la même route.
        Assert.Equal(HeaderKeyAction.ForwardToExercise, LearningModule.ClassifyHeaderButtonKey(WM_CHAR, 0xD83D));
        Assert.Equal(HeaderKeyAction.ForwardToExercise, LearningModule.ClassifyHeaderButtonKey(WM_CHAR, 0xDE00));
    }

    [Theory]
    [InlineData('\t')]
    [InlineData('\r')]
    [InlineData('\b')]
    [InlineData('\u001B')]
    public void CaractèreDeContrôle_NEstPasRenvoyé(char c)
    {
        // Leur effet se décide au keydown ; le WM_CHAR que TranslateMessage poste ensuite
        // au bouton ne doit ni compter une faute ni ramener le focus.
        Assert.Equal(HeaderKeyAction.None, LearningModule.ClassifyHeaderButtonKey(WM_CHAR, c));
    }

    [Fact]
    public void K3_Conservé_TabEntréeÉchap()
    {
        // Accessibilité : atteindre « Quitter » avec Tab, l'activer avec Entrée.
        Assert.Equal(HeaderKeyAction.CycleFocus, LearningModule.ClassifyHeaderButtonKey(WM_KEYDOWN, 0x09));
        Assert.Equal(HeaderKeyAction.Click, LearningModule.ClassifyHeaderButtonKey(WM_KEYDOWN, 0x0D));
        Assert.Equal(HeaderKeyAction.Close, LearningModule.ClassifyHeaderButtonKey(WM_KEYDOWN, 0x1B));
    }

    [Fact]
    public void AutresTouches_LaissentLaProcédureDuBouton()
    {
        // Une lettre au keydown : son WM_CHAR suivra et sera renvoyé ; le keydown lui-même
        // n'a rien à faire. Idem pour le relâchement d'Entrée.
        Assert.Equal(HeaderKeyAction.None, LearningModule.ClassifyHeaderButtonKey(WM_KEYDOWN, 0x41));
        Assert.Equal(HeaderKeyAction.None, LearningModule.ClassifyHeaderButtonKey(WM_KEYUP, 0x0D));
    }
}
