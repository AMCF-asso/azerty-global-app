using AZERTYGlobal;
using Xunit;

using Annonce = AZERTYGlobal.TrayApplication.CompatibilityAnnouncement;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin R15 de la revue de code du 2026-09-21.
///
/// Le sélecteur de tâches appartient à <c>explorer.exe</c> et se résout en
/// <c>Default</c> : un Alt+Tab entre deux applications suspendues passait donc par une
/// sortie de suspension puis une entrée, soit « à nouveau actif » suivi de la bulle de
/// sécurité — deux bulles par bascule, dont une qui ignore le réglage des notifications.
/// Un aller-retour par la barre des tâches, sans changer d'application du tout, en
/// produisait autant.
///
/// Ces témoins jouent des séquences d'événements de premier plan sur la fonction de
/// production et comptent les bulles. La seule chose que le test fait à la place du
/// produit est d'armer <c>ResumeOwed</c>, ce que la branche <c>Leave</c> de
/// <c>OnForegroundChanged</c> fait sur le vrai chemin.
///
/// ⚠️ Ce que ces témoins ne tiennent pas : que <c>IsTransientShellForeground</c> vaille bien
/// vrai sur les cinq surfaces du shell. C'est <c>ApplicationChangeDeadKeyTests</c> qui le
/// tient, côté moteur.
/// </summary>
public class ShellHopBalloonTests
{
    private static readonly ForegroundProcessIdentity JeuA = new(1111, 11);
    private static readonly ForegroundProcessIdentity JeuB = new(2222, 22);

    /// <summary>Rejoue la suite d'événements et retient les bulles réellement émises.</summary>
    private sealed class Journal
    {
        private TrayApplication.CompatibilityAnnouncementState _etat;
        public readonly List<Annonce> Bulles = new();

        /// <summary>Ce que fait la branche <c>Leave</c> : la reprise est due.</summary>
        public Journal Reprise() { _etat.ResumeOwed = true; return this; }

        public Journal Shell()
        {
            Evenement(true, CompatibilitySuspendReason.None, default);
            return this;
        }

        public Journal Application(CompatibilitySuspendReason motif, ForegroundProcessIdentity app)
        {
            Evenement(false, motif, motif == CompatibilitySuspendReason.None ? default : app);
            return this;
        }

        private void Evenement(bool shell, CompatibilitySuspendReason motif, ForegroundProcessIdentity app)
        {
            var annonce = TrayApplication.NextAnnouncement(ref _etat, shell, motif, app);
            if (annonce != Annonce.None) Bulles.Add(annonce);
        }
    }

    /// <summary>
    /// ⛔ Le constat, joué tel quel : Alt+Tab d'un jeu à un autre, les deux suspendus.
    /// Une bulle par jeu réellement atteint, aucune pour le passage par le sélecteur.
    /// </summary>
    [Fact]
    public void Un_alt_tab_entre_deux_jeux_n_annonce_que_les_jeux()
    {
        var journal = new Journal()
            .Application(CompatibilitySuspendReason.AntiCheat, JeuA)
            .Reprise().Shell()
            .Application(CompatibilitySuspendReason.AntiCheat, JeuB);

        Assert.Equal(new[] { Annonce.Suspension, Annonce.Suspension }, journal.Bulles);
    }

    /// <summary>
    /// Le cas le plus banal, et le pire avant le correctif : cliquer la barre des tâches
    /// depuis un jeu suspendu, puis y revenir. L'utilisateur n'a rien changé, il ne doit
    /// rien apprendre de neuf.
    /// </summary>
    [Fact]
    public void Un_aller_retour_par_la_barre_des_taches_n_annonce_rien_de_plus()
    {
        var journal = new Journal()
            .Application(CompatibilitySuspendReason.AntiCheat, JeuA)
            .Reprise().Shell()
            .Application(CompatibilitySuspendReason.AntiCheat, JeuA);

        Assert.Equal(new[] { Annonce.Suspension }, journal.Bulles);
    }

    /// <summary>
    /// ⛔ La réciproque qui empêche le correctif de se transformer en mutisme : sortir
    /// vraiment d'un jeu vers une application ordinaire doit annoncer la reprise, même si
    /// le chemin passe par le shell. Sans elle, les deux témoins ci-dessus passeraient
    /// aussi bien si plus aucune bulle ne sortait jamais.
    /// </summary>
    [Fact]
    public void Sortir_d_un_jeu_vers_une_application_ordinaire_annonce_la_reprise()
    {
        var journal = new Journal()
            .Application(CompatibilitySuspendReason.AntiCheat, JeuA)
            .Reprise().Shell()
            .Application(CompatibilitySuspendReason.None, default);

        Assert.Equal(new[] { Annonce.Suspension, Annonce.Resumed }, journal.Bulles);
    }

    /// <summary>
    /// Sans suspension préalable, rien n'est dû : un utilisateur qui n'a jamais été suspendu
    /// ne reçoit pas de « à nouveau actif ».
    /// </summary>
    [Fact]
    public void Sans_suspension_prealable_aucune_reprise_n_est_annoncee()
    {
        var journal = new Journal()
            .Application(CompatibilitySuspendReason.None, default)
            .Shell()
            .Application(CompatibilitySuspendReason.None, default);

        Assert.Empty(journal.Bulles);
    }

    /// <summary>
    /// Un changement de motif dans la même application reste une information neuve —
    /// c'est la transition que le correctif AG130-06 a ajoutée le 2026-09-21.
    /// </summary>
    [Fact]
    public void Un_changement_de_motif_dans_la_meme_application_s_annonce()
    {
        var journal = new Journal()
            .Application(CompatibilitySuspendReason.UserOverride, JeuA)
            .Application(CompatibilitySuspendReason.AntiCheat, JeuA);

        Assert.Equal(new[] { Annonce.Suspension, Annonce.Suspension }, journal.Bulles);
    }

    /// <summary>
    /// Une surface du shell n'annonce rien, même si elle arrivait avec un motif : le
    /// premier plan du moment ne dure pas, et le prochain événement dira la vérité.
    /// </summary>
    [Fact]
    public void Une_surface_du_shell_n_annonce_jamais_rien()
    {
        var etat = default(TrayApplication.CompatibilityAnnouncementState);

        var annonce = TrayApplication.NextAnnouncement(
            ref etat, transientShellForeground: true,
            CompatibilitySuspendReason.AntiCheat, JeuA);

        Assert.Equal(Annonce.None, annonce);
        Assert.Equal(CompatibilitySuspendReason.None, etat.AnnouncedReason);
    }

    /// <summary>
    /// ⛔ Le témoin anti-mutisme le plus simple : la toute première suspension part
    /// toujours. C'est la bulle de sécurité, celle qui ignore le réglage des notifications.
    /// </summary>
    [Fact]
    public void La_premiere_suspension_part_toujours()
    {
        var journal = new Journal().Application(CompatibilitySuspendReason.AntiCheat, JeuA);

        Assert.Equal(new[] { Annonce.Suspension }, journal.Bulles);
    }
}
