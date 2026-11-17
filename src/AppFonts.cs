// Polices embarquées de la refonte 1.3.0 (direction Tablature, maquettes du 07/10).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Une police d'écran : famille, hauteur de caractère en pixels (déjà mise à l'échelle) et
/// graisse. Les fenêtres refondues décrivent leurs polices ainsi. Sans propriétaire,
/// <see cref="AppFonts"/> crée et garde la HFONT, partagée entre fenêtres : réservé aux tailles
/// qui ne suivent que le DPI. Avec un propriétaire (<see cref="WindowFonts"/>), la police vit
/// avec l'échelle de sa fenêtre (audit des ressources du 16/11, C1 et C2).
/// </summary>
internal readonly record struct FontSpec(string Family, int Height, int Weight = TypeRamp.Regular, WindowFonts? Owner = null)
{
    public IntPtr Handle => Owner is { } owner ? owner.Get(this) : AppFonts.Get(this);

    /// <summary>Une taille de maquette (px à 96 DPI) portée à une échelle qui ne suit que le
    /// DPI : police du cache partagé.</summary>
    public static FontSpec Of(string family, int size, int weight, float scale)
        => new(family, Math.Max(1, (int)(size * scale)), weight);

    /// <summary>Une taille de maquette portée à l'échelle d'une fenêtre : police de son propriétaire.</summary>
    public static FontSpec Of(string family, int size, int weight, FontScale scale)
        => new(family, Math.Max(1, (int)(size * scale.Value)), weight, scale.Owner);
}

/// <summary>
/// L'échelle de dessin d'une fenêtre et le jeu qui possède les polices de cette échelle. Les
/// dessinateurs partagés (<see cref="KeyNotation"/>, <see cref="TablatureRenderer"/>,
/// <see cref="MiniKeyboardRenderer"/>) la reçoivent au lieu d'un <c>float</c> : aucune
/// conversion implicite, un appelant choisit entre <see cref="WindowFonts.At"/> (échelle qui
/// suit la taille de la fenêtre) et <see cref="Shared"/> (échelle qui ne suit que le DPI).
/// </summary>
internal readonly record struct FontScale(float Value, WindowFonts? Owner)
{
    /// <summary>Échelle qui ne suit que le DPI : polices du cache partagé, une par écran.</summary>
    internal static FontScale Shared(float value) => new(value, null);

    /// <summary>La même échelle multipliée (tailles dérivées), au même propriétaire.</summary>
    internal FontScale Times(float factor) => this with { Value = Value * factor };

    /// <summary>Une police de hauteur déjà calculée, au même propriétaire.</summary>
    internal FontSpec Font(string family, int height, int weight) => new(family, Math.Max(1, height), weight, Owner);
}

/// <summary>
/// Les polices d'une fenêtre dont l'échelle suit la taille (Leçons, clavier virtuel) : créées à
/// la demande, toutes détruites quand l'échelle change (<see cref="At"/>) et à la fermeture
/// (<see cref="Dispose"/>). Le cache partagé en gardait une par hauteur traversée, jusqu'à la
/// fin du processus. Les dessinateurs remettent la police d'origine dans le DC après chaque
/// texte (<see cref="PaperText"/>) : aucune police d'ici ne reste sélectionnée entre deux messages.
/// Fil de l'interface seulement.
/// </summary>
internal sealed class WindowFonts : IDisposable
{
    private readonly Dictionary<FontSpec, IntPtr> _fonts = new();
    private float _scale = float.NaN;
    private int _thread;

    /// <summary>Polices vivantes (tests).</summary>
    internal int Count => _fonts.Count;

    /// <summary>L'échelle courante de la fenêtre. Une échelle différente de la précédente
    /// détruit d'abord les polices de l'ancienne.</summary>
    internal FontScale At(float scale)
    {
        CheckThread();
        if (scale != _scale)
        {
            Reset();
            _scale = scale;
        }
        return new FontScale(scale, this);
    }

    internal IntPtr Get(FontSpec spec)
    {
        CheckThread();
        if (_fonts.TryGetValue(spec, out var font))
            return font;
        font = AppFonts.Create(spec);
        _fonts[spec] = font;
        return font;
    }

    /// <summary>Détruit toutes les polices ; les suivantes sont recréées à la demande.</summary>
    internal void Reset()
    {
        foreach (var font in _fonts.Values)
            Win32.DeleteObject(font);
        _fonts.Clear();
    }

