#if DEBUG
using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des chemins d'erreur, rapport-C K37 : une demande d'export illisible ne doit pas être rejouée
/// sans fin. DevRects n'existe qu'en Debug (#if DEBUG) : ce test n'est compilé qu'en Debug. Le dossier
/// d'échange réel de l'outil (%TEMP%\ag-dev-rects) n'est pas touché : TMP/TEMP sont redirigés vers un
/// dossier temporaire le temps du test.
/// </summary>
public sealed class CheminsErreurDevRectsTests : IDisposable
{
    private readonly string? _tmpAvant = Environment.GetEnvironmentVariable("TMP");
    private readonly string? _tempAvant = Environment.GetEnvironmentVariable("TEMP");
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "AZGCE_C37_" + Guid.NewGuid().ToString("N"));

    public CheminsErreurDevRectsTests()
    {
        Directory.CreateDirectory(_temp);
        // LogDirectory = dossier temporaire (error.log de DevRects.OnTick).
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_temp, "config.json"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TMP", _tmpAvant);
        Environment.SetEnvironmentVariable("TEMP", _tempAvant);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_temp, "inutilise-config.json"));
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_temp, true); return; }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }

    // rapport-C K37
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Demande_illisible_est_retiree_apres_un_seul_essai()
    {
        Environment.SetEnvironmentVariable("TMP", _temp);
        Environment.SetEnvironmentVariable("TEMP", _temp);
        string racine = Path.Combine(Path.GetTempPath(), "ag-dev-rects");
        Assert.StartsWith(_temp, racine, StringComparison.OrdinalIgnoreCase); // isolation : jamais le vrai dossier d'échange
        Directory.CreateDirectory(racine);
        string demande = Path.Combine(racine, "demande.json");
        File.WriteAllText(demande, "{ ceci n'est pas du json");

        MethodInfo onTick = typeof(DevRects).GetMethod("OnTick", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(DevRects), "OnTick");
        onTick.Invoke(null, new object[] { IntPtr.Zero, 0u, UIntPtr.Zero, 0u });

        Assert.False(File.Exists(demande),
            "la demande illisible reste en place : elle sera relue et journalisée toutes les 400 ms");
    }
}
#endif
