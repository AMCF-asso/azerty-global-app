using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, lot 8 (F-14), étendu par le lot visuel 1.3.0 — une couleur ne se déclare
/// qu'une fois, dans <see cref="LightTheme"/> ou <see cref="DarkTheme"/>. Que les valeurs
/// n'aient pas bougé, le banc de captures le vérifie au pixel ; ici, qu'aucune fenêtre ne
/// déclare de couleur en littéral, ni en constante ni à l'appel.
/// </summary>
public class LightThemeTests
{
    private static readonly string[] Fenêtres =
    {
        "AboutWindow.cs", "LayoutConflictWindow.cs", "UsageStatsWindow.cs",
        "MaintainableLayersWindow.cs", "OnboardingWindow.cs", "SettingsWindow.cs",
        "PauseDurationDialog.cs", "CharacterSearch.cs", "KeyboardRenderer.cs",
        "LearningModule.cs", "LessonsWindow.cs", "VirtualKeyboard.cs",
        "ToggleNotification.cs", "LayerIndicatorWindow.cs",
    };

    [Fact]
    public void AucuneFenêtreNeDéclareDeCouleurEnLittéral()
    {
        string src = Path.Combine(FindMicrosoftStoreRoot(), "src");
        var littéral = new Regex(
            @"const uint CLR_\w+ = 0x[0-9A-Fa-f]{8}|(SetTextColor|SetBkColor|CreateSolidBrush|ApplyClassBackground|FillSolidRect)\([^;]*0x[0-9A-Fa-f]{6,8}u?\b");
        var fautives = Fenêtres
            .SelectMany(f => littéral.Matches(File.ReadAllText(Path.Combine(src, f)))
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
