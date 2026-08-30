"""`Scale_SuitLEchelleDeLEcran` mesure l'échelle de l'écran, pas la densité.

Rougi par le passage de la densité à 0,90 : le test assertait `Scale(3, 120) == 4`, ce qui vaut
à densité 1 et rend 3 à 0,90. Il a raison de rougir — il compare une valeur qui a changé — mais
ce qu'il éprouve est l'arrondi du DPI, et il doit continuer de l'éprouver quelle que soit la
densité en vigueur. Il fixe donc la densité à 1 pour la durée de ses assertions.

Un second test prend l'autre moitié : que la densité multiplie bien, et qu'elle se restaure à la
sortie du `using`. Sans lui, mettre `Density` à 1 en dur ferait passer les deux.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "src" / "AZERTYGlobal.Tests" / "ThemeControlsTests.cs"

data = TARGET.read_bytes()
if data.count(b"\r\n"):
    sys.exit("ThemeControlsTests.cs n'est plus en LF pur")
text = data.decode("utf-8")

OLD = """    [Fact]
    public void Scale_SuitLEchelleDeLEcran()
    {
        Assert.Equal(3, ThemeControls.Scale(3, 96));
        Assert.Equal(4, ThemeControls.Scale(3, 120));   // 3,75 arrondi au plus loin de zéro
        Assert.Equal(3, ThemeControls.Scale(2, 120));   // 2,5 idem
        Assert.Equal(20, ThemeControls.Scale(16, 120));
        Assert.Equal(24, ThemeControls.Scale(16, 144));
    }"""

NEW = """    [Fact]
    public void Scale_SuitLEchelleDeLEcran()
    {
        // La densité est fixée à 1 le temps de ces assertions : ce test éprouve l'arrondi du
        // DPI, qui doit tenir quelle que soit la densité en vigueur. Elle est passée à 0,90 le
        // 2026-08-30 et il a rougi — à raison, la valeur avait changé, mais pour la mauvaise
        // question.
        using (ThemeControls.OverrideDensityForTests(1.0f))
        {
            Assert.Equal(3, ThemeControls.Scale(3, 96));
            Assert.Equal(4, ThemeControls.Scale(3, 120));   // 3,75 arrondi au plus loin de zéro
            Assert.Equal(3, ThemeControls.Scale(2, 120));   // 2,5 idem
            Assert.Equal(20, ThemeControls.Scale(16, 120));
            Assert.Equal(24, ThemeControls.Scale(16, 144));
        }
    }

    /// <summary>
    /// L'autre moitié : la densité multiplie, et elle se restaure à la sortie du <c>using</c>.
    /// Sans ce test, poser <c>Density = 1</c> en dur ferait passer le précédent.
    /// </summary>
    [Fact]
    public void Scale_SuitLaDensiteGlobale()
    {
        int aPleineDensite;
        using (ThemeControls.OverrideDensityForTests(1.0f))
            aPleineDensite = ThemeControls.Scale(100, 96);

        using (ThemeControls.OverrideDensityForTests(0.5f))
            Assert.Equal(50, ThemeControls.Scale(100, 96));

        Assert.Equal(100, aPleineDensite);

        // Restaurée : la valeur d'application est celle qu'Antoine a arrêtée, pas 0,5.
        Assert.Equal(90, ThemeControls.Scale(100, 96));
    }"""

if text.count(OLD) != 1:
    sys.exit(f"ancre : {text.count(OLD)} occurrence(s), 1 attendue")

TARGET.write_bytes(text.replace(OLD, NEW).encode("utf-8"))
print("  ThemeControlsTests.cs  Scale isole la densite, et un test la couvre")
