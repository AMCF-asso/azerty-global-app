using System.Globalization;

namespace AZERTYGlobal;

/// <summary>Un accent isolé reçoit un support visible ; le texte émis reste inchangé.</summary>
internal static class DisplayGlyph
{
    public static string ForStandaloneMark(string text)
    {
        if (text.Length == 0) return text;
        var category = CharUnicodeInfo.GetUnicodeCategory(text, 0);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark ? "◌" + text : text;
    }
}
