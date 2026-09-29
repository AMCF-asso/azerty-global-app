using System.Diagnostics;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// R58 (recette Sandbox du 2026-09-29, passage de 00:09) : la case principale des Couches
/// cochée, Entrée, la boîte « activé » répondue, puis config.json relu aussitôt :
/// <c>maintainableLayersEnabled</c> n'y était pas encore.
///
/// Verdict : écriture différée, pas perdue. <c>SaveToConfig</c> pose ses sept réglages dans
/// le cache et chacun réarme l'écriture groupée ; le fichier est écrit une fois, sur le pool
/// de threads, <c>SaveDelayMilliseconds</c> (400 ms en production) après le dernier réglage.
/// Ni la boîte ni la fermeture de la fenêtre n'y sont pour rien : l'écriture part pendant que
/// la boîte est encore ouverte. La recette lisait le fichier environ un quart à une demi-seconde
/// après Entrée, avant ou pendant cette écriture.
///
/// Aucune vraie boîte ici : « activé » passe par <c>MaintainableLayersWindow.ShowMessageBox</c>,
/// que le test remplace. La fenêtre n'est jamais affichée.
/// </summary>
public class CouchesEcritureDiffereeTests : IDisposable
{
    private const int IDOK = 1;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGR58_" + Guid.NewGuid().ToString("N"));
    private readonly Func<IntPtr, string, string, uint, int> _boîteDOrigine = MaintainableLayersWindow.ShowMessageBox;
    private string ConfigPath => Path.Combine(_dir, "config.json");

    public CouchesEcritureDiffereeTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath,
            "{\"activationConsent\":true,\"appLanguage\":\"fr\",\"maintainableLayersEnabled\":false}");
        ConfigManager.OverrideConfigPathForTests(ConfigPath); // coupe aussi l'écriture différée
        _ = ConfigManager.MaintainableLayersEnabled;
    }

    public void Dispose()
    {
        MaintainableLayersWindow.ShowMessageBox = _boîteDOrigine;
        ConfigManager.SaveDelayMilliseconds = -1;
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    private static int Écritures() => Volatile.Read(ref ConfigManager.DiskWriteCount);

    /// <summary>
    /// Lit le disque sans gêner l'écrivain. File.Replace n'est pas instantané : pendant le
    /// remplacement, config.json manque un instant ou reste ouvert sans partage, et un
    /// lecteur qui ne partage pas la suppression fait échouer le remplacement (mesuré le
    /// 29/09 : 26 échecs sur 2 000 avec FileShare.Read, aucun avec ReadWrite | Delete).
    /// Un fichier momentanément illisible compte comme « pas encore ».
    /// </summary>
    private bool ActivéSurLeDisque()
    {
        try
        {
            using var flux = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(flux);
            return doc.RootElement.TryGetProperty("maintainableLayersEnabled", out var v)
                && v.ValueKind == JsonValueKind.True;
        }
        catch (IOException) { return false; }
    }

    private static bool Attendre(Func<bool> condition, int millisecondes)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(millisecondes);
        while (DateTime.UtcNow < limite)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }

    /// <summary>La fenêtre créée, jamais affichée, la case principale cochée.</summary>
    private static MaintainableLayersWindow CasePrincipaleCochée()
    {
        var fenêtre = new MaintainableLayersWindow();
        IntPtr maître = BancCapture.Field<IntPtr>(fenêtre, "_hMaster");
        Win32.SendMessageW(maître, Win32.BM_SETCHECK, (IntPtr)Win32.BST_CHECKED, IntPtr.Zero);
        return fenêtre;
    }

    [Fact]
    public void R58_ActivationDesCouches_ÉcriteSeule400msAprèsLeGeste_BoîteEncoreOuverte()
    {
        ConfigManager.SaveDelayMilliseconds = 400; // valeur de production
        int avant = Écritures();
        using var fenêtre = CasePrincipaleCochée();

        var chrono = Stopwatch.StartNew();
        long réglésÀ = -1, écritÀ = -1;
        int boîtes = 0;
        // SaveToConfig lève SettingsChanged après ses sept réglages, avant la boîte.
        fenêtre.SettingsChanged += () => réglésÀ = chrono.ElapsedMilliseconds;
        MaintainableLayersWindow.ShowMessageBox = (owner, text, caption, type) =>
        {
            boîtes++;
            // La boîte reste ouverte jusqu'à l'écriture, trois secondes au plus.
            if (Attendre(ActivéSurLeDisque, 3_000)) écritÀ = chrono.ElapsedMilliseconds;
            return IDOK;
        };

        fenêtre.Hide(); // Enregistrer, Entrée, Échap et la croix passent tous par ici

        Assert.Equal(1, boîtes); // la boîte « activé » s'est ouverte
        Assert.True(réglésÀ >= 0);
        Assert.True(écritÀ >= 0, "l'activation n'a pas atteint config.json tant que la boîte était ouverte");
        Assert.InRange(écritÀ - réglésÀ, 350, 3_000); // pas avant le délai de regroupement
        // Le compteur est incrémenté juste après File.Replace : le fichier peut se voir avant lui.
        Assert.True(Attendre(() => Écritures() >= avant + 1, 2_000), "le compteur d'écritures n'a pas suivi");
        Assert.Equal(avant + 1, Écritures()); // une seule écriture pour les sept réglages
    }

    [Fact]
    public void R58_ActivationRelueAussitôtAprèsLaBoîte_PasEncoreSurLeDisque_PuisÉcriteSansAutreGeste()
    {
        // Délai allongé pour rendre déterministe la lecture « aussitôt » de la recette.
        ConfigManager.SaveDelayMilliseconds = 2_000;
        int avant = Écritures();
        using var fenêtre = CasePrincipaleCochée();

        var chrono = Stopwatch.StartNew();
        long réglésÀ = -1;
        int boîtes = 0;
        fenêtre.SettingsChanged += () => réglésÀ = chrono.ElapsedMilliseconds;
        MaintainableLayersWindow.ShowMessageBox = (owner, text, caption, type) => { boîtes++; return IDOK; };

        fenêtre.Hide();
        long fermée = chrono.ElapsedMilliseconds;

        Assert.Equal(1, boîtes);
        Assert.True(réglésÀ >= 0);
        Assert.True(fermée - réglésÀ < 1_500, $"banc trop lent pour juger : {fermée - réglésÀ} ms");
        // Ce qu'a vu la recette : boîte répondue, fenêtre fermée, fichier pas encore écrit…
        Assert.False(ActivéSurLeDisque());
        Assert.Equal(avant, Écritures());
        // … alors que l'application a bien pris l'activation…
        Assert.True(ConfigManager.MaintainableLayersEnabled);
        // … et que le fichier suit seul, sans autre geste ni fermeture de l'application.
        Assert.True(Attendre(ActivéSurLeDisque, 6_000), "l'activation n'a jamais atteint config.json");
        // Le compteur est incrémenté juste après File.Replace : le fichier peut se voir avant lui.
        Assert.True(Attendre(() => Écritures() >= avant + 1, 2_000), "le compteur d'écritures n'a pas suivi");
        Assert.Equal(avant + 1, Écritures());
    }
}
