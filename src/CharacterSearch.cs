// Recherche de caractère — fenêtre permettant de trouver comment taper un caractère
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Fenêtre de recherche de caractère positionnée en bas à droite (au-dessus de la barre des tâches).
/// 3 colonnes : caractère | nom | méthode de saisie.
/// Hauteur de ligne dynamique selon le contenu.
/// </summary>
sealed class CharacterSearch : IDisposable
{

    private const uint EN_CHANGE = 0x0300;
    private const uint EM_SETMARGINS = 0x00D3;
    private const int EC_LEFTMARGIN = 0x0001;
    private const int EC_RIGHTMARGIN = 0x0002;

    private const int VK_ESCAPE = 0x1B;
    private const int VK_RETURN = 0x0D;
    private const int VK_UP = 0x26;
    private const int VK_DOWN = 0x28;

    // ── Edit control ──────────────────────────────────────────────
    private const int IDC_SEARCH = 3001;
    private const int IDC_TIMER_COPYFEEDBACK = 3002;

    // ── Mesures de la maquette « Recherche B, partition du choix » (px à 96 DPI) ──
    // Couleurs : PaperTheme (jetons de la refonte 1.3.0).
    private const int BASE_WIN_W = 400;
    private const int BASE_FIELD_H = 60;        // Champ : loupe, saisie
    private const int BASE_FIELD_PAD_X = 20;
    private const int BASE_LOUPE = 18;
    private const int BASE_LOUPE_GAP = 12;
    private const int BASE_EDIT_FONT = 19;
    private const int BASE_ROW_H = 52;          // Ligne de résultat : le nom, puis les touches ; filet de 1 px en plus
    private const int BASE_ROW_GLYPH_LEFT = 8;  // Colonne du caractère : 36 px
    private const int BASE_ROW_GLYPH_W = 36;
    private const int BASE_ROW_GAP = 10;
    private const int BASE_ROW_PAD_RIGHT = 16;
    private const int BASE_ROW_NAME_TOP = 6;    // Ligne du nom : 20 px, puis le geste centré 11 px plus bas
    private const int BASE_ROW_NAME_H = 20;
    private const int BASE_ROW_GESTURE_MID = 38;
    private const int BASE_BAND_H = 36;         // Bande sous la liste : la phrase du geste, filet en plus
    private const int BASE_FOOTER_H = 38;       // Pied, filet compris
    private const int BASE_EMPTY_H = 38;        // Bande d'aide sous le champ vide
    private const int BASE_NO_RESULT_H = 52;
    private const int MAX_RESULTS = 20;
    private const int MAX_VISIBLE_ROWS = 5;

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    // ═══════════════════════════════════════════════════════════════
    // Données de recherche
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Entrée dans l'index des caractères.</summary>
    private sealed class CharEntry
    {
        public string Character { get; set; } = "";
        public string CodePoint { get; set; } = "";
        public string NameFr { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string MethodDisplay { get; set; } = "";
        public bool IsDirectAccess { get; set; }
        public MethodData? Method { get; set; }
        public List<MethodData> Methods { get; set; } = new();

        // Champs pré-normalisés (calculés au chargement, évite NormalizeForSearch à chaque frappe)
        public string NormalizedNameFr { get; set; } = "";
        public string NormalizedNameEn { get; set; } = "";
        public string[][] NormalizedAliasWords { get; set; } = Array.Empty<string[]>();
        public string[][] NormalizedEnglishAliasWords { get; set; } = Array.Empty<string[]>();
        public string[] NormalizedNameFrWords { get; set; } = Array.Empty<string>();
        public string[] NormalizedNameEnWords { get; set; } = Array.Empty<string>();
        public string NormalizedChar { get; set; } = "";
        // Recherche par langue (language-search.json) : noms de langue, puis noms de signe
        // (tons des accents combinants), découpés en mots normalisés.
        public string[][] NormalizedLanguageAliasWords { get; set; } = Array.Empty<string[]>();
        public string[][] NormalizedSignAliasWords { get; set; } = Array.Empty<string[]>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Champs d'instance
    // ═══════════════════════════════════════════════════════════════
    private IntPtr _hWnd;
    private IntPtr _hEdit;
    // N8 (accessibilité 1.3.0) : étiquette jamais affichée qui nomme le champ ; son
    // placeholder est dessiné en GDI, et le champ n'avait aucun nom.
    private IntPtr _hEditLabel;
    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _editSubclassProc;
    private List<CharEntry>? _allEntries; // construit à la première recherche (audit du 25/09, V-04)
    private List<CharEntry> _filteredResults = new();
    private int _totalMatches; // correspondances avant le plafond MAX_RESULTS (audit du 25/09, V-08)
    private int _selectedIndex;
    private int _scrollOffset;
    // Lignes montrées en même temps : cinq au plus, moins quand la zone de travail ne laisse
    // pas la place (150 % sur un petit écran). Recalculé par ResizeToFitResults.
    private int _visibleRows = MAX_VISIBLE_ROWS;
    private float _dpiScale = 1.0f;
    private bool _showCopiedFeedback;
    private string _copiedChar = "";
    private bool _inputPaused;
    private readonly TextInsertionService _insertionService;
    private IntPtr _targetWindow;
    private IntPtr _hEditBgBrush;
    // Audit du 25/09 (V-15) : zone de travail de l'écran retenu à l'ouverture. Chaque frappe
    // redimensionne la fenêtre ; relire l'écran du curseur à chaque fois la faisait changer
    // d'écran si la souris y passait pendant la saisie.
    private Win32.RECT _workArea;

    // Séparateurs pour tokeniser les noms/alias (doit matcher le site : tester-search.js)
    private static readonly char[] SearchSeparators = { ' ', '-', '\'', '\u2019', '(', ')' };

    /// <summary>Code de touche (style Web API) → gravure AZERTY : la table du socle, commune
    /// aux Leçons et au clavier virtuel. « Espace » se traduit à part (<see cref="GetKeyLabel"/>).</summary>
    private static IReadOnlyDictionary<string, string> KeyLabels => Gesture.KeyLegends;

    /// <summary>Événement déclenché quand la sélection dans les résultats change.</summary>
    public event Action<MethodData?>? SelectionChanged;

    /// <summary>Déclenché quand l'insertion directe a dû retomber sur la copie.</summary>
    public event Action<string>? FallbackCopied;
    public event Action? InsertionIncomplete;

    public bool IsVisible => _hWnd != IntPtr.Zero && Win32.IsWindowVisible(_hWnd);

    public void SetInputPaused(bool paused)
    {
        _inputPaused = paused;
        if (paused)
            SelectionChanged?.Invoke(null);
    }

    public CharacterSearch(TextInsertionService insertionService)
    {
        _insertionService = insertionService;
        _wndProcDelegate = WndProcCallback;
        _editSubclassProc = EditSubclassProc;
        CreateWindow();
    }

    // ═══════════════════════════════════════════════════════════════
    // Chargement des données
    // ═══════════════════════════════════════════════════════════════

    private List<CharEntry> LoadCharacterIndex()
    {
        var entries = new List<CharEntry>();
        foreach (var item in CharacterIndex.Shared.Entries)
        {
            var charStr = item.Character;
            var nameFr = item.NameFr;
            var nameEn = item.NameEn;
            var methodData = item.Preferred;
            var entry2 = new CharEntry
            {
                Character = charStr,
                CodePoint = item.CodePoint,
                NameFr = nameFr,
                NameEn = nameEn,
                MethodDisplay = methodData != null ? FormatMethod(methodData) : "",
                IsDirectAccess = item.Recommended?.Type == "direct",
                Method = methodData,
                Methods = item.Methods,
            };

            // Pré-normaliser pour la recherche (évite NormalizeForSearch à chaque frappe)
            entry2.NormalizedNameFr = NormalizeForSearch(nameFr);
            entry2.NormalizedNameEn = NormalizeForSearch(nameEn);
            entry2.NormalizedNameFrWords = entry2.NormalizedNameFr.Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);
            entry2.NormalizedNameEnWords = entry2.NormalizedNameEn.Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);
            entry2.NormalizedChar = NormalizeForSearch(charStr);
            entry2.NormalizedAliasWords = item.FrenchAliases.Select(a =>
                NormalizeForSearch(a).Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries)
            ).ToArray();
            entry2.NormalizedEnglishAliasWords = item.EnglishAliases.Select(a =>
                NormalizeForSearch(a).Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries)
            ).ToArray();
            if (LanguageSearchTable.Shared.ByCharacter.TryGetValue(charStr, out var languageAliases))
            {
                entry2.NormalizedLanguageAliasWords = languageAliases.Languages.Select(a =>
                    NormalizeForSearch(a).Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries)
                ).ToArray();
                entry2.NormalizedSignAliasWords = languageAliases.Signs.Select(a =>
                    NormalizeForSearch(a).Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries)
                ).ToArray();
            }

            entries.Add(entry2);
        }
        return entries;
    }

    /// <summary>Formate une méthode de saisie en texte lisible : une ligne pour une méthode
    /// directe, deux pour une touche morte (l'activation, puis la seconde touche).</summary>
    internal static string FormatMethod(MethodData method)
    {
        if (method.Type == "direct")
        {
            return FormatDirectMethod(method.Key, method.Layer);
        }

        if (method.Type == "deadkey")
        {
            var activation = CharacterIndex.Shared.DeadKeyActivations.TryGetValue(method.DeadKey, out var dk)
                ? FormatDirectMethod(dk.Key, dk.Layer)
                : method.DeadKey;
            var keyLabel = GetKeyLabel(method.Key);
            var afterDk = L.Search_AfterDeadKeyLabel(method.Layer, keyLabel);
            return $"{activation}\n{afterDk}";
        }

        return "";
    }

    /// <summary>Retourne le libellé d'affichage d'une touche : légende physique AZERTY
    /// (inchangée quelle que soit la langue), sauf « Espace » qui est un mot traduit.</summary>
    private static string GetKeyLabel(string key) =>
        key == "Space" ? L.Search_SpaceKeyLabel : KeyLabels.GetValueOrDefault(key, key);

    /// <summary>Formate une méthode directe (couche + touche).</summary>
    private static string FormatDirectMethod(string key, string layer)
    {
        var keyLabel = GetKeyLabel(key);
        return L.Search_LayerKeyLabel(layer, keyLabel);
    }

    // ═══════════════════════════════════════════════════════════════
    // Recherche
    // ═══════════════════════════════════════════════════════════════

    private void Search(string query)
    {
        _filteredResults.Clear();
        _totalMatches = 0;
        _selectedIndex = 0;
        _scrollOffset = 0;

        if (string.IsNullOrWhiteSpace(query))
        {
            ResizeToFitResults();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            AnnounceSelection(notify: false);
            return;
        }

        var scored = ScoreEntries(query);
        _totalMatches = scored.Count;
        foreach (var (entry, _) in scored.Take(MAX_RESULTS))
            _filteredResults.Add(entry);

        ResizeToFitResults();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        // Ne PAS appeler NotifySelectionChanged ici : le highlight ne doit
        // se déclencher que sur Entrée, clic ou navigation flèches. Le lecteur d'écran, lui,
        // entend le premier résultat de la nouvelle liste (A18).
        AnnounceSelection(notify: true);
    }

    /// <summary>
    /// A18 (audit 1.3.0) : la liste est peinte en GDI, sans élément accessible ; le champ,
    /// qui garde le focus, porte donc le choix dans son nom, et le lecteur d'écran relit ce
    /// nom quand il change (EVENT_OBJECT_NAMECHANGE). <paramref name="notify"/> faux pose le
    /// nom sans l'annoncer (champ vidé, fin du « Copié »).
    /// </summary>
    private void AnnounceSelection(bool notify)
    {
        if (_hEdit == IntPtr.Zero) return;
        string? result = null;
        if (_selectedIndex >= 0 && _selectedIndex < _filteredResults.Count)
        {
            var entry = _filteredResults[_selectedIndex];
            result = L.Search_AccessibleResult(_selectedIndex + 1, _filteredResults.Count,
                DisplayGlyph.ForStandaloneMark(entry.Character), GetSentenceName(entry),
                Gesture.PlainSentence(StepsOf(entry)));
        }
        else if (GetEditTextLength() > 0)
            result = L.Search_NoResults;
        Announce(result, notify);
    }

    private void Announce(string? message, bool notify)
    {
        string name = AccessibleFieldName(message);
        if (notify) AccessibleName.TrySetAndNotify(_hEdit, name);
        else AccessibleName.TrySet(_hEdit, name);
    }

    /// <summary>A18 : le nom du champ, suivi de ce qu'il faut entendre ; le nom seul quand il
    /// n'y a rien à dire (celui du STATIC qui le nomme, N8).</summary>
    internal static string AccessibleFieldName(string? message) =>
        string.IsNullOrEmpty(message) ? L.Search_WindowTitle : $"{L.Search_WindowTitle}. {message}";

    /// <summary>
    /// Correspondances d'une requête non vide, triées par score décroissant. Séparé de
    /// <see cref="Search"/>, qui touche à la fenêtre, pour que le classement ait un témoin
    /// (audit du 25/09, V-03 : SearchRankingTests).
    /// </summary>
    private List<(CharEntry entry, int score)> ScoreEntries(string query)
    {
        query = query.Trim();
        var lowerQuery = query.ToLowerInvariant();
        var normalizedQuery = NormalizeForSearch(query);
        var queryWords = normalizedQuery.Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);
        // Synonymes de requête : les noms Unicode disent « capital »/« small », jamais
        // « uppercase »/« lowercase » — sans cela, « é uppercase » ne trouvait rien
        // (constat smoke test 2026-07-16). Le synonyme est une ALTERNATIVE (pas un
        // remplacement) et s'applique dès les préfixes (« up », « upper »…) pour que
        // la recherche incrémentale fonctionne — sans casser les requêtes littérales
        // comme « low line » (U+005F). Aligné sur tester-search.js (site).
        var querySynonyms = new string?[queryWords.Length];
        for (int i = 0; i < queryWords.Length; i++)
        {
            var w = queryWords[i];
            querySynonyms[i] = w.Length >= 2 && "uppercase".StartsWith(w, StringComparison.Ordinal)
                ? "capital"
                : w.Length >= 2 && "lowercase".StartsWith(w, StringComparison.Ordinal)
                    ? "small"
                    : null;
        }
        var originalQueryWords = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Score et tri
        var scored = new List<(CharEntry entry, int score)>();
        foreach (var entry in _allEntries ??= LoadCharacterIndex())
        {
            int score = MatchScore(entry, query, lowerQuery, normalizedQuery, queryWords, querySynonyms, originalQueryWords);
            if (score > 0)
            {
                var resultEntry = ApplyQuerySpecificMethod(entry, normalizedQuery);
                if (!ReferenceEquals(resultEntry, entry))
                    score += 50;
                scored.Add((resultEntry, score));
            }
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));
        return scored;
    }

    /// <summary>Nom affiché dans les résultats, selon la langue de l'UI (fallback croisé si le champ est vide).</summary>
    private static string GetDisplayName(CharEntry entry)
    {
        if (L.IsEnglish)
            return !string.IsNullOrEmpty(entry.NameEn) ? entry.NameEn : entry.NameFr;
        return !string.IsNullOrEmpty(entry.NameFr) ? entry.NameFr : entry.NameEn;
    }

    /// <summary>Nom affiché en casse de phrase (« Lettre majuscule latine Z accent aigu ») : les
    /// noms Unicode arrivent en capitales ; les lettres nommées seules gardent leur casse.</summary>
    private static string GetSentenceName(CharEntry entry)
    {
        string name = VirtualKeyboard.InSentence(GetDisplayName(entry));
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>Normalise un texte pour la recherche (supprime les diacritiques, met en minuscule).</summary>
    private static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var cat = char.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private CharEntry ApplyQuerySpecificMethod(CharEntry entry, string normalizedQuery)
    {
        return (normalizedQuery, entry.Character) switch
        {
            ("circonflexe", "^") => WithPreferredMethod(entry, "direct", "", "KeyI", "AltGr"),
            ("circumflex", "^") => WithPreferredMethod(entry, "direct", "", "KeyI", "AltGr"),
            ("accent circonflexe", "^") => WithPreferredMethod(entry, "deadkey", "dk_circumflex", "Space", "Base"),
            ("circumflex accent", "^") => WithPreferredMethod(entry, "deadkey", "dk_circumflex", "Space", "Base"),
            ("backtick", "`") => WithPreferredMethod(entry, "direct", "", "KeyL", "AltGr"),
            ("accent grave", "`") => WithPreferredMethod(entry, "deadkey", "dk_grave", "Space", "Base"),
            ("grave accent", "`") => WithPreferredMethod(entry, "deadkey", "dk_grave", "Space", "Base"),
            _ => entry
        };
    }

    private CharEntry WithPreferredMethod(CharEntry entry, string type, string deadKey, string key, string layer)
    {
        var method = entry.Methods.FirstOrDefault(candidate =>
            candidate.Type == type &&
            candidate.DeadKey == deadKey &&
            candidate.Key == key &&
            candidate.Layer == layer);

        if (method == null)
            return entry;

        return new CharEntry
        {
            Character = entry.Character,
            CodePoint = entry.CodePoint,
            NameFr = entry.NameFr,
            NameEn = entry.NameEn,
            MethodDisplay = FormatMethod(method),
            IsDirectAccess = type == "direct",
            Method = method,
            Methods = entry.Methods,
            NormalizedNameFr = entry.NormalizedNameFr,
            NormalizedNameEn = entry.NormalizedNameEn,
            NormalizedAliasWords = entry.NormalizedAliasWords,
            NormalizedEnglishAliasWords = entry.NormalizedEnglishAliasWords,
            NormalizedNameFrWords = entry.NormalizedNameFrWords,
            NormalizedNameEnWords = entry.NormalizedNameEnWords,
            NormalizedChar = entry.NormalizedChar,
            NormalizedLanguageAliasWords = entry.NormalizedLanguageAliasWords,
            NormalizedSignAliasWords = entry.NormalizedSignAliasWords,
        };
    }

    /// <summary>Vérifie si tous les mots de la requête matchent des mots du texte (chaque mot de requête
    /// est le début d'un mot du texte). L'ordre des mots n'a pas d'importance.
    /// Si un mot de la requête fait 1 caractère, il doit correspondre exactement (pas de StartsWith)
    /// pour éviter que "a" matche "avec", "aigu", etc.
    /// <paramref name="querySynonyms"/> (aligné sur queryWords, éléments null autorisés) offre
    /// une alternative par mot : « upper… » matche aussi « capital » (cf. Search).
    /// Audit du 25/09 (V-03) : comparaisons ordinales. Les deux côtés sont déjà normalisés
    /// (minuscules, sans diacritiques) ; la comparaison culturelle coûtait 8,5 fois plus
    /// pour le même classement (SearchRankingTests).</summary>
    private static bool AllWordsMatch(string[] queryWords, string?[] querySynonyms, string[] textWords)
    {
        for (int i = 0; i < queryWords.Length; i++)
        {
            var qw = queryWords[i];
            var syn = i < querySynonyms.Length ? querySynonyms[i] : null;
            bool found = false;
            foreach (var tw in textWords)
            {
                if (qw.Length == 1 ? tw == qw : (tw == qw || tw.StartsWith(qw, StringComparison.Ordinal)))
                {
                    found = true;
                    break;
                }
                if (syn != null && (tw == syn || tw.StartsWith(syn, StringComparison.Ordinal)))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>Poids d'un caractère trouvé par le nom d'une langue qui l'utilise.</summary>
    internal const int LANGUAGE_SCORE = 45;

    /// <summary>
    /// Un alias de langue ou de signe répond à la requête quand chaque mot de la requête est un
    /// mot de l'alias, ou le début d'un mot de l'alias à partir de trois lettres : « pul » trouve
    /// « pulaar », mais « ma » ne trouve pas toutes les langues en « ma… » pendant la frappe.
    /// Avec <paramref name="nameFr"/> et <paramref name="nameEn"/>, un mot peut aussi venir du
    /// nom du caractère (règle de <see cref="AllWordsMatch"/>), à condition qu'au moins un mot
    /// vienne de l'alias : « pulaar b » trouve ɓ.
    /// </summary>
    private static bool AnyLanguageAliasMatches(string[][] aliases, string[] queryWords,
        string[]? nameFr, string[]? nameEn)
    {
        if (aliases.Length == 0 || queryWords.Length == 0) return false;
        foreach (var aliasWords in aliases)
        {
            bool all = true, fromAlias = false;
            foreach (var qw in queryWords)
            {
                if (aliasWords.Any(aw => aw == qw || (qw.Length >= 3 && aw.StartsWith(qw, StringComparison.Ordinal))))
                {
                    fromAlias = true;
                    continue;
                }
                if (NameWordMatches(qw, nameFr) || NameWordMatches(qw, nameEn))
                    continue;
                all = false;
                break;
            }
            if (all && fromAlias) return true;
        }
        return false;
    }

    private static bool NameWordMatches(string queryWord, string[]? nameWords)
    {
        if (nameWords == null) return false;
        foreach (var tw in nameWords)
            if (queryWord.Length == 1 ? tw == queryWord : tw.StartsWith(queryWord, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Score de correspondance — identique au site web (tester-modal.js searchCharacters).</summary>
    private static int MatchScore(CharEntry entry, string query, string lowerQuery,
        string normalizedQuery, string[] queryWords, string?[] querySynonyms, string[] originalQueryWords)
    {
        int score = 0;

        // Correspondance exacte du caractère (priorité maximale)
        if (entry.Character == query)
        {
            score = 100;
        }
        // Correspondance caractère insensible à la casse
        else if (entry.Character.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            score = 90;
        }
        // Alias français : tous les mots matchent (pré-normalisés)
        else if (entry.NormalizedAliasWords.Length > 0 && queryWords.Length > 0 &&
            entry.NormalizedAliasWords.Any(aliasWords => AllWordsMatch(queryWords, querySynonyms, aliasWords)))
        {
            score = 80;
        }
        // Alias anglais : tous les mots matchent (pré-normalisés) — recherche bilingue
        // permanente, indépendante de la langue de l'UI (cf. plan i18n Phase 3).
        else if (entry.NormalizedEnglishAliasWords.Length > 0 && queryWords.Length > 0 &&
            entry.NormalizedEnglishAliasWords.Any(aliasWords => AllWordsMatch(queryWords, querySynonyms, aliasWords)))
        {
            score = 80;
        }
        // Nom de signe (language-search.json) : « ton », « tone », « ton haut », « accent
        // grave combinant ». Même poids qu'un alias : le ton passe avant le tonos grec (70).
        else if (AnyLanguageAliasMatches(entry.NormalizedSignAliasWords, queryWords, null, null))
        {
            score = 80;
        }
        // Nom français : tous les mots matchent (pré-normalisé)
        else if (entry.NormalizedNameFrWords.Length > 0 && queryWords.Length > 0)
        {
            if (AllWordsMatch(queryWords, querySynonyms, entry.NormalizedNameFrWords))
                score = 70;
        }

        // Nom anglais : tous les mots matchent (pré-normalisé)
        if (score == 0 && entry.NormalizedNameEnWords.Length > 0 && queryWords.Length > 0)
        {
            if (AllWordsMatch(queryWords, querySynonyms, entry.NormalizedNameEnWords))
                score = 50;
        }

        // Recherche par langue (language-search.json). Poids sous tous les autres : un nom de
        // langue qui est aussi un mot (« four », « gun ») ne passe jamais devant un caractère
        // trouvé par son nom ou ses alias, et le classement des requêtes existantes reste le même.
        if (score == 0 && AnyLanguageAliasMatches(entry.NormalizedLanguageAliasWords, queryWords, null, null))
            score = LANGUAGE_SCORE;
        // Langue et lettre mêlées : « pulaar b », « wolof e ».
        if (score == 0 && queryWords.Length > 1
            && AnyLanguageAliasMatches(entry.NormalizedLanguageAliasWords, queryWords,
                entry.NormalizedNameFrWords, entry.NormalizedNameEnWords))
            score = LANGUAGE_SCORE - 5;

        // Code Unicode (U+xxxx)
        if (score == 0 && lowerQuery.StartsWith("u+", StringComparison.Ordinal) &&
            entry.CodePoint.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            score = 90;
        }

        if (score == 0) return 0;

        // ── Bonus (identiques au site web) ──

        var lowerChar = entry.Character.ToLowerInvariant();

        // Bonus correspondance exacte (+50) ou fallback sans diacritiques (+10) — mutuellement exclusifs
        if (originalQueryWords.Contains(entry.Character) || originalQueryWords.Contains(lowerChar))
            score += 50;
        else if (queryWords.Any(w => entry.NormalizedChar == w))
            score += 10;

        // Bonus massif pour recherche d'un seul caractère exact
        if (originalQueryWords.Length == 1 && originalQueryWords[0].Length == 1 &&
            (entry.Character == originalQueryWords[0] ||
             lowerChar == originalQueryWords[0].ToLowerInvariant()))
            score += 100;

        // Bonus Latin de base (U+0020 à U+007F) — parse défensif (les dk:* ont "DEAD_KEY")
        if (entry.CodePoint.StartsWith("U+", StringComparison.Ordinal)
            && int.TryParse(entry.CodePoint.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out int cpNum)
            && cpNum >= 0x0020 && cpNum <= 0x007F)
            score += 15;

        // Bonus minuscule (plus fréquemment recherché que majuscule)
        if (entry.Character.Length == 1 && char.IsLetter(entry.Character[0])
            && char.IsLower(entry.Character[0]))
            score += 5;

        // Bonus accès direct (méthode recommandée est directe, pas touche morte)
        if (entry.IsDirectAccess)
            score += 10;

        // Bonus si le nom français commence par la requête (match plus spécifique)
        if (entry.NormalizedNameFr.Length > 0 && entry.NormalizedNameFr.StartsWith(normalizedQuery, StringComparison.Ordinal))
            score += 15;

        return score;
    }

    // ═══════════════════════════════════════════════════════════════
    // Fenêtre Win32
    // ═══════════════════════════════════════════════════════════════

    private void CreateWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        var className = ProductIdentity.WindowClass("CharSearch");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate, 0x0003 /* CS_HREDRAW | CS_VREDRAW */,
                arrowCursor: false))
            return;

        // Calculer la position en bas à droite — démarrer avec la hauteur minimale
        CaptureWorkArea();
        int baseH = BASE_FIELD_H + 1 + BASE_EMPTY_H;
        var (x, y) = GetBottomRightPosition(BASE_WIN_W, baseH);

        _hWnd = Win32.CreateWindowExW(
            Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW,
            className,
            L.Search_WindowTitle,
            // A17 (audit 1.3.0) : pas de WS_BORDER, dont le trait système #646464 cernait le
            // papier ; le contour est peint par OnPaint, comme dans les Paramètres.
            Win32.WS_POPUP | Win32.WS_CLIPCHILDREN,
            x, y, BASE_WIN_W, baseH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        NativeWindow.ApplyClassBackground(_hWnd, PaperTheme.Paper);
        // Refonte 1.3.0 : fenêtre papier, cadre DWM clair.
        NativeWindow.ApplyFrame(_hWnd, dark: false);

        // Récupérer le DPI
        _dpiScale = Win32.GetDpiForWindow(_hWnd) / 96.0f;

        // Redimensionner selon le DPI
        {
            int w = Scale(BASE_WIN_W);
            int h = Scale(baseH);
            var (dx, dy) = GetBottomRightPosition(w, h);
            Win32.MoveWindow(_hWnd, dx, dy, w, h, false);
        }

        // N8 (accessibilité 1.3.0) : MSAA et UI Automation nomment un EDIT par le STATIC qui
        // le précède dans l'ordre Z, visible ou non (mesuré le 2026-09-23). Même libellé que
        // le placeholder, sans les points de suspension.
        _hEditLabel = Win32.CreateWindowExW(0, "STATIC", L.Search_WindowTitle,
            Win32.WS_CHILD, 0, 0, 0, 0,
            _hWnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        // Créer le champ de recherche (Edit control)
        _hEdit = Win32.CreateWindowExW(0, "EDIT", "",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.ES_AUTOHSCROLL,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_SEARCH, hInstance, IntPtr.Zero);

        ApplyEditFont();

        // Pas de placeholder natif — on dessine le nôtre dans le sous-classement du champ.

        // Fond papier du champ (WM_CTLCOLOREDIT)
        _hEditBgBrush = Win32.CreateSolidBrush(PaperTheme.Paper);

        // Subclasser l'Edit pour intercepter les touches (Entrée, flèches, Échap)
        Win32.SetWindowSubclass(_hEdit, _editSubclassProc, (UIntPtr)1, IntPtr.Zero);
    }

    // ── Polices (AG Texte, AG Titre : HFONT partagées par AppFonts, jamais détruites) ──
    private FontSpec EditFont => FontSpec.Of(AppFonts.Text, BASE_EDIT_FONT, TypeRamp.Regular, _dpiScale);
    private FontSpec RowGlyphFont => FontSpec.Of(AppFonts.Title, 28, TypeRamp.Semibold, _dpiScale);
    private FontSpec RowNameFont(bool chosen) => FontSpec.Of(AppFonts.Text, 15, chosen ? TypeRamp.Semibold : TypeRamp.Regular, _dpiScale);
    private FontSpec SentenceFont(bool strong) => FontSpec.Of(AppFonts.Text, 15, strong ? TypeRamp.Semibold : TypeRamp.Regular, _dpiScale);
    private FontSpec FooterFont(bool strong) => FontSpec.Of(AppFonts.Text, 13, strong ? TypeRamp.Semibold : TypeRamp.Regular, _dpiScale);

    /// <summary>Police du champ, sans marges internes : la saisie et le texte d'attente
    /// commencent au même pixel. Rappelé à chaque changement de DPI.</summary>
    private void ApplyEditFont()
    {
        if (_hEdit == IntPtr.Zero)
            return;
        Win32.SendMessageW(_hEdit, Win32.WM_SETFONT, EditFont.Handle, (IntPtr)1);
        Win32.SendMessageW(_hEdit, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), IntPtr.Zero);
        ResizeEditControl();
    }

    /// <summary>Le champ, à droite de la loupe, centré dans la bande de 60 px : sa hauteur est
    /// celle de la police, le texte d'une ligne s'y pose donc au milieu.</summary>
    private void ResizeEditControl()
    {
        if (_hEdit == IntPtr.Zero)
            return;

        var hdc = Win32.GetDC(_hEdit);
        var (ascent, descent) = PaperText.Metrics(hdc, EditFont);
        Win32.ReleaseDC(_hEdit, hdc);
        int h = ascent + descent;
        int left = Scale(BASE_FIELD_PAD_X + BASE_LOUPE + BASE_LOUPE_GAP);
        int top = (Scale(BASE_FIELD_H) - h) / 2;
        Win32.MoveWindow(_hEdit, left, top, Scale(BASE_WIN_W) - left - Scale(BASE_FIELD_PAD_X), h, true);
    }

    /// <summary>Retient la zone de travail de l'écran du curseur : à la création, puis à
    /// chaque ouverture (<see cref="Show"/>), jamais pendant la saisie (V-15).</summary>
    private void CaptureWorkArea()
    {
        // Trouver l'écran principal (ou celui où se trouve la souris)
        CursorSource.TryGet(out var pt);
        var hMonitor = Win32.MonitorFromPoint(pt, 0x00000001); // MONITOR_DEFAULTTOPRIMARY
        var mi = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(hMonitor, ref mi);

        // rcWork = zone de travail (exclut la barre des tâches)
        _workArea = mi.rcWork;
    }

    private (int x, int y) GetBottomRightPosition(int winW, int winH)
    {
        int x = _workArea.right - winW - 10;
        int y = _workArea.bottom - winH - 10;
        return (x, y);
    }

    private int Scale(int value) => (int)(value * _dpiScale);

    /// <summary>
    /// Redimensionne la fenêtre selon l'état : champ seul et son aide (vide), « Aucun
    /// résultat », ou champ, lignes, bande de la phrase et pied. La zone cliente fait 400 px
    /// de large (96 DPI), le cadre s'y ajoute. Quand la zone de travail est trop basse, la
    /// liste montre moins de lignes (<see cref="LayoutResults"/>) : le pied ne recouvre rien.
    /// </summary>
    private void ResizeToFitResults()
    {
        // Le cadre non client en plus de la zone cliente (aucun depuis A17 : il est peint).
        Win32.GetWindowRect(_hWnd, out var wr);
        Win32.GetClientRect(_hWnd, out var cr);
        int frameW = (wr.right - wr.left) - (cr.right - cr.left);
        int frameH = (wr.bottom - wr.top) - (cr.bottom - cr.top);

        int clientH;
        if (_filteredResults.Count == 0)
        {
            clientH = Scale(BASE_FIELD_H) + 1 + Scale(GetEditTextLength() > 0 ? BASE_NO_RESULT_H : BASE_EMPTY_H);
        }
        else
        {
            // Jamais plus haute que l'écran retenu, à 10 px du bord.
            int workH = _workArea.bottom - _workArea.top;
            var layout = LayoutResults(_filteredResults.Count, _dpiScale, workH > 0 ? workH - 20 - frameH : 0);
            _visibleRows = layout.Rows;
            // Le choix et le défilement restent valables si le nombre de lignes a changé.
            _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _filteredResults.Count - _visibleRows));
            EnsureVisible(_selectedIndex);
            clientH = layout.ClientH;
        }

        int w = Scale(BASE_WIN_W) + frameW;
        int h = clientH + frameH;
        var (x, y) = GetBottomRightPosition(w, h);
        Win32.MoveWindow(_hWnd, x, y, w, h, true);
    }

    /// <summary>Découpage vertical de la zone cliente quand il y a des résultats.</summary>
    internal readonly record struct ResultsLayout(int Rows, int ListTop, int ListBottom, int BandTop, int FooterTop, int ClientH);

    /// <summary>
    /// Place le champ, la liste, la bande de la phrase et le pied du haut vers le bas, sans que
    /// rien ne se recouvre. La liste montre au plus cinq lignes, moins si <paramref name="maxClientH"/>
    /// (zone cliente la plus haute permise, 0 = sans plafond) ne laisse pas la place ; une
    /// ligne au moins reste visible. Fonction pure : testée à tous les zooms.
    /// </summary>
    internal static ResultsLayout LayoutResults(int resultCount, float scale, int maxClientH)
    {
        int S(int value) => (int)(value * scale);
        int listTop = S(BASE_FIELD_H) + 1;
        int rowStep = S(BASE_ROW_H) + 1;
        int bandH = 1 + S(BASE_BAND_H);
        int footerH = S(BASE_FOOTER_H);

        int rows = Math.Min(resultCount, MAX_VISIBLE_ROWS);
        if (maxClientH > 0)
        {
            int room = maxClientH - listTop - bandH - footerH; // n lignes tiennent dans n * rowStep - 1
            rows = Math.Min(rows, (room + 1) / rowStep);
        }
        rows = Math.Max(rows, Math.Min(1, resultCount));

        int listBottom = listTop + (rows > 0 ? rows * rowStep - 1 : 0);
        int footerTop = listBottom + bandH;
        return new ResultsLayout(rows, listTop, listBottom, listBottom, footerTop, footerTop + footerH);
    }

    // ═══════════════════════════════════════════════════════════════
    // Affichage / Masquage
    // ═══════════════════════════════════════════════════════════════

    public void Toggle(IntPtr preferredTarget = default)
    {
        if (IsVisible)
            Hide();
        else
            Show(preferredTarget);
    }

    public void Show(IntPtr preferredTarget = default)
    {
        // Mémoriser la fenêtre qui recevra l'insertion directe : celle passée par
        // le menu tray (le foreground est déjà le menu) ou le foreground courant.
        IntPtr foregroundBeforeSearch = preferredTarget != IntPtr.Zero
            ? preferredTarget
            : Win32.GetForegroundWindow();
        _targetWindow = ResolveInsertionTarget(foregroundBeforeSearch, _hWnd, _targetWindow,
            Win32.IsWindow, ReadWindowClassName);

        // Signal Défi du jour (v1.2.0) : compteur GLOBAL d'ouvertures uniquement —
        // jamais le contenu des requêtes (décision 2026-07-22).
        UsageStats.RecordSearchOpened();

        // N8 : la langue a pu changer depuis la création (la fenêtre vit tout le processus).
        Win32.SetWindowTextW(_hEditLabel, L.Search_WindowTitle);

        // Adapter la taille selon les résultats actuels, puis repositionner sur l'écran
        // du curseur, retenu jusqu'à la prochaine ouverture (V-15).
        CaptureWorkArea();
        ResizeToFitResults();

        // Forcer le focus — AttachThreadInput pour voler le foreground
        var foreWnd = Win32.GetForegroundWindow();
        uint foreThread = Win32.GetWindowThreadProcessId(foreWnd, IntPtr.Zero);
        uint curThread = Win32.GetCurrentThreadId();
        if (foreThread != curThread)
            Win32.AttachThreadInput(curThread, foreThread, true);

        Win32.ShowWindow(_hWnd, 5); // SW_SHOW
        Win32.SetForegroundWindow(_hWnd);
        Win32.SetFocus(_hEdit);

        if (foreThread != curThread)
            Win32.AttachThreadInput(curThread, foreThread, false);

        // Sélectionner tout le texte existant
        Win32.SendMessageW(_hEdit, 0x00B1, IntPtr.Zero, (IntPtr)(-1)); // EM_SETSEL 0, -1
    }

    // Audit 24/09 B4 : fenêtres du shell refusées comme cible d'insertion. Au clic droit sur
    // l'icône, le premier plan est la barre des tâches (ou le panneau de débordement de la
    // zone de notification) : l'insertion y « réussissait », sans copie de secours, et le
    // caractère n'arrivait nulle part.
    private static readonly string[] RefusedTargetClasses =
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
    };

    /// <summary>Audit 24/09 B4 : la classe de fenêtre est-elle celle d'une surface du shell ?</summary>
    internal static bool IsRefusedTargetClass(string? className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        // Les noms de classe Win32 ne sont pas sensibles à la casse.
        foreach (var refused in RefusedTargetClasses)
            if (string.Equals(refused, className, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Audit 24/09 B4 : la fenêtre qui recevra l'insertion directe. La recherche elle-même
    /// garde la cible connue (comportement antérieur) ; une cible nulle, détruite ou refusée
    /// donne <see cref="IntPtr.Zero"/> — l'insertion rend alors NotInserted et retombe sur la
    /// copie de secours avec notification, au lieu de viser une ancienne fenêtre ou le shell.
    /// </summary>
    internal static IntPtr ResolveInsertionTarget(IntPtr candidate, IntPtr self, IntPtr previous,
        Func<IntPtr, bool> isWindow, Func<IntPtr, string?> getClassName)
    {
        if (candidate != IntPtr.Zero && candidate == self) return previous;
        if (candidate == IntPtr.Zero || !isWindow(candidate)) return IntPtr.Zero;
        return IsRefusedTargetClass(getClassName(candidate)) ? IntPtr.Zero : candidate;
    }

    private static string? ReadWindowClassName(IntPtr hWnd)
    {
        // GetClassNameW existe déjà côté moteur (Win32 de TypingEngine.Windows, public) :
        // nom qualifié, la classe Win32 de l'application le masque dans ce namespace.
        var className = new System.Text.StringBuilder(64);
        return TypingEngine.Windows.Win32.GetClassNameW(hWnd, className, className.Capacity) > 0
            ? className.ToString()
            : null;
    }

    public void Hide()
    {
        Win32.ShowWindow(_hWnd, 0); // SW_HIDE
        SelectionChanged?.Invoke(null); // Effacer le highlight
    }

    // ═══════════════════════════════════════════════════════════════
    // WndProc
    // ═══════════════════════════════════════════════════════════════

    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_PAINT:
                    OnPaint(hWnd);
                    return IntPtr.Zero;

                case Win32.WM_ERASEBKGND:
                    return (IntPtr)1; // On gère le fond dans Win32.WM_PAINT

                case Win32.WM_COMMAND:
                    int controlId = (int)(wParam.ToInt64() & 0xFFFF);
                    int notifCode = (int)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (controlId == IDC_SEARCH && notifCode == EN_CHANGE)
                    {
                        var text = GetEditText();
                        Search(text);
                    }
                    return IntPtr.Zero;

                case Win32.WM_ACTIVATE:
                    int activateState = (int)(wParam.ToInt64() & 0xFFFF);
                    if (activateState == 0) // WA_INACTIVE
                        Hide();
                    return IntPtr.Zero;

                case Win32.WM_MOUSEWHEEL:
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (delta > 0 && _scrollOffset > 0)
                        _scrollOffset--;
                    else if (delta < 0 && _scrollOffset < _filteredResults.Count - _visibleRows)
                        _scrollOffset++;
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;

                case Win32.WM_LBUTTONDOWN:
                    {
                        int mouseY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        HandleClick(mouseY);
                    }
                    return IntPtr.Zero;

                case Win32.WM_TIMER:
                    if (wParam == (IntPtr)IDC_TIMER_COPYFEEDBACK)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)IDC_TIMER_COPYFEEDBACK);
                        _showCopiedFeedback = false;
                        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                        AnnounceSelection(notify: false);
                    }
                    return IntPtr.Zero;

                case Win32.WM_DPICHANGED:
                {
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0)
                        _dpiScale = newDpi / 96f;

                    ApplyEditFont();
                    ResizeToFitResults();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_CTLCOLOREDIT:
                    Win32.SetTextColor(wParam, PaperTheme.Ink);
                    Win32.SetBkColor(wParam, PaperTheme.Paper);
                    return _hEditBgBrush;

                case Win32.WM_DESTROY:
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("CharSearch WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>Subclass de l'Edit pour intercepter les touches spéciales.</summary>
    private IntPtr EditSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (_inputPaused && (msg == Win32.WM_KEYDOWN || msg == Win32.WM_CHAR ||
            msg == Win32.WM_PASTE || msg == Win32.WM_CUT || msg == Win32.WM_CLEAR || msg == Win32.WM_UNDO))
            return IntPtr.Zero;

        if (msg == Win32.WM_KEYDOWN)
        {
            int vk = wParam.ToInt32();
            switch (vk)
            {
                case VK_ESCAPE:
                    Hide();
                    return IntPtr.Zero;

                case VK_RETURN:
                    NotifySelectionChanged();
                    InsertSelectedCharacter();
                    return IntPtr.Zero;

                case VK_DOWN:
                    if (_selectedIndex < _filteredResults.Count - 1)
                    {
                        _selectedIndex++;
                        EnsureVisible(_selectedIndex);
                        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                        NotifySelectionChanged();
                        AnnounceSelection(notify: true);
                    }
                    return IntPtr.Zero;

                case VK_UP:
                    if (_selectedIndex > 0)
                    {
                        _selectedIndex--;
                        EnsureVisible(_selectedIndex);
                        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                        NotifySelectionChanged();
                        AnnounceSelection(notify: true);
                    }
                    return IntPtr.Zero;
            }
        }

        // Bloquer Win32.WM_CHAR pour Escape et Entrée (supprime le beep Windows)
        if (msg == Win32.WM_CHAR)
        {
            int ch = wParam.ToInt32();
            if (ch == VK_ESCAPE || ch == '\r' || ch == '\n')
                return IntPtr.Zero;
        }

        // Texte d'attente quand le champ est vide : même police que la saisie, texte secondaire.
        if (msg == Win32.WM_PAINT)
        {
            var result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
            if (GetEditTextLength() == 0)
            {
                var hdc = Win32.GetDC(hWnd);
                Win32.GetClientRect(hWnd, out var editRect);
                PaperText.DrawLine(hdc, L.Search_Placeholder, EditFont, editRect, PaperTheme.TextSecondary);
                Win32.ReleaseDC(hWnd, hdc);
            }
            return result;
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void NotifySelectionChanged()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _filteredResults.Count)
            SelectionChanged?.Invoke(_filteredResults[_selectedIndex].Method);
        else
            SelectionChanged?.Invoke(null);
    }

    private void EnsureVisible(int index)
    {
        if (index < _scrollOffset)
            _scrollOffset = index;
        else if (index >= _scrollOffset + _visibleRows)
            _scrollOffset = index - _visibleRows + 1;
    }

    private int GetEditTextLength()
        => _hEdit == IntPtr.Zero ? 0 : (int)Win32.SendMessageW(_hEdit, Win32.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);

    private string GetEditText()
    {
        int len = (int)Win32.SendMessageW(_hEdit, Win32.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
        if (len <= 0) return "";
        var buffer = new char[len + 1];
        var hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Win32.SendMessageW(_hEdit, Win32.WM_GETTEXT, (IntPtr)(len + 1), hBuffer.AddrOfPinnedObject());
            return new string(buffer, 0, len);
        }
        finally
        {
            hBuffer.Free();
        }
    }

    private void HandleClick(int mouseY)
    {
        if (_inputPaused) return;
        int searchH = Scale(BASE_FIELD_H);
        if (mouseY < searchH) return; // Clic dans la zone de recherche

        int rowCount = Math.Min(_visibleRows, _filteredResults.Count - _scrollOffset);
        int i = RowIndexAt(mouseY, searchH, _scrollOffset, rowCount, GetRowHeight);
        if (i < 0) return;

        _selectedIndex = i;
        NotifySelectionChanged();
        InsertSelectedCharacter();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>
    /// Ligne de résultat sous l'ordonnée <paramref name="mouseY"/>, sur la géométrie
    /// d'<see cref="OnPaint"/> : la liste commence un pixel sous le séparateur de la
    /// recherche, et chaque ligne est suivie d'un séparateur de 1 px qui n'appartient à
    /// aucune ligne. Rend -1 hors des lignes. Audit du 25/09 (V-07) : l'ancien calcul
    /// ignorait ces séparateurs, et le bas de la k-ième ligne visible insérait le
    /// caractère de la ligne suivante.
    /// </summary>
    internal static int RowIndexAt(int mouseY, int searchAreaH, int firstIndex, int rowCount, Func<int, int> rowHeight)
    {
        int y = searchAreaH + 1;
        for (int k = 0; k < rowCount; k++)
        {
            int index = firstIndex + k;
            int h = rowHeight(index);
            if (mouseY >= y && mouseY < y + h) return index;
            y += h + 1;
        }
        return -1;
    }

    // ═══════════════════════════════════════════════════════════════
    // Copie dans le presse-papier
    // ═══════════════════════════════════════════════════════════════

    private void InsertSelectedCharacter()
    {
        if (_inputPaused) return;
        if (_selectedIndex < 0 || _selectedIndex >= _filteredResults.Count) return;

        var entry = _filteredResults[_selectedIndex];
        IntPtr target = _targetWindow;
        // La fenêtre reste ouverte, comme en 1.1.0 : l'insertion rend le focus à la
        // cible (la recherche est topmost, elle reste visible) et d'autres caractères
        // peuvent suivre. Échap ou le raccourci la ferment.
        var outcome = _insertionService.Insert(target, entry.Character);
        if (outcome == TextInsertionOutcome.Partial)
            InsertionIncomplete?.Invoke();
        // Seul un échec sans événement envoyé autorise la copie de secours.
        if (outcome != TextInsertionOutcome.NotInserted)
            return;

        if (!ClipboardText.TrySet(_hWnd, entry.Character)) return;

        // Feedback visuel "Copié !" dans la fenêtre restée ouverte, plus la notification
        _showCopiedFeedback = true;
        _copiedChar = entry.Character;
        FallbackCopied?.Invoke(entry.Character);
        Win32.SetTimer(_hWnd, (UIntPtr)IDC_TIMER_COPYFEEDBACK, 1500, IntPtr.Zero);
        // A18 : « Copié » s'entend aussi, pas seulement dans le pied peint.
        Announce(L.Search_CopiedFeedback(entry.Character), notify: true);
    }

    // La séquence de copie elle-même vit dans ClipboardText.TrySet depuis la v1.2.0 :
    // elle existait ici, dans UsageStatsWindow et dans le partage du défi, chaque copie
    // portant sa propre gestion du transfert de propriété du handle.

    // internal : primitives réutilisées par ClipboardText (même filet de restauration
    // du presse-papiers). Elles restent ici, où vivent les constantes de format.
    internal static IntPtr AllocateClipboardText(string text)
    {
        int byteCount = (text.Length + 1) * 2; // UTF-16 + null terminator
        IntPtr hMem = Win32.GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
        if (hMem == IntPtr.Zero)
            return IntPtr.Zero;

        var ptr = Win32.GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            Win32.GlobalFree(hMem);
            return IntPtr.Zero;
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
            Marshal.WriteInt16(ptr, text.Length * 2, 0); // null terminator
            return hMem;
        }
        finally
        {
            Win32.GlobalUnlock(hMem);
        }
    }

    internal static string? ReadClipboardText()
    {
        if (!Win32.IsClipboardFormatAvailable(CF_UNICODETEXT))
            return null;

        IntPtr hData = Win32.GetClipboardData(CF_UNICODETEXT);
        if (hData == IntPtr.Zero)
            return null;

        IntPtr ptr = Win32.GlobalLock(hData);
        if (ptr == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            Win32.GlobalUnlock(hData);
        }
    }

    internal static void RestoreClipboardText(string? previousText)
    {
        if (previousText == null)
            return;

        IntPtr hRestore = AllocateClipboardText(previousText);
        if (hRestore == IntPtr.Zero)
            return;

        if (Win32.SetClipboardData(CF_UNICODETEXT, hRestore) == IntPtr.Zero)
            Win32.GlobalFree(hRestore);
    }

    // ═══════════════════════════════════════════════════════════════
    // Rendu
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Maquette « Recherche B, partition du choix » : le champ et sa loupe ; cinq lignes au
    /// plus, chacune avec son caractère et son geste abrégé ; sous la liste, le caractère
    /// choisi en grand, sa portée et la phrase qui la lit ; le pied.
    /// </summary>
    private void OnPaint(IntPtr hWnd)
    {
        using var paint = new PaintBuffer(hWnd);
        PaintContent(paint.Hdc, paint.Client.right, paint.Client.bottom);
        // Contour papier d'un pixel, posé en dernier sur les bords de la zone cliente.
        PaperDraw.Frame(paint.Hdc, paint.Client, PaperTheme.Border);
    }

    private void PaintContent(IntPtr hdc, int cw, int ch)
    {
        PaperDraw.Fill(hdc, 0, 0, cw, ch, PaperTheme.Paper);

        // Champ : la loupe ; l'EDIT se dessine lui-même, sur le même papier.
        int fieldH = Scale(BASE_FIELD_H);
        DrawLoupe(hdc, Scale(BASE_FIELD_PAD_X), (fieldH - Scale(BASE_LOUPE)) / 2);
        PaperDraw.Fill(hdc, 0, fieldH, cw, fieldH + 1, PaperTheme.Border);
        int y = fieldH + 1;

        if (_filteredResults.Count == 0)
        {
            // Champ vide : les raccourcis ; requête sans correspondance : « Aucun résultat ».
            var band = new Win32.RECT
            {
                left = Scale(BASE_FIELD_PAD_X), top = y, right = cw - Scale(BASE_FIELD_PAD_X), bottom = ch,
            };
            if (GetEditTextLength() > 0)
                PaperText.DrawLine(hdc, L.Search_NoResults, RowNameFont(false), band, PaperTheme.TextSecondary);
            else
                PaperText.DrawLine(hdc, L.Search_Tip, FooterFont(false), band, PaperTheme.TextSecondary);
            return;
        }

        // Liste : surface blanche, filet entre deux lignes.
        int rows = Math.Min(_visibleRows, _filteredResults.Count - _scrollOffset);
        int listBottom = y + ListHeight(rows);
        PaperDraw.Fill(hdc, 0, y, cw, listBottom, PaperTheme.Surface);
        for (int k = 0; k < rows; k++)
        {
            int index = _scrollOffset + k;
            DrawRow(hdc, _filteredResults[index], index == _selectedIndex, y, cw);
            y += Scale(BASE_ROW_H);
            if (k < rows - 1)
            {
                PaperDraw.Fill(hdc, 0, y, cw, y + 1, PaperTheme.Border);
                y += 1;
            }
        }
        if (_filteredResults.Count > _visibleRows)
            DrawScrollBar(hdc, fieldH + 1, listBottom, cw);

        // Bande puis pied, posés depuis le bas de la liste : jamais depuis le bas de la fenêtre,
        // sinon un plafond de hauteur les ferait recouvrir les lignes.
        int bandH = 1 + Scale(BASE_BAND_H);
        DrawBand(hdc, _selectedIndex >= 0 && _selectedIndex < _filteredResults.Count ? _filteredResults[_selectedIndex] : null,
            listBottom, bandH, cw);
        int footerTop = listBottom + bandH;
        DrawFooter(hdc, footerTop, footerTop + Scale(BASE_FOOTER_H), cw);
    }

    /// <summary>Hauteur de <paramref name="rows"/> lignes et des filets qui les séparent.</summary>
    private int ListHeight(int rows) => rows <= 0 ? 0 : rows * Scale(BASE_ROW_H) + (rows - 1);

    /// <summary>La loupe du champ (18 px à 96 DPI) : un cercle et son manche.</summary>
    private void DrawLoupe(IntPtr hdc, int left, int top)
    {
        float s = _dpiScale;
        int P(float v) => (int)Math.Round(v * s);
        var pen = Win32.CreatePen(Win32.PS_SOLID, Math.Max(1, P(1.6f)), PaperTheme.TextSecondary);
        var oldPen = Win32.SelectObject(hdc, pen);
        var oldBrush = Win32.SelectObject(hdc, Win32.GetStockObject(Win32.NULL_BRUSH));
        int cx = left + P(7.5f), cy = top + P(7.5f), r = P(5.75f);
        Win32.Ellipse(hdc, cx - r, cy - r, cx + r + 1, cy + r + 1);
        Win32.MoveToEx(hdc, left + P(11.8f), top + P(11.8f), IntPtr.Zero);
        Win32.LineTo(hdc, left + P(16.5f), top + P(16.5f));
        Win32.SelectObject(hdc, oldBrush);
        Win32.SelectObject(hdc, oldPen);
        Win32.DeleteObject(pen);
    }

    /// <summary>
    /// Une ligne de résultat sur deux lignes : le caractère (AG Titre 28) à gauche, le nom,
    /// puis dessous le geste abrégé. La ligne choisie est teintée, avec un filet bleu de 3 px
    /// à gauche, son caractère en bleu et son nom en semi-gras. Un nom trop long finit par
    /// des points de suspension.
    /// </summary>
    private void DrawRow(IntPtr hdc, CharEntry entry, bool chosen, int top, int cw)
    {
        int h = Scale(BASE_ROW_H);
        uint backdrop = PaperTheme.Surface;
        if (chosen)
        {
            backdrop = PaperTheme.ActionTint;
            PaperDraw.Fill(hdc, 0, top, cw, top + h, PaperTheme.ActionTint);
            PaperDraw.Fill(hdc, 0, top, Scale(3), top + h, PaperTheme.Action);
        }

        int glyphLeft = Scale(BASE_ROW_GLYPH_LEFT);
        int glyphRight = glyphLeft + Scale(BASE_ROW_GLYPH_W);
        DrawCharacter(hdc, entry.Character, 28,
            new Win32.RECT { left = glyphLeft, top = top, right = glyphRight, bottom = top + h },
            chosen ? PaperTheme.Action : PaperTheme.Ink, backdrop);

        // Deux lignes à droite du caractère : le nom, puis les touches alignées à gauche.
        int left = glyphRight + Scale(BASE_ROW_GAP);
        int right = cw - Scale(BASE_ROW_PAD_RIGHT);
        int nameTop = top + Scale(BASE_ROW_NAME_TOP);
        var font = RowNameFont(chosen);
        string name = Ellipsize(hdc, GetSentenceName(entry), font, right - left);
        PaperText.DrawLine(hdc, name, font,
            new Win32.RECT { left = left, top = nameTop, right = right, bottom = nameTop + Scale(BASE_ROW_NAME_H) }, PaperTheme.Ink);

        var steps = StepsOf(entry);
        if (steps.Count > 0)
        {
            int midY = top + Scale(BASE_ROW_GESTURE_MID);
            int gestureW = TablatureRenderer.DrawShortGesture(hdc, steps, chosen, _dpiScale, 0, midY, draw: false);
            TablatureRenderer.DrawShortGesture(hdc, steps, chosen, _dpiScale, left + gestureW, midY);
        }
    }

    /// <summary>
    /// La bande sous la liste, une ligne : la phrase qui lit le geste du résultat choisi
    /// (« Frappez é. »), toujours visible, car c'est l'équivalent texte des touches de la
    /// ligne. Sans geste connu, elle donne le nom en entier. Une phrase trop longue finit par
    /// des points de suspension : les touches de la ligne choisie la disent aussi.
    /// </summary>
    private void DrawBand(IntPtr hdc, CharEntry? entry, int top, int height, int cw)
    {
        PaperDraw.Fill(hdc, 0, top, cw, top + 1, PaperTheme.Border);
        if (entry == null)
            return;

        var steps = StepsOf(entry);
        var runs = steps.Count > 0 ? Gesture.Sentence(steps) : new List<TextRun> { new(GetSentenceName(entry), true) };
        int left = Scale(BASE_FIELD_PAD_X);
        int right = cw - Scale(BASE_FIELD_PAD_X);
        var band = new Win32.RECT { left = left, top = top + 1, right = right, bottom = top + height };
        var weak = SentenceFont(false);
        var strong = SentenceFont(true);
        // Une ligne : mesurée toute en gras (le pire cas), la phrase qui dépasse est coupée
        // en police ordinaire plutôt que renvoyée à la ligne.
        string plain = string.Concat(runs.Select(r => r.Text));
        if (PaperText.Width(hdc, plain, strong) <= right - left)
            PaperText.DrawParagraph(hdc, runs, weak, strong, band, PaperTheme.TextSecondary, PaperTheme.Ink, band.bottom - band.top);
        else
            PaperText.DrawLine(hdc, Ellipsize(hdc, plain, weak, right - left), weak, band, PaperTheme.TextSecondary);
    }

    /// <summary>Le pied : à gauche le nombre de résultats, à droite ce que fait Entrée, ou
    /// « copié » quand l'insertion est retombée sur le presse-papiers.</summary>
    private void DrawFooter(IntPtr hdc, int top, int bottom, int cw)
    {
        PaperDraw.Fill(hdc, 0, top, cw, top + 1, PaperTheme.Border);
        var band = new Win32.RECT
        {
            left = Scale(BASE_FIELD_PAD_X), top = top + 1, right = cw - Scale(BASE_FIELD_PAD_X), bottom = bottom,
        };
        PaperText.DrawParagraph(hdc, FooterCountRuns(_filteredResults.Count, _totalMatches),
            FooterFont(false), FooterFont(true), band, PaperTheme.TextSecondary, PaperTheme.Ink, band.bottom - band.top);

        if (_showCopiedFeedback)
            PaperText.DrawLine(hdc, L.Search_CopiedFeedback(_copiedChar), FooterFont(true), band, PaperTheme.Success, TextAlign.Right);
        else if (_selectedIndex >= 0 && _selectedIndex < _filteredResults.Count)
            PaperText.DrawLine(hdc, L.Search_FooterInsert(DisplayGlyph.ForStandaloneMark(_filteredResults[_selectedIndex].Character)),
                FooterFont(false), band, PaperTheme.TextSecondary, TextAlign.Right);
    }

    /// <summary>Barre de défilement fine à droite de la liste, quand elle a plus de lignes qu'elle n'en montre.</summary>
    private void DrawScrollBar(IntPtr hdc, int top, int bottom, int cw)
    {
        int w = Scale(4);
        int x = cw - w - Scale(2);
        int h = bottom - top;
        if (h <= 0) return;
        float ratio = (float)_visibleRows / _filteredResults.Count;
        int thumbH = Math.Max(Scale(16), (int)(h * ratio));
        int range = _filteredResults.Count - _visibleRows;
        float scrollRatio = range > 0 ? (float)_scrollOffset / range : 0;
        int thumbY = top + (int)((h - thumbH) * scrollRatio);
        PaperDraw.Fill(hdc, x, top, x + w, bottom, PaperTheme.Border);
        PaperDraw.Fill(hdc, x, thumbY, x + w, thumbY + thumbH, PaperTheme.Pale);
    }

    /// <summary>
    /// Un caractère centré dans <paramref name="rect"/>, en AG Titre de <paramref name="size"/>
    /// px (96 DPI), réduit s'il déborde. Un accent seul reçoit son support (◌) ; une espace
    /// est montrée par le repère de correcteur des Leçons.
    /// </summary>
    private void DrawCharacter(IntPtr hdc, string character, int size, Win32.RECT rect, uint color, uint backdrop)
    {
        string shown = DisplayGlyph.ForStandaloneMark(character);
        var font = FontSpec.Of(AppFonts.Title, size, TypeRamp.Semibold, _dpiScale);
        if (shown.Length == 1 && char.IsWhiteSpace(shown[0]))
        {
            var (ascent, descent) = PaperText.Metrics(hdc, font);
            int baseline = rect.top + (rect.bottom - rect.top - (ascent + descent)) / 2 + ascent;
            bool thin = shown[0] is ' ' or ' ' or ' ';
            int markW = TablatureStyle.Px((thin ? 0.26f : 0.44f) * size, _dpiScale);
            PaperDraw.SpaceMark(hdc, thin ? ' ' : ' ', rect.left + (rect.right - rect.left - markW) / 2, markW,
                baseline, TablatureStyle.Px(0.3f * size, _dpiScale), size / 50f * _dpiScale,
                PaperDraw.Blend(color, backdrop, 0.55f));
            return;
        }
        int available = rect.right - rect.left;
        int width = PaperText.Width(hdc, shown, font);
        if (width > available && width > 0)
            font = FontSpec.Of(AppFonts.Title, Math.Max(8, size * available / width), TypeRamp.Semibold, _dpiScale);
        PaperText.DrawLine(hdc, shown, font, rect, color, TextAlign.Center);
    }

    /// <summary>Les temps du geste recommandé d'un résultat (vide sans méthode connue).</summary>
    private static IReadOnlyList<GestureStep> StepsOf(CharEntry entry)
        => entry.Method is { } method ? Gesture.Of(method) : Array.Empty<GestureStep>();

    /// <summary><paramref name="text"/> coupé avec des points de suspension pour tenir dans
    /// <paramref name="maxWidth"/> pixels.</summary>
    private static string Ellipsize(IntPtr hdc, string text, FontSpec font, int maxWidth)
    {
        if (maxWidth <= 0) return "";
        if (PaperText.Width(hdc, text, font) <= maxWidth) return text;
        const string Dots = "…";
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (PaperText.Width(hdc, text[..mid].TrimEnd() + Dots, font) <= maxWidth) lo = mid;
            else hi = mid - 1;
        }
        if (lo > 0 && char.IsHighSurrogate(text[lo - 1])) lo--;
        return lo == 0 ? Dots : text[..lo].TrimEnd() + Dots;
    }

    /// <summary>
    /// Pied de la liste : le nombre en gras, puis le texte. Audit du 25/09 (V-08) : la liste
    /// est plafonnée à <see cref="MAX_RESULTS"/> lignes ; au-delà, le pied dit « 20 résultats
    /// affichés sur 98 ».
    /// </summary>
    internal static List<TextRun> FooterCountRuns(int shown, int total)
    {
        string rest = total > shown ? L.Search_FooterShownOf(total)
            : shown == 1 ? L.Search_FooterResultSingular
            : L.Search_FooterResultPlural;
        return new List<TextRun> { new(shown.ToString(System.Globalization.CultureInfo.InvariantCulture), true), new(rest) };
    }

    /// <summary>Hauteur d'une ligne de résultat pour le clic : fixe, comme au dessin.</summary>
    private int GetRowHeight(int index) => Scale(BASE_ROW_H);

    // ═══════════════════════════════════════════════════════════════
    // Cleanup
    // ═══════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_hEdit != IntPtr.Zero)
        {
            Win32.RemoveWindowSubclass(_hEdit, _editSubclassProc, (UIntPtr)1);
            _hEdit = IntPtr.Zero;
        }
        if (_hEditBgBrush != IntPtr.Zero)
        {
            Win32.DeleteObject(_hEditBgBrush);
            _hEditBgBrush = IntPtr.Zero;
        }
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("CharSearch"));
    }
}
