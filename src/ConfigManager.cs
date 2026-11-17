// Gestion de la configuration persistante (config.json)
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// Lit et écrit les préférences utilisateur dans config.json.
/// Mode unpackaged (EXE standalone) : à côté de l'exécutable.
/// Mode packaged (MSIX) : dans %LocalAppData%\AZERTY Global\.
/// </summary>
static class ConfigManager
{
    private static string _configPath = GetConfigPath();
    public const string VirtualKeyboardBoundsKey = "virtualKeyboardBounds";
    public const string LessonsWindowBoundsKey = "lessonsWindowBounds";
    public static event Action<string>? WindowBoundsCleared;

    /// <summary>
    /// Hook de test : redirige config.json vers un fichier temporaire.
    /// À n'utiliser que dans le projet de tests via InternalsVisibleTo.
    /// </summary>
    internal static void OverrideConfigPathForTests(string path)
    {
        lock (_lock)
        {
            _configPath = path;
            _logDirectoryOverride = Path.GetDirectoryName(path);
            _cache = null;
            _compatibilityCache = null;
            _compatibilityUnknown = null;
            _loadFailed = false;
            _readPending = false;
            _changedWhileUnread.Clear();
            _compatChangedWhileUnread.Clear();
            _dirty = false;
            _failedWrites = 0;
            _quarantineNoticePending = false;
            // Écritures différées coupées : un test écrit par Flush(), au moment qu'il
            // choisit, jamais pendant le test suivant ni dans un dossier déjà supprimé.
            // Les témoins du regroupement les rallument (audit du 25/09, A-05).
            SaveDelayMilliseconds = -1;
        }
    }

