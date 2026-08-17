// Identité du produit — v1.2.0.
//
// Tranche A de docs/keyboard-platform.md, moitié non localisée. Rassemble ce qui *nomme*
// AZERTY Global partout ailleurs que dans une phrase traduite : nom affiché, forme
// identifiant, URL du site, identifiant Store, dossier de configuration, ressources
// embarquées, noms de classes fenêtre.
//
// Ce qui reste dehors, volontairement : les ~65 occurrences du nom enchâssées dans les
// chaînes de Localization/, qui doivent devenir des interpolations. Elles réécrivent du
// texte visible en français et en anglais, et ne se touchent pas la veille d'une
// soumission Store.
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
    public const string SiteBaseUrl = "https://azerty.global";
    public const string DiscordInviteUrl = "https://discord.gg/nYknqshJz3";
    public const string RepositoryUrl = "https://github.com/AZERTYGlobal/app";
    public const string LogoResourceName = "favicon-azerty-global.png";

    /// <summary>Volet d'avis du Store. Concaténation de constantes, donc utilisable là où
    /// une expression constante est exigée.</summary>
    public const string StoreReviewUrl =
        "ms-windows-store://review/?ProductId=" + StoreProductId;

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
}
