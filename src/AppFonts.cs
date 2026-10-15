// Polices embarquées de la refonte 1.3.0 (direction Tablature, maquettes du 07/10).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Une police d'écran : famille, hauteur de caractère en pixels (déjà mise à l'échelle) et
/// graisse. Les fenêtres refondues décrivent leurs polices ainsi ; <see cref="AppFonts"/> crée
/// et garde la HFONT, et la partage entre fenêtres.
/// </summary>
internal readonly record struct FontSpec(string Family, int Height, int Weight = TypeRamp.Regular)
{
    public IntPtr Handle => AppFonts.Get(this);

    /// <summary>La spécification d'une taille de maquette (px à 96 DPI) portée à l'échelle.</summary>
    public static FontSpec Of(string family, int size, int weight, float scale)
        => new(family, Math.Max(1, (int)(size * scale)), weight);
}

/// <summary>
/// Les polices du site, renommées pour l'app (licence OFL, nom réservé « Source ») et chargées
/// en privé au processus : rien n'est installé dans Windows. Leurs jeux de caractères sont
/// ceux du site, latins : <see cref="PaperText"/> complète un caractère absent par les
/// polices de secours, puis par Segoe UI.
/// </summary>
internal static class AppFonts
{
    /// <summary>Titres, caractères à taper, signes du clavier virtuel (Literata 600).</summary>
    internal const string Title = "AG Titre";
    /// <summary>Texte d'interface (Source Sans 3, 400 et 600).</summary>
    internal const string Text = "AG Texte";
    /// <summary>Gravures des touches et cases à frapper (Source Code Pro).</summary>
    internal const string Mono = "AG Mono";
    /// <summary>Secours du clavier : ◌, marques combinantes, grec et cyrillique des touches.</summary>
    internal const string KeyMarks = "AG Clavier";
    /// <summary>Secours des symboles mathématiques (≤ ≥ ∑ √…).</summary>
    internal const string Symbols = "AG Symboles";
    /// <summary>Dernier secours, présent sur tout Windows.</summary>
    internal const string System = "Segoe UI";

    private static readonly object Gate = new();
    private static readonly Dictionary<FontSpec, IntPtr> Cache = new();
    private static bool _loaded;
    private static int _loadedCount;

    /// <summary>Nombre de polices chargées (tests, journal).</summary>
    internal static int LoadedCount { get { EnsureLoaded(); return _loadedCount; } }

    /// <summary>Charge une fois les TTF embarquées (ressources « font.*.ttf »). La mémoire
    /// reste allouée pour la vie du processus, comme les polices.</summary>
    internal static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
            var assembly = typeof(AppFonts).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith("font.", StringComparison.Ordinal) || !name.EndsWith(".ttf", StringComparison.Ordinal))
                    continue;
                try
                {
                    using var stream = assembly.GetManifestResourceStream(name);
                    if (stream == null) continue;
                    var bytes = new byte[stream.Length];
                    stream.ReadExactly(bytes);
                    IntPtr memory = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, memory, bytes.Length);
                    if (Win32.AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, out uint count) != IntPtr.Zero)
                        _loadedCount += (int)count;
                    else
                        Marshal.FreeHGlobal(memory);
                }
                catch (Exception ex)
                {
                    // Une police manquante n'empêche rien : Segoe UI la remplace.
                    ConfigManager.Log($"AppFonts {name}", ex);
                }
            }
        }
    }

    /// <summary>La HFONT de <paramref name="spec"/>, créée à la première demande puis gardée.
    /// Ne jamais la détruire : elle est partagée.</summary>
    internal static IntPtr Get(FontSpec spec)
    {
        EnsureLoaded();
        lock (Gate)
        {
            if (Cache.TryGetValue(spec, out var font))
                return font;
            font = Win32.CreateFontW(-spec.Height, 0, 0, 0, spec.Weight, 0, 0, 0, 0, 0, 0, 5, 0, spec.Family);
            Cache[spec] = font;
            return font;
        }
    }

    /// <summary>La chaîne de secours d'une police : elle-même, puis les polices qui comblent
    /// ses manques, à la même hauteur et la même graisse.</summary>
    internal static FontSpec[] Chain(FontSpec spec) => spec.Family switch
    {
        System => [spec],
        Symbols => [spec, spec with { Family = System }],
        _ => [spec, spec with { Family = KeyMarks }, spec with { Family = Symbols }, spec with { Family = System }],
    };
}
