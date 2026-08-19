// Couche de politiques d'entreprise — v1.2.0, lot C du plan de déployabilité en parc.
//
// Une app MSIX déclarée runFullTrust lit nativement le registre de stratégie : « At run time,
// your app will have the same view of the Group Policy registry as it would if it had been
// installed using a different method »
// (https://learn.microsoft.com/en-us/windows/msix/group-policy-msix). Rien de spécifique au
// packaging n'est donc nécessaire ici : c'est une lecture de registre ordinaire.
//
// Racine : HKEY_LOCAL_MACHINE\SOFTWARE\Policies\ + ProductIdentity.Namespace, construite dans
// le code. L'identité du produit ne s'écrit jamais en dur ailleurs que dans ProductIdentity —
// la CI refuse tout littéral qui la nomme.
//
// Précédence, la même pour les cinq valeurs : politique HKLM > réglage utilisateur de
// config.json > défaut du canal. Deux écarts assumés, tous deux décidés par Antoine le
// 2026-08-19 :
//   - ShowOnboarding à 1 autorise sans imposer, seul 0 contraint : verrouiller la case à
//     « cochée » ferait revenir la fenêtre de bienvenue à chaque démarrage, y compris pour
//     l'utilisateur qui l'a déjà vue ;
//   - une valeur Language invalide est ignorée et tracée, la politique ne s'appliquant pas
//     plutôt que de verrouiller la langue sur une valeur que personne n'a demandée.
//
// Les valeurs sont lues une fois, au premier accès — qui a lieu au démarrage, Program.Main
// lisant la langue avant toute fenêtre — puis mises en cache. Changer une politique exige donc
// un redémarrage de l'application : c'est le comportement attendu d'une stratégie de groupe,
// et l'ADMX du lot F le documentera.
//
// La lecture passe par RegGetValueW plutôt que par Microsoft.Win32.Registry : toute
// l'application parle déjà à Windows en P/Invoke, la fonction lit une valeur sans ouvrir ni
// fermer de clé, et le binaire AOT n'embarque pas une dépendance de plus pour cinq lectures.

using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Lit une valeur de politique. Rend <c>null</c> quand la valeur est absente, d'un type
/// inattendu ou illisible ; un <see cref="int"/> pour un REG_DWORD ; une <see cref="string"/>
/// pour un REG_SZ.
///
/// C'est le seul point par lequel <see cref="PolicyManager"/> touche le registre, et il est
/// injectable : les tests éprouvent les trois états d'une clé — 0, 1, absente — et lisent le
/// chemin littéral qui leur est demandé, sans droits administrateur ni écriture réelle.
/// </summary>
internal delegate object? PolicyValueReader(string keyPath, string valueName);

/// <summary>
/// Politiques effectivement posées sur ce poste. <c>null</c> signifie « clé absente », ce qui
/// n'est pas la même chose que 0 : sans clé, le réglage utilisateur et le défaut du canal
/// s'appliquent, et l'interface ne grise rien.
/// </summary>
internal sealed record PolicySet(
    bool? Notifications,
    bool? UsageStats,
    bool? ExternalLinks,
    bool? ShowOnboarding,
    string? Language)
{
    /// <summary>Aucune politique posée : l'état de très loin le plus fréquent.</summary>
    internal static readonly PolicySet None = new(null, null, null, null, null);
}

/// <summary>
/// Lit et met en cache les politiques d'entreprise, et porte les règles de précédence qui les
/// combinent au réglage utilisateur et au défaut du canal.
/// </summary>
static class PolicyManager
{
    // ═══════════════════════════════════════════════════════════════
    // Emplacement et noms de valeurs
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Racine des stratégies de groupe, sous HKEY_LOCAL_MACHINE.</summary>
    internal const string PoliciesRoot = @"SOFTWARE\Policies\";

    /// <summary>Clé de ce produit. Concaténation, jamais un littéral : voir l'en-tête.</summary>
    internal static string KeyPath => PoliciesRoot + ProductIdentity.Namespace;

    internal const string ValueNotifications = "NotificationsEnabled";
    internal const string ValueUsageStats = "UsageStatsEnabled";
    internal const string ValueExternalLinks = "ExternalLinksEnabled";
    internal const string ValueShowOnboarding = "ShowOnboarding";

    /// <summary>REG_SZ « fr » ou « en ». Nommée <c>Language</c> et non <c>DefaultLanguage</c>
    /// (décision du 2026-08-19) : la politique impose la langue et verrouille le sélecteur,
    /// or « default » promettrait un défaut que l'utilisateur pourrait changer.</summary>
    internal const string ValueLanguage = "Language";

    // ═══════════════════════════════════════════════════════════════
    // Lecture et cache
    // ═══════════════════════════════════════════════════════════════

    private static readonly object _lock = new();
    private static PolicySet? _cache;
    private static PolicyValueReader _reader = ReadRegistryValue;