    // APPMODEL_ERROR_NO_PACKAGE (15700) = l'app n'est pas dans un package MSIX
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder? packageFullName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, System.Text.StringBuilder? packageFamilyName);

    // LANGID de l'interface Windows — sert au défaut de langue du premier lancement.
    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    /// <summary>Indique si l'app tourne dans un package MSIX.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        try
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, null) != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Family name du package courant (forme <c>&lt;Name&gt;_&lt;PublisherId&gt;</c>), par lequel
    /// se reconnaît le canal de distribution. Renvoie false hors package, et aussi quand l'API
    /// échoue autrement, sans distinguer les deux cas. L'appelant ne les confond pas pour
    /// autant : il tient le fait « suis-je dans un package » de <see cref="IsPackaged"/>, qui
    /// repose sur un appel séparé, et n'a donc rien à décider d'un family name illisible.
    ///
    /// Même patron que <see cref="DetectPackaged"/>, en deux appels : le premier donne la
    /// longueur, le second le contenu.
    /// </summary>
    public static bool TryGetPackageFamilyName(out string? familyName)
    {
        familyName = null;
        try
        {
            int length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) == APPMODEL_ERROR_NO_PACKAGE) return false;
            if (length <= 0) return false;

            var buffer = new System.Text.StringBuilder(length);
            if (GetCurrentPackageFamilyName(ref length, buffer) != 0) return false;

            familyName = buffer.ToString();
            return familyName.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetConfigPath()
    {
        if (DetectPackaged())
        {
            // Mode MSIX : dossier du package en lecture seule → utiliser LocalAppData
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDataDir = Path.Combine(localAppData, ProductIdentity.ConfigFolderName);
            Directory.CreateDirectory(appDataDir);

            return Path.Combine(appDataDir, "config.json");
        }

        // Mode unpackaged (EXE standalone) : à côté de l'exe
        return Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    private static Dictionary<string, JsonElement>? _cache;
    private static Dictionary<string, string>? _compatibilityCache; // sous-objet compatibility (process → "forceOn"/"forceOff")

    /// <summary>
    /// Entrées de <c>compatibility</c> que cette version ne connaît pas (mode d'une version plus
    /// récente, valeur d'une autre forme), gardées telles quelles et réécrites (AF-03, audit des
    /// fichiers 1.3.0) : avant, elles étaient jetées au chargement puis effacées à la première
    /// écriture, alors que les clés inconnues de la racine survivent depuis AG130-11 (a). Elles
    /// n'ont aucun effet ici ; un choix de l'utilisateur pour le même processus les remplace.
    /// </summary>
    private static Dictionary<string, JsonElement>? _compatibilityUnknown;
    private static readonly object _lock = new();
    private static bool _loadFailed;
    // CE-T2 (CE-03) : config.json présent mais illisible pour l'instant (verrou d'un antivirus,
    // d'une sauvegarde, droits). _loadFailed reste vrai (le fichier n'est jamais écrasé) et
    // chaque accès le relit, en un essai, jusqu'à la réussite. Une quarantaine ratée, elle, reste
    // collante : _loadFailed sans _readPending.
    private static bool _readPending;
    // Réglages changés pendant la panne de lecture : ils gagnent sur le fichier relu (décision
    // d'Antoine du 17 novembre 2026), puis tout est écrit. Valeur nulle : clé retirée.
    private static readonly Dictionary<string, JsonElement?> _changedWhileUnread = new();
    private static readonly Dictionary<string, string?> _compatChangedWhileUnread = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Accord explicite ; une ancienne configuration n'en tient pas lieu.</summary>
    public static bool ActivationConsent
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                return !_loadFailed && _cache!.TryGetValue("activationConsent", out var value)
                    && value.ValueKind == JsonValueKind.True;
            }
        }
    }

    /// <summary>La politique peut masquer l'accueil ; l'activation reste alors au menu.</summary>
    public static bool ActivationPromptAtStartup =>
        !ActivationConsent && PolicyManager.Current.ShowOnboarding != false;

    public static void AcceptActivation()
    {
        lock (_lock)
        {
            EnsureLoaded();
            using var doc = JsonDocument.Parse("true");
            SetValueLocked("activationConsent", doc.RootElement.Clone());
        }
    }

    /// <summary>
    /// La fenêtre de bienvenue doit-elle s'ouvrir au démarrage ? Une politique d'entreprise
    /// à 0 la supprime ; à 1 elle autorise sans imposer, l'utilisateur gardant sa case
    /// (décision d'Antoine du 2026-08-19). C'est le seul des cinq réglages sous politique
    /// dont la valeur 1 ne verrouille rien : verrouiller la case à « cochée » ferait revenir
    /// la fenêtre à chaque démarrage, y compris pour qui l'a déjà vue.
    /// </summary>
    public static bool ShowOnboardingAtStartup =>
        PolicyManager.ShowOnboarding(PolicyManager.Current.ShowOnboarding, ShowOnboardingUserSetting);

    /// <summary>Réglage utilisateur seul, sans la politique : ce que la case des Paramètres
    /// écrit, et ce qui reprendrait effet si la politique était retirée.</summary>
    internal static bool ShowOnboardingUserSetting
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();

                if (_cache!.TryGetValue("showOnboardingAtStartup", out var showValue))
                    return showValue.ValueKind != JsonValueKind.False;

                bool show = true;
                if (_cache.TryGetValue("onboardingDone", out var legacyDone) &&
                    legacyDone.ValueKind == JsonValueKind.True)
                {
                    show = false;
                }

                using var doc = JsonDocument.Parse(show ? "true" : "false");
                SetValueLocked("showOnboardingAtStartup", doc.RootElement.Clone());
                return show;
            }
        }
    }

    /// <summary>Marque l'onboarding comme terminé.</summary>
    public static void SetShowOnboardingAtStartup(bool show) => SetBool("showOnboardingAtStartup", show);

    /// <summary>
    /// Compteur monotone des exercices du tutoriel réussis, en rang fixe : n vaut « les
    /// exercices 0 à n − 1 de <see cref="TutorialSteps.All"/> sont réussis » (0..8 ; les
    /// configs d'avant le parcours par usage vont jusqu'à 6, même sens).
    /// Utilisé pour conditionner l'affichage du wizard d'accueil au démarrage : tant que
    /// les 3 premiers exercices ne sont pas tous complétés, le wizard s'affiche
    /// (sauf si l'utilisateur a explicitement désactivé l'option dans les Settings).
    /// </summary>
    public static int LearningMaxStepCompleted => (int)GetUInt("learningMaxStepCompleted");

    /// <summary>
    /// Met à jour le compteur de progression. Monotone : ne décroît jamais.
    /// Pas d'écriture sur disque si pas de progrès.
    /// </summary>
    public static void SetLearningMaxStepCompleted(int step)
    {
        if (step <= LearningMaxStepCompleted) return;
        SetUInt("learningMaxStepCompleted", (uint)step);
    }

    /// <summary>
    /// Exercices du tutoriel réussis, par rang fixe (<see cref="TutorialSteps.All"/>) : ceux
    /// du compteur (0 à <see cref="LearningMaxStepCompleted"/> − 1), plus ceux réussis hors de
    /// cette suite depuis le parcours par usage (clé <c>learningCompletedExercises</c>).
    /// L'import vers l'Initiation des Leçons lit cet ensemble, pas la place dans le parcours.
    /// </summary>
    public static IReadOnlySet<int> LearningCompletedExercises
    {
        get
        {
            var done = new SortedSet<int>(Enumerable.Range(0, Math.Min(LearningMaxStepCompleted, TutorialSteps.All.Length)));
            foreach (int index in GetIntList("learningCompletedExercises"))
            {
                if (index >= 0 && index < TutorialSteps.All.Length)
                    done.Add(index);
            }
            return done;
        }
    }

    /// <summary>
    /// Enregistre la réussite de l'exercice de rang fixe <paramref name="index"/> : dans la
    /// liste, puis le compteur avance tant que la suite depuis 0 est complète.
    /// </summary>
    public static void RecordLearningExerciseCompleted(int index)
    {
        if (index < 0 || index >= TutorialSteps.All.Length) return;
        var done = new SortedSet<int>(LearningCompletedExercises) { index };
        int rank = LearningMaxStepCompleted;
        while (done.Contains(rank)) rank++;
        SetLearningMaxStepCompleted(rank);
        // Seuls les rangs au-delà du compteur ont besoin de la liste.
        SetIntList("learningCompletedExercises", done.Where(i => i >= rank));
    }

    /// <summary>
    /// Usages cochés au dernier passage sur l'écran de choix du tutoriel (identifiants de
    /// <see cref="TutorialSteps.UsagesByPriority"/>). Gardés dans la config locale seulement,
    /// jamais envoyés : ils recochent les cases quand le tutoriel est rouvert.
    /// </summary>
    public static string[] LearningUsages => TutorialSteps.NormalizeUsages(GetStringList("learningUsages"));

    public static void SetLearningUsages(IEnumerable<string> usages) =>
        SetStringList("learningUsages", TutorialSteps.NormalizeUsages(usages));

    /// <summary>
    /// Remet le parcours de l’accueil à zéro : exercices réussis et parcours ouvert. Sert à
    /// « Réinitialiser l’accueil » (Debug) : sans cela, l’accueil relisait la progression
    /// gardée et revenait à « Suivant » seul, sans « Essayer maintenant » (remontée d’Antoine,
    /// 07/11). Les usages choisis restent.
    /// </summary>
    public static void ResetTutorialProgress()
    {
        SetUInt("learningMaxStepCompleted", 0);
        SetIntList("learningCompletedExercises", Array.Empty<int>());
        SetBool("learningTutorialOpened", false);
    }

    /// <summary>
    /// Le parcours d'exercices de l'accueil a été ouvert au moins une fois (audit 1.3.0, A1) :
    /// l'étape 1 de l'accueil garde alors « Suivant », même sans exercice réussi, et les étapes
    /// suivantes (dont « Ne plus afficher ») restent atteignables d'un démarrage à l'autre.
    /// </summary>
    public static bool LearningTutorialOpened => GetBool("learningTutorialOpened");

    public static void MarkLearningTutorialOpened()
    {
        if (!LearningTutorialOpened) SetBool("learningTutorialOpened", true);
    }

    /// <summary>Cache de compatibilité du lancement automatique. L'UI doit utiliser AutoStart.IsRegistered.</summary>
    public static bool AutoStartEnabled => GetBool("autoStartEnabled");

    /// <summary>Met à jour le cache de compatibilité du lancement automatique.</summary>
    public static void SetAutoStart(bool enabled) => SetBool("autoStartEnabled", enabled);

    /// <summary>
    /// Les notifications (balloons) sont-elles activées ? Défaut : true, sur les trois
    /// canaux — la décision D3 n'a éteint que la sollicitation d'avis, et le canal AMCF
    /// garde ses bulles de confirmation (décision d'Antoine du 2026-08-19). Une politique
    /// d'entreprise prime sur le réglage utilisateur.
    ///
    /// Les bulles de sécurité ne passent pas par ici : <c>ShowSecurityBalloon</c> contourne
    /// ce réglage, et la politique ne le referme pas — la DSI éteint le confort, pas
    /// l'avertissement.
    /// </summary>
    public static bool NotificationsEnabled =>
        PolicyManager.NotificationsEnabled(PolicyManager.Current.Notifications, NotificationsUserSetting);

    /// <summary>Réglage utilisateur seul, sans la politique.</summary>
    internal static bool NotificationsUserSetting
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                // Défaut true : si la clé n'existe pas, les notifications sont activées
                return !_cache!.TryGetValue("notificationsEnabled", out var val) || val.ValueKind != JsonValueKind.False;
            }
        }
    }

    /// <summary>Active ou désactive les notifications.</summary>
    public static void SetNotifications(bool enabled) => SetBool("notificationsEnabled", enabled);

    // ── Couches maintenables ───────────────────────────────────────────────

    /// <summary>
    /// Interrupteur volontaire principal. Défaut false pour ne modifier aucune
    /// habitude lors d'une mise à jour depuis une version antérieure.
    /// </summary>
    public static bool MaintainableLayersEnabled => GetBool("maintainableLayersEnabled");

    public static void SetMaintainableLayersEnabled(bool enabled) =>
        SetBool("maintainableLayersEnabled", enabled);

    // Les trois couches sont pré-sélectionnées dans le parcours d'activation,
    // mais restent inactives tant que l'interrupteur principal est à false.
    public static bool MaintainableGreekEnabled => GetBoolDefaultTrue("maintainableGreekEnabled");
    public static bool MaintainableCyrillicEnabled => GetBoolDefaultTrue("maintainableCyrillicEnabled");
    public static bool MaintainableScientificEnabled => GetBoolDefaultTrue("maintainableScientificEnabled");

    public static void SetMaintainableGreekEnabled(bool enabled) =>
        SetBool("maintainableGreekEnabled", enabled);
    public static void SetMaintainableCyrillicEnabled(bool enabled) =>
        SetBool("maintainableCyrillicEnabled", enabled);
    public static void SetMaintainableScientificEnabled(bool enabled) =>
        SetBool("maintainableScientificEnabled", enabled);

    public static int MaintainableDoubleTapMilliseconds
    {
        get
        {
            uint configured = GetUInt("maintainableDoubleTapMilliseconds");
            if (configured != 0)
                return Math.Clamp((int)configured, 150, 1000);

            return Math.Clamp((int)Win32.GetDoubleClickTime(), 150, 1000);
        }
    }

    public static void SetMaintainableDoubleTapMilliseconds(int milliseconds) =>
        SetUInt("maintainableDoubleTapMilliseconds", (uint)Math.Clamp(milliseconds, 150, 1000));

    public static bool MaintainableVisualFeedbackEnabled =>
        GetBoolDefaultTrue("maintainableVisualFeedbackEnabled");

    public static void SetMaintainableVisualFeedbackEnabled(bool enabled) =>
        SetBool("maintainableVisualFeedbackEnabled", enabled);

    // Plus aucune lecture en 1.3.0 (audit du 25/09, X-03). L'écriture reste, pour que
    // config.json garde le même contenu qu'avant (compatibilité de config).
    public static void SetMaintainableTutorialCompleted(bool completed) =>
        SetBool("maintainableTutorialCompleted", completed);

    /// <summary>Langue de l'interface : "fr" (défaut) ou "en". Une politique d'entreprise
    /// impose la langue et verrouille les trois surfaces qui la changent.</summary>
    public static string AppLanguage =>
        PolicyManager.AppLanguage(PolicyManager.Current.Language, AppLanguageUserSetting);

    /// <summary>Réglage utilisateur seul, sans la politique. Toute autre valeur que "en"
    /// retombe sur "fr".</summary>
    internal static string AppLanguageUserSetting
    {
        get
        {
            // Une clé absente signifie « config existante d'avant la dérivation » :
            // une installation neuve reçoit sa clé dès le premier chargement
            // (EnsureLoaded), donc une mise à jour ne change jamais de langue
            // (décision d'Antoine, 2026-08-24).
            var raw = GetString("appLanguage");
            return raw == "en" ? "en" : "fr";
        }
    }

    /// <summary>
    /// Langue de l'interface lue au tout début du démarrage, AVANT les mutex d'instance unique,
    /// sans charger ni mettre de côté config.json (ME-09, C4.5). Seul le gagnant lit et déplace
    /// les fichiers de l'utilisateur : une seconde instance qui chargeait ici le fichier
    /// corrompu se disputait le renommage avec la gagnante, perdait la course et gardait un
    /// « chargement échoué » collé, ou faisait perdre à la gagnante le message de remise à zéro.
    ///
    /// La valeur est celle que donnerait le chargement normal : réglage « en » ou « fr », « fr »
    /// pour une config existante sans clé ou en lecture impossible, langue de l'interface
    /// Windows pour une installation neuve ou un fichier corrompu (que le chargement mettra de
    /// côté puis dérivera de la même façon). Aucun accès au cache, aucune écriture, aucune
    /// quarantaine ; un cache déjà chargé répond directement.
    /// </summary>
    internal static string AppLanguageWithoutLoading()
    {
        bool loaded;
        string path;
        lock (_lock)
        {
            loaded = _cache != null;
            path = _configPath;
        }
        if (loaded) return AppLanguage;
        return PolicyManager.AppLanguage(PolicyManager.Current.Language, ReadLanguageSettingFromDisk(path));
    }

    private static string ReadLanguageSettingFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path)) return DerivedAppLanguage();
            // CE-T2 (CE-04) : mêmes essais courts que le chargement. Un seul essai rendait « fr »
            // pour toute la session quand un verrou passager tenait un fichier qui dit « en ».
            using var doc = FileQuarantine.ParseUserFile(FileQuarantine.ReadAllTextWithRetries(path)); // AF-01
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return DerivedAppLanguage();
            return doc.RootElement.TryGetProperty("appLanguage", out var value)
                   && value.ValueKind == JsonValueKind.String && value.GetString() == "en"
                ? "en" : "fr";
        }
        catch (Exception ex) when (FileQuarantine.IsReadFailure(ex)) { return "fr"; }
        catch (Exception ex) when (FileQuarantine.IsCorruption(ex)) { return DerivedAppLanguage(); }
        catch (IOException) { return DerivedAppLanguage(); } // fichier disparu : installation neuve
    }

    /// <summary>Langue initiale d'une installation neuve : celle de l'interface Windows.</summary>
    private static string DerivedAppLanguage()
    {
        try { return DefaultAppLanguage(GetUserDefaultUILanguage()); }
        catch { return "fr"; }
    }

    /// <summary>« fr » quand le LANGID donné est francophone (langue primaire 0x0C,
    /// toutes régions : fr-FR, fr-CA, fr-BE…), « en » sinon. Fonction pure : c'est
    /// elle que les tests éprouvent, un poste de CI n'étant jamais francophone.</summary>
    internal static string DefaultAppLanguage(ushort windowsUiLangId) =>
        (windowsUiLangId & 0x3FF) == 0x0C ? "fr" : "en";

    /// <summary>LANGID Windows réel du poste, exposé pour que le test d'intégration
    /// du défaut de langue compare au même environnement que le getter.</summary>
    internal static ushort WindowsUiLanguageIdForTests() => GetUserDefaultUILanguage();

    /// <summary>Déclenché après un changement effectif de langue (arg : nouvelle langue).
    /// Permet aux surfaces à texte persistant (tooltip du tray) de se rafraîchir.</summary>
    public static event Action<string>? AppLanguageChanged;

    /// <summary>Définit la langue de l'interface. Seules "fr" et "en" sont acceptées.</summary>
    public static void SetAppLanguage(string lang)
    {
        if (lang != "fr" && lang != "en") return;
        // Porte unique de la langue imposée. Trois surfaces la changent — les Paramètres, le
        // menu tray et le drapeau de la fenêtre de bienvenue — et n'en griser qu'une laisse
        // les deux autres écrire. Le garde est donc posé ici, pas dans les fenêtres.
        if (PolicyManager.LanguageIsManagedNow) return;
        if (AppLanguage == lang) return;
        SetString("appLanguage", lang);
        AppLanguageChanged?.Invoke(lang);
    }

    // ═══════════════════════════════════════════════════════════════
    // Sollicitation d'avis (v1.2.0 : deux essais, seuils d'usage)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Date du premier lancement (ISO 8601 UTC), enregistrée au premier accès.
    /// Depuis la v1.2.0 la sollicitation d'avis se déclenche sur les jours d'usage réels
    /// (<see cref="UsageStats.ActiveDaysCount"/>) et non plus sur cette horloge ; elle
    /// reste écrite pour les installations existantes et le diagnostic. Une valeur
    /// corrompue est ré-enregistrée à maintenant (l'horloge repart, pas de crash).
    /// </summary>
    public static DateTimeOffset EnsureFirstRunTimestamp()
    {
        var raw = GetString("firstRunTimestamp");
        if (!string.IsNullOrEmpty(raw) &&
            DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            return parsed;

        var now = DateTimeOffset.UtcNow;
        SetString("firstRunTimestamp", now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        return now;
    }

    /// <summary>
    /// Nombre de sollicitations d'avis déjà affichées : 0, 1 ou 2 (plafond dur).
    ///
    /// Migration v1.1 → v1.3 (décision d'Antoine du 2026-09-20) : le booléen hérité
    /// `reviewPromptDone` ne vaut plus une sollicitation consommée. Les installations
    /// qui migrent repartent donc avec les deux essais entiers.
    ///
    /// Le raisonnement tient à ce qu'était réellement la sollicitation v1.1 : elle vivait
    /// dans le `else` du test d'affichage de l'accueil — une partie des installations
    /// n'a jamais été sollicitée — et, quand elle l'était, un tirage 50/50 l'envoyait une
    /// fois sur deux vers la page de feedback privée au lieu de la fiche Store. Un
    /// `reviewPromptDone` à true ne prouve donc pas qu'une note ait été demandée. Mesure
    /// du 2026-09-02 : 0 notation sur 394 utilisateurs actifs en juillet 2026.
    ///
    /// ⚠️ Contrepartie assumée : le cas qu'on ne sait pas distinguer est celui de
    /// l'utilisateur v1.1 qui a cliqué sa sollicitation et déposé sa note. La v1.1
    /// n'écrit pas `reviewPromptClicked` — ce champ naît en v1.2.0 — donc rien dans la
    /// configuration ne le sépare de celui qui a ignoré la notification. Il sera
    /// re-sollicité une fois. Les garde-fous ordinaires s'appliquent quand même :
    /// plafond de deux essais sur la vie de l'installation, planchers de 3 puis 10 jours
    /// d'usage distincts, une sollicitation par jour au plus, silence de 48 heures après
    /// une erreur journalisée.
    /// </summary>
    public static int ReviewPromptCount => (int)GetUInt("reviewPromptCount");

    /// <summary>Date (heure locale, au jour près) de la dernière sollicitation affichée, si connue.</summary>
    public static DateOnly? ReviewPromptLastShown
    {
        get
        {
            var raw = GetString("reviewPromptLastShown");
            return DateOnly.TryParseExact(raw, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
        }
    }

    /// <summary>
    /// L'utilisateur a cliqué une sollicitation. Il a répondu : on ne le relance plus,
    /// quel que soit ce qu'il a fait ensuite sur le Store.
    /// </summary>
    public static bool ReviewPromptClicked => GetBool("reviewPromptClicked");

    /// <summary>
    /// Enregistre une sollicitation affichée : incrémente le compteur et pose la date.
    /// `reviewPromptDone` reste écrit pour qu'un retour arrière en v1.1 ne reparte pas
    /// de zéro.
    ///
    /// <paramref name="shownOn"/> est la date que l'appelant a déjà calculée pour ses
    /// propres gardes, en heure locale. Cette date était écrite en UTC alors que tous
    /// ses comparateurs travaillent en local : en UTC+2, une sollicitation affichée
    /// entre 00 h et 02 h locales enregistrait la veille, et le plancher de 7 jours du
    /// second essai s'ouvrait un jour trop tôt (R9 de l'audit v1.2.0). Recevoir la date
    /// de l'appelant rend l'écart structurellement impossible, au lieu de le corriger
    /// à deux endroits qui peuvent redivergier.
    /// </summary>
    public static void RecordReviewPromptShown(DateOnly? shownOn = null)
    {
        lock (_lock)
        {
            var next = (uint)Math.Min(ReviewPromptCount + 1, 2);
            SetUInt("reviewPromptCount", next);
            var date = shownOn ?? DateOnly.FromDateTime(DateTime.Now);
            SetString("reviewPromptLastShown",
                date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            SetBool("reviewPromptDone", true);
        }
        // Durable avant l'affichage (audit du 25/09, A-05) : une seule écriture pour les
        // trois clés, mais tout de suite, pour qu'un arrêt brutal juste après la bulle ne
        // rende pas un essai. Au plus deux fois sur la vie de l'installation. Hors du lock
        // ci-dessus : Flush ne doit jamais être appelé sous _lock.
        Flush();
    }

    /// <summary>Marque la sollicitation comme cliquée : plus aucune relance.</summary>
    public static void SetReviewPromptClicked() => SetBool("reviewPromptClicked", true);

    /// <summary>Premier lancement de la version courante (audit 24/09).</summary>
    /// <param name="Version">Version à laquelle se rapportent les deux autres champs.</param>
    /// <param name="Date">Date locale de ce premier lancement.</param>
    /// <param name="UpgradedWithUsage">Des statistiques d'usage existaient déjà à ce
    /// moment : l'installation vient d'une version antérieure qui a servi.</param>
    internal readonly record struct VersionFirstRun(string Version, DateOnly Date, bool UpgradedWithUsage);

    /// <summary>
    /// Enregistre, une fois par version, la date du premier lancement de cette version et
    /// la présence de statistiques d'usage à ce moment, puis les rend telles quelles aux
    /// lancements suivants. Sert à l'essai 1 de la sollicitation d'avis
    /// (<c>ReviewPromptGate.FirstAttemptAllowed</c>) : un utilisateur actif de la 1.1
    /// dépasse déjà les seuils d'usage, et doit avoir servi un jour de la version installée
    /// avant qu'on lui demande de la noter.
    ///
    /// Trois clés à plat : <c>currentVersionFirstRunVersion</c>,
    /// <c>currentVersionUpgradedWithUsage</c>, puis <c>currentVersionFirstRunDate</c>
    /// (« yyyy-MM-dd », heure locale comme tous les comparateurs de la sollicitation).
    /// La date est écrite en dernier et fait foi : absente ou illisible, les trois sont
    /// réécrites. Une version différente les réécrit aussi : une mise à jour ultérieure
    /// repose la même question pour la version qu'elle installe.
    ///
    /// <paramref name="hadUsageBefore"/> doit être lu avant toute frappe de ce lancement
    /// (<c>UsageStats.FirstRemapDate != null</c> dans le constructeur du tray).
    /// </summary>
    internal static VersionFirstRun EnsureCurrentVersionFirstRun(string version, DateOnly today, bool hadUsageBefore)
    {
        lock (_lock)
        {
            var storedVersion = GetString("currentVersionFirstRunVersion");
            var rawDate = GetString("currentVersionFirstRunDate");
            if (storedVersion == version &&
                DateOnly.TryParseExact(rawDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var storedDate))
                return new VersionFirstRun(version, storedDate, GetBool("currentVersionUpgradedWithUsage"));

            SetString("currentVersionFirstRunVersion", version);
            SetBool("currentVersionUpgradedWithUsage", hadUsageBefore);
            SetString("currentVersionFirstRunDate",
                today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            return new VersionFirstRun(version, today, hadUsageBefore);
        }
    }

    /// <summary>
    /// Date de la dernière erreur journalisée par ce process, ou null. Sert de garde à la
    /// sollicitation d'avis : on ne demande pas un avis à quelqu'un qui vient de
    /// rencontrer un problème.
    ///
    /// Volontairement en mémoire seulement, et alimenté par <see cref="Log(string, Exception)"/>
    /// uniquement : la date d'écriture d'error.log ne conviendrait pas puisque le même
    /// fichier reçoit les événements de compatibilité jeux, qui ne sont pas des erreurs et
    /// bloqueraient un joueur régulier en permanence. Persister ici est également exclu —
    /// <see cref="Flush"/> journalise ses propres échecs, une écriture de config depuis le
    /// chemin d'erreur se rappellerait elle-même.
    /// </summary>
    public static DateTime? LastErrorUtc => _lastErrorUtc;
    private static DateTime? _lastErrorUtc;

    /// <summary>Indique si les indices automatiques du module Lecons sont actives. Defaut : false.</summary>
    public static bool LessonAutoHintsEnabled => GetBool("lessonAutoHintsEnabled");

    /// <summary>Active ou desactive les indices automatiques du module Lecons.</summary>
    public static void SetLessonAutoHints(bool enabled) => SetBool("lessonAutoHintsEnabled", enabled);

    /// <summary>Affiche les stats du mode libre. Defaut : true.</summary>
    public static bool LessonFreeStatsVisible => GetBoolDefaultTrue("lessonFreeStatsVisible");

    public static void SetLessonFreeStatsVisible(bool visible) => SetBool("lessonFreeStatsVisible", visible);

    /// <summary>Affiche le resume de fin d'exercice. Defaut : true.</summary>
    public static bool LessonSummaryVisible => GetBoolDefaultTrue("lessonSummaryVisible");

    public static void SetLessonSummaryVisible(bool visible) => SetBool("lessonSummaryVisible", visible);

    /// <summary>Affiche le clavier visuel dans la fenetre Lecons. Defaut : true.</summary>
    public static bool LessonKeyboardVisible => GetBoolDefaultTrue("lessonKeyboardVisible");

    public static void SetLessonKeyboardVisible(bool visible) => SetBool("lessonKeyboardVisible", visible);

    // ── Notation des touches à taper (QCM du 11/10, voir LessonNotation.cs) ──────
    // Une notation par état du clavier, chacune mémorisée ; stockée par nom d'enum (jamais par
    // numéro : l'ordre de l'enum peut changer). Valeur absente, illisible, d'un autre type ou
    // hors de la liste de l'état : Mots, sans toucher aux autres clés.

    /// <summary>Notation du geste quand le clavier est affiché. Défaut : Mots.</summary>
    public static LessonNotation LessonNotationWithKeyboard => GetLessonNotation("lessonNotationWithKeyboard", true);

    public static void SetLessonNotationWithKeyboard(LessonNotation value) =>
        SetString("lessonNotationWithKeyboard", LessonNotations.Valid(value, true).ToString());

    /// <summary>Notation du geste quand le clavier est masqué. Défaut : Mots.</summary>
    public static LessonNotation LessonNotationWithoutKeyboard => GetLessonNotation("lessonNotationWithoutKeyboard", false);

    public static void SetLessonNotationWithoutKeyboard(LessonNotation value) =>
        SetString("lessonNotationWithoutKeyboard", LessonNotations.Valid(value, false).ToString());

    /// <summary>Notation de l'état courant du clavier (affiché ou masqué).</summary>
    public static LessonNotation LessonNotationFor(bool keyboardVisible) =>
        keyboardVisible ? LessonNotationWithKeyboard : LessonNotationWithoutKeyboard;

    public static void SetLessonNotationFor(bool keyboardVisible, LessonNotation value)
    {
        if (keyboardVisible) SetLessonNotationWithKeyboard(value);
        else SetLessonNotationWithoutKeyboard(value);
    }

    private static LessonNotation GetLessonNotation(string key, bool keyboardVisible)
    {
        // Enum.TryParse accepterait aussi « 2 » ou « Words, Recipe » : on exige le nom exact.
        var raw = GetString(key);
        if (raw is not null && Enum.TryParse<LessonNotation>(raw, out var parsed) && parsed.ToString() == raw)
            return LessonNotations.Valid(parsed, keyboardVisible);
        return LessonNotation.Words;
    }

    /// <summary>Masque le geste des caractères maîtrisés (boîte 4 ou plus). Défaut : true.</summary>
    public static bool LessonHideMastered => GetBoolDefaultTrue("lessonHideMastered");

    public static void SetLessonHideMastered(bool hide) => SetBool("lessonHideMastered", hide);

    /// <summary>Affiche les libelles des espaces invisibles. Defaut : true.</summary>
    public static bool LessonInvisibleMarkersVisible => GetBoolDefaultTrue("lessonInvisibleMarkersVisible");

    public static void SetLessonInvisibleMarkersVisible(bool visible) => SetBool("lessonInvisibleMarkersVisible", visible);

    /// <summary>
    /// Raccourcis Ctrl+Shift+Lettre interdits car trop courants dans les applications.
    /// Voir aussi : Ctrl+Shift+Verr.Maj (toggle on/off), bloqué séparément via VK_CAPITAL.
    /// </summary>
    private static readonly HashSet<char> BlockedLetters = new()
    {
        'T', // Rouvrir onglet fermé (tous les navigateurs)
        'N', // Navigation privée (Chrome, Edge, Firefox)
        'V', // Coller sans formatage (quasi universel)
        'P', // Palette de commandes (VS Code, Sublime, JetBrains)
        'I', // DevTools (Chrome, Edge, Firefox)
        'S', // Enregistrer sous (Word, Excel, Photoshop, VS Code)
        'Z', // Refaire (Photoshop, Figma, Google Docs)
        'F', // Rechercher dans les fichiers (tous les IDE)
        'C', // Copier (terminal), Inspecter élément (Chrome)
        'B', // Barre favoris (Chrome, Edge), Build (VS Code)
        'R', // Rechargement forcé (navigateurs)
    };

    private const uint DefaultShortcutVirtualKeyboardVk = 0x51; // Q
    private const uint DefaultShortcutCharacterSearchVk = 0x57; // W

    /// <summary>
    /// Vérifie si une touche est autorisée comme raccourci Ctrl+Shift+touche.
    /// Formats acceptés : lettre (A-Z sauf bloquées), chiffre (0-9), "F1" à "F12".
    /// </summary>
    public static bool IsShortcutAllowedVk(uint vk)
    {
        if (vk >= 0x70 && vk <= 0x7B) return true;
        // Touches de fonction F1-F12
        if (vk >= '0' && vk <= '9') return true;
        // Chiffres 0-9
        if (vk >= 'A' && vk <= 'Z') return !BlockedLetters.Contains((char)vk);
        // Lettres A-Z (sauf bloquées)
        return false;
    }

    /// <summary>Lettre du raccourci clavier virtuel. Défaut : "Q" → Ctrl+Maj+Q.</summary>
    public static uint ShortcutVirtualKeyboardVk
    {
        get
        {
            uint vk = GetUInt("shortcutVirtualKeyboardVk");
            return IsShortcutAllowedVk(vk) ? vk : DefaultShortcutVirtualKeyboardVk;
        }
        set
        {
            if (IsShortcutAllowedVk(value))
                SetUInt("shortcutVirtualKeyboardVk", value);
        }
    }

    /// <summary>Lettre du raccourci recherche de caractère. Défaut : "W" → Ctrl+Maj+W.</summary>
    public static uint ShortcutCharacterSearchVk
    {
        get
        {
            uint vk = GetUInt("shortcutCharacterSearchVk");
            return IsShortcutAllowedVk(vk) ? vk : DefaultShortcutCharacterSearchVk;
        }
        set
        {
            if (IsShortcutAllowedVk(value))
                SetUInt("shortcutCharacterSearchVk", value);
        }
    }

    /// <summary>
    /// Parse une touche de fonction "F1"-"F12". Retourne le numéro (1-12) ou 0 si invalide.
    /// </summary>
    public static string GetShortcutDisplayName(uint vk)
    {
        if (vk >= 0x70 && vk <= 0x7B)
            return $"F{vk - 0x6F}";

        if ((vk >= '0' && vk <= '9') || (vk >= 'A' && vk <= 'Z'))
            return ((char)vk).ToString();

        return string.Empty;
    }

    /// <summary>
    /// Retourne le VK code pour un raccourci configurable.
    /// Accepte : lettre A-Z (sauf bloquées), chiffre 0-9, "F1"-"F12".
    /// Retourne 0 si la touche est invalide ou bloquée.
    /// </summary>

    // ═══════════════════════════════════════════════════════════════
    // Compatibilité jeux — overrides par process et flag debug log
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Mode override utilisateur pour un process : "forceOn" (combo native), "forceOff"
    /// (désactivation totale), ou null (mode auto). Lecture case-insensitive.
    /// </summary>
    public static string? GetCompatibilityOverride(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return null;
        lock (_lock)
        {
            EnsureLoaded();
            if (_compatibilityCache == null) return null;
            foreach (var (k, v) in _compatibilityCache)
                if (string.Equals(k, processName, StringComparison.OrdinalIgnoreCase))
                    return v;
            return null;
        }
    }

    /// <summary>
    /// Définit ou supprime un override pour un process. mode = "forceOn" / "forceOff"
    /// ou null pour retirer l'entrée (retour au mode auto).
    /// </summary>
    public static void SetCompatibilityOverride(string processName, string? mode)
    {
        if (string.IsNullOrEmpty(processName)) return;
        if (mode != null && mode != "forceOn" && mode != "forceOff") return;
        lock (_lock)
        {
            EnsureLoaded();
            _compatibilityCache ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _compatibilityUnknown?.Remove(processName);
            if (mode == null)
                _compatibilityCache.Remove(processName);
            else
                _compatibilityCache[processName] = mode;
            if (_readPending) _compatChangedWhileUnread[processName] = mode;
            MarkDirty();
        }
    }

    /// <summary>Retourne une copie de la map des overrides (pour audit au démarrage).</summary>
    public static Dictionary<string, string> GetAllCompatibilityOverrides()
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _compatibilityCache == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(_compatibilityCache, StringComparer.OrdinalIgnoreCase);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Défi du jour (v1.2.0) — rappels d'entraînement opt-in
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Opt-in explicite (fin d'onboarding ou Paramètres). Décoché par défaut.</summary>
    public static bool TrainingEnabled => GetBool("trainingEnabled");
    /// <summary>Déclenché après un changement effectif de l'opt-in (arg : nouvelle valeur) : une
    /// fenêtre ouverte qui en dépend (Statistiques, section Défi) refait sa mise en page.</summary>
    public static event Action<bool>? TrainingEnabledChanged;

    public static void SetTrainingEnabled(bool enabled)
    {
        bool changed = TrainingEnabled != enabled;
        SetBool("trainingEnabled", enabled);
        // Réactivation = nouveau consentement : le compteur de rappels ignorés repart à zéro
        // (garde-fou « 3 ignorés → arrêt définitif », réactivable à tout moment).
        if (enabled) SetUInt("trainingIgnoredCount", 0);
        if (changed) TrainingEnabledChanged?.Invoke(enabled);
    }

    /// <summary>Rappels consécutifs ignorés (timeout sans clic). 3 → arrêt définitif.</summary>
    public static uint TrainingIgnoredCount => GetUInt("trainingIgnoredCount");
    public static void SetTrainingIgnoredCount(uint count) => SetUInt("trainingIgnoredCount", count);

    /// <summary>Phase pédagogique : index 0..4 = séquence des 5 changements (désapprentissage),
    /// 5+ = défi du jour commun. Persisté pour reprendre la séquence où elle en était.</summary>
    public static uint TrainingSequenceIndex => GetUInt("trainingSequenceIndex");
    public static void SetTrainingSequenceIndex(uint index) => SetUInt("trainingSequenceIndex", index);

    /// <summary>Date (yyyy-MM-dd) de la dernière séance terminée — une séance par jour max.</summary>
    public static string? TrainingLastSessionDate => GetString("trainingLastSessionDate");
    public static void SetTrainingLastSessionDate(string date) => SetString("trainingLastSessionDate", date);

    /// <summary>Meilleure vitesse (mots/min) à l'extrait du Défi, tous jours confondus ; 0 = aucune.
    /// Audit UX du 09/11, L10 : le record de l'exercice du jour repartait de zéro chaque matin.</summary>
    public static uint TrainingBestWpm => GetUInt("trainingBestWpm");
    public static void SetTrainingBestWpm(uint wpm) => SetUInt("trainingBestWpm", wpm);

    /// <summary>Date (yyyy-MM-dd) du dernier rappel émis — jamais deux rappels le même jour.</summary>
    public static string? TrainingLastReminderDate => GetString("trainingLastReminderDate");
    public static void SetTrainingLastReminderDate(string date) => SetString("trainingLastReminderDate", date);

    /// <summary>Annonce unique « Nouveau : Défi du jour » aux utilisateurs existants faite
    /// (décision 2026-07-30 : une seule balloon post-mise à jour 1.2.0, exception ponctuelle
    /// à la doctrine zéro harcèlement — jamais réémise).</summary>
    public static bool ChallengeAnnounceDone => GetBool("challengeAnnounceDone");
    public static void SetChallengeAnnounceDone() => SetBool("challengeAnnounceDone", true);
    /// <summary>Relance unique du lancement automatique faite (décision 2026-08-17) :
    /// une seule proposition sur la vie de l'installation, jamais réémise. Même doctrine
    /// que l'annonce du Défi du jour ci-dessus.</summary>
    public static bool AutoStartNudgeDone => GetBool("autoStartNudgeDone");
    public static void SetAutoStartNudgeDone() => SetBool("autoStartNudgeDone", true);

    /// <summary>
    /// Active le journal de debug compat niveau 2 (statistiques agrégées d'émission).
    /// Désactivé par défaut. Aucune frappe n'est jamais loguée, quel que soit le flag.
    /// </summary>
    public static bool CompatibilityDebugLog => GetBool("compatibilityDebugLog");

    public static void SetCompatibilityDebugLog(bool enabled) => SetBool("compatibilityDebugLog", enabled);

    public static bool TryGetWindowBounds(string key, out Win32.RECT rect)
    {
        rect = default;
        var value = GetString(key);
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Split(',');
        if (parts.Length != 4) return false;
        if (!TryParseStoredInt(parts[0], out int left) ||
            !TryParseStoredInt(parts[1], out int top) ||
            !TryParseStoredInt(parts[2], out int width) ||
            !TryParseStoredInt(parts[3], out int height))
            return false;

        if (width < 100 || height < 80) return false;
        rect = new Win32.RECT
        {
            left = left,
            top = top,
            right = left + width,
            bottom = top + height
        };
        return true;
    }

    public static void SetWindowBounds(string key, Win32.RECT rect)
    {
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        if (width < 100 || height < 80) return;
        SetString(key, string.Create(CultureInfo.InvariantCulture, $"{rect.left},{rect.top},{width},{height}"));
    }

    /// <summary>
    /// Relit un entier de position de fenêtre (AF-05, audit des fichiers 1.3.0). Écrit en
    /// culture invariante depuis ce correctif ; avant, il suivait le format régional du moment,
    /// dont le signe moins peut être U+2212 (suédois, norvégien, finnois) : une coordonnée
    /// négative ne se relisait plus après un changement de format. Un entier formaté par défaut
    /// ne diffère que par son signe moins (U+2212, ou trait d'union précédé d'une marque de
    /// direction) : on retire les marques, on ramène U+2212 au trait d'union, puis on relit en
    /// culture invariante.
    /// </summary>
    private static bool TryParseStoredInt(string text, out int value)
    {
        // Revue d'AF-05 : sous ICU, les formats arabe, hébreu et persan font précéder le signe
        // moins d'une marque de direction (U+061C, U+200E, U+200F), de catégorie « Format ».
        var digits = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.Format || char.IsWhiteSpace(c)) continue;
            digits.Append(c == '−' ? '-' : c);
        }
        return int.TryParse(digits.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    public static void ClearWindowBounds(string key)
    {
        bool changed;
        lock (_lock)
        {
            EnsureLoaded();
            changed = _cache!.Remove(key);
            // CE-T2 : retirée aussi du fichier relu, même absente du cache de la panne.
            if (_readPending) _changedWhileUnread[key] = null;
            if (changed)
                MarkDirty();
        }
        WindowBoundsCleared?.Invoke(key);
    }

    // ═══════════════════════════════════════════════════════════════
    // Logging centralisé
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Dossier de logs (LocalAppData en MSIX, à côté de l'exe sinon).</summary>
    private static string? _logDirectoryOverride;
    public static string LogDirectory => _logDirectoryOverride ?? (IsPackaged
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductIdentity.ConfigFolderName)
        : AppContext.BaseDirectory);

    private static readonly object _logFlushLock = new();

    /// <summary>Tests : appelé juste avant d'écrire une ligne d'error.log, verrou du journal
    /// tenu (simule un disque lent).</summary>
    internal static Action<string>? LogWritePhaseForTests;

    /// <summary>
    /// File unique d'error.log (audit des accès concurrents, T2 et T5) : chaque ligne y entre
    /// datée, avec son dossier, dans l'ordre des appels. Elle n'est retirée et écrite que sous
    /// <c>_logFlushLock</c>, par un seul vidage à la fois sur le pool (<see cref="DrainLogQueue"/>)
    /// ou en synchrone (<see cref="FlushLog"/>) : l'ordre du fichier est celui des appels, et le
    /// fil d'interface n'attend plus le disque pour une ligne ordinaire.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentQueue<(string Dir, string Entry)> _logQueue = new();
    private static int _logDraining;

    private static void EnqueueLogEntry(string logEntry)
    {
        _logQueue.Enqueue((LogDirectory, logEntry));
        if (Interlocked.Exchange(ref _logDraining, 1) == 0)
            ThreadPool.QueueUserWorkItem(static _ => DrainLogQueue());
    }

    private static void DrainLogQueue()
    {
        do
        {
            FlushLog();
            Volatile.Write(ref _logDraining, 0);
            // Une ligne arrivée entre le dernier retrait et la remise à zéro n'a pas relancé
            // de vidage : la reprendre ici, sauf si un autre vidage l'a déjà fait.
        }
        while (!_logQueue.IsEmpty && Interlocked.Exchange(ref _logDraining, 1) == 0);
    }

    /// <summary>
    /// Écrit maintenant, sur le fil appelant et dans l'ordre, toutes les lignes en file :
    /// ligne critique, fin de l'application, tests. Attend une écriture du journal en cours.
    /// </summary>
    public static void FlushLog()
    {
        lock (_logFlushLock) WriteQueuedLogEntriesLocked();
    }

    /// <summary>Retire et écrit les lignes en file. Sous <c>_logFlushLock</c>, sauf en dernier
    /// recours dans <see cref="LogFatal"/>. Chaque ligne dans son try : une erreur d'écriture
    /// ne bloque ni la suite de la file ni les vidages suivants.
    ///
    /// Un lecteur qui ouvre error.log sans partage d'écriture (antivirus, indexeur, éditeur,
    /// test qui relit le fichier) fait échouer l'ajout par une violation de partage passagère.
    /// L'écriture se fait désormais hors du fil appelant, donc plus souvent pendant une telle
    /// lecture : la ligne est réessayée au lieu d'être jetée. Le budget est par vidage
    /// (<see cref="LogShareRetryBudget"/> attentes de <see cref="LogShareRetryDelayMs"/> ms) :
    /// un fichier tenu longtemps coûte au plus 200 ms au vidage, puis ses lignes sont perdues
    /// comme avant.</summary>
    private static void WriteQueuedLogEntriesLocked()
    {
        int retriesLeft = LogShareRetryBudget;
        while (_logQueue.TryDequeue(out var item))
        {
            try { LogWritePhaseForTests?.Invoke(item.Entry); } catch { }
            while (true)
            {
                try
                {
                    Directory.CreateDirectory(item.Dir);
                    File.AppendAllText(Path.Combine(item.Dir, "error.log"), item.Entry);
                    break;
                }
                catch (IOException) when (retriesLeft > 0)
                {
                    retriesLeft--;
                    Thread.Sleep(LogShareRetryDelayMs);
                }
                catch { break; }
            }
        }
    }

    private const int LogShareRetryBudget = 20;
    private const int LogShareRetryDelayMs = 10;

    /// <summary>
    /// Ligne FATAL du gestionnaire d'exception non rattrapée (n'importe quel fil, processus
    /// condamné) : écrite en synchrone après toutes les lignes encore en file, qui mourraient
    /// sinon avec le processus (T5, P5). Le verrou est attendu 1 s au plus : une écriture
    /// bloquée ne doit pas faire perdre la ligne FATAL.
    /// </summary>
    public static void LogFatal(string safeMessage)
    {
        _logQueue.Enqueue((LogDirectory, $"[{DateTime.Now:s}] FATAL: {safeMessage}\n"));
        bool locked = false;
        try
        {
            Monitor.TryEnter(_logFlushLock, 1000, ref locked);
            WriteQueuedLogEntriesLocked();
        }
        finally
        {
            if (locked) Monitor.Exit(_logFlushLock);
        }
    }

    /// <summary>
    /// Audit sécu 2026-05 SEV-A1-01 : sanitize une exception pour log.
    /// Limite à GetType().Name + premier segment de Message (avant \n ou pattern de path).
    /// Évite de logger stack traces complètes, paths absolus user, données partielles.
    /// </summary>
    internal static string SanitizeException(Exception? ex)
    {
        if (ex == null) return "null";
        var typeName = ex.GetType().Name;
        var msg = ex.Message ?? "";
        var nlIdx = msg.IndexOf('\n');
        if (nlIdx >= 0) msg = msg.Substring(0, nlIdx);
        if (msg.Length > 200) msg = msg.Substring(0, 200) + "…";
        // Masquer les paths user (C:\Users\<NomUser>\…)
        msg = System.Text.RegularExpressions.Regex.Replace(msg,
            @"[A-Z]:\\Users\\[^\\]+\\", @"<UserDir>\");
        return $"{typeName}: {msg}";
    }

    /// <summary>Écrit une entrée dans error.log de façon asynchrone et non-bloquante. Sanitize l'exception (SEV-A1-01).</summary>
    public static void Log(string context, Exception ex)
    {
        _lastErrorUtc = DateTime.UtcNow; // garde de la sollicitation d'avis
        EnqueueLogEntry($"[{DateTime.Now:s}] {context}: {SanitizeException(ex)}\n");
    }

    /// <summary>
    /// Loggue un event de la couche compatibilité (changement de mode foreground,
    /// désactivation auto anti-cheat, override cleanup, etc.) dans error.log.
    /// **Aucune frappe utilisateur ne doit jamais être loguée**, conformément à la
    /// politique de confidentialité onboarding. Ne loguer QUE des événements agrégés
    /// sans contenu (process name, mode, action) — pas de codepoint, pas de char.
    /// Mise en file, écrite sur le pool (audit des accès concurrents, T2) : appelée sur le fil
    /// d'interface à chaque changement de mode au premier plan, elle y attendait le disque sous
    /// le verrou du journal, donc gelait le hook clavier. Un événement qui doit survivre à un
    /// plantage passe par <see cref="LogCompatCriticalEvent"/>.
    /// </summary>
    public static void LogCompatEvent(string eventName, string details) =>
        EnqueueLogEntry($"[{DateTime.Now:s}] {eventName}: {details}\n");

    /// <summary>
    /// Audit sécu 2026-05 SEV-A1-02 : anonymise un nom de process via HMAC-SHA256
    /// avec sel local persisté. Format `hash:8hex`. Permet de tracer des patterns
    /// (même process apparaît N fois) sans révéler le nom dans error.log.
    /// </summary>
    public static string AnonymizeProcessName(string? procName)
    {
        if (string.IsNullOrEmpty(procName)) return "<none>";
        var salt = GetOrCreateLogSalt();
        using var hmac = new HMACSHA256(salt);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(procName.ToLowerInvariant()));
        return "hash:" + Convert.ToHexString(hashBytes, 0, 4).ToLowerInvariant();
    }

    private static byte[]? _logSalt;
    private static byte[] GetOrCreateLogSalt()
    {
        if (_logSalt != null) return _logSalt;
        var saltStr = GetString("_compat_log_salt");
        if (string.IsNullOrEmpty(saltStr) || saltStr.Length < 22)
        {
            // 16 bytes random → ~24 chars base64. Persisté dans config.json pour stabilité
            // entre lancements (même hash pour le même process → suivi de patterns).
            saltStr = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            SetString("_compat_log_salt", saltStr);
        }
        _logSalt = Encoding.UTF8.GetBytes(saltStr);
        return _logSalt;
    }

    /// <summary>
    /// Variante synchrone de <see cref="LogCompatEvent"/> pour les events critiques
    /// (AntiCheatDetected, OverrideInvalidCleanup) qui doivent être persistés même si
    /// l'app crash juste après. Mise en file puis vidage synchrone (<see cref="FlushLog"/>) :
    /// les lignes journalisées avant elle sont écrites avant elle, dans l'ordre (T5).
    /// </summary>
    public static void LogCompatCriticalEvent(string eventName, string details)
    {
        _logQueue.Enqueue((LogDirectory, $"[{DateTime.Now:s}] {eventName}: {details}\n"));
        FlushLog();
    }

    /// <summary>
    /// Audit du 25/09 (M-07) : variante pour le rappel du hook, qui ne doit jamais attendre le
    /// disque. Depuis l'audit des accès concurrents (T2), toute ligne ordinaire suit ce chemin :
    /// datée ici, mise en file, écrite sur le pool dans l'ordre d'arrivée. Un plantage juste
    /// après peut la perdre, sauf s'il passe par <see cref="LogFatal"/>.
    /// </summary>
    public static void LogCompatEventDeferred(string eventName, string details) =>
        EnqueueLogEntry($"[{DateTime.Now:s}] {eventName}: {details}\n");

    /// <summary>
    /// Trace de debug pour le crash post-Reset (cf. project_crash_lm_post_reset_hypothesis).
    /// Compilee en no-op en Release via [Conditional("DEBUG")] : aucun appel n'est emis dans
    /// le binaire AOT distribue. Utilise pour tracer les etapes du ctor de LearningModule
    /// et de LaunchLearningModule afin de pouvoir diagnostiquer un crash a partir d'error.log.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void LogCrashTraceDebug(string detail) => LogCompatCriticalEvent("CrashTrace", detail);

    private const long LogRotationSizeBytes = 5 * 1024 * 1024; // 5 Mo

    /// <summary>
    /// Vérifie la taille de error.log au démarrage. Si > 5 Mo, renomme en error.log.old
    /// (écrase l'éventuel précédent .old). Une seule rotation, pas d'archivage cumulatif.
    /// À appeler une fois au démarrage de l'app, avant toute écriture de log.
    /// </summary>
    public static void RotateLogIfNeeded()
    {
        // Sous le verrou du journal (T5) : une ligne en cours d'écriture ne part pas dans le
        // fichier qu'on renomme, et aucune n'est écrite entre le test de taille et le renommage.
        lock (_logFlushLock)
        try
        {
            var logFile = Path.Combine(LogDirectory, "error.log");
            if (!File.Exists(logFile)) return;
            var info = new FileInfo(logFile);
            if (info.Length < LogRotationSizeBytes) return;

            var oldFile = Path.Combine(LogDirectory, "error.log.old");
            if (File.Exists(oldFile))
                File.Delete(oldFile);
            File.Move(logFile, oldFile);
        }
        catch { /* best-effort, ne pas bloquer le démarrage */ }
    }

    private static bool GetBool(string key)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (_cache!.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.True)
                return true;
            return false;
        }
    }

    private static bool GetBoolDefaultTrue(string key)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return !_cache!.TryGetValue(key, out var val) || val.ValueKind != JsonValueKind.False;
        }
    }

    private static void SetBool(string key, bool value)
    {
        lock (_lock)
        {
            EnsureLoaded();
            using var doc = JsonDocument.Parse(value ? "true" : "false");
            SetValueLocked(key, doc.RootElement.Clone());
        }
    }

    private static string? GetString(string key)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (_cache!.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
            return null;
        }
    }

    private static uint GetUInt(string key)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (_cache!.TryGetValue(key, out var val) &&
                val.ValueKind == JsonValueKind.Number &&
                val.TryGetUInt32(out uint number))
                return number;
            return 0;
        }
    }

    private static void SetString(string key, string value)
    {
        lock (_lock)
        {
            EnsureLoaded();
            // Sérialisation sûre via JsonEncodedText (échappe guillemets, backslash, etc.)
            var encoded = JsonEncodedText.Encode(value);
            using var doc = JsonDocument.Parse($"\"{encoded}\"");
            SetValueLocked(key, doc.RootElement.Clone());
        }
    }

    private static void SetUInt(string key, uint value)
    {
        lock (_lock)
        {
            EnsureLoaded();
            using var doc = JsonDocument.Parse(value.ToString());
            SetValueLocked(key, doc.RootElement.Clone());
        }
    }

    /// <summary>Tableau JSON d'entiers ; les éléments d'un autre type sont ignorés.</summary>
    private static List<int> GetIntList(string key)
    {
        var values = new List<int>();
        lock (_lock)
        {
            EnsureLoaded();
            if (_cache!.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in val.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int number))
                        values.Add(number);
                }
            }
        }
        return values;
    }

    /// <summary>Tableau JSON de chaînes ; les éléments d'un autre type sont ignorés.</summary>
    private static List<string> GetStringList(string key)
    {
        var values = new List<string>();
        lock (_lock)
        {
            EnsureLoaded();
            if (_cache!.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in val.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
                        values.Add(text);
                }
            }
        }
        return values;
    }

    private static void SetIntList(string key, IEnumerable<int> values) =>
        SetArray(key, writer => { foreach (int value in values) writer.WriteNumberValue(value); });

    private static void SetStringList(string key, IEnumerable<string> values) =>
        SetArray(key, writer => { foreach (string value in values) writer.WriteStringValue(value); });

    // Sérialisation par Utf8JsonWriter : pas de JsonSerializer par réflexion sous NativeAOT.
    private static void SetArray(string key, Action<Utf8JsonWriter> writeItems)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            writeItems(writer);
            writer.WriteEndArray();
        }
        lock (_lock)
        {
            EnsureLoaded();
            using var doc = JsonDocument.Parse(buffer.ToArray());
            SetValueLocked(key, doc.RootElement.Clone());
        }
    }

    // config.json corrompu, mis de côté au chargement : message à montrer une fois.
    private static bool _quarantineNoticePending;

    /// <summary>
    /// Vrai une seule fois après la mise de côté d'un config.json corrompu : le tray dit
    /// alors, une fois, que les réglages ont été remis à zéro et qu'une copie est gardée.
    /// </summary>
    internal static bool TakeQuarantineNotice()
    {
        lock (_lock)
        {
            EnsureLoaded();
            bool pending = _quarantineNoticePending;
            _quarantineNoticePending = false;
            return pending;
        }
    }

    private static void EnsureLoaded()
    {
        if (_cache != null)
        {
            if (_readPending) TryRereadLocked();
            return;
        }
        _cache = new Dictionary<string, JsonElement>();
        _loadFailed = false;
        _readPending = false;
        _changedWhileUnread.Clear();
        _compatChangedWhileUnread.Clear();
        try
        {
            if (File.Exists(_configPath))
                (_cache, _compatibilityCache, _compatibilityUnknown) = ParseConfig(FileQuarantine.ReadAllTextWithRetries(_configPath));
        }
        catch (Exception ex) when (FileQuarantine.IsReadFailure(ex))
        {
            // Lecture impossible malgré les essais courts (verrou d'un antivirus ou d'une
            // sauvegarde, droits) : le fichier est sans doute intact. Réglages par défaut en
            // attendant, aucune écriture sur ce fichier (WriteFile refuse d'écraser un fichier non
            // chargé), et relecture au prochain accès (CE-T2, choix d'Antoine du 16 novembre 2026) :
            // avant, l'accord et les réglages restaient perdus pour toute la session.
            _loadFailed = true;
            _readPending = true;
            Log("ConfigManager.EnsureLoaded", ex);
        }
        catch (Exception ex) when (FileQuarantine.IsCorruption(ex))
        {
            // Contenu corrompu (report du 24/09) : l'ancien refus d'écrire bloquait toute
            // sauvegarde pour toujours ; l'accord n'était jamais mémorisé et l'application
            // restait inactive à chaque démarrage. Le fichier est mis de côté, jamais
            // supprimé, et l'on repart de zéro : accord et réglages sont à refaire, l'accueil
            // revient. Renommage raté : même règle qu'une lecture impossible.
            Log("ConfigManager.EnsureLoaded", ex);
            _cache.Clear();
            _compatibilityCache = null;
            _compatibilityUnknown = null;
            if (FileQuarantine.TryMoveAside(_configPath, "ConfigManager.Quarantine") != null)
                _quarantineNoticePending = true;
            else if (File.Exists(_configPath))
                _loadFailed = true;
            // Sinon le fichier a disparu entre la lecture et le renommage : une autre instance
            // l'a déjà mis de côté (ME-09, C4.5). Il n'y a plus de fichier à protéger, l'état
            // « chargement échoué » collé bloquerait toute écriture pour rien, et la notice est
            // celle de l'autre instance.
        }
        catch (IOException ex)
        {
            // Fichier disparu entre File.Exists et la lecture : comme une installation neuve.
            Log("ConfigManager.EnsureLoaded", ex);
        }

        // AF-06 : temporaires laissés par une coupure d'un lancement précédent.
        if (!_loadFailed) FileQuarantine.RemoveOrphanTemps(_configPath);

        // Installation neuve (aucun fichier, ou fichier corrompu mis de côté) : la langue
        // initiale suit l'interface Windows et s'écrit à la première écriture — un défaut
        // qui changerait d'une session à l'autre serait pire qu'un choix. Une config
        // existante sans clé garde le « fr » historique : une mise à jour ne bascule jamais
        // de langue (décision d'Antoine, 2026-08-24). Jamais après un échec de lecture :
        // WriteFile refuse déjà d'écraser un fichier existant non chargé.
        if (!_loadFailed && _cache.Count == 0 && !File.Exists(_configPath))
        {
            string derived = DerivedAppLanguage();
            using var doc = JsonDocument.Parse($"\"{derived}\"");
            _cache["appLanguage"] = doc.RootElement.Clone();
            MarkDirty();
        }
    }

    /// <summary>Analyse le contenu de config.json : réglages, et sous-objet compatibility
    /// (processus → "forceOn"/"forceOff"). Lève <see cref="JsonException"/> sur une racine qui
    /// n'est pas un objet.</summary>
    private static (Dictionary<string, JsonElement> Cache, Dictionary<string, string>? Compatibility,
        Dictionary<string, JsonElement>? CompatibilityUnknown) ParseConfig(string json)
    {
        var cache = new Dictionary<string, JsonElement>();
        Dictionary<string, string>? compatibility = null;
        Dictionary<string, JsonElement>? unknown = null;
        using var doc = FileQuarantine.ParseUserFile(json); // AF-01
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("La configuration doit être un objet JSON.");
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "compatibility" && prop.Value.ValueKind == JsonValueKind.Object)
            {
                compatibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in prop.Value.EnumerateObject())
                {
                    // Valeur d'une version plus récente : gardée telle quelle et réécrite (audit des fichiers).
                    var v = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
                    if (v == "forceOn" || v == "forceOff")
                        compatibility[entry.Name] = v;
                    else
                    {
                        unknown ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                        unknown[entry.Name] = entry.Value.Clone();
                    }
                }
                continue;
            }
            cache[prop.Name] = prop.Value.Clone();
        }
        return (cache, compatibility, unknown);
    }

    /// <summary>
    /// CE-T2 : relit, en un seul essai, un config.json resté illisible au chargement. Sous
    /// <c>_lock</c>. Encore tenu : rien ne change, sans nouvelle ligne de journal. Lu : le
    /// fichier devient la base, les réglages changés pendant la panne passent par-dessus, et
    /// l'écriture reprend. Corrompu entre-temps : même règle qu'au chargement (mise de côté, puis
    /// réglages de la session).
    /// </summary>
    private static void TryRereadLocked()
    {
        Dictionary<string, JsonElement> cache;
        Dictionary<string, string>? compatibility;
        Dictionary<string, JsonElement>? unknown = null;
        try
        {
            (cache, compatibility, unknown) = ParseConfig(File.ReadAllText(_configPath));
        }
        catch (Exception ex) when (FileQuarantine.IsReadFailure(ex))
        {
            return; // toujours tenu : le premier échec est déjà journalisé
        }
        catch (Exception ex) when (FileQuarantine.IsCorruption(ex))
        {
            Log("ConfigManager.Reread", ex);
            if (FileQuarantine.TryMoveAside(_configPath, "ConfigManager.Quarantine") != null)
                _quarantineNoticePending = true;
            else if (File.Exists(_configPath))
            {
                _readPending = false; // quarantaine ratée : collante, comme au chargement
                return;
            }
            cache = new Dictionary<string, JsonElement>();
            compatibility = null;
        }
        catch (IOException)
        {
            // Fichier disparu entre-temps : plus rien à protéger, les réglages de la session restent.
            cache = new Dictionary<string, JsonElement>();
            compatibility = null;
        }

        int kept = _changedWhileUnread.Count + _compatChangedWhileUnread.Count;
        foreach (var (key, value) in _changedWhileUnread)
        {
            if (value is { } element) cache[key] = element;
            else cache.Remove(key);
        }
        foreach (var (process, mode) in _compatChangedWhileUnread)
        {
            compatibility ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (mode == null) compatibility.Remove(process);
            else compatibility[process] = mode;
        }
        // Une valeur connue posée pendant la panne remplace la valeur inconnue relue.
        foreach (var (process, _) in _compatChangedWhileUnread) unknown?.Remove(process);
        _cache = cache;
        _compatibilityCache = compatibility;
        _compatibilityUnknown = unknown;
        _loadFailed = false;
        _readPending = false;
        _changedWhileUnread.Clear();
        _compatChangedWhileUnread.Clear();
        LogCompatEventDeferred("ConfigReadRecovered", $"changesKept={kept}");
        if (kept > 0) MarkDirty();
    }

    /// <summary>
    /// Chemin du fichier temporaire d'une sauvegarde atomique (AG130-11 b).
    ///
    /// L'ancien chemin etait fixe (<c>config.json.tmp</c>), donc partage par deux
    /// instances du meme compte - une console et un Bureau a distance - qui ecrivaient
    /// dedans en meme temps. L'IOException de collision etait avalee : dernier ecrivain
    /// gagnant, sans trace. Le PID suffit a les separer.
    /// </summary>
    internal static string BuildTempPath(string finalPath, int processId) =>
        $"{finalPath}.{processId}.tmp";

    // ═══════════════════════════════════════════════════════════════
    // Écriture groupée, hors du fil du hook (audit du 25/09, A-05 et F-18)
    // ═══════════════════════════════════════════════════════════════
    //
    // Chaque setter réécrivait tout config.json, FlushFileBuffers compris (3,3 ms
    // mesurées), sur le fil qui sert aussi le rappel du hook clavier : 4 écritures pour
    // une couche cochée au menu, 7 pour un enregistrement des Couches maintenables, une
    // par WM_SIZE pendant un redimensionnement du clavier virtuel. Un setter ne fait plus
    // que poser la valeur et marquer le cache ; une écriture unique part du pool de
    // threads SaveDelayMilliseconds après le dernier changement. La fermeture de l'app,
    // WM_QUERYENDSESSION, WM_ENDSESSION et les changements de session écrivent aussi ce
    // qui attend (TrayApplication).
    //
    // Deux verrous, toujours pris dans cet ordre : _fileLock (une écriture à la fois,
    // dans l'ordre des instantanés), puis _lock (le cache). L'instantané est sérialisé
    // sous _lock, sans aucune I/O ; le disque est écrit hors de _lock. Un appelant qui
    // tient _lock ne doit donc jamais appeler Flush : la garde de Flush le refuse.

    private static bool _dirty;
    private static readonly object _fileLock = new();
    private static System.Threading.Timer? _saveTimer;

    /// <summary>Délai de regroupement des écritures, en millisecondes. Négatif : aucune
    /// écriture différée, seul <see cref="Flush"/> écrit (hook de test).</summary>
    internal static int SaveDelayMilliseconds = 400;

    /// <summary>Écritures de config.json réussies depuis le lancement. Instrumentation
    /// des témoins : combien d'écritures coûte un geste.</summary>
    internal static int DiskWriteCount;

    /// <summary>Hook de test : appelé pendant l'écriture disque, hors de <c>_lock</c>.</summary>
    internal static Action? DiskWritePhaseForTests;

    /// <summary>Délais des nouveaux essais après une écriture ratée : 1 s, 5 s, 30 s, puis
    /// toutes les 5 min tant que le cache reste à écrire. Un fichier tenu par un lecteur
    /// (antivirus, indexation, script) fait échouer File.Replace ; sans nouvel essai, le
    /// réglage attendait le suivant ou la sortie de l'application (R58, 29/09).</summary>
    internal static readonly int[] RetryDelaysMilliseconds = { 1_000, 5_000, 30_000, 300_000 };

    /// <summary>Écritures ratées d'affilée, sous <c>_lock</c> ; 0 hors d'un échec.</summary>
    private static int _failedWrites;

    /// <summary>Numéro du dernier <see cref="MarkDirty"/>, sous <c>_lock</c>. <see cref="Flush"/>
    /// le relève avec son instantané : s'il a changé à la fin d'une écriture ratée, un réglage
    /// a relancé le cycle normal pendant l'écriture, et l'échec ne le remplace pas (audit des
    /// accès concurrents, CM6).</summary>
    private static int _dirtyGeneration;

    /// <summary>Hook de test : reçoit chaque délai armé, à la place de la minuterie. Le
    /// test déclenche lui-même l'écriture par <see cref="Flush"/>.</summary>
    internal static Action<int>? SaveSchedulerForTests;

    /// <summary>Délai du nouvel essai après <paramref name="failures"/> échecs d'affilée.</summary>
    internal static int RetryDelayAfter(int failures) =>
        RetryDelaysMilliseconds[Math.Clamp(failures, 1, RetryDelaysMilliseconds.Length) - 1];

    /// <summary>Pose une valeur dans le cache, sous <c>_lock</c>. Une valeur identique ne
    /// marque rien : refermer une fenêtre sans rien changer n'écrit plus le fichier (F-18).</summary>
    private static void SetValueLocked(string key, JsonElement value)
    {
        if (_cache!.TryGetValue(key, out var current) && current.ValueKind == value.ValueKind
            && current.GetRawText() == value.GetRawText())
            return;
        _cache[key] = value;
        if (_readPending) _changedWhileUnread[key] = value;
        MarkDirty();
    }

    /// <summary>Marque le cache à écrire et réarme l'écriture groupée. Appelé sous
    /// <c>_lock</c> : aucune I/O.</summary>
    private static void MarkDirty()
    {
        _dirty = true;
        _dirtyGeneration++;
        _failedWrites = 0; // un nouveau réglage relance le cycle normal
        if (SaveDelayMilliseconds < 0) return;
        ArmSaveLocked(SaveDelayMilliseconds);
    }

    /// <summary>Arme l'écriture différée dans <paramref name="delayMilliseconds"/>. Sous
    /// <c>_lock</c> : aucune I/O. Écritures différées coupées (<c>SaveDelayMilliseconds</c>
    /// négatif) : rien, sauf la minuterie des tests.</summary>
    private static void ArmSaveLocked(int delayMilliseconds)
    {
        if (SaveSchedulerForTests is { } planifier)
        {
            planifier(delayMilliseconds);
            return;
        }
        if (SaveDelayMilliseconds < 0) return;
        _saveTimer ??= new System.Threading.Timer(static _ => FlushInBackgroundCore(), null,
            Timeout.Infinite, Timeout.Infinite);
        _saveTimer.Change(delayMilliseconds, Timeout.Infinite);
    }

    /// <summary>Écrit ce qui attend sur le pool de threads, sans attendre (changement de
    /// session). N'écrit rien si le cache n'a pas changé.</summary>
    public static void FlushInBackground() =>
        ThreadPool.QueueUserWorkItem(static _ => FlushInBackgroundCore());

    private static void FlushInBackgroundCore()
    {
        // Pool de threads : une exception non rattrapée ici terminerait le processus.
        try { Flush(); }
        catch (Exception ex) { Log("ConfigManager.FlushInBackground", ex); }
    }

    /// <summary>
    /// Écrit config.json maintenant, sur le fil appelant, si le cache a changé depuis la
    /// dernière écriture réussie : fermeture de l'app, WM_QUERYENDSESSION, écriture
    /// différée, tests. Une écriture ratée laisse le cache marqué, la suivante réessaie.
    /// </summary>
    public static void Flush()
    {
        // Prendre _fileLock en tenant _lock inverserait l'ordre des verrous : interblocage
        // avec une écriture différée qui tient _fileLock et attend _lock.
        if (Monitor.IsEntered(_lock))
            throw new InvalidOperationException("ConfigManager.Flush appelé sous verrou.");

        lock (_fileLock)
        {
            string path;
            byte[] content;
            bool loadFailed;
            string? failureContext;
            int generation;
            lock (_lock)
            {
                // CE-T2 : un fichier resté illisible est relu avant d'écrire, fermeture comprise
                // (FlushFinal) ; sinon les réglages de la panne n'étaient jamais écrits.
                if (_readPending) TryRereadLocked();
                if (!_dirty || _cache == null) return;
                generation = _dirtyGeneration;
                content = SerializeLocked();
                path = _configPath;
                loadFailed = _loadFailed;
                _dirty = false;
                // Une ligne d'error.log par palier : l'échec qui ouvre un palier se
                // journalise, les essais suivants du dernier palier (5 min) non.
                failureContext = _failedWrites < RetryDelaysMilliseconds.Length
                    ? $"ConfigManager.Save (nouvel essai dans {RetryDelayAfter(_failedWrites + 1) / 1000} s)"
                    : null;
            }

            bool written = WriteFile(path, content, loadFailed, failureContext);
            lock (_lock)
            {
                // Même fichier seulement : un test a pu rediriger la config entre-temps.
                if (path != _configPath) return;
                if (written)
                {
                    _failedWrites = 0;
                }
                else
                {
                    // Le cache reste à écrire, et l'écriture se réessaie seule.
                    _dirty = true;
                    // Un réglage changé pendant l'écriture a déjà relancé le cycle normal
                    // (MarkDirty : compteur à zéro, minuterie au délai normal) : l'échec de
                    // l'instantané précédent ne le remplace pas par un palier d'échec (CM6).
                    if (generation == _dirtyGeneration)
                    {
                        _failedWrites++;
                        ArmSaveLocked(RetryDelayAfter(_failedWrites));
                    }
                }
            }
        }
    }

    /// <summary>Nouveaux essais de l'écriture de fermeture, après le premier.</summary>
    internal const int FinalFlushRetries = 3;

    /// <summary>Pause entre deux essais de l'écriture de fermeture : trois essais de plus coûtent
    /// 750 ms au plus, sous le plafond de 1,5 s qui ne doit pas retarder l'arrêt de Windows.</summary>
    internal const int FinalFlushDelayMilliseconds = 250;

    /// <summary>
    /// Écriture de fermeture (ME-09, C4.3) : <see cref="Flush"/>, puis, tant que le cache reste à
    /// écrire, quelques nouveaux essais bornés, sur le fil appelant. Les reprises sur minuterie
    /// (1 s, 5 s…) meurent avec le processus : un verrou d'antivirus tenu au moment de quitter
    /// faisait perdre les derniers réglages (accord d'activation, progression, compteur d'avis)
    /// sans un mot. Rend vrai si plus rien n'attend ; sinon la perte est journalisée, de façon
    /// synchrone, le processus allant mourir. Jamais sous <c>_lock</c> (voir <see cref="Flush"/>).
    /// </summary>
    /// <param name="sleep">Attente entre deux essais, en millisecondes (remplaçable en test).</param>
    internal static bool FlushFinal(Action<int>? sleep = null, int retries = FinalFlushRetries,
        int delayMilliseconds = FinalFlushDelayMilliseconds)
    {
        sleep ??= static ms => Thread.Sleep(ms);
        Flush();
        for (int essai = 0; essai < retries && StillToWrite(); essai++)
        {
            sleep(delayMilliseconds);
            Flush();
        }
        if (!StillToWrite()) return true;
        LogCompatEvent("ConfigFinalWriteLost", $"failedWrites={FailedWritesSnapshot()}");
        return false;
    }

    private static bool StillToWrite()
    {
        lock (_lock) return _dirty && _cache != null;
    }

    private static int FailedWritesSnapshot()
    {
        lock (_lock) return _failedWrites;
    }

    /// <summary>Instantané du cache en JSON, sous <c>_lock</c>, sans I/O : les mêmes
    /// octets qu'avant, écrits en mémoire au lieu du fichier temporaire.</summary>
    private static byte[] SerializeLocked()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var (key, val) in _cache!)
            {
                writer.WritePropertyName(key);
                // AG130-11 (a) : ecrire l'element tel qu'il a ete lu. L'ancien switch
                // repassait tout nombre par double (un entier long y perdait ses
                // derniers chiffres) et serialisait objet, tableau et null en CHAINE :
                // {"x":{"a":1}} ressortait en "x":"{\"a\":1}". Une cle inconnue
                // survivait donc a la sauvegarde, mais pas sa forme - et la version qui
                // l'avait ecrite ne la relisait plus.
                if (val.ValueKind == JsonValueKind.Undefined) continue;
                val.WriteTo(writer);
            }
            // Sous-objet compatibility (overrides utilisateur par process)
            if (_compatibilityCache?.Count > 0 || _compatibilityUnknown?.Count > 0)
            {
                writer.WritePropertyName("compatibility");
                writer.WriteStartObject();
                if (_compatibilityCache != null)
                    foreach (var (proc, mode) in _compatibilityCache)
                    {
                        writer.WritePropertyName(proc);
                        writer.WriteStringValue(mode);
                    }
                // AF-03 : entrées inconnues, réécrites telles qu'elles ont été lues.
                if (_compatibilityUnknown != null)
                    foreach (var (proc, value) in _compatibilityUnknown)
                    {
                        writer.WritePropertyName(proc);
                        value.WriteTo(writer);
                    }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    /// <summary>Écriture atomique et durable de l'instantané : fichier temporaire par PID,
    /// Flush(true), puis remplacement. Rend false sur un échec à réessayer, journalisé sous
    /// <paramref name="failureContext"/> quand il n'est pas nul.</summary>
    private static bool WriteFile(string path, byte[] content, bool loadFailed, string? failureContext)
    {
        string tempPath = BuildTempPath(path, Environment.ProcessId);
        try
        {
            if (loadFailed && File.Exists(path))
            {
                Log("ConfigManager.Save", new IOException("Sauvegarde ignoree: config.json existant non charge."));
                return true; // rien à réessayer : ce fichier-là n'est jamais écrasé
            }

            var configDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(true);
            }

            DiskWritePhaseForTests?.Invoke();
            if (File.Exists(path))
                File.Replace(tempPath, path, null, true);
            else
                File.Move(tempPath, path);
            Interlocked.Increment(ref DiskWriteCount);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pas critique — on continue sans sauvegarder
            if (failureContext != null) Log(failureContext, ex);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { }
            return false;
        }
    }
}
