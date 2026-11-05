// Sonde d'aperçu : chaque saisie passe par le moteur (ScienceEngine), sa sortie UnicodeMath
// est posée dans un RichEdit (msftedit.dll, classe RICHEDIT50W), marquée zone math puis
// construite par ITextRange2::BuildUpMath. La fenêtre est capturée par PrintWindow en BMP.
//
//   SciencePreviewProbe.exe <capture.bmp> [drapeaux BuildUpMath, défaut 0] [formules.txt]
//
// La fenêtre est visible : la lancer sur le bureau Win32 dédié des tests (scripts/BureauCache.ps1,
// Invoke-SurBureauCache), jamais sur le bureau d'Antoine. PrintWindow y capture le même rendu.
//
// Slots de vtable comptés dans TOM.h (SDK 10.0.26100.0), IUnknown et IDispatch inclus.
using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;

unsafe
{
    Console.OutputEncoding = Encoding.UTF8;
    string output = args.Length > 0 ? args[0] : "apercu.bmp";
    int buildFlags = args.Length > 1 ? Convert.ToInt32(args[1], 16) : 0;
    string[] inputs =
    [
        "1/2x", "x^2+1/x", "√(x+1)", "e^(2x)", "]0,1[", "]0 ; +∞[", "AB⃗", "P(Ā ∩ B)",
        "lim_(x->+oo) x e^(-x) = 0", "int_0^1 x^2dx", "sum_(k=1)^n k = n(n+1)/2",
        "binom(n;k)p^k(1-p)^(n-k)", "mat(1 2 ; 3 4)", "f(x) = sys(x si x ≥ 0 ; −x sinon)",
        "u_n+1", "ln x+1",
    ];

    if (Native.LoadLibraryW("msftedit.dll") == 0) { Console.WriteLine("msftedit.dll absent"); return 2; }
    const uint WS_OVERLAPPEDWINDOW = 0x00CF0000, WS_VISIBLE = 0x10000000, WS_CHILD = 0x40000000;
    const uint ES_MULTILINE = 0x4;
    nint frame = Native.CreateWindowExW(0, "STATIC", "Aperçu Science (sonde)", WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        80, 60, 1180, 1060, 0, 0, 0, 0);
    nint edit = Native.CreateWindowExW(0, "RICHEDIT50W", "", WS_CHILD | WS_VISIBLE | ES_MULTILINE,
        0, 0, 1160, 1020, frame, 0, 0, 0);
    if (frame == 0 || edit == 0) { Console.WriteLine($"fenêtre non créée : cadre {frame}, edit {edit}, erreur {Marshal.GetLastPInvokeError()}"); return 2; }

    // EM_GETOLEINTERFACE puis QueryInterface(ITextDocument2).
    nint ole = 0;
    Native.SendMessageW(edit, 0x0400 + 60, 0, (nint)(&ole));
    Guid iidDoc2 = new("C241F5E0-7206-11D8-A2C7-00A0D1D6C6B3");
    nint doc = 0;
    int hr = Vt(ole, 0) is var qi ? ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)qi)(ole, &iidDoc2, &doc) : -1;
    Console.WriteLine($"IRichEditOle {(ole != 0 ? "ok" : "absent")}, QI ITextDocument2 hr=0x{hr:X8}");
    if (doc == 0) return 3;

    var lines = new StringBuilder();
    // Mode brut : un fichier UTF-8 d'UnicodeMath, une formule par ligne, posée sans le moteur
    // (pour essayer les variantes d'écriture que RichEdit construit).
    if (args.Length > 2)
    {
        foreach (var um in File.ReadAllLines(args[2]))
        {
            if (um.Length == 0) continue;
            Append(doc, $"{um}   →   ", math: false, buildFlags, out _, out _);
            Append(doc, um, math: true, buildFlags, out int hrBuild, out string after);
            Append(doc, "\r", math: false, buildFlags, out _, out _);
            lines.AppendLine($"brut « {um} » BuildUpMath hr=0x{hrBuild:X8} texte après : {Escape(after)}");
        }
        inputs = [];
    }
    foreach (var input in inputs)
    {
        var result = ScienceEngine.Analyze(input, english: false);
        Append(doc, $"{input}   →   ", math: false, buildFlags, out _, out _);
        if (result.Readings.Count == 0)
        {
            Append(doc, $"(refus : {result.Diagnostics[0].Message})", math: false, buildFlags, out _, out _);
        }
        for (int i = 0; i < result.Readings.Count; i++)
        {
            if (i > 0) Append(doc, "   |   ", math: false, buildFlags, out _, out _);
            string um = result.Readings[i].UnicodeMath;
            Append(doc, um, math: true, buildFlags, out int hrBuild, out string after);
            lines.AppendLine($"{input} [{i + 1}] umath « {um} » BuildUpMath hr=0x{hrBuild:X8} texte après : {Escape(after)}");
        }
        Append(doc, "\r", math: false, buildFlags, out _, out _);
    }
    Console.Write(lines);

    // Laisser peindre, puis capturer la fenêtre entière (PW_RENDERFULLCONTENT).
    var until = Environment.TickCount64 + 1500;
    Native.MSG msg;
    while (Environment.TickCount64 < until)
    {
        while (Native.PeekMessageW(&msg, 0, 0, 0, 1)) { Native.TranslateMessage(&msg); Native.DispatchMessageW(&msg); }
        Thread.Sleep(15);
    }
    Capture(frame, output);
    Console.WriteLine($"capture : {output}");
    Native.DestroyWindow(frame);
    return 0;
}

