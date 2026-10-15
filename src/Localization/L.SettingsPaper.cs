namespace AZERTYGlobal;

internal static partial class L
{
    // ── Paramètres « papier » (refonte 1.3.0, maquette « onglets en tête » du 07/10) ──
    /// <summary>Version écrite à droite de la bande d'onglets.</summary>
    public static string SettingsPaper_Version(string version) => T($"Version {version}", $"Version {version}");

    /// <summary>Nom accessible de la bande d'onglets, porté par une étiquette jamais affichée.</summary>
    public static string SettingsPaper_TabsName => T("Rubriques des paramètres", "Settings sections");

    /// <summary>Ce que règlent les boutons radio de compatibilité : l'application choisie
    /// dans la liste (libellé ajouté par la maquette).</summary>
    public static string SettingsPaper_CompatModeFor(string process) => T($"Mode pour {process}", $"Mode for {process}");

    /// <summary>Le même libellé quand aucune application n'est choisie : les boutons sont grisés.</summary>
    public static string SettingsPaper_CompatModeNone => T("Mode pour l’application choisie", "Mode for the selected app");

    // Ce que fait chaque mode, sous son bouton radio, dit par ce que l'on constate (QCM du 07/10).
    public static string SettingsPaper_CompatHintAuto => T(
        $"Recommandé : {ProductIdentity.DisplayName} s’adapte seul à chaque application.",
        $"Recommended: {ProductIdentity.DisplayName} adapts to each app on its own.");
    public static string SettingsPaper_CompatHintForceOn => T(
        "Si des caractères ne s’écrivent pas dans un jeu : ils sont tapés comme de vraies touches.",
        "If some characters don’t appear in a game: they are typed as real keys.");
    public static string SettingsPaper_CompatHintForceOff => T(
        $"{ProductIdentity.DisplayName} ne tape rien dans cette application ; le clavier Windows reprend.",
        $"{ProductIdentity.DisplayName} types nothing in this app; the Windows keyboard takes over.");
}
