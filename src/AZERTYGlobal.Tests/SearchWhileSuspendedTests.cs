using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin R3 de la revue de code du 2026-09-21 (écart 8 de la recette VM du 19/09).
///
/// Sous « forcer la désactivation », le hook garde la détection des raccourcis
/// (<c>ShortcutsWhilePassThrough</c>), mais <c>WM_APP_SEARCH</c> exigeait
/// <c>ShouldProcessHook</c> : Ctrl+Maj+W était bien avalé — plus de Ctrl+W destructeur —
/// et la recherche ne s'ouvrait pas, l'app affichant « Désactivé ». Le drapeau était juste,
/// son consommateur l'ignorait.
///
/// La règle tenue ici : la recherche suit **exactement** la détection des raccourcis sous
/// suspension. Si les deux décisions divergent un jour, le troisième test rougit.
///
/// ⚠️ Limite assumée : ces témoins tiennent la décision, pas le câblage de la fenêtre.
/// L'arrivée réelle de WM_APP_SEARCH reste du Win32, prouvée par la recette VM.
/// </summary>
public class SearchWhileSuspendedTests
{
    private static bool ServirLaRecherche(bool suspendu, CompatibilitySuspendReason raison)
    {
        var method = typeof(TrayApplication).GetMethod(
            "ShouldServeSearchWhileBlocked",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (bool)method!.Invoke(null, new object[] { suspendu, raison })!;
    }

    private static bool DetecterLesRaccourcis(
        bool enPause, bool suspendu, CompatibilitySuspendReason raison)
    {
        var method = typeof(TrayApplication).GetMethod(
            "ShouldDetectShortcutsWhileBlocked",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (bool)method!.Invoke(null, new object[] { enPause, suspendu, raison })!;
    }

    /// <summary>Le cas de l'écart 8 : l'utilisateur a lui-même marqué l'app « désactivée ».</summary>
    [Fact]
    public void SuspensionChoisieParLUtilisateur_LaRechercheSOuvre()
    {
        Assert.True(ServirLaRecherche(true, CompatibilitySuspendReason.UserOverride));
    }

    /// <summary>
    /// Réciproque : une suspension imposée protège quelque chose — anti-cheat, session
    /// distante, premier plan inconnu. La recherche n'y passe pas.
    /// </summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.AntiCheat)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground)]
    [InlineData(CompatibilitySuspendReason.None)]
    public void SuspensionImposee_LaRechercheResteFermee(CompatibilitySuspendReason raison)
    {
        Assert.False(ServirLaRecherche(true, raison));
    }

    /// <summary>
    /// Sans suspension de compatibilité, cette décision ne sert jamais : c'est
    /// <c>ShouldProcessHook</c> qui tranche, et une pause volontaire doit continuer
    /// d'annoncer « En pause » plutôt que d'ouvrir la fenêtre.
    /// </summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.None)]
    [InlineData(CompatibilitySuspendReason.UserOverride)]
    public void HorsSuspension_LaDecisionEstToujoursNegative(CompatibilitySuspendReason raison)
    {
        Assert.False(ServirLaRecherche(false, raison));
    }

    private static bool PeutOuvrir(bool traite, bool choisie, bool securisee)
    {
        var method = typeof(TrayApplication).GetMethod(
            "CanOpenCharacterSearch",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (bool)method!.Invoke(null, new object[] { traite, choisie, securisee })!;
    }

    /// <summary>
    /// Signalé par Antoine le 2026-11-12 : « Rechercher un caractère » grisé « de temps en
    /// temps ». R3 avait corrigé le raccourci, pas l'entrée du menu : dans une application
    /// marquée « désactivée » par l'utilisateur, Ctrl+Maj+W ouvrait la recherche et le menu
    /// la grisait. Le menu, son clic et le raccourci passent par la même décision.
    /// </summary>
    [Theory]
    [InlineData(true, false, false, true)]   // app active
    [InlineData(false, true, false, true)]   // suspension choisie : le cas signalé
    [InlineData(false, false, false, false)] // pause, désactivée, jeu, accès distant
    [InlineData(true, false, true, false)]   // champ de mot de passe
    [InlineData(false, true, true, false)]   // choisie, mais champ de mot de passe
    public void LeMenuLeClicEtLeRaccourciOntLaMemeDecision(
        bool traite, bool choisie, bool securisee, bool attendu)
    {
        Assert.Equal(attendu, PeutOuvrir(traite, choisie, securisee));
    }

    /// <summary>
    /// Le lien entre les deux décisions est le cœur de R3 : un raccourci détecté qui
    /// n'ouvre rien est précisément le défaut corrigé. Ce témoin rougit si l'une des deux
    /// fonctions bouge sans l'autre.
    /// </summary>
    [Theory]
    [InlineData(CompatibilitySuspendReason.None)]
    [InlineData(CompatibilitySuspendReason.AntiCheat)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess)]
    [InlineData(CompatibilitySuspendReason.UserOverride)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground)]
    public void SousSuspension_LaRechercheSuitLaDetectionDesRaccourcis(
        CompatibilitySuspendReason raison)
    {
        Assert.Equal(
            DetecterLesRaccourcis(enPause: false, suspendu: true, raison),
            ServirLaRecherche(true, raison));
    }
}
