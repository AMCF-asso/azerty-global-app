namespace AZERTYGlobal;

internal static partial class L
{
    public static string About_WindowTitle => T($"{Product} — À propos", $"{Product} — About");
    public static string About_LinkSite => T("Site web", "Website");
    public static string About_LinkGithub => T("Code source GitHub", "GitHub source code");
    public static string About_LinkLicense => T("Licence EUPL 1.2 (open source)", "EUPL 1.2 license (open source)");
    public static string About_LinkAmcf => "Association pour la Modernisation du Clavier Français";
    public static string About_Close => T("Fermer", "Close");
    public static string About_Description => T(
        "Un AZERTY amélioré, gratuit et libre.",
        "An improved AZERTY, free and open source.");
    public static string About_AmcfPrefix => T("Édité par l’", "Published by the ");
    public static string About_AmcfSuffix => " (AMCF)";

    // S1-2 (décision S1 du 2026-09-02, textes validés fr + en) : le canal suit la version, sur
    // les trois canaux, séparé par le point médian.
    public static string About_VersionLine(string version, string channel) => $"Version {version} · {channel}";
    public static string About_Channel(DistributionChannel channel) => channel switch
    {
        DistributionChannel.Store => "Microsoft Store",
        DistributionChannel.Amcf => "AMCF",
        _ => T("hors paquet", "unpackaged"),
    };
}