    /// <summary>Politiques de cette instance, lues une seule fois.</summary>
    internal static PolicySet Current
    {
        get
        {
            lock (_lock)
            {
                if (_cache is not null)
                    return _cache;

                try
                {
                    _cache = Read(_reader);
                }
                catch (Exception ex)
                {
                    // Registre illisible : aucune politique ne s'applique, l'app se comporte
                    // comme sur un poste non géré. La direction d'échec inverse — tout
                    // éteindre — punirait l'utilisateur ordinaire pour une panne qui ne le
                    // concerne pas.
                    ConfigManager.Log("PolicyManager.Read", ex);
                    _cache = PolicySet.None;
                }

                return _cache;
            }
        }
    }

    /// <summary>Lit les cinq valeurs par le lecteur donné. Sans état : c'est la fonction que
    /// les tests éprouvent, ce qui leur évite de toucher au cache du processus.</summary>
    internal static PolicySet Read(PolicyValueReader reader) => new(
        Notifications: ReadFlag(reader, ValueNotifications),
        UsageStats: ReadFlag(reader, ValueUsageStats),
        ExternalLinks: ReadFlag(reader, ValueExternalLinks),
        ShowOnboarding: ReadFlag(reader, ValueShowOnboarding),
        Language: ReadLanguage(reader));

    private static bool? ReadFlag(PolicyValueReader reader, string valueName) =>
        reader(KeyPath, valueName) switch
        {
            int dword => dword != 0,
            _ => null,
        };

    private static string? ReadLanguage(PolicyValueReader reader)
    {
        if (reader(KeyPath, ValueLanguage) is not string raw)
            return null;

        var lang = raw.Trim().ToLowerInvariant();
        if (lang is "fr" or "en")
            return lang;

        // Valeur invalide : la politique de langue ne s'applique pas, et la trace permet à la
        // DSI de comprendre à distance pourquoi sa clé ne prend pas. Journalisée par
        // LogCompatEvent et non par Log : celui-ci arme la garde de 48 h de la sollicitation
        // d'avis, qu'une clé mal saisie n'a aucune raison de déclencher.
        ConfigManager.LogCompatEvent("PolicyValueIgnored",
            $"{ValueLanguage}: unsupported value, policy not applied");
        return null;
    }

    /// <summary>
    /// Hook de test : force le lecteur de politiques et vide le cache jusqu'au <c>Dispose</c>,
    /// qui restaure les deux — y compris quand le corps du <c>using</c> lève.
    ///
    /// Le cache fait partie de ce qui est restauré : un état statique laissé derrière soi
    /// traverse toute la suite dans un seul processus, et rend un test vert en isolation mais
    /// rouge dans la suite entière.
    /// </summary>
    internal static IDisposable OverrideForTests(PolicyValueReader reader)
    {
        lock (_lock)
        {
            var scope = new OverrideScope(_reader, _cache);
            _reader = reader;
            _cache = null;
            return scope;
        }
    }

    private sealed class OverrideScope : IDisposable
    {
        private readonly PolicyValueReader _previousReader;
        private readonly PolicySet? _previousCache;
        private bool _disposed;

