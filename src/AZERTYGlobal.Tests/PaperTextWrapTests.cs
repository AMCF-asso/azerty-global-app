using System;
using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 1.3.0, A19 (VIS-11) : dans le panneau du geste des Leçons, « Tenez AltGr, frappez
/// Espace » finissait sur une ligne et le « . » partait seul sur la suivante. Une ponctuation
/// collée à un mot ne se sépare pas de lui : la coupure se fait à l'espace qui les précède.
/// </summary>
public class PaperTextWrapTests
{
    // « Tenez AltGr, frappez Espace. » en largeurs fictives : 50 5 40 5 5 60 5 50 5, espace = 5.
    private static readonly (int Width, bool PrecededBySpace)[] Phrase =
    {
        (50, false), (40, true), (5, false), (60, true), (50, true), (5, false),
    };

    [Fact]
    public void PointFinal_PartAvecSonMot()
    {
        // 220 px tiennent « … Espace », pas le point : le mot et son point passent ensemble.
        var lines = PaperText.BreakLines(Phrase, spaceWidth: 5, maxWidth: 222);

        Assert.Equal((1, 0), lines[4]);
        Assert.Equal((1, 50), lines[5]);
        Assert.All(lines[..4], position => Assert.Equal(0, position.Line));
    }

    [Fact]
    public void VirguleCollée_PartAvecSonMot()
    {
        // 97 px : « Tenez AltGr » tient (95), pas la virgule ; « AltGr, » passe ensemble.
        var lines = PaperText.BreakLines(Phrase, spaceWidth: 5, maxWidth: 97);

        Assert.Equal((0, 0), lines[0]);
        Assert.Equal((1, 0), lines[1]);
        Assert.Equal((1, 40), lines[2]);
    }

    [Fact]
    public void GroupePlusLargeQueLaLigne_SeCoupeEncoreEntreSesMots()
    {
        // Sans cette issue, un groupe collé plus large que le cadre déborderait d'un bloc.
        var words = new (int, bool)[] { (40, false), (30, false), (5, false) };
        var lines = PaperText.BreakLines(words, spaceWidth: 5, maxWidth: 50);

        Assert.Equal(new[] { (0, 0), (1, 0), (1, 30) }, lines);
    }

    [Fact]
    public void MotsSéparésParDesEspaces_CoupureInchangée()
    {
        var words = new (int, bool)[] { (30, false), (30, true), (30, true) };
        var lines = PaperText.BreakLines(words, spaceWidth: 5, maxWidth: 70);

        Assert.Equal(new[] { (0, 0), (0, 35), (1, 0) }, lines);
    }

    [Fact]
    public void PoliceRéelle_LePointResteSurLaLigneDEspace()
    {
        AppFonts.EnsureLoaded();
        var runs = new[]
        {
            new TextRun("Tenez "), new TextRun("AltGr", true), new TextRun(", frappez "),
            new TextRun("Espace", true), new TextRun("."),
        };
        var regular = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f);
        var strong = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, 1f);
        IntPtr hdc = Win32.GetDC(IntPtr.Zero);
        try
        {
            // Largeur qui tient la phrase jusqu'à « Espace » inclus, pas le point.
            var oneLine = PaperText.Layout(hdc, runs, regular, strong, int.MaxValue);
            int dotX = oneLine.Single(w => w.Text == ".").X;
            var placed = PaperText.Layout(hdc, runs, regular, strong, dotX + 1);

            var espace = placed.Single(w => w.Text == "Espace");
            var dot = placed.Single(w => w.Text == ".");
            Assert.Equal(1, espace.Line);
            Assert.Equal(espace.Line, dot.Line);
            Assert.Equal(0, espace.X);
        }
        finally { Win32.ReleaseDC(IntPtr.Zero, hdc); }
    }
}
