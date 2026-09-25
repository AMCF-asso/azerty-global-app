// Banc hors application — audit visuel 2026-09-25 (zone V-).
// Réplique, sans référence à l'application, de :
//   1. CharacterSearch.LoadCharacterIndex + Search/MatchScore/AllWordsMatch (src/CharacterSearch.cs @ f0a98ba) ;
//   2. un repeint GDI du clavier dans un DC mémoire, chemin VirtualKeyboard.PaintContent (f0a98ba)
//      et chemin KeyboardRenderer.DrawKey (pinceau + stylo créés par touche).
// Usage : dotnet run -c Release -- "<chemin de src/character-index.json>"
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

static class Program
{
    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "character-index.json";
        // L'application tourne dans la culture de l'utilisateur (Antoine : fr-FR).
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine($"Runtime : {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, JIT (l'application publiée est AOT)");
        Console.WriteLine($"Globalisation ICU : {IsIcu()}  (DOTNET_SYSTEM_GLOBALIZATION_USENLS={Environment.GetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_USENLS") ?? "non défini"})");
        Console.WriteLine($"Fichier : {path} ({new FileInfo(path).Length:N0} octets)");
        Console.WriteLine();

        SearchBench.Run(path);
        Console.WriteLine();
        GdiBench.Run();
        return 0;
    }

    static bool IsIcu()
    {
        // Détection documentée par Microsoft (docs « Globalization and ICU ») : vrai sous ICU.
        var sortVersion = CultureInfo.InvariantCulture.CompareInfo.Version;
        byte[] bytes = sortVersion.SortId.ToByteArray();
        int version = bytes[3] << 24 | bytes[2] << 16 | bytes[1] << 8 | bytes[0];
        return version != 0 && version == sortVersion.FullVersion;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// 1. Recherche
// ═════════════════════════════════════════════════════════════════════════════
sealed class MethodData
{
    public string Type { get; set; } = "";
    public string Key { get; set; } = "";
    public string Layer { get; set; } = "";
    public string DeadKey { get; set; } = "";
    public string DkActivationKey { get; set; } = "";
    public string DkActivationLayer { get; set; } = "";
}

sealed class CharEntry
{
    public string Character { get; set; } = "";
    public string CodePoint { get; set; } = "";
    public string NameFr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string[] EnglishAliases { get; set; } = Array.Empty<string>();
    public string MethodDisplay { get; set; } = "";
    public bool IsDirectAccess { get; set; }
    public MethodData? Method { get; set; }
    public List<MethodData> Methods { get; set; } = new();
    public string NormalizedNameFr { get; set; } = "";
    public string NormalizedNameEn { get; set; } = "";
    public string[][] NormalizedAliasWords { get; set; } = Array.Empty<string[]>();
    public string[][] NormalizedEnglishAliasWords { get; set; } = Array.Empty<string[]>();
    public string[] NormalizedNameFrWords { get; set; } = Array.Empty<string>();
    public string[] NormalizedNameEnWords { get; set; } = Array.Empty<string>();
    public string NormalizedChar { get; set; } = "";
}

sealed class CharIndex
{
    // Copie fidèle de CharacterSearch.cs:234-368 (L.* remplacés par leur texte français).
    public List<CharEntry> All = new();
    readonly Dictionary<string, string> _deadKeyActivations = new();
    readonly Dictionary<string, (string key, string layer)> _deadKeyActivationRaw = new();
    static readonly char[] SearchSeparators = { ' ', '-', '\'', '’', '(', ')' };
    static readonly Dictionary<string, string> KeyLabels = new()
    {
        ["Backquote"] = "²", ["Digit1"] = "&", ["Digit2"] = "é", ["Digit3"] = "\"", ["Digit4"] = "'",
        ["Digit5"] = "(", ["Digit6"] = "-", ["Digit7"] = "è", ["Digit8"] = "_", ["Digit9"] = "ç",
        ["Digit0"] = "à", ["Minus"] = ")", ["Equal"] = "=", ["KeyQ"] = "A", ["KeyW"] = "Z",
        ["KeyE"] = "E", ["KeyR"] = "R", ["KeyT"] = "T", ["KeyY"] = "Y", ["KeyU"] = "U",
        ["KeyI"] = "I", ["KeyO"] = "O", ["KeyP"] = "P", ["BracketLeft"] = "^", ["BracketRight"] = "$",
        ["KeyA"] = "Q", ["KeyS"] = "S", ["KeyD"] = "D", ["KeyF"] = "F", ["KeyG"] = "G",
        ["KeyH"] = "H", ["KeyJ"] = "J", ["KeyK"] = "K", ["KeyL"] = "L", ["Semicolon"] = "M",
        ["Quote"] = "ù", ["Backslash"] = "*", ["IntlBackslash"] = "<", ["KeyZ"] = "W", ["KeyX"] = "X",
        ["KeyC"] = "C", ["KeyV"] = "V", ["KeyB"] = "B", ["KeyN"] = "N", ["KeyM"] = ",",
        ["Comma"] = ".", ["Period"] = "/", ["Slash"] = "§", ["Space"] = "Espace",
    };

    public void Load(string path)
    {
        string json = File.ReadAllText(path); // l'app : StreamReader.ReadToEnd sur la ressource
        using var doc = JsonDocument.Parse(json);
        var characters = doc.RootElement.GetProperty("characters");
        foreach (var entry in characters.EnumerateObject())
        {
            if (!entry.Name.StartsWith("dk:")) continue;
            if (!entry.Value.TryGetProperty("methods", out var methods)) continue;
            foreach (var method in methods.EnumerateArray())
            {
                if (method.GetProperty("type").GetString() != "deadkey_activation") continue;
                var dkName = method.GetProperty("deadkey").GetString() ?? "";
                var key = method.GetProperty("key").GetString() ?? "";
                var layer = method.GetProperty("layer").GetString() ?? "";
                _deadKeyActivations[dkName] = FormatDirectMethod(key, layer);
                _deadKeyActivationRaw[dkName] = (key, layer);
                break;
            }
        }
        foreach (var entry in characters.EnumerateObject())
        {
            if (entry.Name.StartsWith("dk:")) continue;
            var charStr = entry.Name;
            var codePoint = entry.Value.TryGetProperty("codePoint", out var cp) ? cp.GetString() ?? "" : "";
            var nameFr = entry.Value.TryGetProperty("unicodeNameFr", out var nf) ? nf.GetString() ?? "" : "";
            var nameEn = entry.Value.TryGetProperty("unicodeName", out var ne) ? ne.GetString() ?? "" : "";
            var aliases = new List<string>();
            if (entry.Value.TryGetProperty("frenchAliases", out var fa) && fa.ValueKind == JsonValueKind.Array)
                foreach (var alias in fa.EnumerateArray()) if (alias.GetString() is string a) aliases.Add(a);
            var englishAliases = new List<string>();
            if (entry.Value.TryGetProperty("englishAliases", out var ea) && ea.ValueKind == JsonValueKind.Array)
                foreach (var alias in ea.EnumerateArray()) if (alias.GetString() is string a) englishAliases.Add(a);

            string methodDisplay = ""; bool isDirectAccess = false; MethodData? methodData = null;
            var methodEntries = new List<MethodData>();
            if (entry.Value.TryGetProperty("methods", out var methods2))
            {
                JsonElement? recommended = null, fallback = null; MethodData? recommendedData = null, fallbackData = null;
                foreach (var method in methods2.EnumerateArray())
                {
                    var currentData = CreateMethodData(method);
                    methodEntries.Add(currentData);
                    fallback ??= method; fallbackData ??= currentData;
                    if (method.TryGetProperty("recommended", out var rec) && rec.GetBoolean())
                    { recommended = method; recommendedData = currentData; }
                }
                var chosen = recommended ?? fallback; var chosenData = recommendedData ?? fallbackData;
                if (chosen.HasValue && chosenData != null)
                {
                    methodDisplay = FormatMethod(chosenData);
                    isDirectAccess = chosen.Value.TryGetProperty("recommended", out var r) && r.GetBoolean()
                        && chosen.Value.GetProperty("type").GetString() == "direct";
                    methodData = chosenData;
                }
            }
            var e2 = new CharEntry
            {
                Character = charStr, CodePoint = codePoint, NameFr = nameFr, NameEn = nameEn,
                Aliases = aliases.ToArray(), EnglishAliases = englishAliases.ToArray(),
                MethodDisplay = methodDisplay, IsDirectAccess = isDirectAccess, Method = methodData, Methods = methodEntries,
            };
            e2.NormalizedNameFr = NormalizeForSearch(nameFr);
            e2.NormalizedNameEn = NormalizeForSearch(nameEn);
            e2.NormalizedNameFrWords = e2.NormalizedNameFr.Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);
            e2.NormalizedNameEnWords = e2.NormalizedNameEn.Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);
            e2.NormalizedChar = NormalizeForSearch(charStr);
            e2.NormalizedAliasWords = aliases.Select(a => NormalizeForSearch(a).Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            e2.NormalizedEnglishAliasWords = englishAliases.Select(a => NormalizeForSearch(a).Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            All.Add(e2);
        }
    }

    MethodData CreateMethodData(JsonElement method)
    {
        var mType = method.GetProperty("type").GetString() ?? "";
        var mKey = method.TryGetProperty("key", out var mk) ? mk.GetString() ?? "" : "";
        var mLayer = method.TryGetProperty("layer", out var ml) ? ml.GetString() ?? "" : "";
        var md = new MethodData { Type = mType, Key = mKey, Layer = mLayer };
        if (mType == "deadkey")
        {
            var dkName = method.GetProperty("deadkey").GetString() ?? "";
            md.DeadKey = dkName;
            if (_deadKeyActivationRaw.TryGetValue(dkName, out var dkAct)) { md.DkActivationKey = dkAct.key; md.DkActivationLayer = dkAct.layer; }
        }
        return md;
    }

    string FormatMethod(MethodData m)
    {
        if (m.Type == "direct") return FormatDirectMethod(m.Key, m.Layer);
        if (m.Type == "deadkey")
        {
            var activation = _deadKeyActivations.GetValueOrDefault(m.DeadKey, m.DeadKey);
            var keyLabel = KeyLabels.GetValueOrDefault(m.Key, m.Key);
            var afterDk = m.Layer == "Shift" ? $"puis Maj + {keyLabel}" : $"puis {keyLabel}";
            return $"{activation}\n{afterDk}";
        }
        return "";
    }

    static string FormatDirectMethod(string key, string layer)
    {
        var k = KeyLabels.GetValueOrDefault(key, key);
        return layer switch
        {
            "Base" => k, "Shift" => $"Maj + {k}", "AltGr" => $"AltGr + {k}",
            "AltGr+Shift" or "Shift+AltGr" => $"AltGr + Maj + {k}", "Caps" => $"Verr. Maj. + {k}",
            "Caps+Shift" => $"Verr. Maj. + Maj + {k}", "Caps+AltGr" => $"Verr. Maj. + AltGr + {k}",
            "Caps+Shift+AltGr" or "Caps+AltGr+Shift" => $"Verr. Maj. + AltGr + Maj + {k}", _ => k,
        };
    }

    public static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // ── Search : copie de CharacterSearch.cs:452-509 (sans ResizeToFitResults/InvalidateRect) ──
    public List<CharEntry> Search(string query, bool ordinal)
    {
        var results = new List<CharEntry>();
        if (string.IsNullOrWhiteSpace(query)) return results;
        query = query.Trim();
        var lowerQuery = query.ToLowerInvariant();
        var normalizedQuery = NormalizeForSearch(query);
        var queryWords = normalizedQuery.Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);
        var querySynonyms = new string?[queryWords.Length];
        for (int i = 0; i < queryWords.Length; i++)
        {
            var w = queryWords[i];
            querySynonyms[i] = w.Length >= 2 && "uppercase".StartsWith(w, StringComparison.Ordinal) ? "capital"
                : w.Length >= 2 && "lowercase".StartsWith(w, StringComparison.Ordinal) ? "small" : null;
        }
        var originalQueryWords = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var scored = new List<(CharEntry entry, int score)>();
        foreach (var entry in All)
        {
            int score = MatchScore(entry, query, lowerQuery, normalizedQuery, queryWords, querySynonyms, originalQueryWords, ordinal);
            if (score > 0)
            {
                var resultEntry = ApplyQuerySpecificMethod(entry, normalizedQuery);
                if (!ReferenceEquals(resultEntry, entry)) score += 50;
                scored.Add((resultEntry, score));
            }
        }
        scored.Sort((a, b) => b.score.CompareTo(a.score));
        foreach (var (entry, _) in scored.Take(20)) results.Add(entry);
        LastMatchCount = scored.Count;
        return results;
    }
    public int LastMatchCount;

    CharEntry ApplyQuerySpecificMethod(CharEntry entry, string normalizedQuery) => (normalizedQuery, entry.Character) switch
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

    CharEntry WithPreferredMethod(CharEntry entry, string type, string deadKey, string key, string layer)
    {
        var method = entry.Methods.FirstOrDefault(c => c.Type == type && c.DeadKey == deadKey && c.Key == key && c.Layer == layer);
        if (method == null) return entry;
        return new CharEntry
        {
            Character = entry.Character, CodePoint = entry.CodePoint, NameFr = entry.NameFr, NameEn = entry.NameEn,
            Aliases = entry.Aliases, EnglishAliases = entry.EnglishAliases, MethodDisplay = FormatMethod(method),
            IsDirectAccess = type == "direct", Method = method, Methods = entry.Methods,
            NormalizedNameFr = entry.NormalizedNameFr, NormalizedNameEn = entry.NormalizedNameEn,
            NormalizedAliasWords = entry.NormalizedAliasWords, NormalizedEnglishAliasWords = entry.NormalizedEnglishAliasWords,
            NormalizedNameFrWords = entry.NormalizedNameFrWords, NormalizedNameEnWords = entry.NormalizedNameEnWords,
            NormalizedChar = entry.NormalizedChar,
        };
    }

    static bool SW(string a, string b, bool ordinal) => ordinal ? a.StartsWith(b, StringComparison.Ordinal) : a.StartsWith(b);

    public static long StartsWithCalls;

    static bool AllWordsMatch(string[] queryWords, string?[] querySynonyms, string[] textWords, bool ordinal)
    {
        for (int i = 0; i < queryWords.Length; i++)
        {
            var qw = queryWords[i];
            var syn = i < querySynonyms.Length ? querySynonyms[i] : null;
            bool found = false;
            foreach (var tw in textWords)
            {
                StartsWithCalls++;
                if (qw.Length == 1 ? tw == qw : (tw == qw || SW(tw, qw, ordinal))) { found = true; break; }
                if (syn != null && (tw == syn || SW(tw, syn, ordinal))) { found = true; break; }
            }
            if (!found) return false;
        }
        return true;
    }

    static int MatchScore(CharEntry entry, string query, string lowerQuery, string normalizedQuery, string[] queryWords,
        string?[] querySynonyms, string[] originalQueryWords, bool ordinal)
    {
        int score = 0;
        if (entry.Character == query) score = 100;
        else if (entry.Character.Equals(query, StringComparison.OrdinalIgnoreCase)) score = 90;
        else if (entry.NormalizedAliasWords.Length > 0 && queryWords.Length > 0 &&
            entry.NormalizedAliasWords.Any(aw => AllWordsMatch(queryWords, querySynonyms, aw, ordinal))) score = 80;
        else if (entry.NormalizedEnglishAliasWords.Length > 0 && queryWords.Length > 0 &&
            entry.NormalizedEnglishAliasWords.Any(aw => AllWordsMatch(queryWords, querySynonyms, aw, ordinal))) score = 80;
        else if (entry.NormalizedNameFrWords.Length > 0 && queryWords.Length > 0)
        { if (AllWordsMatch(queryWords, querySynonyms, entry.NormalizedNameFrWords, ordinal)) score = 70; }
        if (score == 0 && entry.NormalizedNameEnWords.Length > 0 && queryWords.Length > 0)
        { if (AllWordsMatch(queryWords, querySynonyms, entry.NormalizedNameEnWords, ordinal)) score = 50; }
        if (score == 0 && lowerQuery.StartsWith("u+") && entry.CodePoint.Equals(query, StringComparison.OrdinalIgnoreCase)) score = 90;
        if (score == 0) return 0;
        var lowerChar = entry.Character.ToLowerInvariant();
        if (originalQueryWords.Contains(entry.Character) || originalQueryWords.Contains(lowerChar)) score += 50;
        else if (queryWords.Any(w => entry.NormalizedChar == w)) score += 10;
        if (originalQueryWords.Length == 1 && originalQueryWords[0].Length == 1 &&
            (entry.Character == originalQueryWords[0] || lowerChar == originalQueryWords[0].ToLowerInvariant())) score += 100;
        if (entry.CodePoint.StartsWith("U+", StringComparison.Ordinal)
            && int.TryParse(entry.CodePoint.AsSpan(2), NumberStyles.HexNumber, null, out int cpNum)
            && cpNum >= 0x0020 && cpNum <= 0x007F) score += 15;
        if (entry.Character.Length == 1 && char.IsLetter(entry.Character[0]) && char.IsLower(entry.Character[0])) score += 5;
        if (entry.IsDirectAccess) score += 10;
        if (entry.Character.StartsWith("dk:")) score += 30;
        if (entry.NormalizedNameFr.Length > 0 && (ordinal ? entry.NormalizedNameFr.StartsWith(normalizedQuery, StringComparison.Ordinal)
                                                          : entry.NormalizedNameFr.StartsWith(normalizedQuery))) score += 15;
        return score;
    }
}

