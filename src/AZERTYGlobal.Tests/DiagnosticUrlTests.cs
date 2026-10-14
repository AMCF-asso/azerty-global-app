using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin R16 de la revue de code du 2026-09-21.
///
/// « Signaler un bug » emportait la version du binaire, la version de Windows et l'origine
/// du clic ; les quatre entrées « Donner mon avis » ouvraient un « /feedback » nu. Un retour
/// sans version ne se rattache à aucun binaire : sur une application dont la 1.1.0, la 1.2.0
/// et la 1.3.0 coexistent dans le parc, c'est la première question qu'on se pose en lisant
/// le message.
///
/// ⚠️ Ces témoins tiennent la construction de l'URL, pas les cinq points d'appel :
/// <c>ShellExecuteW</c> est hors de portée de la suite. Ce qui les protège est qu'il n'y a
/// plus qu'un seul corps à appeler — c'est la raison d'être du correctif.
/// </summary>
public class DiagnosticUrlTests
{
    private const string Os = "Windows 11 (26200)";

    [Fact]
    public void Un_chemin_nu_recoit_la_version_l_os_et_l_origine()
    {
        Assert.Equal(
            "https://azerty.global/guide?v=1.3.0&os=Windows%2011%20%2826200%29&src=tray-menu",
            ProductIdentity.DiagnosticUrl("/guide", "1.3.0", Os, "tray-menu"));
    }

    /// <summary>
    /// ⛔ Le cas qui décide du séparateur : les pages de retour portent déjà
    /// « ?source=… » (« /contact?source=app-notification »). Un « ? » de plus rendrait la
    /// requête illisible côté site, et le paramètre existant doit survivre — il est lu
    /// depuis la 1.2.0.
    /// </summary>
    [Fact]
    public void Un_chemin_qui_porte_deja_une_requete_enchaine_avec_une_esperluette()
    {
        var url = ProductIdentity.DiagnosticUrl(
            ProductIdentity.ReviewPromptPath, "1.3.0", Os, "app-notification");

        Assert.StartsWith("https://azerty.global/contact?source=app-notification&v=1.3.0", url);
        Assert.Equal(1, url.Split('?').Length - 1);
    }

    /// <summary>
    /// Bascule du site v2 : aucune page citée par l'app ne passe par une redirection
    /// (runbook de bascule, point 7). /feedback, /bug et l'ancien PDF sont des 301 du site
    /// v2 ; /guide n'a pas d'ancre « cartes ». Les retours vont à /contact, avec une
    /// provenance de sa liste blanche.
    /// </summary>
    [Fact]
    public void Les_pages_citees_par_l_app_sont_les_cibles_du_site_v2()
    {
        Assert.Equal("/contact?source=feedback", ProductIdentity.FeedbackPath);
        Assert.Equal("/contact?source=bug", ProductIdentity.BugReportPath);
        Assert.Equal("/contact?source=app-notification", ProductIdentity.ReviewPromptPath);
        Assert.Equal("/assets/aide-memoire-azerty-global.pdf", ProductIdentity.CheatSheetPdfPath);
        Assert.Equal("/guide#aide-memoire", ProductIdentity.KeyboardCardsPath);
    }

    /// <summary>
    /// Les trois valeurs sont échappées. Sans cela, une version ou un OS contenant « & »
    /// fabriquerait un paramètre de plus — et l'origine est la seule des trois que le code
    /// écrit à la main, donc la seule qui puisse dériver.
    /// </summary>
    [Fact]
    public void Les_valeurs_sont_echappees()
    {
        var url = ProductIdentity.DiagnosticUrl("/bug", "1.3.0&x=1", "Windows 11", "a b&c");

        Assert.Contains("v=1.3.0%26x%3D1", url);
        Assert.Contains("src=a%20b%26c", url);
        Assert.DoesNotContain("&x=1", url);
    }

    /// <summary>
    /// « Signaler un bug » garde ses trois marqueurs, dans le même ordre et avec la même
    /// valeur d'origine ; seule la page change (/contact, provenance « bug », bascule v2).
    /// </summary>
    [Fact]
    public void La_page_de_bug_garde_ses_trois_marqueurs()
    {
        Assert.Equal(
            "https://azerty.global/contact?source=bug&v=1.3.0&os=Windows%2010%20%2819045%29&src=app",
            ProductIdentity.DiagnosticUrl(ProductIdentity.BugReportPath, "1.3.0", "Windows 10 (19045)", "app"));
    }

    /// <summary>
    /// La frontière 10/11 est le build 22000, et c'est le build — pas le nom — qui distingue
    /// vraiment deux postes. Le seuil est vérifié des deux côtés : sans la réciproque, un
    /// <c>OsDescription</c> qui répondrait « Windows 11 » en toutes circonstances passerait.
    /// </summary>
    [Fact]
    public void La_description_de_l_os_porte_le_numero_de_build()
    {
        var description = ProductIdentity.OsDescription();

        Assert.Matches(@"^Windows 1[01] \(\d+\)$", description);
        Assert.Equal(Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10",
            description.Split(' ')[0] + " " + description.Split(' ')[1]);
    }
}
