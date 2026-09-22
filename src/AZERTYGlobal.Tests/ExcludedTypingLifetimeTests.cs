using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin R9 de la revue de code du 2026-09-21.
///
/// Le constructeur de <c>LearningModule</c> ouvre une plage de frappe exclue
/// (<c>UsageStats.BeginExcludedTyping</c>) qui n'est refermée que par <c>Dispose</c>.
/// Si la suite du constructeur lève — création de fenêtre, chargement des fontes,
/// contrôles — personne ne tient l'instance, donc personne n'appelle <c>Dispose</c> :
/// la plage restait ouverte et plus une seule frappe n'était comptée jusqu'au
/// redémarrage du processus. Le constructeur referme désormais la plage avant de
/// relancer.
///
/// Ce que tient ce témoin : l'ordre de la validation. Les <c>ArgumentNullException</c>
/// sont levées **avant** l'ouverture de la plage, donc le chemin le plus courant
/// d'échec du constructeur ne touche pas au compteur. C'est un ordre qui se perd
/// facilement en déplaçant trois lignes.
///
/// ⚠️ Limite assumée : le <c>catch</c> lui-même n'est pas éprouvé ici. Le faire lever
/// après l'ouverture de la plage demande un constructeur qui aille jusqu'à Win32 —
/// hors de portée d'une suite sans bureau. C'est un geste de la recette VM.
/// </summary>
public class ExcludedTypingLifetimeTests : IDisposable
{
    private readonly string _tempDir;

    public ExcludedTypingLifetimeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "usage-stats.json"));
    }

    public void Dispose()
    {
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Un_constructeur_refuse_sur_un_parametre_nul_n_ouvre_aucune_plage()
    {
        Assert.False(UsageStats.IsTypingExcluded);

        Assert.Throws<ArgumentNullException>(() =>
            new LearningModule(IntPtr.Zero, null, null, null));

        // ⛔ Si cette assertion rougit, c'est que BeginExcludedTyping est remonté
        // au-dessus des ThrowIfNull : la frappe cesserait d'être comptée pour tout
        // le processus dès le premier appel mal formé.
        Assert.False(UsageStats.IsTypingExcluded);
    }

    [Fact]
    public void Trois_refus_de_suite_ne_derivent_pas_le_compteur()
    {
        for (int i = 0; i < 3; i++)
            Assert.Throws<ArgumentNullException>(() =>
                new LearningModule(IntPtr.Zero, null, null, null));

        Assert.False(UsageStats.IsTypingExcluded);
    }
}