static class SearchBench
{
    // Frappe incrémentale : chaque préfixe est une requête (EN_CHANGE à chaque caractère).
    static readonly string[] Phrases =
    {
        "e", "é", "euro", "accent aigu", "e accent aigu", "guillemet", "fleche droite",
        "espace insecable", "alpha", "cyrillique", "u+00e9", "e uppercase", "oe", "tiret cadratin", "zzzz",
    };

    static IEnumerable<string> Prefixes(string phrase)
    {
        for (int i = 1; i <= phrase.Length; i++) yield return phrase[..i];
    }

    public static void Run(string path)
    {
        Console.WriteLine("=== 1. Recherche de caractère (réplique CharacterSearch @ f0a98ba) ===");

        // Chargement : 1er passage (inclut le JIT, pessimiste pour AOT) puis 15 passages chauds.
        long before = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var index = new CharIndex();
        index.Load(path);
        sw.Stop();
        long allocLoad = GC.GetAllocatedBytesForCurrentThread() - alloc0;
        long after = GC.GetTotalMemory(true);
        GC.KeepAlive(index);
        Console.WriteLine($"Entrées chargées : {index.All.Count}");
        Console.WriteLine($"Chargement 1er passage (JIT compris) : {sw.Elapsed.TotalMilliseconds:F1} ms ; alloué {allocLoad / 1024.0 / 1024.0:F2} Mo");
        Console.WriteLine($"Mémoire retenue, 1re instance (inclut les caches statiques du runtime) : {(after - before) / 1024.0:F0} Ko");
        long b2 = GC.GetTotalMemory(true);
        var second = new CharIndex(); second.Load(path);
        long a2 = GC.GetTotalMemory(true);
        GC.KeepAlive(second);
        Console.WriteLine($"Mémoire retenue par une 2e instance (index seul) : {(a2 - b2) / 1024.0:F0} Ko");
        long b3 = GC.GetTotalMemory(true);
        var names = new Dictionary<string, (string Fr, string En)>();
        foreach (var e in index.All) names[e.Character] = (e.NameFr, e.NameEn);
        long a3 = GC.GetTotalMemory(true);
        GC.KeepAlive(names);
        Console.WriteLine($"Dictionnaire GetCharacterNames() (copie pour le clavier virtuel, chaînes partagées) : {(a3 - b3) / 1024.0:F0} Ko");
        second = null!;

        var warm = new List<double>();
        for (int i = 0; i < 15; i++)
        {
            var s2 = Stopwatch.StartNew();
            var ix = new CharIndex(); ix.Load(path);
            s2.Stop(); warm.Add(s2.Elapsed.TotalMilliseconds);
        }
        warm.Sort();
        Console.WriteLine($"Chargement chaud : médiane {warm[warm.Count / 2]:F1} ms, min {warm[0]:F1} ms (15 passages)");

        // Part du parse JSON seul.
        var parseTimes = new List<double>();
        string json = File.ReadAllText(path);
        for (int i = 0; i < 15; i++)
        {
            var s3 = Stopwatch.StartNew();
            using var d = JsonDocument.Parse(json);
            s3.Stop(); parseTimes.Add(s3.Elapsed.TotalMilliseconds);
        }
        parseTimes.Sort();
        Console.WriteLine($"  dont JsonDocument.Parse seul : médiane {parseTimes[parseTimes.Count / 2]:F2} ms");

        // Normalisation seule (1005 × (nom FR + nom EN + car. + alias)).
        var normTimes = new List<double>();
        for (int i = 0; i < 15; i++)
        {
            var s4 = Stopwatch.StartNew();
            foreach (var e in index.All)
            {
                CharIndex.NormalizeForSearch(e.NameFr); CharIndex.NormalizeForSearch(e.NameEn); CharIndex.NormalizeForSearch(e.Character);
                foreach (var a in e.Aliases) CharIndex.NormalizeForSearch(a);
                foreach (var a in e.EnglishAliases) CharIndex.NormalizeForSearch(a);
            }
            s4.Stop(); normTimes.Add(s4.Elapsed.TotalMilliseconds);
        }
        normTimes.Sort();
        Console.WriteLine($"  dont normalisation FormD de l'index : médiane {normTimes[normTimes.Count / 2]:F2} ms");
        Console.WriteLine();

        foreach (bool ordinal in new[] { false, true })
        {
            // Échauffement
            for (int w = 0; w < 30; w++) foreach (var p in Phrases) foreach (var q in Prefixes(p)) index.Search(q, ordinal);

            Console.WriteLine(ordinal
                ? "-- Variante : StartsWith(..., StringComparison.Ordinal) (proposition) --"
                : "-- Code actuel : string.StartsWith(string) = culture courante fr-FR --");
            var all = new List<double>();
            long totalAlloc = 0; int totalQueries = 0;
            foreach (var phrase in Phrases)
            {
                var perKey = new List<double>();
                foreach (var q in Prefixes(phrase))
                {
                    const int reps = 40;
                    var samples = new double[reps];
                    CharIndex.StartsWithCalls = 0;
                    long a0 = GC.GetAllocatedBytesForCurrentThread();
                    for (int r = 0; r < reps; r++)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        index.Search(q, ordinal);
                        samples[r] = (Stopwatch.GetTimestamp() - t0) * 1_000_000.0 / Stopwatch.Frequency;
                    }
                    totalAlloc += (GC.GetAllocatedBytesForCurrentThread() - a0) / reps;
                    totalQueries++;
                    Array.Sort(samples);
                    perKey.Add(samples[reps / 2]);
                    all.Add(samples[reps / 2]);
                }
                int matches = index.Search(phrase, ordinal).Count;
                Console.WriteLine($"  « {phrase} » ({phrase.Length} frappes) : médiane/frappe {Median(perKey),7:F0} µs, pire frappe {perKey.Max(),7:F0} µs, {index.LastMatchCount} correspondances");
            }
            all.Sort();
            Console.WriteLine($"  Toutes frappes ({all.Count}) : médiane {Median(all):F0} µs, p90 {all[(int)(all.Count * 0.9)]:F0} µs, max {all[^1]:F0} µs ; alloué moyen/frappe {totalAlloc / (double)totalQueries / 1024.0:F1} Ko");
            Console.WriteLine();
        }

