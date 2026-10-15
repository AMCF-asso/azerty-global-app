using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>Leçon depuis un texte collé (refonte 1.3.0).</summary>
public class LessonCustomTextTests
{
    [Fact]
    public void Build_WolofSentence_KeepsEverythingTypable()
    {
        // Lettres du wolof et du peul présentes dans l'index de la disposition.
        var result = LessonCustomText.Build("Ñaata ngay jënd ? Ɓeyngu ŋ", "Tapez votre texte");

        Assert.NotNull(result.Exercise);
        Assert.Empty(result.Removed);
        Assert.Equal("Ñaata ngay jënd ? Ɓeyngu ŋ", result.Exercise!.Content);
        Assert.Equal(LessonCatalogLoader.CustomTextModuleId, result.Exercise.ModuleId);
        Assert.True(LessonCatalogLoader.IsComposed(result.Exercise.ModuleId));
    }

    [Fact]
    public void Build_RemovesAndListsUntypableOnce()
    {
        var result = LessonCustomText.Build("a☃b☃c😀", "Tape", c => c is "a" or "b" or "c");

        Assert.Equal("abc", result.Exercise!.Content);
        Assert.Equal(new[] { "☃", "😀" }, result.Removed);
    }

    [Fact]
    public void Build_NormalizesToComposedFormAndCollapsesSpaces()
    {
        // « e » + accent aigu combinant devient « é » ; tabulation et doubles espaces.
        var result = LessonCustomText.Build("café\t  noir\r\nsucre", "Tape", _ => true);

        Assert.Equal(new[] { "café noir", "sucre" }, result.Exercise!.Lines);
    }

    [Fact]
    public void Build_WrapsAtWordsAndCapsLines()
    {
        string text = string.Join(' ', Enumerable.Repeat("mot", 400));

        var result = LessonCustomText.Build(text, "Tape", _ => true);

        Assert.Equal(LessonCustomText.MaxLines, result.Exercise!.Lines.Length);
        Assert.All(result.Exercise.Lines, line =>
        {
            Assert.InRange(line.Length, 1, LessonReviewBuilder.TargetLineLength);
            Assert.DoesNotContain("  ", line);
        });
    }

    [Fact]
    public void Build_LongWordIsChunkedWithoutSplittingDiacritics()
    {
        string word = string.Concat(Enumerable.Repeat("ɛ̀", 30)); // 60 caractères UTF-16

        var result = LessonCustomText.Build(word, "Tape", _ => true);

        Assert.All(result.Exercise!.Lines, line =>
        {
            Assert.InRange(line.Length, 2, LessonReviewBuilder.TargetLineLength);
            Assert.Equal('ɛ', line[0]);
        });
    }

    [Fact]
    public void Build_NothingTypable_ReturnsNullExercise()
    {
        var result = LessonCustomText.Build("☃ ☃", "Tape", c => c == " ");

        Assert.Null(result.Exercise);
        Assert.Equal(new[] { "☃" }, result.Removed);
    }
}
