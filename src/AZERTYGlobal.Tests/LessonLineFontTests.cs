using System;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Police de la ligne des Leçons : Segoe UI Variable Text là où Windows l'a (Windows 11),
/// Segoe UI sinon ; jamais la police de substitution que GDI choisirait en silence.
/// </summary>
public class LessonLineFontTests
{
    [Fact]
    public void LaLigneDesLeconsObtientSegoeUIVariableOuSonRepli()
    {
        IntPtr font = LessonsWindow.CreateLessonLineFont(-17);
        try
        {
            Assert.Contains(LessonsWindow.FontFace(font),
                new[] { LessonsWindow.LessonLineFace, LessonsWindow.LessonLineFallbackFace });
        }
        finally
        {
            Win32.DeleteObject(font);
        }
    }
}
