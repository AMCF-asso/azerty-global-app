using System.Reflection;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoin R4 de la revue de code du 2026-09-21.
///
/// La doctrine est écrite dans <c>KeyMapper.ArbitratePendingDeadKeyWhileSuspended</c> :
/// une pause volontaire conserve la touche morte en attente, seule une suspension imposée
/// par le premier plan l'abandonne. Le produit ne la tenait pas — la reprise passait par
/// <c>StopPause</c> → <c>ApplyHookState(syncWhenActive: true)</c> → <c>SyncState()</c>,
/// qui appelait <c>_composition.Cancel()</c> sans condition. Le geste 16 de la recette
/// était donc faux.
///
/// <c>DeadKeyWhileSuspendedTests</c> ne le voyait pas : il court-circuite <c>StopPause</c>
/// en remettant <c>EmissionPaused</c> à false directement, sans jamais resynchroniser.
///
/// ⚠️ Limite assumée : la suite ne sait pas construire <c>TrayApplication</c>. Ces témoins
/// tiennent la couture — le paramètre de <c>SyncState</c> — et non l'appelant ;
/// <c>StopPause</c> reste prouvé par le geste 16 en VM.
/// </summary>
public class PauseResumeDeadKeyTests
{
    private const uint SC_CIRCONFLEXE = 0x1A;
    private const uint SC_A = 0x1E;
    private const uint VK_CIRCONFLEXE = 0xDD;
    private const uint VK_A = 0x41;

    private static Layout LayoutAvecCirconflexe()
    {
        var layout = new Layout();
        layout.Keys[SC_CIRCONFLEXE] = new KeyDefinition
        {
            Scancode = (ushort)SC_CIRCONFLEXE,
            Position = "D11",
            Base = "dk_circumflex"
        };
        layout.Keys[SC_A] = new KeyDefinition
        {
            Scancode = (ushort)SC_A,
            Position = "C01",
            Base = "a"
        };

        var dk = new DeadKeyDefinition { Name = "dk_circumflex", Description = "circonflexe" };
        dk.Table["a"] = "â";
        dk.Table[" "] = "^";
        layout.DeadKeys["dk_circumflex"] = dk;
        return layout;
    }

    private static string? ToucheMorteEnAttente(KeyMapper km)
    {
        var champ = typeof(KeyMapper).GetField("_composition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(champ);
        var composition = (CompositionEngine)champ!.GetValue(km)!;
        return composition.ActiveDeadKey;
    }

    /// <summary>Accent circonflexe armé, puis pause volontaire — l'état d'avant la reprise.</summary>
    private static KeyMapper MapperEnPauseAvecToucheMorte()
    {
        var km = new KeyMapper(LayoutAvecCirconflexe(), new MockWin32Api());
        km.ProcessKey(VK_CIRCONFLEXE, SC_CIRCONFLEXE, 0, isKeyDown: true);
        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));

        km.EmissionPaused = true;
        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));
        return km;
    }

    /// <summary>Le correctif : la reprise d'une pause volontaire préserve l'attente.</summary>
    [Fact]
    public void ReprisePreservante_LaToucheMorteSurvit()
    {
        var km = MapperEnPauseAvecToucheMorte();
        km.EmissionPaused = false;

        km.SyncState(preservePendingDeadKey: true);

        Assert.Equal("dk_circumflex", ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// Réciproque obligatoire : sans le drapeau, <c>SyncState</c> annule bel et bien.
    /// Sans ce témoin, un <c>SyncState</c> devenu incapable d'annuler quoi que ce soit
    /// rendrait le précédent vert pour la mauvaise raison.
    /// </summary>
    [Fact]
    public void ResynchronisationOrdinaire_LaToucheMorteEstAbandonnee()
    {
        var km = MapperEnPauseAvecToucheMorte();
        km.EmissionPaused = false;

        km.SyncState();

        Assert.Null(ToucheMorteEnAttente(km));
    }

    /// <summary>
    /// La touche morte préservée doit être utilisable, pas seulement présente en champ :
    /// la lettre suivante la consomme et compose.
    /// </summary>
    [Fact]
    public void ApresUneReprisePreservante_LaLettreSuivanteCompose()
    {
        var km = MapperEnPauseAvecToucheMorte();
        km.EmissionPaused = false;
        km.SyncState(preservePendingDeadKey: true);

        km.ProcessKey(VK_A, SC_A, 0, isKeyDown: true);

        Assert.Null(ToucheMorteEnAttente(km));
    }
}
