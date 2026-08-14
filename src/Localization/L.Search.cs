namespace AZERTYGlobal;

internal static partial class L
{
    public static string Search_WindowTitle => T("Rechercher un caractère", "Find a character");
    public static string Search_Placeholder => T("Rechercher un caractère…", "Find a character…");
    public static string Search_NoResults => T("Aucun résultat", "No results");
    public static string Search_Tip => T("Entrée copier · Échap fermer", "Enter to copy · Esc to close");
    public static string Search_CopiedFeedback(string ch) => T($"« {ch} » copié !", $"\"{ch}\" copied!");
    public static string Search_ResultCountSingular => T("1 résultat — Entrée pour copier", "1 result — Enter to copy");
    public static string Search_ResultCountPlural(int count) => T($"{count} résultats — Entrée pour copier", $"{count} results — Enter to copy");

    // ── Libellés des méthodes de saisie (touche morte, couches) ──────
    public static string Search_SpaceKeyLabel => T("Espace", "Space");
    public static string Search_ThenWord => T("puis", "then");
    public static string Search_CapsLockWord => T("Verr.Maj", "Caps Lock");

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

    /// <summary>Libellé « puis Maj + touche » / « puis touche » après une touche morte.</summary>
    public static string Search_AfterDeadKeyLabel(string layer, string keyLabel) => layer switch
    {
        "Shift" => $"{Search_ThenWord} {Settings_ShortcutModifier2} + {keyLabel}",
        _ => $"{Search_ThenWord} {keyLabel}",
    };
}
