// Banc hors application — audit zone lecons 2026-09-25 (préfixe L-).
// Réplique, sans référence à l'application, de :
//   1. LearningModule.LoadCharacterMethods + CreateMethodData (src/LearningModule.cs:660-736, 1084-1103 @ f0a98ba),
//      rejoué à chaque lancement du tutoriel ;
//   2. LessonHintProvider.LoadCharacterIndex (src/LessonHintProvider.cs:72-138) ;
//   3. LessonCatalogLoader.LoadFromResource + ComputeExerciseHash (src/LessonCatalog.cs:126-199) ;
//   4. le travail hors GDI d'un repeint de LessonsWindow : CountCompleted (StableKey recalculée),
//      comptes LINQ de la barre latérale, AddRequiredCharacters sur la consigne (src/LessonsWindow.cs:1021-1080, 1465-1497) ;
//   5. la propriété LearningModule.Steps reconstruite à chaque accès (62 accès par repeint, compte lu).
// Usage : dotnet run -c Release -- "<dossier src de l'app>"
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class Program
{
    static int Main(string[] args)
    {
        string src = args.Length > 0 ? args[0] : ".";
        string ci = Path.Combine(src, "character-index.json");
        string lessons = Path.Combine(src, "lessons.json");
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine($"Runtime : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} (JIT ; l'app publiée est AOT)");
        Console.WriteLine($"character-index.json : {new FileInfo(ci).Length:N0} octets ; lessons.json : {new FileInfo(lessons).Length:N0} octets");
        Console.WriteLine();

        Measure("1. Tutoriel : LoadCharacterMethods (ReadToEnd + JsonDocument + 2 passes)", () => LmLoad.Run(ci), 15);
        Measure("2. Leçons : LessonHintProvider.LoadCharacterIndex (Parse(stream))", () => LhpLoad.Run(ci), 15);
        Measure("3. Leçons : LessonCatalog (parse + 79 SHA-256)", () => Catalog.Load(lessons), 15);

        // Mémoire retenue par les deux index (ce que garde un tutoriel ouvert / une fenêtre Leçons masquée)
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true);
        var keepLm = LmLoad.Run(ci);
        long afterLm = GC.GetTotalMemory(true);
        var keepLhp = LhpLoad.Run(ci);
        long afterLhp = GC.GetTotalMemory(true);
        Console.WriteLine($"Mémoire retenue : index du tutoriel {(afterLm - before) / 1024} Ko ; index des indices Leçons {(afterLhp - afterLm) / 1024} Ko");
        GC.KeepAlive(keepLm); GC.KeepAlive(keepLhp);
        Console.WriteLine();

        var catalog = Catalog.Load(lessons);
        var hints = LhpLoad.Run(ci);
        PaintWork.Run(catalog, hints);
        Console.WriteLine();
        StepsProperty.Run();
        Console.WriteLine();
        MeasureChars.Run();
        return 0;
    }

    static void Measure(string label, Func<object> action, int runs)
    {
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        GC.KeepAlive(action());
        sw.Stop();
        long a1 = GC.GetAllocatedBytesForCurrentThread();
        var times = new List<double>();
        for (int i = 0; i < runs; i++)
        {
            var s = Stopwatch.StartNew();
            GC.KeepAlive(action());
            s.Stop();
            times.Add(s.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        Console.WriteLine($"{label}");
        Console.WriteLine($"   1er passage (JIT compris) {sw.Elapsed.TotalMilliseconds:F1} ms, alloué {(a1 - a0) / 1024.0 / 1024.0:F2} Mo ; chaud : médiane {times[times.Count / 2]:F2} ms, min {times[0]:F2} ms ({runs} passages)");
    }
}

sealed class MethodData
{
    public string Type = "", Key = "", Layer = "", DeadKey = "", DkActivationKey = "", DkActivationLayer = "";
}

static class LmLoad
{
    public static object Run(string path)
    {
        var dkActivations = new Dictionary<string, (string key, string layer)>();
        var charMethods = new Dictionary<string, MethodData>();
        var charMethodsCaps = new Dictionary<string, MethodData>();
        var charDeadKeyMethods = new Dictionary<string, List<MethodData>>();
        var charNames = new Dictionary<string, (string, string)>();
        string json;
        using (var reader = new StreamReader(path)) json = reader.ReadToEnd();
        using var doc = JsonDocument.Parse(json);
        var characters = doc.RootElement.GetProperty("characters");
        foreach (var entry in characters.EnumerateObject())
        {
            if (!entry.Name.StartsWith("dk:")) continue;
            if (!entry.Value.TryGetProperty("methods", out var methods)) continue;
            foreach (var method in methods.EnumerateArray())
            {
                if (method.GetProperty("type").GetString() != "deadkey_activation") continue;
                dkActivations[method.GetProperty("deadkey").GetString() ?? ""] =
                    (method.GetProperty("key").GetString() ?? "", method.GetProperty("layer").GetString() ?? "");
                break;
            }
        }
        foreach (var entry in characters.EnumerateObject())
        {
            if (entry.Name.StartsWith("dk:")) continue;
            if (!entry.Value.TryGetProperty("methods", out var methods)) continue;
            JsonElement? recommended = null, capsMethod = null, fallback = null;
            var dkm = new List<MethodData>();
            foreach (var method in methods.EnumerateArray())
            {
                var layer = method.TryGetProperty("layer", out var l) ? l.GetString() ?? "" : "";
                if (capsMethod == null && layer.StartsWith("Caps")) capsMethod = method;
                if (recommended == null && method.TryGetProperty("recommended", out var rec) && rec.GetBoolean()) recommended = method;
                fallback ??= method;
                var md = Create(method, dkActivations);
                if (md.Type == "deadkey" && md.DeadKey.Length > 0) dkm.Add(md);
            }
            JsonElement? chosen = recommended ?? fallback;
            if (!chosen.HasValue) continue;
            charMethods[entry.Name] = Create(chosen.Value, dkActivations);
            if (dkm.Count > 0) charDeadKeyMethods[entry.Name] = dkm;
            if (capsMethod.HasValue) charMethodsCaps[entry.Name] = Create(capsMethod.Value, dkActivations);
            string fr = entry.Value.TryGetProperty("unicodeNameFr", out var nfr) ? nfr.GetString() ?? "" : "";
            string en = entry.Value.TryGetProperty("unicodeName", out var nen) ? nen.GetString() ?? "" : "";
            if (fr.Length > 0 || en.Length > 0) charNames[entry.Name] = (fr, en);
        }
        return (charMethods, charMethodsCaps, charDeadKeyMethods, charNames, dkActivations);
    }

    static MethodData Create(JsonElement method, Dictionary<string, (string key, string layer)> dk)
    {
        var md = new MethodData
        {
            Type = method.GetProperty("type").GetString() ?? "",
            Key = method.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
            Layer = method.TryGetProperty("layer", out var l) ? l.GetString() ?? "" : "",
        };
        if (md.Type == "deadkey")
        {
            md.DeadKey = method.GetProperty("deadkey").GetString() ?? "";
            if (dk.TryGetValue(md.DeadKey, out var a)) { md.DkActivationKey = a.key; md.DkActivationLayer = a.layer; }
        }
        return md;
    }
}

sealed record HintMethod(string Type, string? Key, string? Layer, string? DeadKey, string? DkActivationKey, string? DkActivationLayer)
{
    public string? DeadKeyToken => !string.IsNullOrEmpty(DeadKey) && DeadKey.StartsWith("dk_", StringComparison.Ordinal) ? "dk:" + DeadKey[3..] : null;
}

static class LhpLoad
{
    public static Dictionary<string, HintMethod> Run(string path)
    {
        var result = new Dictionary<string, HintMethod>(StringComparer.Ordinal);
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var characters = doc.RootElement.GetProperty("characters");
        var dkActivations = new Dictionary<string, (string Key, string Layer)>(StringComparer.Ordinal);
        foreach (var entry in characters.EnumerateObject())
        {
            if (!entry.Name.StartsWith("dk:", StringComparison.Ordinal)) continue;
            if (!entry.Value.TryGetProperty("methods", out var methods) || methods.ValueKind != JsonValueKind.Array) continue;
            foreach (var method in methods.EnumerateArray())
            {
                if (Read(method, "type") != "deadkey_activation") continue;
                string? d = Read(method, "deadkey"), k = Read(method, "key"), l = Read(method, "layer");
                if (!string.IsNullOrEmpty(d) && !string.IsNullOrEmpty(k)) dkActivations[d] = (k, l ?? "Base");
                break;
            }
        }
        foreach (var entry in characters.EnumerateObject())
        {
            if (entry.Name.StartsWith("dk:", StringComparison.Ordinal)) continue;
            if (!entry.Value.TryGetProperty("methods", out var methods) || methods.ValueKind != JsonValueKind.Array) continue;
            JsonElement? selected = null;
            foreach (var method in methods.EnumerateArray())
            {
                if (method.TryGetProperty("recommended", out var rec) && rec.ValueKind == JsonValueKind.True) { selected = method; break; }
                selected ??= method;
            }
            if (!selected.HasValue) continue;
            var el = selected.Value;
            string? dk = Read(el, "deadkey");
            dkActivations.TryGetValue(dk ?? "", out var act);
            result[entry.Name] = new HintMethod(Read(el, "type") ?? "", Read(el, "key"), Read(el, "layer"), dk, act.Key, act.Layer);
        }
        return result;
    }

    static string? Read(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

sealed class Exercise
{
    public Exercise(string m, string l, int i, string type, string instr, string content)
    {
        ModuleId = m; LessonId = l; Index = i; Content = content.Replace("\r\n", "\n");
        Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001F', type, instr, Content)))).ToLowerInvariant();
    }
    public string ModuleId, LessonId, Content, Hash; public int Index;
    public string StableKey => $"{ModuleId}/{LessonId}/{Index}/{Hash}";
}

sealed record Lesson(string Id, string Title, List<string> Characters, List<Exercise> Exercises);
sealed record Module(string Id, string Title, List<Lesson> Lessons);

static class Catalog
{
    public static List<Module> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var modules = new List<Module>();
        // Module d'initiation synthétique : 6 exercices (réplique de BuildInitiationModule)
        var init = new List<Exercise>();
        for (int i = 0; i < 6; i++) init.Add(new Exercise("initiation", "premiers-pas", i, "practice", "consigne " + i, "cible " + i));
        modules.Add(new Module("initiation", "Initiation", new() { new Lesson("premiers-pas", "Premiers pas", new(), init) }));
        foreach (var m in doc.RootElement.GetProperty("modules").EnumerateArray())
        {
            string mid = m.GetProperty("id").GetString()!;
            var ls = new List<Lesson>();
            foreach (var l in m.GetProperty("lessons").EnumerateArray())
            {
                string lid = l.GetProperty("id").GetString()!;
                var chars = new List<string>();
                if (l.TryGetProperty("characters", out var c)) foreach (var x in c.EnumerateArray()) chars.Add(x.GetString() ?? "");
                var ex = new List<Exercise>(); int idx = 0;
                foreach (var e in l.GetProperty("exercises").EnumerateArray())
                    ex.Add(new Exercise(mid, lid, idx++, "practice", e.GetProperty("instruction").GetString()!, e.GetProperty("content").GetString()!));
                ls.Add(new Lesson(lid, l.GetProperty("title").GetString()!, chars, ex));
            }
            modules.Add(new Module(mid, m.GetProperty("title").GetString()!, ls));
        }
        return modules;
    }
}

static class PaintWork
{
    public static void Run(List<Module> modules, Dictionary<string, HintMethod> hints)
    {
        var all = modules.SelectMany(m => m.Lessons).SelectMany(l => l.Exercises).ToList();
        // Progression : un exercice sur deux terminé (clé = StableKey, hash re-comparé comme GetValidProgress)
        var progress = new Dictionary<string, (string Hash, bool Completed)>(StringComparer.Ordinal);
        for (int i = 0; i < all.Count; i += 2) progress[all[i].StableKey] = (all[i].Hash, true);
        bool IsCompleted(Exercise e) => progress.TryGetValue(e.StableKey, out var p) && p.Hash == e.Hash && p.Completed;

        int moduleIndex = 1;
        var lesson = modules[moduleIndex].Lessons[0];
        var exercise = lesson.Exercises[0];
        int sink = 0;

        void OnePaint()
        {
            // DrawHeader : _progress.CountCompleted(_catalog)
            foreach (var e in all) if (IsCompleted(e)) sink++;
            // DrawSidebar : par module, deux SelectMany + Count ; libellés interpolés
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                int completed = m.Lessons.SelectMany(l => l.Exercises).Count(IsCompleted);
                int total = m.Lessons.SelectMany(l => l.Exercises).Count();
                string label = $"{m.Title}  {completed}/{total}";
                sink += label.Length;
                Action a = () => sink++; GC.KeepAlive(a); // fermeture AddClick
                if (i != moduleIndex) continue;
                foreach (var l in m.Lessons)
                {
                    int lc = l.Exercises.Count(IsCompleted);
                    string ll = $"{l.Title}  {lc}/{l.Exercises.Count}";
                    sink += ll.Length;
                    Action b = () => sink++; GC.KeepAlive(b);
                }
            }
            // DrawKeyboard (profil Leçon) : AddRequiredCharacters(lesson.Characters) + AddRequiredCharacters(exercise.Content)
            var visible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in lesson.Characters) AddRequired(s, visible, hints);
            AddRequired(exercise.Content, visible, hints);
            sink += visible.Count;
        }

        for (int i = 0; i < 200; i++) OnePaint(); // chauffe
        const int N = 2000;
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) OnePaint();
        sw.Stop();
        long a1 = GC.GetAllocatedBytesForCurrentThread();
        Console.WriteLine("4. Repeint LessonsWindow, part hors GDI répliquée (en-tête + barre latérale + caractères requis)");
        Console.WriteLine($"   {all.Count} exercices, {modules.Count} modules : {sw.Elapsed.TotalMilliseconds * 1000 / N:F1} µs et {(a1 - a0) / N / 1024.0:F1} Ko alloués par repeint (moyenne sur {N})");

        // Variante proposée : clé calculée une fois (champ), comptes mis en cache par module
        var keys = all.ToDictionary(e => e, e => e.StableKey);
        a0 = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        for (int i = 0; i < N; i++)
            foreach (var e in all) if (progress.TryGetValue(keys[e], out var p) && p.Completed) sink++;
        sw.Stop();
        a1 = GC.GetAllocatedBytesForCurrentThread();
        Console.WriteLine($"   variante clé mémorisée (CountCompleted seul) : {sw.Elapsed.TotalMilliseconds * 1000 / N:F1} µs et {(a1 - a0) / N / 1024.0:F1} Ko par repeint");
        GC.KeepAlive(sink);
    }

    static void AddRequired(string text, HashSet<string> visible, Dictionary<string, HintMethod> hints)
    {
        foreach (char ch in text)
        {
            if (ch == '\r' || ch == '\n') continue;
            visible.Add(ch.ToString());
            if (hints.TryGetValue(ch.ToString(), out var m) && !string.IsNullOrEmpty(m.DeadKeyToken)) visible.Add(m.DeadKeyToken);
        }
    }
}