        // Nombre d'appels AllWordsMatch-StartsWith pour une requête type
        CharIndex.StartsWithCalls = 0; index.Search("accent aigu", false);
        Console.WriteLine($"Comparaisons mot/mot pour « accent aigu » : {CharIndex.StartsWithCalls:N0}");
        CharIndex.StartsWithCalls = 0; index.Search("e", false);
        Console.WriteLine($"Comparaisons mot/mot pour « e » : {CharIndex.StartsWithCalls:N0}");

        // Divergence de résultat culture vs ordinal ?
        int diff = 0;
        foreach (var p in Phrases) foreach (var q in Prefixes(p))
        {
            var a = index.Search(q, false).Select(x => x.Character).ToArray();
            var b = index.Search(q, true).Select(x => x.Character).ToArray();
            if (!a.SequenceEqual(b)) { diff++; Console.WriteLine($"  divergence culture/ordinal sur « {q} »"); }
        }
        Console.WriteLine($"Requêtes dont le top 20 diffère entre culture et ordinal : {diff}");
    }

    static double Median(List<double> xs) { var c = xs.OrderBy(x => x).ToList(); return c[c.Count / 2]; }
}

// ═════════════════════════════════════════════════════════════════════════════
// 2. Repeint GDI du clavier dans un DC mémoire (sans fenêtre, sans hook)
// ═════════════════════════════════════════════════════════════════════════════
static class GdiBench
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint c);
    [DllImport("gdi32.dll")] static extern IntPtr CreatePen(int style, int w, uint c);
    [DllImport("gdi32.dll")] static extern bool RoundRect(IntPtr hdc, int l, int t, int r, int b, int w, int h);
    [DllImport("gdi32.dll")] static extern bool Polygon(IntPtr hdc, POINT[] pts, int n);
    [DllImport("gdi32.dll")] static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr p);
    [DllImport("gdi32.dll")] static extern bool LineTo(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll")] static extern int SetBkMode(IntPtr hdc, int m);
    [DllImport("gdi32.dll")] static extern uint SetTextColor(IntPtr hdc, uint c);
    [DllImport("gdi32.dll")] static extern bool GdiFlush();
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, uint rop);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateFontW(int h, int w, int e, int o, int wt, uint i, uint u, uint s, uint cs, uint op, uint cp, uint q, uint pf, string face);
    [DllImport("user32.dll")] static extern int FillRect(IntPtr hdc, ref RECT r, IntPtr br);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int DrawTextW(IntPtr hdc, string s, int n, ref RECT r, uint f);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr hProcess, uint flags);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();

    const uint DT_CENTER = 0x1, DT_VCENTER = 0x4, DT_SINGLELINE = 0x20, DT_NOPREFIX = 0x800, DT_NOCLIP = 0x100, DT_LEFT = 0, DT_RIGHT = 0x2;

    // Géométrie des 61 touches : VirtualKeyboard.BuildKeyLayout (f0a98ba), largeurs seules.
    static List<(float X, float Y, float W, float H, bool Ctx, string Glyph)> Keys()
    {
        var k = new List<(float, float, float, float, bool, string)>();
        float y = 0, x = 0;
        for (int i = 0; i < 13; i++) { k.Add((x, y, 1, 1, false, "é")); x += 1.1f; }
        k.Add((x, y, 2, 1, true, "⌫")); y += 1.1f; x = 0;
        k.Add((x, y, 1.5f, 1, true, "Tab")); x = 1.6f;
        for (int i = 0; i < 12; i++) { k.Add((x, y, 1, 1, false, "A")); x += 1.1f; }
        k.Add((x, y, 1.5f, 2.1f, true, "Entrée")); y += 1.1f; x = 0;
        k.Add((x, y, 1.75f, 1, true, "Verr. Maj.")); x = 1.85f;
        for (int i = 0; i < 12; i++) { k.Add((x, y, 1, 1, false, "Q")); x += 1.1f; }
        y += 1.1f; x = 0;
        k.Add((x, y, 1.25f, 1, true, "Maj ⇧")); x = 1.35f;
        for (int i = 0; i < 11; i++) { k.Add((x, y, 1, 1, false, "W")); x += 1.1f; }
        k.Add((x, y, 2.85f, 1, true, "Maj ⇧")); y += 1.1f; x = 0;
        foreach (var w in new[] { 1.25f, 1.25f, 1.25f, 6.25f, 1.25f, 1.25f, 1.25f, 1.85f }) { k.Add((x, y, w, 1, w < 6, "Ctrl")); x += w + 0.1f; }
        return k;
    }

    public static void Run()
    {
        Console.WriteLine("=== 2. Repeint GDI du clavier (réplique, DC mémoire 864×336, sans fenêtre) ===");
        const int cw = 864, ch = 336;
        var keys = Keys();
        float scale = Math.Min((cw - 20) / 16.3f, (ch - 20) / 5.4f);
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr target = CreateCompatibleDC(screen);
        IntPtr targetBmp = CreateCompatibleBitmap(screen, cw, ch);
        IntPtr oldTarget = SelectObject(target, targetBmp);
        IntPtr fontMain = CreateFontW((int)(scale * 0.72f), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr fontSmall = CreateFontW((int)(scale * 0.42f), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr fontCtx = CreateFontW((int)(scale * 0.35f), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        uint gdi0 = GetGuiResources(GetCurrentProcess(), 0);

        // A. Chemin VirtualKeyboard.PaintContent @ f0a98ba : DC+bitmap par repeint, 5 objets partagés, 1 glyphe/touche.
        double PaintVk()
        {
            long t0 = Stopwatch.GetTimestamp();
            IntPtr hdc = CreateCompatibleDC(screen);
            IntPtr bmp = CreateCompatibleBitmap(screen, cw, ch);
            IntPtr oldBmp = SelectObject(hdc, bmp);
            var rc = new RECT { right = cw, bottom = ch };
            IntPtr bg = CreateSolidBrush(0x00201C18); FillRect(hdc, ref rc, bg); DeleteObject(bg);
            IntPtr bKey = CreateSolidBrush(0x00484038), bCtx = CreateSolidBrush(0x00383028), bPr = CreateSolidBrush(0x00D4A060), bCaps = CreateSolidBrush(0x0000A5FF);
            IntPtr pen = CreatePen(0, 1, 0x00302820);
            SetBkMode(hdc, 1);
            foreach (var k in keys)
            {
                int kx = 10 + (int)(k.X * scale), ky = 10 + (int)(k.Y * scale), kw = (int)(k.W * scale), kh = (int)(k.H * scale);
                IntPtr ob = SelectObject(hdc, k.Ctx ? bCtx : bKey); IntPtr op = SelectObject(hdc, pen);
                RoundRect(hdc, kx, ky, kx + kw, ky + kh, 6, 6);
                SelectObject(hdc, ob); SelectObject(hdc, op);
                SelectObject(hdc, k.Ctx ? fontCtx : fontMain);
                SetTextColor(hdc, 0x00F0EDE8);
                var r = new RECT { left = kx, top = ky, right = kx + kw, bottom = ky + kh };
                DrawTextW(hdc, k.Glyph, k.Glyph.Length, ref r, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_NOCLIP);
            }
            DeleteObject(bKey); DeleteObject(bCtx); DeleteObject(bPr); DeleteObject(bCaps); DeleteObject(pen);
            BitBlt(target, 0, 0, cw, ch, hdc, 0, 0, 0x00CC0020);
            SelectObject(hdc, oldBmp); DeleteObject(bmp); DeleteDC(hdc);
            GdiFlush();
            return (Stopwatch.GetTimestamp() - t0) * 1_000_000.0 / Stopwatch.Frequency;
        }

        // B. Chemin KeyboardRenderer.DrawKey : pinceau + stylo créés PAR TOUCHE, FillRect + 4 LineTo, jusqu'à 4 glyphes.
        double PaintKr(bool cachedObjects)
        {
            long t0 = Stopwatch.GetTimestamp();
            IntPtr hdc = CreateCompatibleDC(screen);
            IntPtr bmp = CreateCompatibleBitmap(screen, cw, ch);
            IntPtr oldBmp = SelectObject(hdc, bmp);
            var rc = new RECT { right = cw, bottom = ch };
            IntPtr bg = CreateSolidBrush(0x00201C18); FillRect(hdc, ref rc, bg); DeleteObject(bg);
            IntPtr cKey = cachedObjects ? CreateSolidBrush(0x003A3A3A) : IntPtr.Zero;
            IntPtr cPen = cachedObjects ? CreatePen(0, 1, 0x00555555) : IntPtr.Zero;
            SetBkMode(hdc, 1);
            string[] four = { "a", "æ", "A", "Æ" };
            foreach (var k in keys)
            {
                var r = new RECT { left = 10 + (int)(k.X * scale), top = 10 + (int)(k.Y * scale) };
                r.right = 10 + (int)((k.X + k.W) * scale) - 1; r.bottom = 10 + (int)((k.Y + k.H) * scale) - 1;
                IntPtr brush = cachedObjects ? cKey : CreateSolidBrush(k.Ctx ? 0x002D2D2Du : 0x003A3A3Au);
                IntPtr pen = cachedObjects ? cPen : CreatePen(0, 1, 0x00555555);
                IntPtr ob = SelectObject(hdc, brush); IntPtr op = SelectObject(hdc, pen);
                FillRect(hdc, ref r, brush);
                MoveToEx(hdc, r.left, r.top, IntPtr.Zero); LineTo(hdc, r.right, r.top); LineTo(hdc, r.right, r.bottom); LineTo(hdc, r.left, r.bottom); LineTo(hdc, r.left, r.top);
                SelectObject(hdc, op); SelectObject(hdc, ob);
                if (!cachedObjects) { DeleteObject(pen); DeleteObject(brush); }
                int kw = r.right - r.left, kh = r.bottom - r.top;
                if (k.Ctx)
                {
                    IntPtr of = SelectObject(hdc, fontCtx); SetTextColor(hdc, 0x00E0E0E0);
                    DrawTextW(hdc, k.Glyph, -1, ref r, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                    SelectObject(hdc, of);
                }
                else
                {
                    for (int q = 0; q < 4; q++)
                    {
                        IntPtr of = SelectObject(hdc, q == 0 ? fontMain : fontSmall);
                        SetTextColor(hdc, q == 0 ? 0x00FFB366u : 0x00999999u);
                        var cr = new RECT { left = r.left + (q % 2) * kw / 2, top = r.top + (q / 2) * kh / 2, right = r.left + (q % 2 + 1) * kw / 2, bottom = r.top + (q / 2 + 1) * kh / 2 };
                        DrawTextW(hdc, four[q], 1, ref cr, DT_SINGLELINE | DT_NOPREFIX | DT_NOCLIP | ((q % 2) == 0 ? DT_LEFT : DT_RIGHT));
                        SelectObject(hdc, of);
                    }
                }
            }
            if (cachedObjects) { DeleteObject(cKey); DeleteObject(cPen); }
            BitBlt(target, 0, 0, cw, ch, hdc, 0, 0, 0x00CC0020);
            SelectObject(hdc, oldBmp); DeleteObject(bmp); DeleteDC(hdc);
            GdiFlush();
            return (Stopwatch.GetTimestamp() - t0) * 1_000_000.0 / Stopwatch.Frequency;
        }

        void Report(string label, Func<double> paint)
        {
            for (int i = 0; i < 50; i++) paint();
            var s = new double[400];
            for (int i = 0; i < s.Length; i++) s[i] = paint();
            Array.Sort(s);
            Console.WriteLine($"  {label,-66} médiane {s[s.Length / 2],6:F0} µs, p95 {s[(int)(s.Length * 0.95)],6:F0} µs, objets GDI du processus ensuite : {GetGuiResources(GetCurrentProcess(), 0)}");
        }

        Report("A. VirtualKeyboard @ f0a98ba (1 glyphe, 5 objets/repeint)", PaintVk);
        Report("B. KeyboardRenderer (pinceau+stylo par touche, 4 glyphes)", () => PaintKr(false));
        Report("B'. idem avec pinceau/stylo mis en cache (proposition)", () => PaintKr(true));
        uint gdi1 = GetGuiResources(GetCurrentProcess(), 0);
        Console.WriteLine($"  Objets GDI du processus avant/après {3 * 450 + 50} repeints : {gdi0} → {gdi1} (réplique sans fuite)");
        Console.WriteLine($"  Créations GDI par repeint : A = 2 (DC, bitmap) + 1 fond + 5 = 8 ; B = 2 + 1 + 2 × {keys.Count} touches = {3 + 2 * keys.Count}");

        SelectObject(target, oldTarget); DeleteObject(targetBmp); DeleteDC(target);
        DeleteObject(fontMain); DeleteObject(fontSmall); DeleteObject(fontCtx);
        ReleaseDC(IntPtr.Zero, screen);
    }
}