        internal OverrideScope(PolicyValueReader previousReader, PolicySet? previousCache)
        {
            _previousReader = previousReader;
            _previousCache = previousCache;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            lock (_lock)
            {
                _reader = _previousReader;
                _cache = _previousCache;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Précédence — politique > réglage utilisateur > défaut du canal
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Notifications effectives. Le défaut reste celui d'aujourd'hui sur les trois
    /// canaux : la décision D3 n'a éteint que la sollicitation d'avis, et le canal AMCF garde
    /// ses bulles de confirmation (décision du 2026-08-19).</summary>
    internal static bool NotificationsEnabled(bool? policy, bool userSetting) =>
        policy ?? userSetting;

    /// <summary>Collecte de statistiques effective. Aucun réglage utilisateur n'existe pour
    /// l'instant : la politique s'applique directement au défaut du canal, éteint sur le canal
    /// sobre (D5).</summary>
    internal static bool UsageStatsEnabled(bool? policy, DistributionChannel channel) =>
        policy ?? !AppChannel.IsSober(channel);

    /// <summary>Liens externes — « Soutenir le projet », Discord, « Noter sur le Microsoft
    /// Store ». Jamais « Donner mon avis » ni « Signaler un bug », qui restent sur tous les
    /// canaux (D4). Éteints par défaut sur le canal sobre (D3), et la politique permet à une
    /// structure de les éteindre aussi sur le canal Store.</summary>
    internal static bool ExternalLinksEnabled(bool? policy, DistributionChannel channel) =>
        policy ?? !AppChannel.IsSober(channel);

    /// <summary>Fenêtre de bienvenue. La politique à 1 autorise sans imposer : seul 0
    /// contraint (décision du 2026-08-19). L'utilisateur garde donc sa case tant que la DSI
    /// n'a pas explicitement supprimé la fenêtre.</summary>
    internal static bool ShowOnboarding(bool? policy, bool userSetting) =>
        policy == false ? false : userSetting;

    /// <summary>Langue effective : la politique impose, sinon le choix de l'utilisateur.</summary>
    internal static string AppLanguage(string? policy, string userSetting) =>
        policy ?? userSetting;

    // ═══════════════════════════════════════════════════════════════
    // Ce que l'interface doit griser
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Un réglage booléen est géré dès qu'une valeur est posée.</summary>
    internal static bool IsManaged(bool? policy) => policy is not null;

    /// <summary>La case « Fenêtre de bienvenue » n'est grisée que par une politique
    /// contraignante : à 1 l'utilisateur garde la main, la griser mentirait sur ce qu'il peut
    /// faire.</summary>
    internal static bool IsOnboardingManaged(bool? policy) => policy == false;

    /// <summary>Le sélecteur de langue est verrouillé dès qu'une langue valide est
    /// imposée.</summary>
    internal static bool IsLanguageManaged(string? policy) => policy is not null;

    // ═══════════════════════════════════════════════════════════════
    // Raccourcis pour cette instance
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Liens externes de cette instance, politique et canal combinés. Consulté par
    /// trois fenêtres et deux chemins de sollicitation, d'où un raccourci ici plutôt qu'une
    /// expression recopiée.</summary>
    internal static bool ExternalLinksEnabledNow =>
        ExternalLinksEnabled(Current.ExternalLinks, AppChannel.Current);

    /// <summary>La langue de cette instance est-elle imposée par une politique ?</summary>
    internal static bool LanguageIsManagedNow => IsLanguageManaged(Current.Language);

    // ═══════════════════════════════════════════════════════════════
    // Lecture registre — RegGetValueW
    // ═══════════════════════════════════════════════════════════════

    private static readonly IntPtr HKEY_LOCAL_MACHINE = new(unchecked((int)0x80000002));

    private const uint RRF_RT_REG_SZ = 0x00000002;
    private const uint RRF_RT_REG_DWORD = 0x00000010;

    /// <summary>Force la vue 64 bits du registre. Les stratégies de groupe s'écrivent dans la
    /// vue native ; l'expliciter évite qu'un jour un binaire 32 bits lise Wow6432Node et
    /// conclue à l'absence de politique.</summary>
    private const uint RRF_SUBKEY_WOW6464KEY = 0x00010000;

    private const int ERROR_SUCCESS = 0;

    /// <summary>Une valeur de langue tient en deux caractères ; au-delà de cette taille, la
    /// valeur est de toute façon invalide et la lecture rend null.</summary>
    private const int MaxStringChars = 64;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
    private static extern int RegGetValueDword(IntPtr hkey, string lpSubKey, string lpValue,
        uint dwFlags, IntPtr pdwType, out int pvData, ref uint pcbData);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
    private static extern int RegGetValueString(IntPtr hkey, string lpSubKey, string lpValue,
        uint dwFlags, IntPtr pdwType, [Out] char[]? pvData, ref uint pcbData);

    /// <summary>
    /// Lecteur réel, celui du produit. RegGetValueW ouvre, lit et referme la clé en un appel,
    /// et le filtre de type fait échouer la lecture sans effet de bord quand la valeur existe
    /// dans un autre type que celui attendu.
    ///
    /// Interne plutôt que privé : les tests l'appellent sur des valeurs que Windows publie
    /// sous HKLM et que n'importe quel compte peut lire. Sans cela, le P/Invoke serait la
    /// seule pièce de cette couche sans témoin, les tests n'ayant pas le droit d'écrire une
    /// politique pour s'en fabriquer une.
    /// </summary>
    internal static object? ReadRegistryValue(string keyPath, string valueName)
    {
        // REG_DWORD d'abord : quatre des cinq valeurs en sont.
        uint size = sizeof(int);
        if (RegGetValueDword(HKEY_LOCAL_MACHINE, keyPath, valueName,
                RRF_RT_REG_DWORD | RRF_SUBKEY_WOW6464KEY, IntPtr.Zero, out int dword, ref size)
            == ERROR_SUCCESS)
        {
            return dword;
        }

        var buffer = new char[MaxStringChars];
        size = (uint)(buffer.Length * sizeof(char));
        if (RegGetValueString(HKEY_LOCAL_MACHINE, keyPath, valueName,
                RRF_RT_REG_SZ | RRF_SUBKEY_WOW6464KEY, IntPtr.Zero, buffer, ref size)
            == ERROR_SUCCESS)
        {
            // pcbData rend une taille en octets, terminateur compris.
            int chars = Math.Min((int)(size / sizeof(char)) - 1, buffer.Length);
            return chars > 0 ? new string(buffer, 0, chars) : string.Empty;
        }

        return null;
    }
}
