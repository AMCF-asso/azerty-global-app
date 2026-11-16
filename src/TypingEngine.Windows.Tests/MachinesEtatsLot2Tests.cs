namespace TypingEngine.Windows.Tests;

/// <summary>
/// Tests de régression du lot 2 de l'analyse des machines à états (constat C2.2).
/// </summary>
public class MachinesEtatsLot2Tests
{
    // Constat C2.2
    // Modèle suivi : SecureInputProbeTests (même couture : délégué _query injecté,
    // ManualResetEventSlim). Adapté au correctif : le délai d'abandon (2 s) est piloté par
    // une horloge injectée au lieu d'une boucle de 3 s en temps réel.

    /// <summary>
    /// Constat C2.2 — Un ouvrier UIA bloqué sur la fenêtre 1 rendait « sécurisé » le
    /// premier plan de la fenêtre 2, indéfiniment, alors que la fenêtre 2 répondrait vite.
    /// Invariant : un ouvrier bloqué est abandonné au-delà d'un délai borné ; la requête
    /// d'une autre fenêtre finit par rendre son vrai résultat (non sécurisé).
    /// </summary>
    [Fact]
    public void Ouvrier_bloque_sur_une_fenetre_ne_garde_pas_une_autre_fenetre_securisee_indefiniment()
    {
        long maintenant = 1_000_000;
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        using var probe = new SecureInputProbe(
            () => true,
            window =>
            {
                if (window == (IntPtr)1)
                {
                    entree.Release();
                    release.Wait();   // l'application figée : COM ne revient pas
                }
                return false;         // toute autre fenêtre répond vite : non sécurisée
            },
            timeout: 100, retryDelay: 0, abandonAfter: 2000,
            clock: () => Volatile.Read(ref maintenant));
        try
        {
            // Fail-closed voulu pendant le blocage lui-même.
            Assert.True(probe.Query((IntPtr)1));
            Assert.True(entree.Wait(10_000));

            // Avant le délai d'abandon : toujours sécurisé (on ne sait pas).
            Interlocked.Add(ref maintenant, 1999);
            Assert.True(probe.Query((IntPtr)2));

            // Délai écoulé : la sonde rend le vrai verdict de la fenêtre 2. Le nombre
            // d'essais est borné (le nouveau fil peut démarrer après l'expiration d'une requête).
            Interlocked.Add(ref maintenant, 1);
            bool securise = true;
            for (int essai = 0; essai < 100 && securise; essai++)
                securise = probe.Query((IntPtr)2);

            Assert.False(securise,
                "La fenêtre 2 est restée sécurisée malgré le délai d'abandon de l'ouvrier bloqué sur la fenêtre 1.");
        }
        finally { release.Set(); }
    }

    // Constat C2.2 (suite ME-12 : l'instance IUIAutomation de chaque ouvrier)
    // _automation est propre à chaque fil ([ThreadStatic]) : seul le fil qui l'a créée peut la
    // relâcher, et il doit le faire à toute sortie, sans quoi l'instance de chaque ouvrier abandonné
    // (ou arrêté) est perdue. La couture workerExit compte ces sorties et le fil qui les fait.

