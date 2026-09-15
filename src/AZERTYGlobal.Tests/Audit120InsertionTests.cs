using System.Reflection;
using System.Runtime.CompilerServices;
using TypingEngine.Windows;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>Contrat d'insertion sans fenêtre, presse-papiers ni frappe réelle.</summary>
public class Audit120InsertionTests
{
    [Theory]
    [InlineData(0, false, (int)TextInsertionOutcome.NotInserted)]
    [InlineData(1, false, (int)TextInsertionOutcome.Partial)]
    [InlineData(3, false, (int)TextInsertionOutcome.Partial)]
    [InlineData(4, false, (int)TextInsertionOutcome.Inserted)]
    [InlineData(0, true, (int)TextInsertionOutcome.Blocked)]
    public void RetourWindows_PropageSansReessayer(uint sent, bool blocked, int expected)
    {
        int emissions = 0;
        var service = new TextInsertionService(_ => { emissions++; return new(4, sent, blocked); }, _ => true);
        Assert.Equal((TextInsertionOutcome)expected, service.Insert((IntPtr)42, "ab"));
        Assert.Equal(1, emissions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PauseAvantOuApresFocus_InterditEmission(bool pauseAfterFocus)
    {
        var calls = new List<string>();
        bool allowed = pauseAfterFocus;
        var service = new TextInsertionService(_ =>
            { calls.Add("émission"); return new(2, 2); },
            _ => { calls.Add("focus"); return true; },
            () => allowed,
            () => { calls.Add("contrôle"); allowed = false; });
        Assert.Equal(TextInsertionOutcome.Blocked, service.Insert((IntPtr)42, "α"));
        Assert.DoesNotContain("émission", calls);
        Assert.Equal(pauseAfterFocus ? new[] { "focus", "contrôle" } : Array.Empty<string>(), calls);
    }

    [Fact]
    public void RecalculDeCible_PrecedeEmission()
    {
        var calls = new List<string>();
        var service = new TextInsertionService(_ => { calls.Add("émission"); return new(2, 2); },
            _ => { calls.Add("focus"); return true; },
            refreshTarget: () => calls.Add("contrôle"));
        Assert.Equal(TextInsertionOutcome.Inserted, service.Insert((IntPtr)42, "α"));
        Assert.Equal(new[] { "focus", "contrôle", "émission" }, calls);
    }

    [Theory]
    [InlineData("HandleClick")]
    [InlineData("InsertSelectedCharacter")]
    public void RechercheEnPause_RefuseAvantToutAccesAuxResultats(string methodName)
    {
        // Objet sans constructeur : pas de HWND ni de résultat. Un accès au-delà de la
        // garde de pause lèverait une exception, avant même une éventuelle API native.
        var search = (CharacterSearch)RuntimeHelpers.GetUninitializedObject(typeof(CharacterSearch));
        search.SetInputPaused(true);
        var method = typeof(CharacterSearch).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var parameters = methodName == "HandleClick" ? new object[] { 100 } : null;
        Assert.Null(Record.Exception(() => method.Invoke(search, parameters)));
    }
}
