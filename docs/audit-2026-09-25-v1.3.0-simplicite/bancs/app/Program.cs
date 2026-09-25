// Micro-benchmark hors application (audit zone app, 2026-09-25).
// Mesure : (1) chargement + parse JsonDocument de character-index.json (ce que fait
// CharacterSearch.LoadCharacterIndex au demarrage) ; (2) cout d'une sauvegarde de
// config.json au format de ConfigManager.Save (Utf8JsonWriter + Flush(true) + File.Replace) ;
// (3) cout d'un SetBool (JsonDocument.Parse("true") + Clone).
// JIT et non AOT : ordre de grandeur seulement.
using System.Diagnostics;
using System.Text.Json;

string src = args.Length > 0 ? args[0] : throw new ArgumentException("chemin src requis");
string tmpDir = args.Length > 1 ? args[1] : Path.GetTempPath();
string idx = Path.Combine(src, "character-index.json");

static double Median(List<double> xs) { xs.Sort(); return xs[xs.Count / 2]; }

// (1) character-index.json
var parse = new List<double>();
int entries = 0;
for (int i = 0; i < 12; i++)
{
    var sw = Stopwatch.StartNew();
    string json = File.ReadAllText(idx);
    using var doc = JsonDocument.Parse(json);
    int n = 0;
    foreach (var e in doc.RootElement.GetProperty("characters").EnumerateObject())
    {
        if (e.Value.TryGetProperty("unicodeName", out var v)) _ = v.GetString();
        n++;
    }
    sw.Stop();
    entries = n;
    if (i >= 2) parse.Add(sw.Elapsed.TotalMilliseconds); // 2 premiers = chauffe JIT
}
Console.WriteLine($"character-index.json : {new FileInfo(idx).Length} octets, {entries} entrees, " +
                  $"lecture+parse+parcours mediane {Median(parse):F1} ms (min {parse.Min():F1}, max {parse.Max():F1}) sur {parse.Count} essais");

// (2) sauvegarde atomique config.json
var cache = new Dictionary<string, JsonElement>();
for (int k = 0; k < 45; k++)
{
    using var d = JsonDocument.Parse(k % 3 == 0 ? "true" : k % 3 == 1 ? "12" : "\"2026-09-25\"");
    cache["cle" + k] = d.RootElement.Clone();
}
string cfg = Path.Combine(tmpDir, "bench-config.json");
var save = new List<double>();
for (int i = 0; i < 30; i++)
{
    var sw = Stopwatch.StartNew();
    string tmp = cfg + "." + Environment.ProcessId + ".tmp";
    using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
    {
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        foreach (var (key, val) in cache) { w.WritePropertyName(key); val.WriteTo(w); }
        w.WriteEndObject();
        w.Flush();
        stream.Flush(true);
    }
    if (File.Exists(cfg)) File.Replace(tmp, cfg, null, true); else File.Move(tmp, cfg);
    sw.Stop();
    if (i >= 3) save.Add(sw.Elapsed.TotalMilliseconds);
}
Console.WriteLine($"Save config (45 cles, fsync + File.Replace) : mediane {Median(save):F2} ms (min {save.Min():F2}, max {save.Max():F2}) sur {save.Count} essais");
File.Delete(cfg);

// (3) SetBool en memoire (hors Save)
var sw3 = Stopwatch.StartNew();
for (int i = 0; i < 100_000; i++)
{
    using var d = JsonDocument.Parse("true");
    cache["x"] = d.RootElement.Clone();
}
sw3.Stop();
Console.WriteLine($"JsonDocument.Parse(\"true\")+Clone : {sw3.Elapsed.TotalMilliseconds * 1000 / 100_000:F2} us par appel");

// (4) Process.GetProcesses + ProcessName (ce que fait GameRegistry.IsRemoteAccessHostRunning toutes les 5 s)
var gp = new List<double>();
int procCount = 0;
long allocBefore = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < 22; i++)
{
    var sw = Stopwatch.StartNew();
    var ps = System.Diagnostics.Process.GetProcesses();
    foreach (var p in ps) { try { _ = p.ProcessName; } catch { } }
    foreach (var p in ps) p.Dispose();
    sw.Stop();
    procCount = ps.Length;
    if (i >= 2) gp.Add(sw.Elapsed.TotalMilliseconds);
}
long allocPer = (GC.GetTotalAllocatedBytes(true) - allocBefore) / 22;
Console.WriteLine($"Process.GetProcesses ({procCount} processus) : mediane {Median(gp):F2} ms, ~{allocPer / 1024} Kio alloues par sonde");
