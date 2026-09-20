namespace TypingEngine.Windows.Tests;

/// <summary>
/// AG130-10 — la détection d'un hook décroché en silence par Windows.
///
/// Deux pièces, éprouvées ici : la décision (<c>HookSilenceWatchdog</c>) et le balayage
/// clavier (<c>KeyboardHook.AnyKeyPressed</c>). Le câblage dans <c>TrayApplication</c> —
/// timer de 2 s, appel de <c>ReinstallHook</c>, journal — n'est pas couvert : il tient à
/// une boucle de messages Win32 qu'aucun témoin ne peut faire tourner. Ce que les témoins
/// barrent, c'est la règle qui décide, pas la tuyauterie qui l'exécute.
/// </summary>
public class HookSilenceWatchdogTests
{
    // Une fenêtre de sonde : ouverte à 1 000, refermée plus tard. Un rappel « pendant »
    // porte un instant ≥ 1 000, un rappel « avant » un instant < 1 000.
    private const long DébutFenêtre = 1_000;

    // ═══ La décision ═══════════════════════════════════════════════════════════

    [Fact]
    public void RappelPendantLaFenêtre_LeHookEstVivant()
    {
        var chien = new HookSilenceWatchdog();

        // Une frappe ET un rappel : c'est le fonctionnement normal, rien à signaler,
        // même en répétant. Sans ce témoin, un watchdog qui réinstalle dès qu'il voit
        // une frappe passerait — et réinstallerait le hook toutes les deux secondes.
        for (int i = 0; i < 10; i++)
            Assert.False(chien.Observe(true, DébutFenêtre + 500, DébutFenêtre));

        Assert.Equal(0, chien.ReinstallCount);
    }

    [Fact]
    public void RappelExactementAuDébutDeLaFenêtre_CompteCommePendant()
    {
        var chien = new HookSilenceWatchdog();

        // La borne est inclusive : un rappel arrivé à l'instant même où la sonde
        // précédente a lu l'horloge appartient à la fenêtre qui s'ouvre, pas à celle
        // qui se ferme. Basculer en « > » ici ferait réinstaller sur une égalité.
        Assert.False(chien.Observe(true, DébutFenêtre, DébutFenêtre));
        Assert.Equal(0, chien.SilenceStreak);
    }

    [Fact]
    public void AucuneFrappe_LeSilenceNeProuveRien()
    {
        var chien = new HookSilenceWatchdog();

        // Personne ne tape : le hook peut être mort ou vivant, la sonde n'en sait rien.
        // Réinstaller ici, ce serait revenir au watchdog aveugle — en pire, toutes les 2 s.
        for (int i = 0; i < 50; i++)
            Assert.False(chien.Observe(false, lastCallbackTicks: 0, DébutFenêtre));

        Assert.Equal(0, chien.ReinstallCount);
    }

    [Fact]
    public void UneSeuleSondeMuette_NeSuffitPas()
    {
        var chien = new HookSilenceWatchdog();

        // Une frappe peut tomber juste après la lecture de l'horodatage et juste avant
        // la fermeture de la fenêtre. Une sonde isolée réinstallerait alors un hook
        // parfaitement vivant.
        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        Assert.Equal(1, chien.SilenceStreak);
        Assert.Equal(0, chien.ReinstallCount);
    }

    [Fact]
    public void DeuxSondesMuettesConsécutives_LeHookEstRéinstallé()
    {
        var chien = new HookSilenceWatchdog();

        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        Assert.True(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 2_000));

