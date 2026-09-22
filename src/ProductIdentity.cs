// Identité du produit — v1.2.0.
//
// Tranche A de docs/keyboard-platform.md, complète depuis le 2026-08-17. Rassemble tout ce
// qui *nomme* AZERTY Global : nom affiché, forme identifiant, domaine et URL du site,
// identifiant Store, dossier de configuration, ressources embarquées, noms de classes
// fenêtre.
//
// Les phrases traduites de Localization/ n'écrivent plus le nom en dur : ses 86 occurrences
// y passent par l'alias privé L.Product, et les 4 sites qui portaient le domaine par
// SiteDomain et Url(). Conversion vérifiée en comparant les 700 chaînes rendues (fr et en)
// avant et après — identiques.
//
// Deux formes, jamais confondues :
//   - DisplayName « AZERTY Global », ce que l'utilisateur lit ;
//   - Namespace « AZERTYGlobal », l'identifiant — RootNamespace du csproj, Identity Name
//     du MSIX (AZERTYGlobal.AZERTYGlobal), Application Id, préfixe du TaskId de démarrage.
// Renommer l'un ne renomme pas l'autre.

namespace AZERTYGlobal;

static class ProductIdentity
{
    /// <summary>Nom lu par l'utilisateur : titres de fenêtre, infobulle, notifications.</summary>
    public const string DisplayName = "AZERTY Global";

    /// <summary>Forme identifiant, jamais affichée. Doit rester alignée sur
    /// <c>&lt;RootNamespace&gt;</c> du csproj et sur l'<c>Identity</c> du manifeste MSIX.</summary>
    public const string Namespace = "AZERTYGlobal";

    /// <summary>Nom du binaire publié. Sa source est <c>&lt;AssemblyName&gt;</c> du csproj et
    /// non <see cref="DisplayName"/> : les deux coïncident aujourd'hui, ce n'est pas la même
    /// décision.</summary>
    public const string ExecutableName = "AZERTY Global.exe";

    /// <summary>Raccourci du démarrage automatique, hors package uniquement.</summary>
    public const string ShortcutFileName = "AZERTY Global.lnk";

    /// <summary>Dossier sous <c>%LocalAppData%</c>. Littéral distinct de
    /// <see cref="DisplayName"/> à dessein : renommer le produit ne doit pas déplacer la
    /// configuration ni la progression de tout le monde.</summary>
    public const string ConfigFolderName = "AZERTY Global";

    public const string StoreProductId = "9N4BTS43SSSZ";
    /// <summary>Domaine nu, tel qu'il est écrit dans une phrase adressée à
    /// l'utilisateur (« → azerty.global ») et non cliqué.</summary>
    public const string SiteDomain = "azerty.global";
    public const string SiteBaseUrl = "https://" + SiteDomain;
    public const string DiscordInviteUrl = "https://discord.gg/nYknqshJz3";
    public const string RepositoryUrl = "https://github.com/AMCF-asso/azerty-global-app";
    public const string LogoResourceName = "favicon-azerty-global.png";

    /// <summary>Volet d'avis du Store. Concaténation de constantes, donc utilisable là où
    /// une expression constante est exigée.</summary>
    public const string StoreReviewUrl =
        "ms-windows-store://review/?ProductId=" + StoreProductId;

    /// <summary>Package family name de l'identité Store, forme <c>&lt;Name&gt;_&lt;PublisherId&gt;</c>.
    /// PublisherId n'est pas un identifiant attribué mais un condensé déterministe de la chaîne
    /// d'éditeur du paquet : SHA-256 de cette chaîne en UTF-16LE, 8 premiers octets, base32
    /// Crockford sur 13 caractères. Celui-ci est le condensé de
    /// <c>CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38</c>, l'éditeur du manifeste destiné au Store ;
    /// il ne dépend donc pas de la façon dont le paquet installé a été signé.
    ///
    /// Remesure, par deux voies indépendantes qui doivent s'accorder : d'une part
    /// <c>Get-AppxPackage -Name *AZERTY* | Select-Object PackageFamilyName</c> sur un poste où le
    /// paquet est installé, d'autre part le condensé recalculé depuis l'attribut
    /// <c>Publisher</c> de <c>msix/AppxManifest.xml</c>. Mesuré le 2026-08-19 : les deux donnent
    /// cette valeur.
    ///
    /// Sert à reconnaître le canal de distribution, voir <see cref="AppChannel"/>.</summary>
    public const string StorePackageFamilyName = "AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg";

    /// <summary>Mutex d'instance unique ; l'appelant y ajoute le SID de session.</summary>
    public const string SingleInstanceMutexName = Namespace + "SingleInstance";

    /// <summary>
    /// Nom de classe fenêtre Win32. Le séparateur est un détail d'implémentation : les
    /// appelants ne donnent que le suffixe. Enregistrement et désenregistrement passent
    /// désormais par le même appel, ce qui supprime la paire de littéraux que sept fenêtres
    /// dupliquaient entre <c>RegisterClassEx</c> et <c>UnregisterClass</c> — en renommer un
    /// sans l'autre laissait la classe enregistrée.
    /// </summary>
    public static string WindowClass(string suffix) => $"{Namespace}_{suffix}";

    /// <summary>URL du site, chemin compris (« /guide », « /feedback »…).</summary>
    public static string Url(string path) => SiteBaseUrl + path;

    /// <summary>
    /// URL du site enrichie des marqueurs de diagnostic : version du binaire, version de
    /// Windows, origine du clic.
    ///
    /// R16 de la revue du 2026-09-21 : seul « Signaler un bug » les portait. Les quatre
    /// entrées « Donner mon avis » — menu du tray, sollicitation d'avis, fenêtre de
    /// statistiques, accueil — ouvraient un « /feedback » nu, et un retour sans version ne
    /// se rattache à aucun binaire : la 1.1.0, la 1.2.0 et la 1.3.0 arrivaient indistinctes.
    /// Un seul corps désormais, pour que l'oubli ne puisse plus porter sur une branche.
    ///
    /// Le chemin peut déjà porter une requête (« ?source=… ») : le séparateur suit.
    /// </summary>
    public static string DiagnosticUrl(string path, string version, string osVersion, string source) =>
        Url(path) + (path.Contains('?') ? '&' : '?')
        + "v=" + Uri.EscapeDataString(version)
        + "&os=" + Uri.EscapeDataString(osVersion)
        + "&src=" + Uri.EscapeDataString(source);

    /// <summary>
    /// Version de Windows telle qu'elle part dans un retour : « Windows 11 (26200) ».
    /// Le numéro de build est ce qui distingue vraiment deux postes ; 22000 est la
    /// frontière 10/11. Ici plutôt qu'au point d'appel pour que les cinq liens de retour
    /// écrivent la même chose.
    /// </summary>
    public static string OsDescription()
    {
        var os = Environment.OSVersion;
        return $"Windows {(os.Version.Build >= 22000 ? "11" : "10")} ({os.Version.Build})";
    }
}
