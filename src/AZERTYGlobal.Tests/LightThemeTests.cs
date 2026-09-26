using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, lot 8 (F-14) — une couleur de la palette claire ne se déclare qu'une fois,
/// dans <see cref="LightTheme"/>. Que les valeurs n'aient pas bougé, le banc de captures le
/// vérifie au pixel ; ici, qu'aucune fenêtre ne la redéclare en littéral.
/// </summary>
public class LightThemeTests
{
    private static readonly string[] Fenêtres =
    {
        "AboutWindow.cs", "LayoutConflictWindow.cs", "UsageStatsWindow.cs",
        "MaintainableLayersWindow.cs", "OnboardingWindow.cs", "SettingsWindow.cs",
    };

    [Fact]
    public void AucuneFenêtreNeRedéclareUneCouleurDeLaPalette()
    {
        var palette = typeof(LightTheme).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Select(f => (uint)f.GetRawConstantValue()!)
            .ToHashSet();
        Assert.Equal(10, palette.Count);

        string src = Path.Combine(FindMicrosoftStoreRoot(), "src");
        var déclaration = new Regex(@"const uint (CLR_\w+) = 0x([0-9A-Fa-f]{8});");
        var fautives = Fenêtres
            .SelectMany(f => déclaration.Matches(File.ReadAllText(Path.Combine(src, f)))
                .Where(m => palette.Contains(Convert.ToUInt32(m.Groups[2].Value, 16)))
                .Select(m => $"{f} : {m.Value}"))
            .ToList();

        Assert.True(fautives.Count == 0, string.Join(Environment.NewLine, fautives));
    }

    private static string FindMicrosoftStoreRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "LightTheme.cs")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Racine Microsoft Store introuvable depuis les tests.");
    }
}
