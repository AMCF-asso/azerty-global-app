// Aperçu de dev : export des rectangles dessinés, pour l'outil annotate-visual (déplacer).
#if DEBUG
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>
/// L'interface est dessinée en GDI, sans arbre d'éléments : rien à cliquer. En Debug avec
/// AG_DEV_PREVIEW, ce module garde, pendant un repaint forcé, chaque primitive GDI que l'app
/// appelle (texte, aplat, rectangle arrondi, image) avec son rectangle en px de la capture, son
/// texte et les lignes de code qui l'ont appelée (PDB), ajoute les contrôles enfants, puis
/// écrit une capture PNG par fenêtre et un <c>index.json</c> que lit
/// <c>IA/tools/annotate-visual/deplacer.py</c>.
///
/// Déclenchement : l'outil écrit <c>%TEMP%\ag-dev-rects\demande.json</c> {"dossier": "..."} ;
/// un minuteur de la boucle de messages (400 ms) la lit, exporte, puis supprime la demande.
/// <c>vivant.json</c> dit que l'aperçu tourne. Compilé en Debug seulement : le binaire du
/// Store n'en porte aucune trace (vérifié par recherche de « DevRects » dans l'assembly).
/// </summary>
internal static class DevRects
{
    private sealed class Item
    {
        public string Kind = "";
        public string Name = "";
        public int X, Y, W, H;
        public bool Background;
        public string[] Source = Array.Empty<string>();
    }

    private const int MaxItemsPerWindow = 6000;
    private static readonly object Gate = new();
    private static List<Item> _items = new();
    private static volatile bool _on;
    private static IntPtr _top;
    private static int _clientScreenX, _clientScreenY, _clientW, _clientH;

    private static string Root => Path.Combine(Path.GetTempPath(), "ag-dev-rects");

    // ── Enregistrement des primitives (appelé par les enveloppes de Win32.cs) ──

    internal static bool Active => _on;

    internal static void Text(IntPtr hdc, string? text, int l, int t, int r, int b)
    {
        if (!_on || string.IsNullOrEmpty(text)) return;
        Add("texte", hdc, l, t, r, b, text);
    }

    /// <summary>TextOutW : (x, y) est le point d'ancrage choisi par SetTextAlign (PaperText écrit sur la ligne de base).</summary>
    internal static void TextOut(IntPtr hdc, int x, int y, string? text)
    {
        if (!_on || string.IsNullOrEmpty(text)) return;
        N.GetTextExtentPoint32W(hdc, text, text.Length, out var size);
        uint align = N.GetTextAlign(hdc);
        N.GetTextMetricsW(hdc, out var tm);
        int left = (align & 6) == 6 ? x - size.x / 2 : (align & 2) != 0 ? x - size.x : x;
        int top = (align & 24) == 24 ? y - tm.ascent : (align & 8) != 0 ? y - size.y : y;
        Add("texte", hdc, left, top, left + size.x, top + size.y, text);
    }

    internal static void Fill(IntPtr hdc, int l, int t, int r, int b, string kind)
    {
        if (!_on) return;
        Add(kind, hdc, l, t, r, b, "");
    }

    internal static void Image(IntPtr hdc, int x, int y, int w, int h)
    {
        if (!_on) return;
        Add("image", hdc, x, y, x + w, y + h, "");
    }

    /// <summary>Élément de plus haut niveau (bouton, panneau) : même rectangle que ses primitives, avec un nom.</summary>
    internal static void Element(IntPtr hdc, string kind, string? name, int l, int t, int r, int b)
    {
        if (!_on) return;
        Add(kind, hdc, l, t, r, b, name ?? "");
    }

