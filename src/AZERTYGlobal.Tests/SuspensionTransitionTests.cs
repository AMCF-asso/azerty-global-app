using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins de l'écart 8 de la recette VM du 2026-09-19 et du trou de transition mesuré
/// par l'audit du 2026-09-20 (AG130-06).
///
/// Avant ce lot, <c>ShortcutsWhilePassThrough</c> n'était lu par aucun test : la seule
/// vérification de l'écart 8 était un œil en VM. Les deux décisions qu'il porte sont
/// désormais des fonctions pures, et ces témoins les tiennent.
/// </summary>
public class SuspensionTransitionTests
{
    private static string Classify(
        CompatibilityMode mode,
        bool suspended,
        CompatibilitySuspendReason applied,
        CompatibilitySuspendReason current)
    {
        var method = typeof(TrayApplication).GetMethod(
            "ClassifySuspensionTransition",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { mode, suspended, applied, current });
        Assert.NotNull(result);
        return result!.ToString()!;
    }

    private static bool ShouldDetectShortcuts(
        bool isPaused, bool suspended, CompatibilitySuspendReason reason)
    {
        var method = typeof(TrayApplication).GetMethod(
            "ShouldDetectShortcutsWhileBlocked",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (bool)method!.Invoke(null, new object[] { isPaused, suspended, reason })!;
    }

    // ---- Les deux transitions que la version à deux branches ratait ----

    /// <summary>
    /// Le sens dangereux : l'utilisateur a marqué une application « désactivée », puis passe
    /// à un jeu anti-triche. Le mode ne change pas (les quatre raisons rendent toutes
    /// DisabledAntiCheat) et la suspension non plus, donc rien ne rappelait ApplyHookState :
    /// le hook restait configuré pour l'override, c'est-à-dire à détecter les raccourcis
    /// dans un jeu qui bannit pour cela.
    /// </summary>
    [Fact]
    public void OverrideUtilisateurVersAntiTriche_EstUneTransition()
    {
        Assert.Equal("ReasonChanged", Classify(
            CompatibilityMode.DisabledAntiCheat,
            suspended: true,
            applied: CompatibilitySuspendReason.UserOverride,
            current: CompatibilitySuspendReason.AntiCheat));
    }

    /// <summary>
    /// L'écart 8 lui-même, dans l'autre sens : en sortant d'un jeu anti-triche vers une
    /// application que l'utilisateur a marquée « désactivée », le hook restait en inertie
    /// totale, donc Ctrl+Maj+W n'ouvrait pas la recherche de caractères et atteignait
    /// l'application, qui y lisait Ctrl+W et fermait sa fenêtre.
    /// </summary>
    [Fact]
    public void AntiTricheVersOverrideUtilisateur_EstUneTransition()
    {
        Assert.Equal("ReasonChanged", Classify(
            CompatibilityMode.DisabledAntiCheat,
            suspended: true,
            applied: CompatibilitySuspendReason.AntiCheat,
            current: CompatibilitySuspendReason.UserOverride));
    }

    /// <summary>Toute paire de raisons distinctes est une transition, pas seulement celles de la recette.</summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.UserOverride, CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.UserOverride, CompatibilitySuspendReason.UnknownForeground)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess, CompatibilitySuspendReason.UserOverride)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground, CompatibilitySuspendReason.UserOverride)]
    [InlineData(CompatibilitySuspendReason.AntiCheat, CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess, CompatibilitySuspendReason.AntiCheat)]
    public void ChangementDeRaisonPendantLaSuspension_EstUneTransition(
        CompatibilitySuspendReason applied, CompatibilitySuspendReason current)
    {
        Assert.Equal("ReasonChanged", Classify(
            CompatibilityMode.DisabledAntiCheat, suspended: true, applied, current));
    }

    // ---- Témoins négatifs : ce qui ne doit PAS devenir une transition ----

    /// <summary>
    /// Contre-témoin indispensable : sans lui, un classificateur qui rendrait toujours
    /// ReasonChanged passerait tous les tests ci-dessus. Changer de fenêtre dans la même
    /// application suspendue ne doit rien réappliquer, ni rejouer la bulle.
    /// </summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.AntiCheat)]
    [InlineData(CompatibilitySuspendReason.UserOverride)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground)]
    public void MemeRaison_NEstPasUneTransition(CompatibilitySuspendReason reason)
    {
        Assert.Equal("None", Classify(
            CompatibilityMode.DisabledAntiCheat, suspended: true, reason, reason));
    }

    [Fact]
    public void PremierPlanOrdinaire_NonSuspendu_NEstPasUneTransition()
    {
        Assert.Equal("None", Classify(
            CompatibilityMode.Default,
            suspended: false,
            applied: CompatibilitySuspendReason.None,
            current: CompatibilitySuspendReason.None));
    }

    // ---- Les deux transitions que la version à deux branches traitait déjà ----

    [Fact]
    public void EntreeEnSuspension_EstUneEntree()
    {
        Assert.Equal("Enter", Classify(
            CompatibilityMode.DisabledAntiCheat,
            suspended: false,
            applied: CompatibilitySuspendReason.None,
            current: CompatibilitySuspendReason.AntiCheat));
    }

    [Fact]
    public void SortieDeSuspension_EstUneSortie()
    {
        Assert.Equal("Leave", Classify(
            CompatibilityMode.Default,
            suspended: true,
            applied: CompatibilitySuspendReason.AntiCheat,
            current: CompatibilitySuspendReason.None));
    }

    // ---- La propriété de sécurité elle-même ----

    /// <summary>Une suspension de sécurité exige l'inertie totale, quelle qu'elle soit.</summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.AntiCheat)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground)]
    public void SuspensionDeSecurite_NeDetectePasLesRaccourcis(CompatibilitySuspendReason reason)
    {
        Assert.False(ShouldDetectShortcuts(isPaused: false, suspended: true, reason));
    }

    /// <summary>
    /// Le cas que l'écart 8 a révélé : un choix de confort de l'utilisateur n'a rien à
    /// protéger, donc les raccourcis restent détectés. Le remappage, lui, reste éteint.
    /// </summary>
    [Fact]
    public void OverrideUtilisateur_DetecteLesRaccourcis()
    {
        Assert.True(ShouldDetectShortcuts(
            isPaused: false, suspended: true, CompatibilitySuspendReason.UserOverride));
    }

    /// <summary>Pause volontaire hors suspension : les raccourcis restent détectés pour permettre la reprise au clavier.</summary>
    [Fact]
    public void PauseVolontaire_DetecteLesRaccourcis()
    {
        Assert.True(ShouldDetectShortcuts(
            isPaused: true, suspended: false, CompatibilitySuspendReason.None));
    }

    /// <summary>
    /// Le piège : une pause qui tombe pendant une suspension de sécurité ne la lève pas.
    /// Un prédicat écrit comme « pause OU override » rendrait true ici et rouvrirait la
    /// détection des raccourcis dans un jeu anti-triche.
    /// </summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.AntiCheat)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground)]
    public void PausePendantUneSuspensionDeSecurite_NeLaLevePas(CompatibilitySuspendReason reason)
    {
        Assert.False(ShouldDetectShortcuts(isPaused: true, suspended: true, reason));
    }

    [Fact]
    public void NiPauseNiSuspension_NeDetectePasLesRaccourcisEnPassThrough()
    {
        Assert.False(ShouldDetectShortcuts(
            isPaused: false, suspended: false, CompatibilitySuspendReason.None));
    }
}
