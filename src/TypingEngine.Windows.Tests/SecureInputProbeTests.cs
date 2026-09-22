using Xunit;

namespace TypingEngine.Windows.Tests;

public class SecureInputProbeTests
{
    [Fact]
    public void Requete_et_resultat_portent_la_fenetre_attendue()
    {
        using var probe = new SecureInputProbe(() => true, window => window != (IntPtr)7, timeout: 1000);
        Assert.False(probe.Query((IntPtr)7));
        Assert.True(probe.Query((IntPtr)8));
    }

    [Fact]
    public void Etat_securise_est_recontrole_meme_sans_nouvel_evenement_focus()
    {
        long clock = 100;
        var api = new TypingEngine.Windows.Testing.MockWin32Api
        {
            ScriptedProcessName = "notepad.exe", ScriptedSecureInput = true
        };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, clock: () => clock);
        Assert.False(monitor.IsSnapshotStale);
        clock += 1000;
        Assert.True(monitor.IsSnapshotStale);
        api.ScriptedSecureInput = false;
        monitor.Recompute();
        Assert.False(monitor.IsSecureInput);
        Assert.False(monitor.IsSnapshotStale);
    }

    [Fact]
    public void Resultat_frais_non_securise_autorise_les_fonctions_avancees()
    {
        using var probe = new SecureInputProbe(() => true, _ => false, timeout: 1000);
        Assert.False(probe.Query());
    }

    [Fact]
    public void Echec_de_requete_est_securise()
    {
        using var probe = new SecureInputProbe(() => true, _ => throw new InvalidOperationException(), timeout: 1000);
        Assert.True(probe.Query());
    }

    [Fact]
    public void Worker_bloque_ne_reutilise_pas_le_resultat_non_securise_precedent()
    {
        using var release = new ManualResetEventSlim();
        int queries = 0;
        int workers = 0;
        using var probe = new SecureInputProbe(() => { Interlocked.Increment(ref workers); return true; },
            _ => { if (Interlocked.Increment(ref queries) > 1) release.Wait(); return false; }, timeout: 100);
        try
        {
            Assert.False(probe.Query());
            Assert.True(probe.Query());
            Assert.True(probe.Query());
            Assert.Equal(1, Volatile.Read(ref workers));
        }
        finally { release.Set(); }
    }

    [Fact]
    public void Initialisation_echouee_reste_securisee_puis_peut_repartir()
    {
        int starts = 0;
        using var probe = new SecureInputProbe(() => Interlocked.Increment(ref starts) > 1,
            _ => false, timeout: 100, retryDelay: 0);
        Assert.True(probe.Query());
        Assert.False(probe.Query());
        Assert.Equal(2, starts);
    }
}
