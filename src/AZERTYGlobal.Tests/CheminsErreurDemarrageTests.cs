using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des chemins d'erreur, rapport-B : démarrage (mutex d'instance unique). Les noms de mutex
/// sont uniques par test (GUID) et dans l'espace Local\ : le vrai nom de mutex d'une application
/// AZERTY Global qui tournerait sur la machine n'est jamais occupé.
/// </summary>
public sealed class CheminsErreurDemarrageTests
{
    private static (Mutex Mutex, bool IsNew) CreerVraiMutex(string nom)
    {
        var mutex = new Mutex(true, nom, out bool isNew);
        return (mutex, isNew);
    }

    // ── B1 (K66) ─────────────────────────────────────────────────────────

    // rapport-B B1, K66
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void K66_NomGlobalPrisParUnAutreTypeDObjet_NeFaitPasPlanterLeLancement()
    {
        string suffixe = Guid.NewGuid().ToString("N");
        string local = "Local\\azg-audit-b-local-" + suffixe;
        // Local\ ici pour ne pas dépendre du privilège SeCreateGlobalPrivilege : même type d'exception.
        string global = "Local\\azg-audit-b-global-" + suffixe;
        using var occupant = new EventWaitHandle(false, EventResetMode.ManualReset, global);
        Mutex? l = null, g = null;
        try
        {
            var erreur = Record.Exception(() =>
                Program.AcquireSingleInstance(local, global, CreerVraiMutex, (_, _) => { }, out l, out g));

            Assert.Null(erreur); // WaitHandleCannotBeOpenedException attendue aujourd'hui
        }
        finally
        {
            l?.Dispose();
            g?.Dispose();
        }
    }

    // ── B1 (K65) ─────────────────────────────────────────────────────────

    // rapport-B B1, K65
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void K65_NomLocalPrisParUnAutreTypeDObjet_NeFaitPasPlanterLeLancement()
    {
        string suffixe = Guid.NewGuid().ToString("N");
        string local = "Local\\azg-audit-b-local2-" + suffixe;
        string global = "Local\\azg-audit-b-global2-" + suffixe;
        using var occupant = new EventWaitHandle(false, EventResetMode.ManualReset, local);
        Mutex? l = null, g = null;
        try
        {
            var erreur = Record.Exception(() =>
                Program.AcquireSingleInstance(local, global, CreerVraiMutex, (_, _) => { }, out l, out g));

            Assert.Null(erreur); // WaitHandleCannotBeOpenedException attendue aujourd'hui
        }
        finally
        {
            l?.Dispose();
            g?.Dispose();
        }
    }
}