static unsafe nint Vt(nint obj, int slot) => (*(nint**)obj)[slot];

// Insère du texte en fin de document ; en zone math, le construit avec BuildUpMath.
static unsafe void Append(nint doc, string text, bool math, int buildFlags, out int hrBuild, out string after)
{
    hrBuild = 0;
    after = "";
    nint range = 0;
    // ITextDocument2::Range2 (53) en fin d'histoire (cp bornés par RichEdit).
    ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Vt(doc, 53))(doc, int.MaxValue, int.MaxValue, &range);
    nint bstr = Marshal.StringToBSTR(text);
    int hrSet = ((delegate* unmanaged[Stdcall]<nint, int, nint, int>)Vt(range, 103))(range, 0, bstr); // SetText2
    Marshal.FreeBSTR(bstr);
    int start = 0, end = 0;
    ((delegate* unmanaged[Stdcall]<nint, int*, int>)Vt(range, 14))(range, &start); // GetStart
    ((delegate* unmanaged[Stdcall]<nint, int*, int>)Vt(range, 16))(range, &end); // GetEnd
    nint font = 0;
    ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vt(range, 73))(range, &font); // GetFont2
    ((delegate* unmanaged[Stdcall]<nint, float, int>)Vt(font, 47))(font, 22f); // SetSize
    if (math)
    {
        const int CFE_MATH = 0x10000000; // tomMathZone sans son bit de signe (CFM_MATH de Richedit.h)
        int hrFx = ((delegate* unmanaged[Stdcall]<nint, int, int, int>)Vt(font, 105))(font, CFE_MATH, CFE_MATH); // SetEffects
        hrBuild = ((delegate* unmanaged[Stdcall]<nint, int, int>)Vt(range, 87))(range, buildFlags); // BuildUpMath
        nint got = 0;
        int hrGet = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vt(range, 7))(range, &got); // GetText
        after = $"[SetText2 0x{hrSet:X8} cp {start}-{end} SetEffects 0x{hrFx:X8} GetText 0x{hrGet:X8}] "
            + (got == 0 ? "(vide)" : Marshal.PtrToStringBSTR(got));
        if (got != 0) Marshal.FreeBSTR(got);
    }
    ((delegate* unmanaged[Stdcall]<nint, uint>)Vt(font, 2))(font);
    ((delegate* unmanaged[Stdcall]<nint, uint>)Vt(range, 2))(range);
}

static string Escape(string s)
{
    var b = new StringBuilder();
    foreach (char c in s) b.Append(c < 0x20 || (c >= 0xFDD0 && c <= 0xFDEF) || c == 0xFFFC ? $"<U+{(int)c:X4}>" : c.ToString());
    return b.ToString();
}

static unsafe void Capture(nint hwnd, string path)
{
    Native.RECT r;
    Native.GetWindowRect(hwnd, &r);
    int w = r.Right - r.Left, h = r.Bottom - r.Top;
    nint screen = Native.GetDC(0);
    nint mem = Native.CreateCompatibleDC(screen);
    var bi = new Native.BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
    void* bits;
    nint dib = Native.CreateDIBSection(screen, &bi, 0, &bits, 0, 0);
    nint old = Native.SelectObject(mem, dib);
    bool printed = Native.PrintWindow(hwnd, mem, 2);
    int size = w * h * 4;
    using (var f = File.Create(path))
    using (var bw = new BinaryWriter(f))
    {
        bw.Write((ushort)0x4D42); bw.Write(14 + 40 + size); bw.Write(0); bw.Write(14 + 40);
        bw.Write(40); bw.Write(w); bw.Write(-h); bw.Write((ushort)1); bw.Write((ushort)32);
        bw.Write(0); bw.Write(size); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        bw.Write(new ReadOnlySpan<byte>(bits, size));
    }
    Console.WriteLine($"PrintWindow={printed} {w}×{h}");
    Native.SelectObject(mem, old);
    Native.DeleteObject(dib);
    Native.DeleteDC(mem);
    Native.ReleaseDC(0, screen);
}

static unsafe partial class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)] public static partial nint LoadLibraryW(string name);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [LibraryImport("user32.dll")] public static partial nint SendMessageW(nint hwnd, uint msg, nint w, nint l);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool PeekMessageW(MSG* msg, nint hwnd, uint min, uint max, uint remove);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool TranslateMessage(MSG* msg);
    [LibraryImport("user32.dll")] public static partial nint DispatchMessageW(MSG* msg);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyWindow(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetWindowRect(nint hwnd, RECT* r);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool PrintWindow(nint hwnd, nint hdc, uint flags);
    [LibraryImport("user32.dll")] public static partial nint GetDC(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(nint hwnd, nint hdc);
    [LibraryImport("gdi32.dll")] public static partial nint CreateCompatibleDC(nint hdc);
    [LibraryImport("gdi32.dll")] public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* bi, uint usage, void** bits, nint section, uint offset);
    [LibraryImport("gdi32.dll")] public static partial nint SelectObject(nint hdc, nint obj);
    [LibraryImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DeleteObject(nint obj);
    [LibraryImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DeleteDC(nint hdc);
}