    private static void Add(string kind, IntPtr hdc, int l, int t, int r, int b, string name)
    {
        try
        {
            var pts = new N.POINT[] { new() { x = l, y = t }, new() { x = r, y = b } };
            N.LPtoDP(hdc, pts, 2);
            // Région de découpe du DC (contenu défilé, rognage) : hors d'elle rien n'est visible.
            int region = N.GetClipBox(hdc, out var clip);
            if (region == 1) return; // NULLREGION : rien de visible
            if (region > 1)
            {
                var cp = new N.POINT[] { new() { x = clip.left, y = clip.top }, new() { x = clip.right, y = clip.bottom } };
                N.LPtoDP(hdc, cp, 2);
                pts[0].x = Math.Max(pts[0].x, cp[0].x); pts[0].y = Math.Max(pts[0].y, cp[0].y);
                pts[1].x = Math.Min(pts[1].x, cp[1].x); pts[1].y = Math.Min(pts[1].y, cp[1].y);
            }
            int ox = 0, oy = 0;
            IntPtr wnd = N.WindowFromDC(hdc);
            if (wnd != IntPtr.Zero && wnd != _top)
            {
                var origin = new N.POINT();
                N.ClientToScreen(wnd, ref origin);
                ox = origin.x - _clientScreenX;
                oy = origin.y - _clientScreenY;
            }
            int x = pts[0].x + ox, y = pts[0].y + oy, w = pts[1].x - pts[0].x, h = pts[1].y - pts[0].y;
            if (w <= 0 || h <= 0) return;
            var item = new Item
            {
                Kind = kind, Name = name.Length > 120 ? name[..120] : name, X = x, Y = y, W = w, H = h,
                Background = w >= _clientW - 2 && h >= _clientH - 2,
                Source = CallSites(),
            };
            lock (Gate)
            {
                if (_items.Count < MaxItemsPerWindow) _items.Add(item);
            }
        }
        catch { /* l'export de dev ne doit jamais gêner le dessin */ }
    }

    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
        { "Win32.cs", "DevRects.cs" };

    /// <summary>Jusqu'à trois lignes de code appelantes (fichier:ligne méthode), hors Win32.cs.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Debug seulement : jamais dans le binaire publié")]
    private static string[] CallSites()
    {
        var found = new List<string>();
        var trace = new StackTrace(2, true);
        foreach (var frame in trace.GetFrames())
        {
            string? file = frame.GetFileName();
            if (file is null) continue;
            string name = Path.GetFileName(file);
            if (Skipped.Contains(name)) continue;
            var method = frame.GetMethod();
            found.Add($"{name}:{frame.GetFileLineNumber()} {method?.DeclaringType?.Name}.{method?.Name}");
            if (found.Count == 3) break;
        }
        return found.ToArray();
    }

    // ── Déclenchement ──

    private static N.TimerProc? _timer; // gardé vivant : Windows garde un pointeur vers le délégué
    private static int _beat;

    internal static void StartWatching()
    {
        Directory.CreateDirectory(Root);
        WriteAlive();
        _timer = OnTick;
        N.SetTimer(IntPtr.Zero, UIntPtr.Zero, 400, _timer);
    }

    private static void WriteAlive()
    {
        try
        {
            File.WriteAllText(Path.Combine(Root, "vivant.json"),
                $"{{\"pid\": {Environment.ProcessId}, \"ts\": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}");
        }
        catch { }
    }

