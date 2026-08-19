// Statistiques d'usage 100 % locales — v1.1 (arbitrage LLM Council 2026-07-11 : pas de
// télémétrie réseau, aucune donnée ne quitte la machine, cf. Plan implémentation v1.1.md).
using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// Compteurs locaux d'utilisation d'AZERTY Global : date de la première frappe remappée,
/// jours d'utilisation distincts (avec série en cours / record), et compteurs de
/// caractères spéciaux produits grâce au remapping, par catégorie.
///
/// Stockage dans usage-stats.json, à côté de config.json (même dossier, même logique
/// packagé/non packagé que <see cref="ConfigManager"/>).
///
/// Contrat de confidentialité : aucune touche individuelle, aucun texte ni horodatage
/// par frappe n'est jamais enregistré. Seuls des compteurs agrégés et des dates au jour
/// près sont conservés — c'est ce qui permet d'afficher ces statistiques sans jamais
/// les transmettre.
/// </summary>
static class UsageStats
{
    private static string _statsPath = GetStatsPath();
    private static readonly object _lock = new();

    private static bool _loaded;
    private static bool _dirty;

    private static string? _firstRemapDate; // "yyyy-MM-dd", null tant qu'aucune frappe remappée
    private static string? _lastActiveDate; // "yyyy-MM-dd"
    private static int _activeDaysCount;
    private static int _currentStreak;
    private static int _bestStreak;
    // Minutes distinctes avec au moins une frappe remappée, cumulées. Mesure d'intensité
    // (distingue 5 min et 10 h dans une même journée) sans aucune distribution horaire :
    // seul le total est persisté, jamais quelles minutes.
    private static long _totalActiveMinutes;
    private static long _lastActiveMinute; // Ticks/minute de la dernière minute comptée (non persisté)
    private static long _accentedUppercaseCount;
    private static long _frenchTypographyCount;
    private static long _internationalCount;
    private static long _symbolsCount;
    // Défi du jour (v1.2.0) — signaux de cadence 100 % locaux (décision 2026-07-22 :
    // compteurs GLOBAUX d'ouvertures uniquement, jamais le contenu des requêtes ni les
    // caractères insérés) + compteur de séances terminées et date du dernier caractère
    // spécial tapé (signal « N jours sans caractère enrichi »).
    private static long _searchOpenCount;
    private static long _virtualKeyboardOpenCount;
    private static long _challengesCompletedCount;
    private static string? _lastSpecialCharDate; // "yyyy-MM-dd"

    /// <summary>
    /// Hook de test : redirige usage-stats.json vers un fichier temporaire et réinitialise
    /// l'état en mémoire. À n'utiliser que dans le projet de tests via InternalsVisibleTo.
    /// </summary>
    internal static void OverrideStatsPathForTests(string path)
    {
        lock (_lock)
        {
            _statsPath = path;
            _loaded = false;
            _dirty = false;
            ResetInMemoryState();
        }
    }

    private static void ResetInMemoryState()
    {
        _firstRemapDate = null;
        _lastActiveDate = null;
        _activeDaysCount = 0;
        _currentStreak = 0;
        _bestStreak = 0;
        _totalActiveMinutes = 0;
        _lastActiveMinute = 0;
        _accentedUppercaseCount = 0;
        _frenchTypographyCount = 0;
        _internationalCount = 0;
        _symbolsCount = 0;
        _searchOpenCount = 0;
        _virtualKeyboardOpenCount = 0;
        _challengesCompletedCount = 0;
        _lastSpecialCharDate = null;
    }

    private static string GetStatsPath() => Path.Combine(ConfigManager.LogDirectory, "usage-stats.json");

    // ═══════════════════════════════════════════════════════════════
    // Lecture publique (utilisée par UsageStatsWindow)
    // ═══════════════════════════════════════════════════════════════

    public static DateOnly? FirstRemapDate { get { lock (_lock) { EnsureLoaded(); return ParseDate(_firstRemapDate); } } }
    public static int ActiveDaysCount { get { lock (_lock) { EnsureLoaded(); return _activeDaysCount; } } }

