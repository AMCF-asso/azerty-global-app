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

    // Horloge injectée : le délai d'abandon d'un worker bloqué se pilote sans attente
    // réelle. Seule l'attente de la requête elle-même (timeout) reste en temps réel, et
    // seulement quand on attend justement l'expiration d'un worker bloqué.
    private sealed class Horloge
    {
        private long _now = 1_000_000;
        public long Now => Volatile.Read(ref _now);
        public void Avance(long ms) => Interlocked.Add(ref _now, ms);
    }

    // Délégué de requête : la fenêtre 1 reste bloquée dans « COM » jusqu'à release et
    // rend alors `retourFenetre1` ; toute autre fenêtre répond vite, non sécurisée.
    // `entree` est libéré à chaque entrée dans la fenêtre 1 : le test attend que le
    // worker soit réellement occupé avant de faire avancer l'horloge.
    private static Func<IntPtr, bool> FenetreUnBloquee(
        ManualResetEventSlim release, SemaphoreSlim entree, bool retourFenetre1)
        => window =>
        {
            if (window != (IntPtr)1) return false;
            entree.Release();
            release.Wait();
            return retourFenetre1;
        };

    // Relance une requête rapide jusqu'au verdict frais : le thread du nouveau worker
    // peut démarrer après l'expiration de la requête ; le nombre d'essais est borné,
    // pas la durée.
    private static bool VerdictRapide(SecureInputProbe probe, IntPtr window)
    {
        bool securise = true;
        for (int essai = 0; essai < 100 && securise; essai++)
            securise = probe.Query(window);
        return securise;
    }

    [Fact]
    public void Worker_bloque_n_est_ni_multiplie_ni_abandonne_avant_le_delai()
    {
        var horloge = new Horloge();
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        int workers = 0;
        using var probe = new SecureInputProbe(() => { Interlocked.Increment(ref workers); return true; },
            FenetreUnBloquee(release, entree, false), timeout: 100, retryDelay: 0,
            abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            Assert.True(probe.Query((IntPtr)1));
            Assert.True(entree.Wait(10_000));
            horloge.Avance(1999);
            // Fail-closed pendant le blocage, tant que le délai n'est pas écoulé.
            Assert.True(probe.Query((IntPtr)2));
            Assert.True(probe.Query((IntPtr)2));
            Assert.Equal(1, Volatile.Read(ref workers));
            Assert.Equal(0, probe.AbandonedWorkers);
        }
        finally { release.Set(); }
    }

    [Fact]
    public void Worker_bloque_est_abandonne_apres_le_delai_et_une_nouvelle_detection_repart()
    {
        var horloge = new Horloge();
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        int workers = 0;
        using var probe = new SecureInputProbe(() => { Interlocked.Increment(ref workers); return true; },
            FenetreUnBloquee(release, entree, false), timeout: 100, retryDelay: 0,
            abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            Assert.True(probe.Query((IntPtr)1));
            Assert.True(entree.Wait(10_000));
            horloge.Avance(2000);

            Assert.False(VerdictRapide(probe, (IntPtr)2));
            Assert.Equal(2, Volatile.Read(ref workers));
            Assert.Equal(1, probe.AbandonedWorkers);
            // La nouvelle génération sert les requêtes suivantes sans nouvel abandon.
            Assert.False(VerdictRapide(probe, (IntPtr)3));
            Assert.Equal(2, Volatile.Read(ref workers));
        }
        finally { release.Set(); }
    }

    [Fact]
    public void Retour_tardif_d_un_worker_abandonne_n_ecrase_pas_la_detection_recente()
    {
        var horloge = new Horloge();
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        // Le worker abandonné rend « sécurisé » en retard, à l'inverse du verdict récent.
        using var probe = new SecureInputProbe(() => true,
            FenetreUnBloquee(release, entree, true), timeout: 100, retryDelay: 0,
            abandonAfter: 2000, clock: () => horloge.Now);
        int levees = 0;
        probe.ResultChangedLate += () => Interlocked.Increment(ref levees);
        try
        {
            Assert.True(probe.Query((IntPtr)1));
            Assert.True(entree.Wait(10_000));
            horloge.Avance(2000);
            Assert.False(VerdictRapide(probe, (IntPtr)2));
            int idRecent = probe.LastResultRequestId;
            int leveesAvant = Volatile.Read(ref levees);
            horloge.Avance(5000);   // la requête 1 est largement hors délai : retour « tardif »

            release.Set();
            Assert.True(SpinWait.SpinUntil(() => probe.AbandonedWorkers == 0, 10_000));

            Assert.Equal(idRecent, probe.LastResultRequestId);
            Assert.Equal(leveesAvant, Volatile.Read(ref levees));
            Assert.False(VerdictRapide(probe, (IntPtr)2));
        }
        finally { release.Set(); }
    }

    [Fact]
    public void Workers_abandonnes_sont_bornes_puis_la_sonde_reste_securisee()
    {
        var horloge = new Horloge();
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        int workers = 0;
        // Toute fenêtre reste bloquée : on ne doit jamais accumuler de fils sans borne.
        using var probe = new SecureInputProbe(() => { Interlocked.Increment(ref workers); return true; },
            _ => { entree.Release(); release.Wait(); return false; }, timeout: 50, retryDelay: 0,
            abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            for (int i = 1; i <= SecureInputProbe.MaxAbandonedWorkers + 1; i++)
            {
                Assert.True(probe.Query((IntPtr)i));
                Assert.True(entree.Wait(10_000));   // le worker courant est dans sa requête
                horloge.Avance(2000);
            }
            // Plafond atteint : les requêtes suivantes ne lancent plus aucun fil.
            Assert.True(probe.Query((IntPtr)50));
            horloge.Avance(2000);
            Assert.True(probe.Query((IntPtr)51));
            // 3 workers abandonnés, 1 courant : jamais plus de 4 fils.
            Assert.Equal(SecureInputProbe.MaxAbandonedWorkers, probe.AbandonedWorkers);
            Assert.Equal(SecureInputProbe.MaxAbandonedWorkers + 1, Volatile.Read(ref workers));
            Assert.True(probe.Query((IntPtr)99));
        }
        finally { release.Set(); }
    }

    [Fact]
    public void Apres_le_retour_des_workers_abandonnes_la_sonde_repart_normalement()
    {
        var horloge = new Horloge();
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        using var probe = new SecureInputProbe(() => true,
            FenetreUnBloquee(release, entree, false), timeout: 100, retryDelay: 0,
            abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            Assert.True(probe.Query((IntPtr)1));
            Assert.True(entree.Wait(10_000));
            horloge.Avance(2000);
            Assert.False(VerdictRapide(probe, (IntPtr)2));
            release.Set();
            Assert.True(SpinWait.SpinUntil(() => probe.AbandonedWorkers == 0, 10_000));
            Assert.False(VerdictRapide(probe, (IntPtr)3));
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
