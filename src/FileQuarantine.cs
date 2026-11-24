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
    /// Analyse un fichier de l'utilisateur et vérifie que chaque chaîne et chaque nom de
    /// clé se relit (AF-01, audit des fichiers 1.3.0). <see cref="JsonDocument.Parse(string, JsonDocumentOptions)"/>
    /// accepte un demi-caractère isolé échappé (<c>"\uD800"</c>), mais
    /// <see cref="JsonElement.GetString"/> et <see cref="JsonProperty.Name"/> lèvent alors
    /// <see cref="InvalidOperationException"/>, qu'aucun filtre de chargement ne connaît :
    /// plantage au lancement, ou fichier à moitié chargé puis réécrit. Le contenu est donc
    /// refusé ici, d'un bloc et avant toute lecture, comme une corruption
    /// (<see cref="JsonException"/>) : mise de côté et repartir de zéro.
    /// </summary>
    internal static JsonDocument ParseUserFile(string json)
    {
        var doc = JsonDocument.Parse(json);
        try
        {
            EnsureReadable(doc.RootElement);
            return doc;
        }
        catch (InvalidOperationException ex)
        {
            doc.Dispose();
            throw new JsonException("Chaîne illisible (demi-caractère isolé).", ex);
        }
    }

    /// <summary>
    /// Retire les fichiers temporaires « &lt;fichier&gt;.&lt;pid&gt;.tmp » laissés par un autre
    /// processus (AF-06, audit des fichiers 1.3.0) : une coupure entre l'écriture du temporaire
    /// et le remplacement le laisse, et le PID change au lancement suivant ; seul le temporaire
    /// du processus courant était jamais retiré. Celui d'une instance vivante de l'application
    /// (autre session, autre compte sur un exécutable partagé) est laissé : elle peut être en
    /// train de l'écrire. Au mieux : un échec ne fait que reporter au prochain chargement.
    /// </summary>
    internal static void RemoveOrphanTemps(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            string prefix = Path.GetFileName(path) + ".";
            string selfName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            foreach (string temp in Directory.EnumerateFiles(dir, prefix + "*.tmp"))
            {
                string middle = Path.GetFileName(temp)[prefix.Length..^".tmp".Length];
                if (!int.TryParse(middle, NumberStyles.None, CultureInfo.InvariantCulture, out int pid)) continue;
                if (pid == Environment.ProcessId || IsLiveInstance(pid, selfName)) continue;
                try { File.Delete(temp); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConfigManager.Log("FileQuarantine.RemoveOrphanTemps", ex);
        }
    }

    private static bool IsLiveInstance(int pid, string selfName)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return string.Equals(process.ProcessName, selfName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }          // aucun processus de ce numéro
        catch (InvalidOperationException) { return false; }  // terminé entre-temps
        catch (System.ComponentModel.Win32Exception) { return true; } // inaccessible : prudence
    }

    private static void EnsureReadable(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                _ = element.GetString();
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    _ = prop.Name;
                    EnsureReadable(prop.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    EnsureReadable(item);
                break;
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