    /// <summary>Un ouvrier qui sort normalement (arrêt de la sonde) relâche son instance.</summary>
    [Fact]
    public void Ouvrier_arrete_libere_son_instance_sur_son_propre_fil()
    {
        int sorties = 0;
        int filRequete = 0, filSortie = 0;
        var probe = new SecureInputProbe(() => true,
            _ => { Volatile.Write(ref filRequete, Environment.CurrentManagedThreadId); return false; },
            timeout: 1000, retryDelay: 0,
            workerExit: () => { Volatile.Write(ref filSortie, Environment.CurrentManagedThreadId); Interlocked.Increment(ref sorties); });

        Assert.False(probe.Query((IntPtr)1));
        probe.Dispose();

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref sorties) == 1, 10_000),
            "l'ouvrier arrêté n'a pas relâché son instance");
        Assert.Equal(Volatile.Read(ref filRequete), Volatile.Read(ref filSortie));
    }

    /// <summary>Un ouvrier abandonné qui revient de sa requête relâche son instance, sans toucher
    /// à celle de son remplaçant, toujours en service.</summary>
    [Fact]
    public void Ouvrier_abandonne_qui_revient_libere_son_instance_et_pas_celle_du_remplacant()
    {
        long maintenant = 1_000_000;
        using var release = new ManualResetEventSlim();
        using var entree = new SemaphoreSlim(0);
        int sorties = 0;
        int filAbandonne = 0, filSortie = 0;
        using var probe = new SecureInputProbe(
            () => true,
            window =>
            {
                if (window != (IntPtr)1) return false;
                Volatile.Write(ref filAbandonne, Environment.CurrentManagedThreadId);
                entree.Release();
                release.Wait();
                return false;
            },
            timeout: 100, retryDelay: 0, abandonAfter: 2000,
            clock: () => Volatile.Read(ref maintenant),
            workerExit: () => { Volatile.Write(ref filSortie, Environment.CurrentManagedThreadId); Interlocked.Increment(ref sorties); });
        try
        {
            Assert.True(probe.Query((IntPtr)1));
            Assert.True(entree.Wait(10_000));
            Interlocked.Add(ref maintenant, 2000);
            bool securise = true;
            for (int essai = 0; essai < 100 && securise; essai++)
                securise = probe.Query((IntPtr)2);
            Assert.False(securise, "le remplaçant n'a pas pris le relais");
            Assert.Equal(1, probe.AbandonedWorkers);
            Assert.Equal(0, Volatile.Read(ref sorties));

            release.Set();

            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref sorties) == 1, 10_000),
                "l'ouvrier abandonné n'a pas relâché son instance en revenant");
            Assert.Equal(Volatile.Read(ref filAbandonne), Volatile.Read(ref filSortie));
            Assert.False(probe.Query((IntPtr)3)); // le remplaçant sert toujours
            Assert.Equal(1, Volatile.Read(ref sorties));
        }
        finally { release.Set(); }
    }

    /// <summary>Initialisation en échec ou qui lève : le fil sort aussi, et relâche.</summary>
    [Fact]
    public void Ouvrier_dont_l_initialisation_echoue_ou_leve_libere_aussi()
    {
        foreach (Func<bool> initialisation in new Func<bool>[] { () => false, () => throw new InvalidOperationException() })
        {
            int sorties = 0;
            using var probe = new SecureInputProbe(initialisation, _ => false, timeout: 100, retryDelay: 0,
                workerExit: () => Interlocked.Increment(ref sorties));

            Assert.True(probe.Query((IntPtr)1)); // sécurisé : l'initialisation est indisponible

            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref sorties) == 1, 10_000),
                "le fil sorti sur échec d'initialisation n'a pas relâché son instance");
        }
    }

    /// <summary>Le détecteur branche bien la libération sur la sonde : sans ce branchement, la couture
    /// ne relâcherait rien en production. Test de texte source (COM réel indisponible en test).</summary>
    [Fact]
    public void Le_detecteur_branche_ReleaseAutomation_sur_la_sortie_de_l_ouvrier()
    {
        string source = File.ReadAllText(SourceDuMoteur("SecureInputDetector.cs"));
        int debut = source.IndexOf("SecureInputProbe Probe = new(", StringComparison.Ordinal);
        Assert.True(debut > 0, "construction de la sonde introuvable");
        int fin = source.IndexOf(");", debut, StringComparison.Ordinal);
        Assert.True(fin > debut);
        Assert.Contains("workerExit: ReleaseAutomation", source[debut..fin]);
    }

    private static string SourceDuMoteur(string fichier)
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier != null; dossier = dossier.Parent)
        {
            string chemin = Path.Combine(dossier.FullName, "src", "TypingEngine.Windows", fichier);
            if (File.Exists(chemin)) return chemin;
        }
        throw new FileNotFoundException(fichier);
    }
}
