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
}