static class MeasureChars
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr CreateFontW(int h, int w, int e, int o, int weight, uint i, uint u, uint s, uint cs, uint op, uint cp, uint q, uint pf, string face);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int DrawTextW(IntPtr hdc, string text, int len, ref RECT rc, uint flags);
    const uint DT_CALCRECT = 0x400, DT_SINGLELINE = 0x20, DT_NOPREFIX = 0x800;

    // LessonsWindow : une ligne de 62 caractères (WrapText 62) est mesurée caractère par caractère
    // 4 fois par repeint (2 préfixes dans CalculateLessonLineScrollOffset, cellules cible, cellules saisies).
    public static void Run()
    {
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr font = CreateFontW(-17, 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 5, 0, "Consolas");
        SelectObject(dc, font);
        string line = "Lætitia demande « d'où vient ce chef-d'œuvre… » — elle l'approuve à 100 %.".Substring(0, 62);
        int sink = 0;
        void OnePaint()
        {
            for (int pass = 0; pass < 4; pass++)
                foreach (char ch in line)
                {
                    SelectObject(dc, font); // MeasureSingleLineWidth resélectionne la police à chaque appel
                    var r = new RECT { right = 9999, bottom = 9999 };
                    DrawTextW(dc, ch.ToString(), -1, ref r, DT_SINGLELINE | DT_NOPREFIX | DT_CALCRECT);
                    sink += r.right;
                }
        }
        for (int i = 0; i < 200; i++) OnePaint();
        const int N = 2000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) OnePaint();
        sw.Stop();
        Console.WriteLine("6. LessonsWindow : mesures de largeur par caractère (4 x 62 DrawTextW DT_CALCRECT par repeint)");
        Console.WriteLine($"   {sw.Elapsed.TotalMilliseconds * 1000 / N:F0} µs par repeint (DC mémoire, Consolas 17 px)");
        DeleteObject(font); DeleteDC(dc);
        GC.KeepAlive(sink);

        // Tampon de double buffering recréé à chaque WM_PAINT (LessonsWindow.OnPaint 965-987,
        // LearningModule.OnPaint 2002-2049) : zone client 1120x760 à 100 %, 1680x1140 à 150 %.
        IntPtr screen = GetDC(IntPtr.Zero);
        foreach (var (w, h) in new[] { (1088, 721), (1632, 1082) })
        {
            for (int i = 0; i < 20; i++) Cycle(screen, w, h);
            const int M = 300;
            var s2 = Stopwatch.StartNew();
            for (int i = 0; i < M; i++) Cycle(screen, w, h);
            s2.Stop();
            Console.WriteLine($"7. Tampon {w}x{h} : CreateCompatibleDC + CreateCompatibleBitmap + SelectObject + remplissage + suppression = {s2.Elapsed.TotalMilliseconds * 1000 / M:F0} µs par repeint");
        }
        ReleaseDC(IntPtr.Zero, screen);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool PatBlt(IntPtr hdc, int x, int y, int w, int h, uint rop);

    static void Cycle(IntPtr screen, int w, int h)
    {
        IntPtr mem = CreateCompatibleDC(screen);
        IntPtr bmp = CreateCompatibleBitmap(screen, w, h);
        IntPtr old = SelectObject(mem, bmp);
        PatBlt(mem, 0, 0, w, h, 0x00000042); // BLACKNESS : touche toute la surface, comme le FillSolidRect du fond
        SelectObject(mem, old);
        DeleteObject(bmp);
        DeleteDC(mem);
    }
}

