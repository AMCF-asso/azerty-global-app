"""Correctifs 1.3.0 de l'audit du 25/09 (F-10, V-07, V-08, V-06, A-11).

Règle 2 du dépôt : jamais d'Edit sur un .cs. Chaque remplacement :
- exige une ancre unique (en CRLF ou en LF, pas les deux) ;
- écrit le bloc neuf avec la fin de ligne de l'ancre trouvée ;
- conserve le BOM ;
- imprime les comptes CRLF/LF avant et après.
Relancer le script sur un fichier déjà patché échoue (ancre absente) : pas de double application.
"""
import pathlib, sys

sys.stdout.reconfigure(encoding="utf-8")
RACINE = pathlib.Path("D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store")
BOM = b"\xef\xbb\xbf"


def comptes(b):
    c = b.count(b"\r\n")
    return c, b.count(b"\n") - c


ETAT = {}  # rel -> [bom, texte, octets d'origine] ; rien n'est écrit avant la fin


def patch(rel, old, new):
    """Toutes les ancres contiennent au moins un saut de ligne. Une ancre en LF ne peut pas
    être sous-chaîne d'une ancre en CRLF (le \\n y est précédé d'un \\r) : les deux comptes
    sont indépendants. Une ancre à cheval sur des fins mixtes ne matche ni l'un ni l'autre,
    et fait donc échouer le script plutôt que d'écrire une fin de ligne au hasard."""
    assert "\n" in old
    if rel not in ETAT:
        raw = (RACINE / rel).read_bytes()
        bom = raw.startswith(BOM)
        ETAT[rel] = [bom, (raw[len(BOM):] if bom else raw).decode("utf-8"), raw]
    bom, texte, raw = ETAT[rel]
    a_crlf, a_lf = old.replace("\n", "\r\n"), old
    n_crlf, n_lf = texte.count(a_crlf), texte.count(a_lf)
    if (n_crlf, n_lf) == (1, 0):
        eol, ancre = "\r\n", a_crlf
    elif (n_crlf, n_lf) == (0, 1):
        eol, ancre = "\n", a_lf
    else:
        sys.exit(f"ARRÊT {rel} : ancre non unique ou absente (CRLF {n_crlf}, LF {n_lf}) — rien n'a été écrit\n---\n{old}")
    ETAT[rel][1] = texte.replace(ancre, new.replace("\n", eol), 1)
    print(f"  {rel}: ancre {'CRLF' if eol == chr(13) + chr(10) else 'LF'}, {new.count(chr(10)) - old.count(chr(10)):+d} lignes")


def ecrire_tout():
    for rel, (bom, texte, raw) in ETAT.items():
        out = (BOM if bom else b"") + texte.encode("utf-8")
        (RACINE / rel).write_bytes(out)
        print(f"{rel}: CRLF/LF {comptes(raw)} -> {comptes(out)}, BOM {bom}")


# ─── F-10 : Échap sur un lien de l'Accueil ferme la fenêtre, comme la croix ───────────
patch("src/OnboardingWindow.cs",
"""                if (wParam == (IntPtr)0x0D) // VK_RETURN
                {
                    int ctrlId = Win32.GetDlgCtrlID(hWnd);
                    Win32.SendMessageW(_hWnd, Win32.WM_COMMAND, (IntPtr)ctrlId, hWnd);
                    return IntPtr.Zero;
                }
                break;
            case Win32.WM_SETFOCUS:
""",
"""                if (wParam == (IntPtr)0x0D) // VK_RETURN
                {
                    int ctrlId = Win32.GetDlgCtrlID(hWnd);
                    Win32.SendMessageW(_hWnd, Win32.WM_COMMAND, (IntPtr)ctrlId, hWnd);
                    return IntPtr.Zero;
                }
                // Échap est gardé par le lien (WANTALLKEYS) : IsDialogMessageW n'en fait pas
                // un IDCANCEL. Même sortie que la croix (audit du 25/09, F-10 ; À propos et
                // les statistiques le faisaient déjà).
                if (wParam == (IntPtr)0x1B) // VK_ESCAPE
                {
                    Close(validated: false);
                    return IntPtr.Zero;
                }
                break;
            case Win32.WM_SETFOCUS:
""")

