using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// AG130-09 — la garde de suspension interne à <c>KeyMapper.SendInputs</c>.
///
/// Pourquoi un fichier à part, et pourquoi l'appel direct : <c>SendInputs</c> rend 0 quand
/// l'émission est suspendue, et ce 0 ne doit pas être compté comme une perte. Or les sept
/// sites d'émission vérifient tous <c>IsEmissionSuspended</c> avant d'appeler, si bien
/// qu'aucun chemin public n'atteint cette garde. Mesuré par mutation le 2026-09-20 : en
/// faisant compter la suspension comme une perte, la suite entière restait verte — le témoin
/// écrit par <c>TryEmitText</c> passait pour la mauvaise raison, sa garde d'entrée le faisant
/// sortir avant.
///
/// Ce témoin appelle donc la méthode directement. Il est le seul à le faire ; les onze autres
/// témoins d'AG130-09 vivent dans <c>AZERTYGlobal.Tests/EmissionLossTests.cs</c> et passent
/// par l'API publique.
/// </summary>
public class EmissionLossGuardTests
{
    private static Win32.INPUT[] UnEvenement() => new[]
    {
        new Win32.INPUT { type = 1, u = new Win32.INPUTUNION { ki = new Win32.KEYBDINPUT { wVk = 0x41 } } }
    };

    [Fact]
    public void ÉmissionSuspendue_LeZéroInterneNEstPasUnePerte()
    {
        var api = new MockWin32Api { SendInputResult = 0 };
        var mapper = new KeyMapper(new Layout(), api) { EmissionPaused = true };

        uint sent = mapper.SendInputs(UnEvenement());

        Assert.Equal(0u, sent);
        Assert.Equal(0, mapper.EmissionLossCount);
        Assert.Empty(api.SendInputCalls); // rien n'est même tenté
    }

    /// <summary>
    /// Contrôle négatif : le même appel, émission active, compte bien la perte. Sans lui, un
    /// compteur devenu inerte ferait passer le témoin ci-dessus.
    /// </summary>
    [Fact]
    public void ÉmissionActive_LeZéroDeWindowsEstUnePerte()
    {
        var api = new MockWin32Api { SendInputResult = 0 };
        var mapper = new KeyMapper(new Layout(), api);

        uint sent = mapper.SendInputs(UnEvenement());

        Assert.Equal(0u, sent);
        Assert.Equal(1, mapper.EmissionLossCount);
        Assert.Single(api.SendInputCalls);
    }

    /// <summary>
    /// Second contrôle négatif : un lot accepté ne compte rien, ce qui exclut un compteur
    /// incrémenté à chaque passage plutôt que sur le refus.
    /// </summary>
    [Fact]
    public void LotAccepté_NeCompteRien()
    {
        var api = new MockWin32Api();
        var mapper = new KeyMapper(new Layout(), api);

        uint sent = mapper.SendInputs(UnEvenement());

        Assert.Equal(1u, sent);
        Assert.Equal(0, mapper.EmissionLossCount);
    }
}
