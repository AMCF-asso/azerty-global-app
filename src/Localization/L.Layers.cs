namespace AZERTYGlobal;

internal static partial class L
{
    // ── Fenêtre des couches maintenables ─────────────────────────────
    public static string Layers_WindowTitleSuffix => T("Couches maintenables", "Maintainable layers");
    public static string Layers_Title => T("Écrire plusieurs caractères dans un autre alphabet", "Type several characters in another alphabet");
    public static string Layers_Explainer => T(
        "Appui simple : la prochaine frappe.   Maintien : tant que la touche reste enfoncée.\r\n" +
        "Double appui : verrouillage dans l’application active.   Échap : déverrouiller.",
        "Single press: the next keystroke.   Hold: as long as the key is held down.\r\n" +
        "Double press: lock inside the active application.   Esc: unlock.");
    public static string Layers_MasterCheckbox => T("Activer les couches maintenables", "Enable maintainable layers");
    public static string Layers_AvailableLabel => T("Couches disponibles", "Available layers");
    public static string Layers_GreekCheckbox => T("Grec — Maj + *", "Greek — Shift + *");
    public static string Layers_CyrillicCheckbox => T("Cyrillique — AltGr + *", "Cyrillic — AltGr + *");
    public static string Layers_ScientificCheckbox => T("Scientifique — AltGr + =", "Scientific — AltGr + =");
    public static string Layers_VisualCheckbox => T("Afficher un indicateur près du curseur", "Show an indicator near the caret");
    public static string Layers_DelayLabel => T("Délai du double appui :", "Double-press delay:");
    public static string Layers_DelayUnit => T("ms (150 à 1000)", "ms (150 to 1000)");
    public static string Layers_DiagnosticsCheckbox => T(
        "Autoriser l’export volontaire de diagnostics techniques locaux",
        "Allow voluntary export of local technical diagnostics");
    public static string Layers_SaveButton => T("Enregistrer", "Save");
    public static string Layers_ActivatedBody => T(
        "Couches activées.\n\nAppui simple : une frappe.\nMaintien : plusieurs frappes.\n" +
        "Double appui : verrouillage dans l’application.\nÉchap : déverrouiller.",
        "Layers enabled.\n\nSingle press: one keystroke.\nHold: several keystrokes.\n" +
        "Double press: lock inside the application.\nEsc: unlock.");

    // ── Libellés des couches (indicateur, infobulle du tray) ─────────
    public static string Layers_LabelGreek => T("Grec", "Greek");
    public static string Layers_LabelCyrillic => T("Cyrillique", "Cyrillic");
    public static string Layers_LabelScientific => T("Scientifique", "Scientific");
    public static string Layers_IndicatorHeldSuffix => T(" · maintien", " · hold");
    public static string Layers_IndicatorLockedSuffix => T(" · verrou", " · lock");

    // ── Entrées du menu et infobulle ──────────────────────────────────
    public static string Layers_MenuEntry => T("Couches maintenables", "Maintainable layers");
    public static string Layers_TooltipLayer(string label) => T($"Couche : {label}", $"Layer: {label}");

    // ── Notifications (gabarit : état au titre, action au corps) ─────
    public static string Layers_SearchSecureTitle => T("Recherche indisponible", "Search unavailable");
    public static string Layers_SearchSecureBody => T(
        $"Les champs de mot de passe suspendent la recherche de {ProductIdentity.DisplayName}.",
        $"Password fields suspend {ProductIdentity.DisplayName}'s character search.");
    public static string Layers_InsertFallbackTitle => T("Caractère copié", "Character copied");
    public static string Layers_InsertFallbackBody(string ch) => T(
        $"L’insertion directe de {ProductIdentity.DisplayName} n’était pas disponible — collez « {ch} » avec Ctrl+V.",
        $"{ProductIdentity.DisplayName}'s direct insertion was unavailable — paste \"{ch}\" with Ctrl+V.");
}