# ─── V-07 : le clic suit la géométrie du dessin ───────────────────────────────────────
patch("src/CharacterSearch.cs",
"""        // Trouver quel résultat a été cliqué — on calcule les hauteurs de lignes
        int y = searchH;
        for (int i = _scrollOffset; i < _filteredResults.Count && i < _scrollOffset + VISIBLE_RESULTS; i++)
        {
            int rowH = GetRowHeight(i);
            if (mouseY >= y && mouseY < y + rowH)
            {
                _selectedIndex = i;
                NotifySelectionChanged();
                InsertSelectedCharacter();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                return;
            }
            y += rowH;
        }
    }
""",
"""        int rowCount = Math.Min(VISIBLE_RESULTS, _filteredResults.Count - _scrollOffset);
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
""")

# ─── V-08 : le pied dit « 20 sur 98 » quand la liste est plafonnée ─────────────────────
patch("src/CharacterSearch.cs",
"""    private List<CharEntry> _filteredResults = new();
""",
"""    private List<CharEntry> _filteredResults = new();
    private int _totalMatches; // correspondances avant le plafond MAX_RESULTS (audit du 25/09, V-08)
""")
patch("src/CharacterSearch.cs",
"""        _filteredResults.Clear();
        _selectedIndex = 0;
        _scrollOffset = 0;
""",
"""        _filteredResults.Clear();
        _totalMatches = 0;
        _selectedIndex = 0;
        _scrollOffset = 0;
""")
patch("src/CharacterSearch.cs",
"""        scored.Sort((a, b) => b.score.CompareTo(a.score));
        foreach (var (entry, _) in scored.Take(MAX_RESULTS))
""",
"""        scored.Sort((a, b) => b.score.CompareTo(a.score));
        _totalMatches = scored.Count;
        foreach (var (entry, _) in scored.Take(MAX_RESULTS))
""")
patch("src/CharacterSearch.cs",
"""            var countText = _filteredResults.Count == 1
                ? L.Search_ResultCountSingular
                : L.Search_ResultCountPlural(_filteredResults.Count);
""",
"""            var countText = FooterCountText(_filteredResults.Count, _totalMatches);
""")

# ─── V-06 : couleurs de la méthode de saisie dans la langue courante ──────────────────
patch("src/CharacterSearch.cs",
"""                // Couleur selon le token
                uint color;
                if (token == "AltGr")
                    color = CLR_METHOD_ALTGR;
                else if (token == "Maj")
                    color = CLR_METHOD_MAJ;
                else if (token == "+" || token == "puis")
                    color = CLR_METHOD_SEP;
                else
                    color = CLR_METHOD_KEY;
""",
"""                // Couleur selon le token
                uint color = ClassifyMethodToken(token) switch
                {
                    MethodToken.AltGr => CLR_METHOD_ALTGR,
                    MethodToken.Shift => CLR_METHOD_MAJ,
                    MethodToken.Separator => CLR_METHOD_SEP,
                    _ => CLR_METHOD_KEY,
                };
""")
patch("src/CharacterSearch.cs",
"""    /// <summary>Dessine la méthode de saisie avec des couleurs par token.</summary>
""",
"""    /// <summary>
    /// Texte du pied de la liste. Audit du 25/09 (V-08) : la liste est plafonnée à
    /// <see cref="MAX_RESULTS"/> lignes, et le pied annonçait « 20 résultats » quand il y
    /// en avait 98 ; il dit maintenant « 20 sur 98 ».
    /// </summary>
    internal static string FooterCountText(int shown, int total)
    {
        if (total > shown) return L.Search_ResultCountCapped(shown, total);
        return shown == 1 ? L.Search_ResultCountSingular : L.Search_ResultCountPlural(shown);
    }

    internal enum MethodToken { Key, AltGr, Shift, Separator }

    /// <summary>
    /// Genre d'un mot de la méthode de saisie, comparé aux libellés de la langue courante.
    /// Audit du 25/09 (V-06) : les mots étaient comparés à « Maj » et « puis » en dur, et en
    /// anglais « Shift » et « then » prenaient la couleur d'une touche. « Verr. Maj. » et
    /// « Caps Lock » restent sans couleur propre dans les deux langues, comme avant.
    /// </summary>
    internal static MethodToken ClassifyMethodToken(string token)
    {
        if (token == "AltGr") return MethodToken.AltGr;
        if (token == L.Settings_ShortcutModifier2) return MethodToken.Shift;
        if (token == "+" || token == L.Search_ThenWord) return MethodToken.Separator;
        return MethodToken.Key;
    }

    /// <summary>Dessine la méthode de saisie avec des couleurs par token.</summary>
""")

