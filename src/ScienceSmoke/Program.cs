// Sonde de liaison : chaque cas de corpus/*.txt du dépôt science passe par l'ABI C et le
// contrat JSON, et ses sorties exactes (latex, umath, canon, type, code d'erreur) sont
// comparées à celles du corpus, comme crates/science-core/tests/corpus.rs le fait en Rust.
//
//   ScienceSmoke.exe <dossier corpus>
//
// Sortie 0 si tout concorde, 1 sinon, 2 si le moteur est absent.
using System.Text;
using AZERTYGlobal;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.WriteLine("usage : ScienceSmoke <dossier corpus>");
    return 2;
}
if (!ScienceEngine.IsAvailable)
{
    Console.WriteLine("moteur absent ou ABI différente");
    return 2;
}

int cases = 0, readings = 0, checks = 0;
var errors = new List<string>();
foreach (var path in Directory.GetFiles(args[0], "*.txt").Order(StringComparer.Ordinal))
{
    string file = Path.GetFileName(path);
    string? id = null, expected = null, input = null;
    bool english = false;
    var expectations = new List<(int N, string Key, string Value)>();

    void Flush()
    {
        if (id is null || input is null) return;
        cases++;
        var r = ScienceEngine.Analyze(input, english);
        string tag = $"{file} {id} « {input} »";
        string got = r.Status switch
        {
            ScienceStatus.Formula => "formule",
            ScienceStatus.Ambiguous => $"lectures {r.Readings.Count}",
            ScienceStatus.Rejected => $"erreur {r.Diagnostics[0].Code}",
            _ => $"erreur-abi {r.Error}",
        };
        if (got != expected)
        {
            errors.Add($"{tag} : attendu {expected}, obtenu {got}");
            return;
        }
        readings += r.Readings.Count;
        foreach (var reading in r.Readings)
        {
            if (!reading.MathML.StartsWith("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"", StringComparison.Ordinal))
                errors.Add($"{tag} : MathML inattendu {reading.MathML}");
        }
        foreach (var (n, key, value) in expectations)
        {
            checks++;
            if (n > r.Readings.Count) { errors.Add($"{tag} : pas de lecture {n}"); continue; }
            var reading = r.Readings[n - 1];
            string actual = key switch { "latex" => reading.Latex, "umath" => reading.UnicodeMath, _ => reading.Canonical };
            if (actual != value) errors.Add($"{tag} : {n} {key} attendu « {value} », obtenu « {actual} »");
        }
    }

    foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
    {
        if (line.StartsWith("=== ", StringComparison.Ordinal))
        {
            Flush();
            var head = line[4..];
            int bar = head.IndexOf(" | ", StringComparison.Ordinal);
            id = (bar < 0 ? head : head[..bar]).Trim();
            expected = bar < 0 ? "formule" : head[(bar + 3)..].Trim();
            input = null;
            english = false;
            expectations.Clear();
            continue;
        }
        if (id is null) continue;
        if (line.StartsWith("> ", StringComparison.Ordinal)) input = line[2..];
        else if (line == ">") input = "";
        else if (line.StartsWith("lang: ", StringComparison.Ordinal)) english = line[6..].Trim() == "en";
        else
        {
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            if (colon < 0) continue;
            string key = line[..colon], value = line[(colon + 2)..];
            int n = 1;
            int space = key.IndexOf(' ');
            if (space >= 0)
            {
                n = int.TryParse(key[..space], out var parsed) ? parsed : 1;
                key = key[(space + 1)..];
            }
            if (key is "latex" or "umath" or "canon") expectations.Add((n, key, value));
        }
    }
    Flush();
}

foreach (var e in errors) Console.WriteLine(e);
Console.WriteLine($"{RuntimeInformationText()} : {cases} cas, {readings} lectures, {checks} sorties exactes comparées, {errors.Count} écart(s)");
return errors.Count == 0 ? 0 : 1;

static string RuntimeInformationText() =>
    $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} " +
    (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported ? "JIT" : "NativeAOT");
