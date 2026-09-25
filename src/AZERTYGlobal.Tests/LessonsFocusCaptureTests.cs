using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Report de 1911581 (lot 2, c2de614) : les Leçons mettaient en file le texte attendu de chaque
/// frappe dès que leur fenêtre était visible, même sans le focus. Tapé ailleurs, ce texte restait
/// en file jusqu'à une seconde, et les premiers caractères tapés au retour dans les Leçons étaient
/// pris pour lui. La capture exige maintenant le focus, comme le tutoriel, et la file est vidée
/// quand les Leçons le reprennent.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette : la tenue du focus par WM_SETFOCUS et
/// WM_KILLFOCUS, et la file vidée au retour du focus.
/// </summary>
public class LessonsFocusCaptureTests
{
    [Fact]
    public void AvecLeFocus_LaFrappeEstCapturee()
    {
        Assert.True(LessonsWindow.ShouldCaptureExpectedText(visible: true, hasFocus: true, settingsOpen: false, showSummary: false));
    }

    [Fact]
    public void FenetreVisibleSansLeFocus_RienNEntreEnFile()
    {
        // Le défaut lui-même : on tape dans une autre application, les Leçons restent affichées.
        Assert.False(LessonsWindow.ShouldCaptureExpectedText(visible: true, hasFocus: false, settingsOpen: false, showSummary: false));
    }

    [Theory]
    [InlineData(false, true, false, false)] // fenêtre masquée
    [InlineData(true, true, true, false)]   // réglages ouverts
    [InlineData(true, true, false, true)]   // récapitulatif affiché
    public void LesGardesDAvantRestent(bool visible, bool hasFocus, bool settingsOpen, bool showSummary)
    {
        Assert.False(LessonsWindow.ShouldCaptureExpectedText(visible, hasFocus, settingsOpen, showSummary));
    }
}
