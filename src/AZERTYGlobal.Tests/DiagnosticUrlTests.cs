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
            "https://azerty.global/feedback?v=1.3.0&os=Windows%2011%20%2826200%29&src=tray-menu",
            ProductIdentity.DiagnosticUrl("/feedback", "1.3.0", Os, "tray-menu"));
    }

    /// <summary>
    /// ⛔ Le cas qui décide du séparateur : la sollicitation d'avis ouvre déjà
    /// « /feedback?source=app-notification ». Un « ? » de plus rendrait la requête
    /// illisible côté site, et le paramètre existant doit survivre — il est lu depuis la
    /// 1.2.0.
    /// </summary>
    [Fact]
    public void Un_chemin_qui_porte_deja_une_requete_enchaine_avec_une_esperluette()
    {
        var url = ProductIdentity.DiagnosticUrl(
            "/feedback?source=app-notification", "1.3.0", Os, "app-notification");

        Assert.StartsWith("https://azerty.global/feedback?source=app-notification&v=1.3.0", url);
        Assert.Equal(1, url.Split('?').Length - 1);
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
    /// La forme historique de « Signaler un bug » est conservée à l'identique : mêmes trois
    /// paramètres, même ordre, même valeur d'origine. Le correctif étend « Donner mon avis »,
    /// il ne déplace pas ce que le site reçoit déjà.
    /// </summary>
    [Fact]
    public void La_page_de_bug_garde_sa_forme_historique()
    {
        Assert.Equal(
            "https://azerty.global/bug?v=1.3.0&os=Windows%2010%20%2819045%29&src=app",
            ProductIdentity.DiagnosticUrl("/bug", "1.3.0", "Windows 10 (19045)", "app"));
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
