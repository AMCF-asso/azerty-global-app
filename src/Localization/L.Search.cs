namespace AZERTYGlobal;

internal static partial class L
{
    public static string Search_WindowTitle => T("Rechercher un caractère", "Find a character");
    // Recherche par langue (1.3.0) : le champ dit qu'un nom de langue suffit (« wolof », « pulaar »).
    public static string Search_Placeholder => T("Caractère, nom ou langue…", "Character, name or language…");
    public static string Search_NoResults => T("Aucun résultat", "No results");
    public static string Search_Tip => T("Entrée pour insérer · Échap pour fermer", "Enter to insert · Esc to close");
    public static string Search_CopiedFeedback(string ch) => T($"« {ch} » copié !", $"“{ch}” copied!");
    /// <summary>A18 : le résultat choisi, tel que le lecteur d'écran le lit sur le champ.</summary>
    public static string Search_AccessibleResult(int position, int count, string glyph, string name, string gesture)
        => T($"Résultat {position} sur {count} : {glyph}, {name}. {gesture}",
            $"Result {position} of {count}: {glyph}, {name}. {gesture}");
    public static string Search_ResultCountSingular => T("1 résultat — Entrée pour insérer", "1 result — Enter to insert");
    public static string Search_ResultCountPlural(int count) => T($"{count} résultats — Entrée pour insérer", $"{count} results — Enter to insert");
    public static string Search_ResultCountCapped(int shown, int total) => T($"{shown} sur {total} résultats — Entrée pour insérer", $"{shown} of {total} results — Enter to insert");

    // ── Libellés des méthodes de saisie (touche morte, couches) ──────
    public static string Search_SpaceKeyLabel => T("Espace", "Space");
    public static string Search_ThenWord => T("puis", "then");
    public static string Search_CapsLockWord => T("Verr. Maj.", "Caps Lock");

    /// <summary>Libellé d'une couche + touche (ex. "Maj + é", "Verr.Maj + AltGr + Maj + à").</summary>
    public static string Search_LayerKeyLabel(string layer, string keyLabel) => layer switch
    {
        "Base" => keyLabel,
        "Shift" => $"{Settings_ShortcutModifier2} + {keyLabel}",
        "AltGr" => $"AltGr + {keyLabel}",
        "AltGr+Shift" or "Shift+AltGr" => $"AltGr + {Settings_ShortcutModifier2} + {keyLabel}",
        "Caps" => $"{Search_CapsLockWord} + {keyLabel}",
        "Caps+Shift" => $"{Search_CapsLockWord} + {Settings_ShortcutModifier2} + {keyLabel}",
        "Caps+AltGr" => $"{Search_CapsLockWord} + AltGr + {keyLabel}",
        "Caps+Shift+AltGr" or "Caps+AltGr+Shift" => $"{Search_CapsLockWord} + AltGr + {Settings_ShortcutModifier2} + {keyLabel}",
        _ => keyLabel,
    };

    /// <summary>
    /// Seconde touche après une touche morte : « puis » suivi de sa couche et de la touche,
    /// comme une méthode directe (« puis Maj + E », « puis AltGr + E »). Audit final 1.3.0,
    /// A02-02 : seule la couche Maj était dite, et « ∉ » se lisait « puis E », qui donne « ∈ ».
    /// </summary>
    public static string Search_AfterDeadKeyLabel(string layer, string keyLabel) =>
        $"{Search_ThenWord} {Search_LayerKeyLabel(layer, keyLabel)}";
}