    private static void OnTick(IntPtr hwnd, uint msg, UIntPtr id, uint time)
    {
        if (++_beat % 10 == 0) WriteAlive();
        string request = Path.Combine(Root, "demande.json");
        if (!File.Exists(request)) return;
        string? dir = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(request));
            dir = doc.RootElement.GetProperty("dossier").GetString();
            File.Delete(request);
            // Écriture limitée au dossier d'échange de l'aperçu.
            string full = Path.GetFullPath(dir ?? "");
            if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("dossier hors de " + Root);
            Export(full);
        }
        catch (Exception ex)
        {
            ConfigManager.Log("DevRects", ex);
            try
            {
                if (dir is not null && Path.GetFullPath(dir).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "index.json"),
                        "{\"erreur\": \"" + JsonEncodedText.Encode(ex.Message) + "\"}");
                }
            }
            catch { }
        }
    }

    // ── Export ──

    private static void Export(string dir)
    {
        Directory.CreateDirectory(dir);
        var windows = new List<IntPtr>();
        uint pid = (uint)Environment.ProcessId;
        N.EnumWindows((h, _) =>
        {
            N.GetWindowThreadProcessId(h, out uint p);
            if (p == pid && N.IsWindowVisible(h) && !N.IsIconic(h) && WindowText(h).Length > 0) windows.Add(h);
            return true;
        }, IntPtr.Zero);

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("version", "ag-dev-rects/1");
            json.WriteString("date", DateTimeOffset.Now.ToString("o"));
            json.WriteString("unite", "px de la capture PNG (physiques) ; x,y depuis le coin haut-gauche de la fenêtre entière");
            json.WriteStartArray("fenetres");
            int n = 0;
            foreach (var hwnd in windows)
            {
                if (ExportWindow(hwnd, dir, ++n, json) is false) n--;
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        string tmp = Path.Combine(dir, "index.json.tmp");
        File.WriteAllBytes(tmp, stream.ToArray());
        File.Move(tmp, Path.Combine(dir, "index.json"), true);
    }

    private static bool ExportWindow(IntPtr hwnd, string dir, int n, Utf8JsonWriter json)
    {
        N.GetWindowRect(hwnd, out var wr);
        int width = wr.right - wr.left, height = wr.bottom - wr.top;
        if (width < 20 || height < 20) return false;
        var origin = new N.POINT();
        N.ClientToScreen(hwnd, ref origin);
        N.GetClientRect(hwnd, out var client);
        string title = WindowText(hwnd);
        string cls = ClassName(hwnd);

        lock (Gate) { _items = new List<Item>(); }
        _top = hwnd;
        _clientScreenX = origin.x; _clientScreenY = origin.y;
        _clientW = client.right; _clientH = client.bottom;
        int cx = origin.x - wr.left, cy = origin.y - wr.top;

        _on = true;
        try { N.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, N.RDW_INVALIDATE | N.RDW_ERASE | N.RDW_ALLCHILDREN | N.RDW_UPDATENOW); }
        finally { _on = false; }

        List<Item> items;
        lock (Gate) { items = _items; _items = new List<Item>(); }
        // Les coordonnées client deviennent des coordonnées de la capture (cadre de fenêtre compris).
        foreach (var it in items) { it.X += cx; it.Y += cy; }

        N.EnumChildWindows(hwnd, (child, _) =>
        {
            if (!N.IsWindowVisible(child)) return true;
            N.GetWindowRect(child, out var r);
            if (r.right <= r.left || r.bottom <= r.top) return true;
            items.Add(new Item
            {
                Kind = "contrôle", Name = $"{ClassName(child)} « {WindowText(child)} »",
                X = r.left - wr.left, Y = r.top - wr.top, W = r.right - r.left, H = r.bottom - r.top,
                Source = Array.Empty<string>(),
            });
            return true;
        }, IntPtr.Zero);

        string png = $"fenetre-{n}.png";
        if (!CapturePng(hwnd, width, height, Path.Combine(dir, png))) return false;

        int dpi = N.GetDpiForWindow(hwnd);
        json.WriteStartObject();
        json.WriteString("id", $"fenetre-{n}");
        json.WriteString("titre", title);
        json.WriteString("classe", cls);
        json.WriteString("png", png);
        json.WriteNumber("largeur", width);
        json.WriteNumber("hauteur", height);
        json.WriteNumber("dpi", dpi);
        json.WriteNumber("echelle", Math.Round(dpi / 96.0, 4));
        json.WriteStartObject("client");
        json.WriteNumber("x", cx); json.WriteNumber("y", cy);
        json.WriteNumber("largeur", client.right); json.WriteNumber("hauteur", client.bottom);
        json.WriteEndObject();
        json.WriteStartArray("elements");
        int k = 0;
        foreach (var it in items)
        {
            json.WriteStartObject();
            json.WriteString("id", $"e{++k}");
            json.WriteString("type", it.Kind);
            json.WriteString("nom", it.Name);
            json.WriteNumber("x", it.X); json.WriteNumber("y", it.Y);
            json.WriteNumber("largeur", it.W); json.WriteNumber("hauteur", it.H);
            if (it.Background) json.WriteBoolean("fond", true);
            json.WriteStartArray("source");
            foreach (var s in it.Source) json.WriteStringValue(s);
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
        return true;
    }

    private static string WindowText(IntPtr h)
    {
        var sb = new StringBuilder(256);
        N.GetWindowTextW(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string ClassName(IntPtr h)
    {
        var sb = new StringBuilder(128);
        N.GetClassNameW(h, sb, sb.Capacity);
        return sb.ToString();
    }

    // ── Capture PNG (PrintWindow, comme le banc de captures des tests) ──

    private static bool CapturePng(IntPtr hwnd, int w, int h, string path)
    {
        IntPtr screen = N.GetDC(IntPtr.Zero);
        IntPtr dc = N.CreateCompatibleDC(screen);
        IntPtr bmp = N.CreateCompatibleBitmap(screen, w, h);
        N.ReleaseDC(IntPtr.Zero, screen);
        if (dc == IntPtr.Zero || bmp == IntPtr.Zero) return false;
        IntPtr old = N.SelectObject(dc, bmp);
        try
        {
            if (!N.PrintWindow(hwnd, dc, N.PW_RENDERFULLCONTENT)) return false;
            var bi = new N.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<N.BITMAPINFOHEADER>(), biWidth = w, biHeight = -h,
                biPlanes = 1, biBitCount = 32, biCompression = 0,
            };
            var pixels = new byte[w * h * 4];
            if (N.GetDIBits(dc, bmp, 0, (uint)h, pixels, ref bi, 0) == 0) return false;
            WritePng(path, w, h, pixels);
            return true;
        }
        finally
        {
            N.SelectObject(dc, old);
            N.DeleteObject(bmp);
            N.DeleteDC(dc);
        }
    }

    private static void WritePng(string path, int w, int h, byte[] bgra)
    {
        var raw = new byte[(w * 3 + 1) * h];
        int o = 0;
        for (int y = 0; y < h; y++)
        {
            raw[o++] = 0;
            int p = y * w * 4;
            for (int x = 0; x < w; x++, p += 4)
            {
                raw[o++] = bgra[p + 2]; raw[o++] = bgra[p + 1]; raw[o++] = bgra[p];
            }
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)w); WriteBe(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 2;
        Chunk(fs, "IHDR", ihdr);
        using var z = new MemoryStream();
        using (var zl = new ZLibStream(z, CompressionLevel.Fastest, true)) zl.Write(raw);
        Chunk(fs, "IDAT", z.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void WriteBe(byte[] b, int at, uint v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static uint[]? _crc;

    private static void Chunk(Stream s, string type, byte[] data)
    {
        _crc ??= Enumerable.Range(0, 256).Select(n =>
        {
            uint c = (uint)n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            return c;
        }).ToArray();
        var head = new byte[8];
        WriteBe(head, 0, (uint)data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(head, 4);
        s.Write(head);
        s.Write(data);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in head.AsSpan(4)) crc = _crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data) crc = _crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        var tail = new byte[4];
        WriteBe(tail, 0, crc ^ 0xFFFFFFFF);
        s.Write(tail);
    }

    private static class N
    {
        internal const uint RDW_INVALIDATE = 0x1, RDW_ERASE = 0x4, RDW_ALLCHILDREN = 0x80, RDW_UPDATENOW = 0x100;
        internal const uint PW_RENDERFULLCONTENT = 2;

        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        internal delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        internal delegate void TimerProc(IntPtr hwnd, uint msg, UIntPtr id, uint time);

        [StructLayout(LayoutKind.Sequential)] internal struct SIZE2 { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct TEXTMETRIC { public int height, ascent, descent, a, b, c, d, e, f, g, h, i, j, k, l; }
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetTextExtentPoint32W(IntPtr hdc, string s, int c, out SIZE2 size);
        [DllImport("gdi32.dll")] internal static extern uint GetTextAlign(IntPtr hdc);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetTextMetricsW(IntPtr hdc, out TEXTMETRIC tm);
        [DllImport("gdi32.dll")] internal static extern int GetClipBox(IntPtr hdc, out RECT r);
        [DllImport("gdi32.dll")] internal static extern bool LPtoDP(IntPtr hdc, [In, Out] POINT[] pts, int c);
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromDC(IntPtr hdc);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc p, IntPtr l);
        [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr h, EnumProc p, IntPtr l);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool RedrawWindow(IntPtr h, IntPtr rc, IntPtr rgn, uint flags);
        [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] internal static extern int GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] internal static extern UIntPtr SetTimer(IntPtr h, UIntPtr id, uint ms, TimerProc p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassNameW(IntPtr h, StringBuilder sb, int n);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    }
}
#endif