        Assert.Equal(1, chien.ReinstallCount);
        // La série repart de zéro : sans cela, chaque sonde suivante réinstallerait,
        // sans laisser au hook fraîchement reposé le temps de rendre un rappel.
        Assert.Equal(0, chien.SilenceStreak);
    }

    [Fact]
    public void UnRappelAuMilieuDeLaSérie_LaCasse()
    {
        var chien = new HookSilenceWatchdog();

        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        // Le hook se manifeste : la sonde muette d'avant n'a plus de valeur.
        Assert.False(chien.Observe(true, DébutFenêtre + 2_500, DébutFenêtre + 2_000));
        // Donc celle-ci est la PREMIÈRE d'une nouvelle série, pas la deuxième.
        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 4_000));

        Assert.Equal(0, chien.ReinstallCount);
    }

    [Fact]
    public void UneFenêtreSansFrappeAuMilieu_CasseAussiLaSérie()
    {
        var chien = new HookSilenceWatchdog();

        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        // Plus personne ne tape : on ne peut plus affirmer que le hook est muet à tort.
        Assert.False(chien.Observe(false, lastCallbackTicks: 0, DébutFenêtre + 2_000));
        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 4_000));

        Assert.Equal(0, chien.ReinstallCount);
    }

    [Fact]
    public void Suspend_CasseLaSérieEnCours()
    {
        var chien = new HookSilenceWatchdog();

        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        // L'application passe en pause ou se suspend pour compatibilité : le silence
        // qui suit est voulu. Une série entamée avant ne doit pas se poursuivre au
        // travers et déclencher une réinstallation à la reprise.
        chien.Suspend();
        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 2_000));

        Assert.Equal(0, chien.ReinstallCount);
    }

    [Fact]
    public void SérieRéclamée_LeSeuilEstCeluiQuOnADemandé()
    {
        var immédiat = new HookSilenceWatchdog(consecutiveProbes: 1);
        Assert.True(immédiat.Observe(true, lastCallbackTicks: 0, DébutFenêtre));

        var patient = new HookSilenceWatchdog(consecutiveProbes: 3);
        Assert.False(patient.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        Assert.False(patient.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 2_000));
        Assert.True(patient.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 4_000));
    }

    [Fact]
    public void SeuilDeSérieInvalide_Refusé()
    {
        // Zéro voudrait dire « réinstalle sans même avoir sondé ».
        Assert.Throws<ArgumentOutOfRangeException>(() => new HookSilenceWatchdog(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HookSilenceWatchdog(-1));
    }

    [Fact]
    public void SeuilParDéfaut_TientEnDeuxSondes()
    {
        // Le défaut est une décision produit : 2 sondes de 2 s, soit une détection en
        // ~4 s au lieu des 60 s du watchdog aveugle. La changer se voit ici.
        Assert.Equal(2, HookSilenceWatchdog.DefaultConsecutiveProbes);

        var chien = new HookSilenceWatchdog();
        Assert.False(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre));
        Assert.True(chien.Observe(true, lastCallbackTicks: 0, DébutFenêtre + 2_000));
    }

    // ═══ Le balayage clavier ═══════════════════════════════════════════════════

    [Fact]
    public void AucuneToucheDepuisLeDernierBalayage_RienÀSignaler()
    {
        Assert.False(KeyboardHook.AnyKeyPressed(_ => 0));
    }

    [Fact]
    public void UneToucheDeLaPlage_EstVue()
    {
        // 0x41 = A. Le bit de poids faible est celui de « pressée depuis le dernier appel ».
        Assert.True(KeyboardHook.AnyKeyPressed(vk => vk == 0x41 ? (short)0x0001 : (short)0));
        // Les deux bornes de la plage comptent autant que le milieu.
        Assert.True(KeyboardHook.AnyKeyPressed(vk => vk == 0x08 ? (short)0x0001 : (short)0));
        Assert.True(KeyboardHook.AnyKeyPressed(vk => vk == 0xFE ? (short)0x0001 : (short)0));
    }

    [Fact]
    public void SeulLeBitDePoidsFaibleCompte()
    {
        // 0x8000 = « actuellement enfoncée ». Une touche maintenue depuis avant la
        // fenêtre n'est pas une frappe DE cette fenêtre : la prendre pour telle ferait
        // réinstaller le hook pendant qu'on tient Maj enfoncée.
        Assert.False(KeyboardHook.AnyKeyPressed(_ => unchecked((short)0x8000)));
    }

    [Fact]
    public void LesBoutonsDeSouris_SontHorsPlage()
    {
        // 0x01 à 0x06 : boutons de souris. Une souris ne prouve rien sur un hook
        // clavier — les compter rendrait la sonde bavarde pendant qu'on clique sans taper.
        for (int vk = 0x01; vk <= 0x07; vk++)
        {
            int cible = vk;
            Assert.False(KeyboardHook.AnyKeyPressed(v => v == cible ? (short)0x0001 : (short)0));
        }
    }

    [Fact]
    public void LeBalayageNeSArrêtePasÀLaPremièreTouche()
    {
        // ⛔ Pas de sortie anticipée : lire GetAsyncKeyState CONSOMME le bit. Sortir dès
        // la première touche vue laisserait les bits des touches suivantes en place ;
        // la sonde d'après les lirait et croirait à une frappe dans une fenêtre où
        // personne n'a tapé — un faux positif à chaque rafale de frappe.
        var lues = new List<int>();
        KeyboardHook.AnyKeyPressed(vk =>
        {
            lues.Add(vk);
            return (short)0x0001; // toutes pressées : le pire cas pour une sortie anticipée
        });

        Assert.Equal(0x08, lues[0]);
        Assert.Equal(0xFE, lues[^1]);
        Assert.Equal(0xFE - 0x08 + 1, lues.Count);
        // Aucune touche lue deux fois, aucune sautée.
        Assert.Equal(lues.Count, lues.Distinct().Count());
    }
}
