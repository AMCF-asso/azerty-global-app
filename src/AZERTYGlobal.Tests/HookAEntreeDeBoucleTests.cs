using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot 4 de l'audit 1.3.0, A22 (PERF-1) : le hook est posé dans Run(), juste avant la boucle
/// de messages, et non plus au milieu de la construction de l'application.
///
/// ⚠️ Limite assumée : ce témoin tient la décision pure (accord, une seule pose), pas l'ordre
/// des appels dans Run() ni le gain de temps, non mesuré. Protocole machine : build Release
/// AOT, chronométrer Install → premier GetMessageW au logon, frappes SendInput à 10 Hz.
/// </summary>
public class HookAEntreeDeBoucleTests
{
    [Theory]
    [InlineData(true, true, false, false, true)]   // lancement avec accord : pose à l'entrée de la boucle
    [InlineData(false, true, false, false, false)] // sans accord : la pose attend ActivateWithConsent
    [InlineData(true, false, false, false, false)] // construction échouée avant le hook
    [InlineData(true, true, true, false, false)]   // déjà posé (Reinstall pendant la construction) : pas de second hook
    [InlineData(true, true, false, true, false)]   // constructeur en échec : WM_QUIT attend, rien à poser
    public void La_pose_exige_l_accord_et_n_a_lieu_qu_une_fois(bool accord, bool cree, bool pose, bool echec, bool attendu)
    {
        Assert.Equal(attendu, TrayApplication.ShouldInstallHookAtLoopStart(accord, cree, pose, echec));
    }
}