    public void Dispose()
    {
        Reset();
        _scale = float.NaN;
    }

    /// <summary>Sans verrou : un seul fil, celui de la fenêtre. Un appel d'un autre fil
    /// (fournisseur UIA, minuterie du pool) pourrait détruire les polices d'une peinture en
    /// cours ; les tests le voient en Debug.</summary>
    private void CheckThread()
    {
        int current = Environment.CurrentManagedThreadId;
        if (_thread == 0) _thread = current;
        global::System.Diagnostics.Debug.Assert(_thread == current, "WindowFonts appelé hors du fil de sa fenêtre");
    }
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
    /// <summary>Pictogrammes absents des polices embarquées (✈ ☎ ⚠ ✅ ❄…), en monochrome gras :
    /// présente sur tout Windows 10 et 11.</summary>
    internal const string Pictographs = "Segoe UI Emoji";
    /// <summary>Pictogrammes que Segoe UI Emoji n'a pas (★ ☞ ☽).</summary>
    internal const string PictographsFallback = "Segoe UI Symbol";

    /// <summary>Hauteur d'un pictogramme rapportée à celle du texte. Segoe UI Emoji remplit tout
    /// son em : à la hauteur du texte, l'avion et l'étoile prenaient 1,3 touche de largeur sur le
    /// clavier virtuel (touche morte Symboles divers). Réglé par <c>PictographFitTests</c> pour
    /// que leur encre mesure à peu près celle d'une lettre capitale.</summary>
    internal const float PictographScale = 0.7f;

    /// <summary>Même rapport pour Segoe UI Symbol (★ ☽ ☞), dont les glyphes sont dessinés plus
    /// petits dans leur em que ceux de Segoe UI Emoji : à 0,62 l'étoile ne mesurait que 12 px
    /// pour une touche de 50.</summary>
    internal const float PictographFallbackScale = 1.0f;

    /// <summary>Un pictogramme : ℹ et les blocs U+2300–U+27BF (Technique, Symboles divers,
    /// Dingbats). Les flèches, formes géométriques et symboles mathématiques gardent le secours
    /// de Segoe UI, qui les dessine à la taille du texte.</summary>
    internal static bool IsPictograph(char c) => c == 'ℹ' || (c >= '⌀' && c <= '➿');

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

    /// <summary>La HFONT partagée de <paramref name="spec"/> (sans propriétaire), créée à la
    /// première demande puis gardée jusqu'à la fin du processus. Ne jamais la détruire : elle
    /// est partagée. Réservé aux tailles qui ne suivent que le DPI (un jeu par écran).</summary>
    internal static IntPtr Get(FontSpec spec)
    {
        global::System.Diagnostics.Debug.Assert(spec.Owner == null, "police à propriétaire demandée au cache partagé");
        lock (Gate)
        {
            if (Cache.TryGetValue(spec, out var font))
                return font;
            font = Create(spec);
            Cache[spec] = font;
            return font;
        }
    }

    /// <summary>Crée la HFONT de <paramref name="spec"/> ; l'appelant la garde ou la détruit
    /// (<see cref="Get"/> la garde, <see cref="WindowFonts"/> la détruit).</summary>
    internal static IntPtr Create(FontSpec spec)
    {
        EnsureLoaded();
        return Win32.CreateFontW(-spec.Height, 0, 0, 0, spec.Weight, 0, 0, 0, 0, 0, 0, 5, 0, spec.Family);
    }

    /// <summary>La chaîne de secours d'une police : elle-même, puis les polices qui comblent
    /// ses manques, à la même hauteur et la même graisse.</summary>
    internal static FontSpec[] Chain(FontSpec spec) => spec.Family switch
    {
        System => [spec],
        Symbols => [spec, spec with { Family = System }],
        _ => [spec, spec with { Family = KeyMarks }, spec with { Family = Symbols },
            Pictograph(spec, Pictographs, PictographScale), Pictograph(spec, PictographsFallback, PictographFallbackScale),
            spec with { Family = System }],
    };

    /// <summary>Police des pictogrammes : à la hauteur réduite, et en graisse normale (ces polices
    /// n'ont pas de gras : GDI l'épaissirait à la main). <see cref="PaperText"/> ne l'essaie que
    /// pour un pictogramme.</summary>
    private static FontSpec Pictograph(FontSpec spec, string family, float scale)
        => new(family, Math.Max(1, (int)MathF.Round(spec.Height * scale)), TypeRamp.Regular);
}
