using System.Globalization;
using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// Fichier de l'utilisateur illisible au chargement (audit du 25/09 : L-14 pour la
/// progression des Leçons, report du 24/09 pour config.json). Même distinction que le
/// correctif A-11 de <see cref="UsageStats"/> :
/// <list type="bullet">
/// <item>lecture impossible (verrou d'un antivirus ou d'une sauvegarde, droits) : le fichier
/// est sans doute intact. Rien n'est écrit pendant la session, le prochain lancement relit ;</item>
/// <item>contenu corrompu (JSON invalide, racine inattendue) : le fichier est renommé à côté
/// de l'original, jamais supprimé, et l'on repart de zéro. Les sauvegardes reprennent, et un
/// message le dit une fois.</item>
/// </list>
/// </summary>
internal static class FileQuarantine
{
    /// <summary>Marque du nom de la copie : « config.json.illisible-20260925-213005 ».</summary>
    internal const string Marker = ".illisible-";

    /// <summary>Heure locale de l’horodatage, remplaçable par les témoins.</summary>
    internal static Func<DateTime> Clock = () => DateTime.Now;

    /// <summary>Chemin de la copie, à côté de l'original, horodaté à la seconde.</summary>
    internal static string BuildPath(string path, DateTime localNow) =>
        path + Marker + localNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    /// <summary>Lecture impossible pour l'instant : présent mais verrouillé ou interdit. Un
    /// fichier absent n'en est pas une.</summary>
    internal static bool IsReadFailure(Exception ex) =>
        ex is UnauthorizedAccessException
        || (ex is IOException && ex is not FileNotFoundException && ex is not DirectoryNotFoundException);

    /// <summary>Contenu corrompu : JSON invalide, racine non-objet, valeur mal formée.</summary>
    internal static bool IsCorruption(Exception ex) => ex is JsonException or FormatException;

    /// <summary>Pauses entre les essais de lecture du premier chargement : 350 ms au plus, une
    /// fois par fichier, et seulement quand le fichier est tenu (CE-T2).</summary>
    internal static int[] ReadRetryDelaysMilliseconds = { 50, 100, 200 };

    /// <summary>Attente entre deux essais, remplaçable par les témoins.</summary>
    internal static Action<int> ReadRetrySleep = static ms => Thread.Sleep(ms);

    /// <summary>
    /// Lecture du premier chargement (audit des chemins d'erreur, CE-T2 : constats CE-03 à
    /// CE-06). Un antivirus, une sauvegarde ou une synchronisation tient le fichier quelques
    /// dizaines de millisecondes : un seul essai concluait à l'échec pour toute la session. Ici,
    /// quelques essais courts, comme l'écriture de fermeture de <see cref="ConfigManager"/> ; seul
    /// un échec de lecture (<see cref="IsReadFailure"/>) est réessayé, la dernière exception
    /// sort telle quelle. Les relectures suivantes, au prochain accès, n'essaient qu'une fois.
    /// </summary>
    internal static string ReadAllTextWithRetries(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return File.ReadAllText(path); }
            catch (Exception ex) when (IsReadFailure(ex) && attempt < ReadRetryDelaysMilliseconds.Length)
            {
                ReadRetrySleep(ReadRetryDelaysMilliseconds[attempt]);
            }
        }
    }

    /// <summary>
    /// Renomme le fichier en copie mise de côté. Jamais de suppression, jamais d'écrasement
    /// d'une copie existante. Rend le chemin de la copie, ou null si le renommage a échoué :
    /// l'appelant retombe alors sur la règle de la lecture impossible (aucune écriture).
    /// </summary>
    internal static string? TryMoveAside(string path, string logContext)
    {
        string target = BuildPath(path, Clock());
        try
        {
            File.Move(path, target, overwrite: false);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConfigManager.Log(logContext, ex);
            return null;
        }
    }
}