patch("src/Localization/L.Search.cs",
"""    public static string Search_ResultCountPlural(int count) => T($"{count} résultats — Entrée pour insérer", $"{count} results — Enter to insert");
""",
"""    public static string Search_ResultCountPlural(int count) => T($"{count} résultats — Entrée pour insérer", $"{count} results — Enter to insert");
    public static string Search_ResultCountCapped(int shown, int total) => T($"{shown} sur {total} résultats — Entrée pour insérer", $"{shown} of {total} results — Enter to insert");
""")

# ─── A-11 : ne pas écraser usage-stats.json après une lecture ratée ───────────────────
patch("src/UsageStats.cs",
"""    private static bool _loaded;
    private static bool _dirty;
""",
"""    private static bool _loaded;
    private static bool _dirty;
    // Audit du 25/09 (A-11) : le fichier existait mais n'a pas pu être lu. Il n'est jamais
    // réécrit pendant cette session ; le prochain lancement relit.
    private static bool _readFailed;
""")
patch("src/UsageStats.cs",
"""            _statsPath = path;
            _loaded = false;
            _dirty = false;
""",
"""            _statsPath = path;
            _loaded = false;
            _dirty = false;
            _readFailed = false;
""")
patch("src/UsageStats.cs",
"""        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or FormatException)
        {
            // Fichier absent ou corrompu : redémarrer avec des statistiques vides plutôt
            // que de planter l'app (cf. Plan implémentation v1.1.md § 1d).
            ResetInMemoryState();
            ConfigManager.Log("UsageStats.EnsureLoaded", ex);
        }
""",
"""        catch (Exception ex) when ((ex is IOException and not FileNotFoundException and not DirectoryNotFoundException)
                                   or ex is UnauthorizedAccessException)
        {
            // Audit du 25/09 (A-11) : fichier présent mais illisible pour l'instant (verrou d'un
            // antivirus ou d'une sauvegarde, droits). Il est sans doute intact : repartir de
            // zéro en mémoire, mais ne jamais l'écraser avec ces compteurs-là.
            ResetInMemoryState();
            _readFailed = true;
            ConfigManager.Log("UsageStats.EnsureLoaded", ex);
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException)
        {
            // Fichier absent ou corrompu : redémarrer avec des statistiques vides plutôt
            // que de planter l'app (cf. Plan implémentation v1.1.md § 1d).
            ResetInMemoryState();
            ConfigManager.Log("UsageStats.EnsureLoaded", ex);
        }
""")
patch("src/UsageStats.cs",
"""        if (!CollectionEnabled) return true;

        // AG130-11 (b)""",
"""        if (!CollectionEnabled) return true;

        // Lecture ratée au chargement (A-11) : même réponse, pour la même raison — rien ne
        // doit remplacer un fichier qu'on n'a pas pu lire.
        if (_readFailed) return true;

        // AG130-11 (b)""")

ecrire_tout()
print("OK")