static class StepsProperty
{
    readonly record struct Step(string Title, string Instruction, string Target, bool Skippable, bool KeepCaps);
    static bool English;
    static string T(string fr, string en) => English ? en : fr;
    static Step[] Steps => new Step[]
    {
        new(T("a","b"), T("c","d"), "É", false, true), new(T("a","b"), T("c","d"), "GRÂCE", false, true),
        new(T("a","b"), T("c","d"), "jean", false, false), new(T("a","b"), T("c","d"), "Lætitia", false, false),
        new(T("a","b"), T("c","d"), "type", true, false), new(T("a","b"), T("c","d"), "São", true, false),
    };

    public static void Run()
    {
        const int accessesPerPaint = 62; // 7 (boucle des points) + 5 (PaintExercise) + ~48 (PaintKeyCharacters) + 2 (Verr. Maj.)
        const int N = 20000;
        int sink = 0;
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < accessesPerPaint; j++) sink += Steps.Length;
        sw.Stop();
        long a1 = GC.GetAllocatedBytesForCurrentThread();
        Console.WriteLine("5. Tutoriel : propriété Steps reconstruite à chaque accès");
        Console.WriteLine($"   {accessesPerPaint} accès/repeint : {sw.Elapsed.TotalMilliseconds * 1000 / N:F2} µs et {(a1 - a0) / N / 1024.0:F1} Ko alloués par repeint");
        GC.KeepAlive(sink);
    }
}