    /// <summary>
    /// Série de jours consécutifs « en cours » : vaut 0 si la dernière activité date
    /// d'avant-hier ou plus (série rompue). Le champ interne n'est pas remis à zéro :
    /// RecordActivityLocked repartira de 1 à la prochaine frappe remappée.
    /// </summary>
    public static int CurrentStreak
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                var last = ParseDate(_lastActiveDate);
                if (last == null) return 0;
                var today = DateOnly.FromDateTime(DateTime.Now);
                return last.Value == today || last.Value.AddDays(1) == today ? _currentStreak : 0;
            }
        }
    }
    public static int BestStreak { get { lock (_lock) { EnsureLoaded(); return _bestStreak; } } }
    public static DateOnly? LastActiveDate { get { lock (_lock) { EnsureLoaded(); return ParseDate(_lastActiveDate); } } }
    public static long TotalActiveMinutes { get { lock (_lock) { EnsureLoaded(); return _totalActiveMinutes; } } }
    public static long AccentedUppercaseCount { get { lock (_lock) { EnsureLoaded(); return _accentedUppercaseCount; } } }
    public static long FrenchTypographyCount { get { lock (_lock) { EnsureLoaded(); return _frenchTypographyCount; } } }
    public static long InternationalCount { get { lock (_lock) { EnsureLoaded(); return _internationalCount; } } }
    public static long SymbolsCount { get { lock (_lock) { EnsureLoaded(); return _symbolsCount; } } }

    public static long TotalSpecialCharsCount
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _accentedUppercaseCount + _frenchTypographyCount + _internationalCount + _symbolsCount;
            }
        }
    }

    public static long SearchOpenCount { get { lock (_lock) { EnsureLoaded(); return _searchOpenCount; } } }
    public static long VirtualKeyboardOpenCount { get { lock (_lock) { EnsureLoaded(); return _virtualKeyboardOpenCount; } } }
    public static long ChallengesCompletedCount { get { lock (_lock) { EnsureLoaded(); return _challengesCompletedCount; } } }
    public static DateOnly? LastSpecialCharDate { get { lock (_lock) { EnsureLoaded(); return ParseDate(_lastSpecialCharDate); } } }

    /// <summary>Ouverture de la recherche de caractères (compteur global, aucun contenu).</summary>
    public static void RecordSearchOpened()
    {
        lock (_lock) { EnsureLoaded(); _searchOpenCount++; _dirty = true; }
    }

    /// <summary>Ouverture du clavier virtuel (compteur global, aucun contenu).</summary>
    public static void RecordVirtualKeyboardOpened()
    {
        lock (_lock) { EnsureLoaded(); _virtualKeyboardOpenCount++; _dirty = true; }
    }

    /// <summary>Séance « Défi du jour » terminée.</summary>
    public static void RecordChallengeCompleted()
    {
        lock (_lock) { EnsureLoaded(); _challengesCompletedCount++; _dirty = true; }
    }

    // ═══════════════════════════════════════════════════════════════
    // Enregistrement — appelé depuis KeyMapper.EmitText (thread du hook clavier)
    // ═══════════════════════════════════════════════════════════════

    private enum Category { AccentedUppercase, FrenchTypography, International, Symbol }

    /// <summary>
    /// Analyse un texte émis par le remapping et incrémente les compteurs de la catégorie
    /// correspondante. DOIT rester in-memory uniquement (aucune I/O ici) : le hook clavier
    /// est sur le chemin critique de la frappe. La sauvegarde sur disque est différée
    /// (cf. <see cref="Flush"/>), appelée depuis un timer périodique et à la fermeture.
    /// </summary>
    public static void RecordEmittedText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        lock (_lock)
        {
            EnsureLoaded();

            // Toute émission remappée compte comme activité du jour (cf. plan § 1a :
            // « jours distincts avec au moins une frappe remappée ») — pas seulement
            // les caractères spéciaux, sinon un jour de frappe ordinaire (point direct,
            // ponctuation échangée…) ne serait pas compté.
            bool changed = RecordActivityLocked();

            foreach (char c in text)
            {
                if (!TryCategorize(c, out var bucket)) continue;
                changed = true;
                switch (bucket)
                {
                    case Category.AccentedUppercase: _accentedUppercaseCount++; break;
                    case Category.FrenchTypography: _frenchTypographyCount++; break;
                    case Category.International: _internationalCount++; break;
                    case Category.Symbol: _symbolsCount++; break;
                }
                // Signal Défi du jour : date au jour près du dernier caractère spécial
                // (déjà en cache — aucune I/O, aucun caractère enregistré).
                if (_lastSpecialCharDate != _todayCacheStr && _todayCacheStr != null)
                    _lastSpecialCharDate = _todayCacheStr;
            }

            if (changed)
                _dirty = true;
        }
    }

    /// <summary>
    /// Catégorise un caractère « spécial » (produit grâce au remapping AZERTY Global).
    /// Règle produit (décision 2026-07-16) : on ne compte pas ce que l'AZERTY Windows
    /// traditionnel offre déjà facilement (ASCII, minuscules accentuées françaises
    /// directes ou via touches mortes natives, symboles gravés € £ µ § ° ² ¤ ¨) — les
    /// compteurs mesurent ce qu'AZERTY Global apporte. Exception assumée : les lettres
    /// non françaises comptent même quand une touche morte native les permettait (ñ).
    /// Quatre catégories : majuscules accentuées françaises, typographie française
    /// (guillemets, apostrophe, tirets, ligatures…), caractères internationaux (toute
    /// lettre non française, ponctuations propres à des langues), et symboles
    /// (signes transversaux absents de l'AZERTY trad : ©, →, ½… — filet de sécurité).
    /// </summary>
    private static bool TryCategorize(char c, out Category category)
    {
        if (c <= 127)
        {
            category = default;
            return false;
        }

        // 1. Typographie française (liste explicite ; l'apostrophe ’ passe ici en
        //    priorité même si elle sert aussi à d'autres langues).
        switch (c)
        {
            case '«': case '»': case '‹': case '›': // guillemets français (1er et 2d niveau)
            case '’':                                 // apostrophe typographique
            case '—': case '–': case '‑':   // tirets cadratin, demi-cadratin, insécable (‑)
            case '…':
            case ' ': case ' ':         // espaces insécables (normale, fine)
            case 'Œ': case 'œ': case 'Æ': case 'æ':   // ligatures françaises
                category = Category.FrenchTypography;
                return true;
        }

        if (IsFrenchAccentedUppercase(c))
        {
            category = Category.AccentedUppercase;
            return true;
        }

        // 2. Déjà accessible sur l'AZERTY Windows traditionnel → non compté :
        //    minuscules accentuées françaises (touches directes ou mortes natives ^ ¨)
        //    et symboles gravés (€ AltGr+E, £ Maj+$, µ Maj+*, § Maj+!, ° Maj+parenthèse,
        //    ² direct, ¤ AltGr+$, ¨ tréma espaçant). Testé avant la règle « lettre »
        //    ci-dessous : µ est une lettre pour Unicode.
        if (c is 'à' or 'â' or 'ç' or 'é' or 'è' or 'ê' or 'ë' or 'î' or 'ï' or 'ô' or 'ù' or 'û' or 'ü' or 'ÿ'
              or '€' or '£' or 'µ' or '§' or '°' or '²' or '¤' or '¨')
        {
            category = default;
            return false;
        }

        // 3. Ponctuations propres à certaines langues → internationales, comme les lettres.
        switch (c)
        {
            case '¿': case '¡':                       // espagnol
            case ';':                      // point d'interrogation grec (;)
            case '⸮':                      // point d'interrogation retourné (⸮)
            case '“': case '”': case '‘': case '‚': case '„': case '‟': case '‛': // guillemets anglais/allemands
                category = Category.International;
                return true;
        }

        // 4. Toute autre lettre (latin étendu, grec, API… — ª et º sont des lettres pour
        //    Unicode) et les diacritiques combinants → caractères internationaux.
        if (char.IsLetter(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
        {
            category = Category.International;
            return true;
        }

        // 5. Filet de sécurité : symboles transversaux absents de l'AZERTY traditionnel
        //    (©, →, ½, ¥, ‰, †…) et tout le reste.
        category = Category.Symbol;
        return true;
    }

    private static bool IsFrenchAccentedUppercase(char c) => c is
        'À' or 'Â' or 'Ç' or 'É' or 'È' or 'Ê' or 'Ë' or
        'Î' or 'Ï' or 'Ô' or 'Ù' or 'Û' or 'Ü' or 'Ÿ';

    // Cache du jour courant : appelé à chaque frappe remappée, on évite de reformater
    // la date à chaque fois. La chaîne n'est reconstruite qu'au changement de jour.
    private static DateOnly _todayCache;
    private static string? _todayCacheStr;

    /// <summary>
    /// Met à jour la date de première frappe remappée, la série de jours consécutifs
    /// (streak) et le compteur de jours actifs. Retourne true si l'état persistant a
    /// changé (nouveau jour ou première frappe) ; no-op sinon.
    /// </summary>
    private static bool RecordActivityLocked()
    {
        var now = DateTime.Now;

        // Minute active : première frappe remappée de cette minute → +1 au total.
        // Comparaison d'un entier en cache, négligeable sur le chemin du hook.
        bool minuteCounted = false;
        long minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (minute != _lastActiveMinute)
        {
            _lastActiveMinute = minute;
            _totalActiveMinutes++;
            minuteCounted = true;
        }

        var todayDate = DateOnly.FromDateTime(now);
        if (todayDate != _todayCache || _todayCacheStr == null)
        {
            _todayCache = todayDate;
            _todayCacheStr = todayDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        string today = _todayCacheStr;

        bool firstRemapSet = _firstRemapDate == null;
        _firstRemapDate ??= today;

        if (_lastActiveDate == today)
            return firstRemapSet || minuteCounted; // jour déjà compté

        _currentStreak = _lastActiveDate != null && IsConsecutiveDay(_lastActiveDate, today)
            ? _currentStreak + 1
            : 1;

        _bestStreak = Math.Max(_bestStreak, _currentStreak);
        _activeDaysCount++;
        _lastActiveDate = today;
        return true;
    }

    private static bool IsConsecutiveDay(string previousDate, string today)
    {
        if (!DateOnly.TryParseExact(previousDate, "yyyy-MM-dd", out var prev)) return false;
        if (!DateOnly.TryParseExact(today, "yyyy-MM-dd", out var todayDate)) return false;
        return prev.AddDays(1) == todayDate;
    }

    private static DateOnly? ParseDate(string? s) =>
        s != null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d) ? d : null;

    /// <summary>
    /// Formate un total de minutes actives en texte lisible : « 12 min », « 3 h 05 », « 127 h ».
    /// Au-delà de 100 h, les minutes n'apportent rien — heures seules.
    /// </summary>
    internal static string FormatActiveTime(long minutes)
    {
        if (minutes < 60) return $"{minutes} min";
        long hours = minutes / 60;
        long rest = minutes % 60;
        if (hours >= 100) return L.IsEnglish ? $"{hours} hr" : $"{hours} h";
        return L.IsEnglish ? $"{hours} hr {rest:00} min" : $"{hours} h {rest:00}";
    }

    // ═══════════════════════════════════════════════════════════════
    // Export volontaire (bouton « Copier mes statistiques »)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Construit un bloc de texte lisible résumant l'usage, pour témoignages, retours
    /// pilotes ou mails au support. Aucun envoi automatique — copie presse-papiers
    /// uniquement, à l'initiative explicite de l'utilisateur (cf. UsageStatsWindow).
    /// </summary>
    public static string BuildShareText()
    {
        lock (_lock)
        {
            EnsureLoaded();
            var first = ParseDate(_firstRemapDate);
            if (first == null || _activeDaysCount == 0)
                return L.Stats_ShareJustStarted;

            long total = _accentedUppercaseCount + _frenchTypographyCount + _internationalCount + _symbolsCount;

            string dateText = L.FormatDate(first.Value);

            // Intensité : temps de frappe actif cumulé + moyenne par jour actif (dérivée,
            // pas de compteur supplémentaire). Omise tant qu'elle n'est pas significative.
            string intensityText = "";
            if (_totalActiveMinutes >= 60 && _activeDaysCount > 0)
            {
                long avg = _totalActiveMinutes / _activeDaysCount;
                bool showAvg = avg >= 5 && _activeDaysCount > 1;
                intensityText = L.Stats_ShareIntensity(FormatActiveTime(_totalActiveMinutes), showAvg ? FormatActiveTime(avg) : null);
            }

            string dayWord = L.Stats_ShareDayWord(_activeDaysCount);
            string baseText = L.Stats_ShareBase(dateText, _activeDaysCount, dayWord, intensityText);

            if (total == 0)
                return baseText + ".";

            var details = new List<string>();
            if (_accentedUppercaseCount > 0) details.Add(L.Stats_ShareAccentedDetail(_accentedUppercaseCount));
            if (_frenchTypographyCount > 0) details.Add(L.Stats_ShareTypographyDetail(_frenchTypographyCount));
            if (_internationalCount > 0) details.Add(L.Stats_ShareInternationalDetail(_internationalCount));
            if (_symbolsCount > 0) details.Add(L.Stats_ShareSymbolsDetail(_symbolsCount));

            string detailText = details.Count > 0 ? L.Stats_ShareDetailWrapper(details) : "";
            string charWord = L.Stats_ShareCharWord(total);

            return L.Stats_ShareFull(baseText, total, charWord, detailText);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Persistance — chargement paresseux, sauvegarde différée
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// La collecte est-elle active ? Défaut de canal de la v1.2.0 : éteinte sur le canal
    /// sobre (décision D5 du 2026-08-19), active partout ailleurs — le canal hors package
    /// garde le comportement d'aujourd'hui (D8).
    ///
    /// Le lot C ajoutera par-dessus la politique HKLM <c>UsageStatsEnabled</c> et le réglage
    /// utilisateur, avec la précédence politique &gt; config.json &gt; défaut de canal. Ce
    /// point existe pour que cette couche s'insère à un seul endroit.
    ///
    /// Relue à chaque appel, jamais mise en cache : les tests forcent le canal par un scope,
    /// et un cache statique figerait la valeur au premier test de la suite — c'est
    /// exactement l'incident de dépendance à l'ordre du 2026-08-18.
    /// </summary>
    internal static bool CollectionEnabled => !AppChannel.CurrentIsSober;

    /// <summary>
    /// Charge usage-stats.json en mémoire, à appeler au démarrage depuis le thread UI.
    /// Garantit que la première frappe remappée ne déclenche pas le chargement paresseux :
    /// RecordEmittedText reste sans aucune I/O sur le chemin du hook clavier.
    /// </summary>
    public static void Preload()
    {
        lock (_lock) EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        // Collecte éteinte : on ne lit pas non plus. Les compteurs de la session partent de
        // zéro et y retournent à la fermeture. Afficher des chiffres relus d'un fichier qu'on
        // n'écrit plus contredirait ce que la fenêtre annonce, et ferait dépendre l'affichage
        // d'un reliquat laissé par une installation précédente.
        if (!CollectionEnabled) return;

        try
        {
            if (File.Exists(_statsPath))
            {
                var json = File.ReadAllText(_statsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                // Un JSON valide mais non-objet ("[]", "null"…) ferait jeter TryGetProperty
                // (InvalidOperationException, hors filtre) : le convertir en corruption ordinaire.
                if (root.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Racine JSON inattendue (objet requis).");
                _firstRemapDate = GetStringProp(root, "firstRemapDate");
                _lastActiveDate = GetStringProp(root, "lastActiveDate");
                _activeDaysCount = GetIntProp(root, "activeDaysCount");
                _currentStreak = GetIntProp(root, "currentStreak");
                _bestStreak = GetIntProp(root, "bestStreak");
                _totalActiveMinutes = GetLongProp(root, "totalActiveMinutes");
                _accentedUppercaseCount = GetLongProp(root, "accentedUppercaseCount");
                _frenchTypographyCount = GetLongProp(root, "frenchTypographyCount");
                _internationalCount = GetLongProp(root, "internationalCount");
                _symbolsCount = GetLongProp(root, "symbolsCount");
                _searchOpenCount = GetLongProp(root, "searchOpenCount");
                _virtualKeyboardOpenCount = GetLongProp(root, "virtualKeyboardOpenCount");
                _challengesCompletedCount = GetLongProp(root, "challengesCompletedCount");
                _lastSpecialCharDate = GetStringProp(root, "lastSpecialCharDate");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or FormatException)
        {
            // Fichier absent ou corrompu : redémarrer avec des statistiques vides plutôt
            // que de planter l'app (cf. Plan implémentation v1.1.md § 1d).
            ResetInMemoryState();
            ConfigManager.Log("UsageStats.EnsureLoaded", ex);
        }
    }

    private static string? GetStringProp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetIntProp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;

    private static long GetLongProp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : 0L;

    /// <summary>
    /// Écrit usage-stats.json sur disque si des données ont changé depuis le dernier flush.
    /// À appeler depuis un timer périodique (jamais depuis le hook clavier) et à la
    /// fermeture de l'app. Écriture atomique (fichier temporaire + remplacement), même
    /// pattern que <see cref="ConfigManager"/>.
    /// </summary>
    public static void Flush()
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (!_dirty) return;
            if (SaveLocked())
                _dirty = false;
        }
    }

    private static bool SaveLocked()
    {
        // Collecte éteinte : aucun fichier créé, aucune écriture, pas même un JSON de zéros.
        // Unique porte vers le disque de ce fichier, donc le seul endroit où ce test doit
        // vivre : tout appelant présent ou futur y passe. Le <c>true</c> annonce « plus rien
        // à écrire » et non « écrit » — un false ferait réessayer à chaque flush et laisserait
        // _dirty armé pour toujours.
        if (!CollectionEnabled) return true;

        string tempPath = _statsPath + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(_statsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();
                if (_firstRemapDate != null) writer.WriteString("firstRemapDate", _firstRemapDate);
                if (_lastActiveDate != null) writer.WriteString("lastActiveDate", _lastActiveDate);
                writer.WriteNumber("activeDaysCount", _activeDaysCount);
                writer.WriteNumber("currentStreak", _currentStreak);
                writer.WriteNumber("bestStreak", _bestStreak);
                writer.WriteNumber("totalActiveMinutes", _totalActiveMinutes);
                writer.WriteNumber("accentedUppercaseCount", _accentedUppercaseCount);
                writer.WriteNumber("frenchTypographyCount", _frenchTypographyCount);
                writer.WriteNumber("internationalCount", _internationalCount);
                writer.WriteNumber("symbolsCount", _symbolsCount);
                writer.WriteNumber("searchOpenCount", _searchOpenCount);
                writer.WriteNumber("virtualKeyboardOpenCount", _virtualKeyboardOpenCount);
                writer.WriteNumber("challengesCompletedCount", _challengesCompletedCount);
                if (_lastSpecialCharDate != null) writer.WriteString("lastSpecialCharDate", _lastSpecialCharDate);
                writer.WriteEndObject();
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(_statsPath))
                File.Replace(tempPath, _statsPath, null, true);
            else
                File.Move(tempPath, _statsPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConfigManager.Log("UsageStats.Save", ex);
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
    }
}
