// Socle i18n — pas de .resx (incompatible avec l'AOT single-exe zéro dépendance).
// Chaînes exposées comme propriétés statiques évaluées à l'accès : toute fenêtre
// reconstruite après un changement de langue est automatiquement dans la bonne langue.
namespace AZERTYGlobal;

internal static partial class L
{
    /// <summary>"fr" ou "en". Initialisé au démarrage depuis config.json (ConfigManager.AppLanguage).</summary>
    public static string Language { get; set; } = "fr";

    public static bool IsEnglish => Language == "en";

    /// <summary>Culture utilisée pour formater dates et nombres affichés à l'utilisateur.</summary>
    public static System.Globalization.CultureInfo DisplayCulture =>
        IsEnglish
            ? new System.Globalization.CultureInfo("en-US")
            : new System.Globalization.CultureInfo("fr-FR");

    private static string T(string fr, string en) => IsEnglish ? en : fr;

    /// <summary>Formate une date longue lisible selon la langue courante (ex. "12 mars 2026" / "March 12, 2026").</summary>
    public static string FormatDate(DateOnly date)
    {
        try
        {
            string pattern = IsEnglish ? "MMMM d, yyyy" : "d MMMM yyyy";
            return date.ToDateTime(TimeOnly.MinValue).ToString(pattern, DisplayCulture);
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return date.ToString("yyyy-MM-dd");
        }
    }
}
