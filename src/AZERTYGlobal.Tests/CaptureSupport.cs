using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AZERTYGlobal;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Outillage commun des deux bancs de capture, <see cref="CaptureBench"/> (les fenêtres à
/// contrôles) et <see cref="KeyboardContextBench"/> (les fenêtres qui portent un clavier).
///
/// Principe (option A de la revue du 25/09) : rien n'est ajouté à <c>src/</c>. Les bancs
/// ouvrent les fenêtres par leurs points d'entrée publics, et n'atteignent les membres privés
/// que par réflexion. Le code du produit reste donc identique à l'octet près, et les mêmes
/// fichiers de banc peuvent être déposés sur un commit « avant » pour une comparaison.
///
/// Portes et réglages, tous par variable d'environnement :
/// <list type="bullet">
/// <item><c>AZERTY_CAPTURE</c> ouvre le banc des fenêtres, <c>AZERTY_CONTEXTE</c> celui du
/// clavier. Sans elles, chaque [Fact] sort tout de suite et compte comme réussi.</item>
/// <item><c>AZERTY_CAPTURE_DIR</c> : dossier de sortie commun. À défaut, la valeur de la porte
/// si ce n'est pas « 1 » (usage hérité du banc de main).</item>
/// <item><c>AZERTY_CAPTURE_DPI</c> : DPI à rendre, séparés par des virgules. Défaut :
/// 96,120,144,168, soit 100, 125, 150 et 175 %.</item>
/// <item><c>AZERTY_CAPTURE_LANG</c> : langues à rendre. Défaut : fr,en.</item>
/// </list>
///
/// Le DPI est simulé par un WM_DPICHANGED synthétique : changer l'échelle de Windows
/// déconnecterait la session. Les gestionnaires des fenêtres lisent le HIWORD et appliquent le
/// rectangle suggéré, exactement comme au passage d'un écran à l'autre. Limites : la barre de
/// titre reste dessinée par DWM au DPI réel de l'écran, et un arrondi peut différer d'un pixel
/// d'une fenêtre née à ce DPI. Les deux s'annulent dans un avant/après rendu par la même voie.
///
/// Chaque banc écrit un journal (<c>banc-fenetres.tsv</c>, <c>banc-clavier.tsv</c>) : le poste,
/// puis une ligne par image avec ses dimensions, le DPI de la fenêtre avant simulation, le DPI
/// demandé, la voie de lecture (voir <see cref="Capture"/>) et l'empreinte SHA-256.
/// </summary>
internal static class BancCapture
{
    internal const string DirVariable = "AZERTY_CAPTURE_DIR";
    internal const string DpiVariable = "AZERTY_CAPTURE_DPI";
    internal const string LangVariable = "AZERTY_CAPTURE_LANG";

    private static readonly int[] DefaultDpis = { 96, 120, 144, 168 };
    private static readonly string[] DefaultLanguages = { "fr", "en" };

    /// <summary>
    /// Dossier de sortie, ou null quand la porte est fermée. Une porte ouverte sans dossier est
    /// une erreur de lancement : mieux vaut un échec lisible qu'un banc vert qui n'a rien écrit
    /// là où on le cherche.
    /// </summary>
    internal static string? OutputDirectory(string gateVariable)
    {
        string? gate = Environment.GetEnvironmentVariable(gateVariable);
        if (string.IsNullOrWhiteSpace(gate))
            return null;

        string? dir = Environment.GetEnvironmentVariable(DirVariable);
        if (!string.IsNullOrWhiteSpace(dir))
            return dir;

        if (gate != "1" && !gate.Equals("true", StringComparison.OrdinalIgnoreCase))
            return gate;

        throw new InvalidOperationException(
            $"{gateVariable} est ouverte mais {DirVariable} ne donne aucun dossier de sortie.");
    }

    /// <summary>Les DPI demandés, dans l'ordre donné, sans doublon.</summary>
    internal static int[] Dpis
    {
        get
        {
            string? raw = Environment.GetEnvironmentVariable(DpiVariable);
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultDpis;

            var list = new List<int>();
            foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dpi)
                    && dpi >= 48 && dpi <= 480 && !list.Contains(dpi))
                {
                    list.Add(dpi);
                }
            }
            return list.Count > 0 ? list.ToArray() : DefaultDpis;
        }
    }

    /// <summary>Les langues demandées. Le produit n'en connaît que deux.</summary>
    internal static string[] Languages
    {
        get
        {
            string? raw = Environment.GetEnvironmentVariable(LangVariable);
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultLanguages;

            var list = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .Where(s => s == "fr" || s == "en")
                .Distinct()
                .ToArray();
            return list.Length > 0 ? list : DefaultLanguages;
        }
    }

    /// <summary>Pourcentage d'échelle affiché dans les noms de fichiers : 96 → 100, 144 → 150.</summary>
    internal static int Percent(int dpi) => (int)Math.Round(dpi * 100 / 96.0, MidpointRounding.AwayFromZero);

    // ═══════════════════════════════════════════════════════════════
    // Isolation
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tout ce que le banc change dans le processus de test, et que <see cref="Dispose"/>
    /// restaure : configuration et statistiques dans un dossier temporaire (la progression des
    /// leçons suit la configuration), aucune politique d'entreprise, canal Store, langue fixée,
    /// sensibilité DPI du fil d'exécution alignée sur le produit.
    ///
    /// Le canal est forcé à Store parce que c'est celui que voient la plupart des utilisateurs :
    /// le processus de test, hors paquet, se classerait sinon « hors paquet ». Les politiques sont
    /// vidées pour qu'un poste géré ne rende pas d'autres fenêtres que le runner de la CI.
    ///
    /// La sensibilité DPI : le produit se déclare Per-Monitor V2 (manifeste et Program.Main),
    /// alors que testhost.exe ne déclare rien. Sans ce réglage, Windows virtualiserait les
    /// fenêtres à 96 DPI et WM_DPICHANGED n'aurait aucun sens.
    /// </summary>
    internal sealed class Isolation : IDisposable
    {
        private const string ConfigPathField = "_configPath";
        private const string LogDirectoryField = "_logDirectoryOverride";
        private const string StatsPathField = "_statsPath";
        private static readonly IntPtr PerMonitorAwareV2 = new(-4);

        private readonly string _tempDir;
        private readonly object? _previousConfigPath;
        private readonly object? _previousLogDirectory;
        private readonly object? _previousStatsPath;
        private readonly string _previousLanguage;
        private readonly IDisposable _policy;
        private readonly IDisposable _channel;
        private readonly IntPtr _previousDpiContext;
        private readonly IntPtr _gdipToken;
        private bool _disposed;

        public Isolation()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AZGCapture_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _previousConfigPath = ReadStatic(typeof(ConfigManager), ConfigPathField);
            _previousLogDirectory = ReadStatic(typeof(ConfigManager), LogDirectoryField);
            _previousStatsPath = ReadStatic(typeof(UsageStats), StatsPathField);

            // La clé de langue est posée avant le premier chargement : absente, une installation
            // neuve la déduirait de la langue de Windows, anglaise sur le runner.
            string configPath = Path.Combine(_tempDir, "config.json");
            File.WriteAllText(configPath, "{\n  \"appLanguage\": \"fr\"\n}\n", new UTF8Encoding(false));
            ConfigManager.OverrideConfigPathForTests(configPath);
            UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "usage-stats.json"));

            _policy = PolicyManager.OverrideForTests((_, _) => null);
            _channel = AppChannel.OverrideForTests(DistributionChannel.Store);
            _previousLanguage = L.Language;
            _previousDpiContext = Native.SetThreadDpiAwarenessContext(PerMonitorAwareV2);

            var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
            Win32.GdiplusStartup(out _gdipToken, ref input, IntPtr.Zero);
        }

        /// <summary>Langue de l'interface, dans la configuration et dans L : les Paramètres
        /// lisent la première pour cocher leur bouton radio, tout le reste lit la seconde.</summary>
        public void SetLanguage(string language)
        {
            ConfigManager.SetAppLanguage(language);
            L.Language = language;
        }

        /// <summary>Description du poste, en tête du journal : elle dit d'où vient l'image.</summary>
        public string Describe()
        {
            IntPtr hdc = Win32.GetDC(IntPtr.Zero);
            int systemDpi = Win32.GetDeviceCaps(hdc, 88);
            int width = Win32.GetDeviceCaps(hdc, 118);  // DESKTOPHORZRES
            int height = Win32.GetDeviceCaps(hdc, 117); // DESKTOPVERTRES
            Win32.ReleaseDC(IntPtr.Zero, hdc);

            IntPtr context = Native.GetThreadDpiAwarenessContext();
            int awareness = Native.GetAwarenessFromDpiAwarenessContext(context);

            var monitors = new List<string>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref Win32.RECT bounds, IntPtr _) =>
            {
                Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _);
                monitors.Add($"{bounds.right - bounds.left}x{bounds.bottom - bounds.top} en ({bounds.left}, {bounds.top}) a {dpi} DPI");
                return true;
            }, IntPtr.Zero);

            return string.Join("\n", new[]
            {
                $"# os\t{System.Environment.OSVersion.VersionString}",
                $"# ecran principal\t{width}x{height}, DPI systeme {systemDpi}",
                $"# moniteurs\t{string.Join(" ; ", monitors)}",
                $"# sensibilite DPI du fil\t{awareness} (2 = par ecran)",
                $"# comctl32\t{ModulePath("comctl32.dll")}",
                $"# canal\t{AppChannel.Current}",
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_gdipToken != IntPtr.Zero)
                Win32.GdiplusShutdown(_gdipToken);
            Native.SetThreadDpiAwarenessContext(_previousDpiContext);
            L.Language = _previousLanguage;
            _channel.Dispose();
            _policy.Dispose();

            if (_previousStatsPath is string statsPath)
                UsageStats.OverrideStatsPathForTests(statsPath);
            if (_previousConfigPath is string configPath)
            {
                ConfigManager.OverrideConfigPathForTests(configPath);
                WriteStatic(typeof(ConfigManager), LogDirectoryField, _previousLogDirectory);
            }

            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string ModulePath(string module)
        {
            IntPtr handle = Native.GetModuleHandleW(module);
            if (handle == IntPtr.Zero)
                return "non charge";
            var buffer = new StringBuilder(1024);
            return Native.GetModuleFileNameW(handle, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "?";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Réflexion
    // ═══════════════════════════════════════════════════════════════

    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Valeur d'un champ d'instance. Un champ renommé dans <c>src/</c> fait échouer le banc avec
    /// son nom exact, au lieu de rendre une capture vide.
    /// </summary>
    internal static T Field<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, AnyInstance)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target)!;
    }

    internal static void SetField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, AnyInstance)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    /// <summary>Appelle une méthode d'instance, publique ou non, choisie par son nom et son
    /// nombre de paramètres.</summary>
    internal static object? Call(object target, string name, params object?[] args)
    {
        var method = target.GetType().GetMethods(AnyInstance)
            .SingleOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length)
            ?? throw new MissingMethodException(target.GetType().Name, name);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException($"{target.GetType().Name}.{name} : {ex.InnerException.Message}", ex.InnerException);
        }
    }

    /// <summary>Handle d'une fenêtre, lu dans son champ privé <c>_hWnd</c>.</summary>
    internal static IntPtr Handle(object window) => Field<IntPtr>(window, "_hWnd");

    private static object? ReadStatic(Type type, string name) =>
        type.GetField(name, AnyStatic)?.GetValue(null);

    private static void WriteStatic(Type type, string name, object? value) =>
        type.GetField(name, AnyStatic)?.SetValue(null, value);

    // ═══════════════════════════════════════════════════════════════
    // DPI, activation, rendu
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Porte la fenêtre au DPI demandé par un WM_DPICHANGED synthétique, et rend le DPI qu'elle
    /// avait. Le rectangle suggéré garde le coin haut-gauche et met la zone cliente à
    /// l'échelle : le cadre, lui, reste au DPI réel de l'écran, comme la barre de titre que DWM
    /// dessine. Rien n'est envoyé quand la fenêtre est déjà au bon DPI.
    /// </summary>
    internal static int ApplyDpi(IntPtr hwnd, int targetDpi)
    {
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("handle nul avant la mise à l'échelle");

        int current = Win32.GetDpiForWindow(hwnd);
        if (current <= 0 || current == targetDpi)
            return current;

        Win32.GetWindowRect(hwnd, out var window);
        Win32.GetClientRect(hwnd, out var client);
        int frameW = (window.right - window.left) - (client.right - client.left);
        int frameH = (window.bottom - window.top) - (client.bottom - client.top);
        int clientW = (int)Math.Round((client.right - client.left) * (double)targetDpi / current, MidpointRounding.AwayFromZero);
        int clientH = (int)Math.Round((client.bottom - client.top) * (double)targetDpi / current, MidpointRounding.AwayFromZero);

        var suggested = new Win32.RECT
        {
            left = window.left,
            top = window.top,
            right = window.left + clientW + frameW,
            bottom = window.top + clientH + frameH,
        };

        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.RECT>());
        try
        {
            Marshal.StructureToPtr(suggested, pointer, false);
            Win32.SendMessageW(hwnd, Win32.WM_DPICHANGED, (IntPtr)((targetDpi << 16) | targetDpi), pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
        return current;
    }

    /// <summary>
    /// Barre de titre à l'état actif. Le produit ouvre ses fenêtres au premier plan ; le
    /// processus de test, lui, n'obtient pas toujours le premier plan, et la barre de titre
    /// changerait de couleur d'une passe à l'autre selon ce que fait le poste. Le clavier
    /// virtuel, jamais activé par le produit, n'y passe pas.
    /// </summary>
    internal static void MarkActive(IntPtr hwnd)
    {
        const uint WM_NCACTIVATE = 0x0086;
        Win32.SendMessageW(hwnd, WM_NCACTIVATE, (IntPtr)1, IntPtr.Zero);
    }

    /// <summary>
    /// Noircit la zone cliente, enfants compris, sans rien invalider. La capture qui suit
    /// repeint la fenêtre avec effacement (<see cref="Capture"/>) : tout ce que la fenêtre
    /// n'efface ni ne peint reste noir. C'est ce que montre DWM à l'écran, alors que le
    /// runner rend ces zones en blanc et cachait le fond noir de la Durée de pause.
    /// </summary>
    internal static void BlackenClient(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("handle nul avant le noircissement");

        Win32.GetClientRect(hwnd, out var client);
        IntPtr hdc = Native.GetDCEx(hwnd, IntPtr.Zero, Native.DCX_CACHE);
        IntPtr black = Win32.CreateSolidBrush(0x00000000);
        try
        {
            Win32.FillRect(hdc, ref client, black);
            Native.GdiFlush();
        }
        finally
        {
            Win32.DeleteObject(black);
            Win32.ReleaseDC(hwnd, hdc);
        }
    }

    /// <summary>
    /// Laisse les fenêtres se peindre. Une boucle GetMessage bloquerait : la fenêtre ne se
    /// ferme pas d'elle-même, et rien ne posterait le WM_QUIT qui en sortirait.
    /// </summary>
    internal static void Pump(int rounds)
    {
        for (int i = 0; i < rounds; i++)
        {
            while (Native.PeekMessageW(out var msg, IntPtr.Zero, 0, 0, Native.PM_REMOVE))
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessageW(ref msg);
            }
            Thread.Sleep(15);
        }
    }

    /// <summary>
    /// Détruit la fenêtre, puis laisse la file se vider avant la suivante. Chaque fenêtre
    /// désenregistre sa classe dans son Dispose, et une classe ne se libère que lorsque la
    /// dernière fenêtre qui la porte a fini d'être détruite : sans cette pompe, la fenêtre
    /// suivante trouve la classe encore prise et CreateWindowExW rend un handle nul.
    /// </summary>
    internal static void Teardown(IDisposable window)
    {
        window.Dispose();
        Pump(5);
    }

    /// <summary>
    /// Rend la fenêtre entière, barre de titre comprise, l'enregistre en PNG et dit par quelle
    /// voie la zone cliente a été lue.
    ///
    /// Deux voies, mesurées le 25/09 sur 38 fenêtres, trois clichés chacune sans rien pomper
    /// entre eux : PrintWindow avec PW_RENDERFULLCONTENT rendait une image différente d'un
    /// cliché à l'autre pour 7 fenêtres (des contrôles enfants manquaient : bouton Fermer,
    /// boutons radio, drapeau de l'accueil), la copie du DC de la fenêtre pour aucune.
    /// PrintWindow reste la seule voie qui ramène la barre de titre que DWM dessine ; la zone
    /// cliente est donc recopiée par-dessus depuis le DC, quand la fenêtre tient entière sur son
    /// écran. Le DC ne rend rien hors de l'écran : une fenêtre plus grande que lui garde la zone
    /// cliente de PrintWindow, et le journal le dit (voie « pw »).
    ///
    /// L'image est recadrée sur le cadre visible (DWMWA_EXTENDED_FRAME_BOUNDS) : GetWindowRect
    /// compte les bordures invisibles de redimensionnement, qui sortiraient en noir.
    /// </summary>
    internal static (int Width, int Height, string Source) Capture(IntPtr hwnd, string file)
    {
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"{Path.GetFileName(file)} : handle nul");

        Pump(20);
        bool onScreen = KeepOnMonitor(hwnd);

        // Peinture complète et synchrone, enfants compris, puis composition DWM achevée.
        Native.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
            Native.RDW_INVALIDATE | Native.RDW_ERASE | Native.RDW_FRAME | Native.RDW_ALLCHILDREN | Native.RDW_UPDATENOW);
        Native.GdiFlush();
        Native.DwmFlush();

        if (!Win32.GetWindowRect(hwnd, out var rect))
            throw new InvalidOperationException($"{Path.GetFileName(file)} : GetWindowRect a échoué");

        int w = rect.right - rect.left;
        int h = rect.bottom - rect.top;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"{Path.GetFileName(file)} : taille {w}x{h}");

        int cropX = 0, cropY = 0, cropW = w, cropH = h;
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, Marshal.SizeOf<Win32.RECT>()) == 0
            && frame.left >= rect.left && frame.top >= rect.top
            && frame.right <= rect.right && frame.bottom <= rect.bottom
            && frame.right > frame.left && frame.bottom > frame.top)
        {
            cropX = frame.left - rect.left;
            cropY = frame.top - rect.top;
            cropW = frame.right - frame.left;
            cropH = frame.bottom - frame.top;
        }

        Win32.GetClientRect(hwnd, out var client);
        var origin = new Win32.POINT { x = 0, y = 0 };
        Win32.ClientToScreen(hwnd, ref origin);
        int clientX = origin.x - rect.left;
        int clientY = origin.y - rect.top;

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdcFull = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr hdcCrop = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr full = Win32.CreateCompatibleBitmap(hdcScreen, w, h);
        IntPtr crop = Win32.CreateCompatibleBitmap(hdcScreen, cropW, cropH);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        bool rendered;
        string source = "pw";
        IntPtr previousFull = Win32.SelectObject(hdcFull, full);
        IntPtr previousCrop = Win32.SelectObject(hdcCrop, crop);
        try
        {
            rendered = Native.PrintWindow(hwnd, hdcFull, Native.PW_RENDERFULLCONTENT);
            if (rendered && onScreen && client.right > 0 && client.bottom > 0)
            {
                // Sans DCX_CLIPCHILDREN : les contrôles enfants sont lus avec leur parent.
                IntPtr hdcWindow = Native.GetDCEx(hwnd, IntPtr.Zero, Native.DCX_CACHE);
                try
                {
                    if (Win32.BitBlt(hdcFull, clientX, clientY, client.right, client.bottom, hdcWindow, 0, 0, Win32.SRCCOPY))
                        source = "dc";
                }
                finally
                {
                    Win32.ReleaseDC(hwnd, hdcWindow);
                }
            }
            rendered = rendered && Win32.BitBlt(hdcCrop, 0, 0, cropW, cropH, hdcFull, cropX, cropY, Win32.SRCCOPY);
        }
        finally
        {
            Win32.SelectObject(hdcFull, previousFull);
            Win32.SelectObject(hdcCrop, previousCrop);
            Win32.DeleteDC(hdcFull);
            Win32.DeleteDC(hdcCrop);
            Win32.DeleteObject(full);
        }

        try
        {
            if (!rendered)
                throw new InvalidOperationException($"{Path.GetFileName(file)} : PrintWindow a échoué ({w}x{h})");
            SavePng(crop, file);
        }
        finally
        {
            Win32.DeleteObject(crop);
        }
        return (cropW, cropH, source);
    }

    /// <summary>
    /// Ramène la fenêtre entière sur son écran, sans changer d'écran, et dit si elle y tient. La
    /// mise à l'échelle garde le coin haut-gauche : agrandie, la fenêtre peut déborder en bas
    /// ou à droite, alors qu'une fenêtre née à ce DPI serait centrée. Une fenêtre plus grande
    /// que l'écran n'est pas déplacée : la pousser vers un autre écran lui enverrait un vrai
    /// WM_DPICHANGED.
    /// </summary>
    private static bool KeepOnMonitor(IntPtr hwnd)
    {
        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST), ref info)
            || !Win32.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var screen = info.rcMonitor;
        int w = rect.right - rect.left;
        int h = rect.bottom - rect.top;
        if (w > screen.right - screen.left || h > screen.bottom - screen.top)
            return false;

        int x = Math.Clamp(rect.left, screen.left, screen.right - w);
        int y = Math.Clamp(rect.top, screen.top, screen.bottom - h);
        if (x != rect.left || y != rect.top)
        {
            Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            Pump(3);
        }
        return true;
    }

    /// <summary>Enregistre une HBITMAP en PNG par GDI+ plat (l'appelant a démarré GDI+).</summary>
    internal static void SavePng(IntPtr hBitmap, string file)
    {
        if (Native.GdipCreateBitmapFromHBITMAP(hBitmap, IntPtr.Zero, out IntPtr image) != 0 || image == IntPtr.Zero)
            throw new InvalidOperationException($"{Path.GetFileName(file)} : GdipCreateBitmapFromHBITMAP a échoué");
        try
        {
            var encoder = Native.PngEncoderClsid;
            int status = Native.GdipSaveImageToFile(image, file, ref encoder, IntPtr.Zero);
            if (status != 0)
                throw new InvalidOperationException($"{Path.GetFileName(file)} : GdipSaveImageToFile a rendu {status}");
        }
        finally
        {
            Win32.GdipDisposeImage(image);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Cible d'une capture et journal
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Une langue et un DPI : nomme les fichiers et tient le journal.</summary>
    internal sealed class Target
    {
        public Target(string outDir, string language, int dpi, Journal journal)
        {
            OutDir = outDir;
            Language = language;
            Dpi = dpi;
            Journal = journal;
        }

        public string OutDir { get; }
        public string Language { get; }
        public int Dpi { get; }
        public Journal Journal { get; }

        /// <summary>Nom de fichier : <c>{sujet}-{langue}-{échelle}.png</c>.</summary>
        public string File(string slug) => Path.Combine(OutDir, $"{slug}-{Language}-{Percent(Dpi)}.png");

        /// <summary>Capture la fenêtre et inscrit le fichier au journal.</summary>
        public void Shoot(IntPtr hwnd, string slug, int windowDpi)
        {
            string file = File(slug);
            var (w, h, source) = Capture(hwnd, file);
            Journal.Add(file, w, h, windowDpi, Dpi, source);
        }
    }

    /// <summary>
    /// Journal d'un banc : une ligne par image, avec ses dimensions, le DPI de la fenêtre avant
    /// simulation, le DPI demandé et l'empreinte du fichier. Il permet de compter les rendus
    /// distincts : un banc paramétré peut écrire N fichiers pour un seul rendu, si le sujet ne
    /// lit pas le paramètre (leçon du banc de main, 29/08).
    /// </summary>
    internal sealed class Journal
    {
        private readonly string _path;
        private readonly List<string> _lines = new();

        public Journal(string outDir, string name) => _path = Path.Combine(outDir, name);

        public string Header { get; set; } = "";

        public int Count => _lines.Count;

        public void Add(string file, int width, int height, int windowDpi, int targetDpi, string source)
        {
            string hash = Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(file))).ToLowerInvariant();
            _lines.Add(string.Join('\t', Path.GetFileName(file), width.ToString(CultureInfo.InvariantCulture),
                height.ToString(CultureInfo.InvariantCulture), windowDpi.ToString(CultureInfo.InvariantCulture),
                targetDpi.ToString(CultureInfo.InvariantCulture), source, hash));
        }

        public void Write()
        {
            var text = new StringBuilder();
            if (Header.Length > 0)
                text.Append(Header).Append('\n');
            text.Append("fichier\tlargeur\thauteur\tdpi_fenetre\tdpi_cible\tvoie\tsha256\n");
            foreach (string line in _lines)
                text.Append(line).Append('\n');
            System.IO.File.WriteAllText(_path, text.ToString(), new UTF8Encoding(false));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Appels natifs propres au banc
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Déclarés ici et non dans <c>Win32.cs</c> : le produit n'en a pas l'usage.</summary>
    internal static class Native
    {
        internal const uint PW_RENDERFULLCONTENT = 0x00000002;
        internal const uint PM_REMOVE = 0x0001;
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        internal static readonly Guid PngEncoderClsid = new("557CF406-1A04-11D3-9A73-0000F81EF32E");

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessageW(out Win32.MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        internal static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        internal const uint RDW_INVALIDATE = 0x0001;
        internal const uint RDW_ERASE = 0x0004;
        internal const uint RDW_ALLCHILDREN = 0x0080;
        internal const uint RDW_UPDATENOW = 0x0100;
        internal const uint RDW_FRAME = 0x0400;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        internal const uint DCX_CACHE = 0x00000002;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;

        internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Win32.RECT bounds, IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hrgnClip, uint flags);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GdiFlush();

        [DllImport("dwmapi.dll")]
        internal static extern int DwmFlush();

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Win32.RECT pvAttribute, int cbAttribute);

        [DllImport("gdiplus.dll")]
        internal static extern int GdipCreateBitmapFromHBITMAP(IntPtr hbm, IntPtr hpal, out IntPtr bitmap);

        [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)]
        internal static extern int GdipSaveImageToFile(IntPtr image, string filename, ref Guid clsidEncoder, IntPtr encoderParams);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetModuleFileNameW(IntPtr module, StringBuilder fileName, int size);
    }
}
