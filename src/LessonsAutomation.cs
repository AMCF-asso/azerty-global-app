// UI Automation de la ligne des Leçons — lot « surface unique », étape 3 (1.3.0).
//
// Décision d'Antoine du 29/09 : fournisseur UIA complet, Text Pattern compris, dans la 1.3.0.
// La fenêtre des Leçons peint tout dans un seul HWND : sans fournisseur, la loupe, Narrateur
// ou NVDA n'y trouvent qu'une fenêtre vide. Arbre exposé : la fenêtre (racine, dont le HWND
// fournit les propriétés par UiaHostProviderFromHwnd) et un enfant, la surface où l'on tape,
// de type Edit. Elle porte TextPattern et TextPattern2 : le texte de la ligne, le curseur
// (GetCaretRange) et la place de chaque caractère, relevée au dernier repeint.
//
// Interop source-générée, comme ToastActivation.cs : compatible NativeAOT, sans bloc unsafe
// dans l'application. SAFEARRAY à la main (oleaut32), VARIANT par une structure blittable.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace AZERTYGlobal;

/// <summary>Ce que montre la surface de frappe au dernier repeint, en coordonnées client.</summary>
/// <param name="Cells">Case de chaque caractère de <paramref name="Text"/>, défilement compris ;
/// vide quand la géométrie n'est pas relevée (mode Libre) : la surface entière en tient lieu.</param>
internal sealed record LessonSurfaceView(string Name, string Text, int Caret, Win32.RECT Box,
    Win32.RECT[] Cells, Win32.RECT? CaretRect);

/// <summary>Ce que la fenêtre des Leçons donne à lire à l'UI Automation.</summary>
internal interface ILessonSurfaceHost
{
    IntPtr Handle { get; }
    /// <summary>Null quand rien ne se tape (récapitulatif, Paramètres).</summary>
    LessonSurfaceView? SurfaceView { get; }
    bool WindowHasFocus { get; }
    bool SurfaceHasFocus { get; }
    void FocusSurface();
}

internal enum ProviderOptions
{
    ServerSideProvider = 0x2,
    UseComThreading = 0x20,
}

internal enum NavigateDirection { Parent = 0, NextSibling = 1, PreviousSibling = 2, FirstChild = 3, LastChild = 4 }

internal enum TextUnit { Character = 0, Format = 1, Word = 2, Line = 3, Paragraph = 4, Page = 5, Document = 6 }

internal enum TextPatternRangeEndpoint { Start = 0, End = 1 }

internal enum SupportedTextSelection { None = 0, Single = 1, Multiple = 2 }

[StructLayout(LayoutKind.Sequential)]
internal struct UiaRect
{
    public double Left, Top, Width, Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct UiaPoint
{
    public double X, Y;
}

/// <summary>VARIANT (oaidl.h), disposition 64 bits : 8 octets d'en-tête, puis l'union.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Variant
{
    private const ushort VT_I4 = 3;
    private const ushort VT_BSTR = 8;
    private const ushort VT_BOOL = 11;
    private const ushort VT_UNKNOWN = 13;

    public ushort Vt;
    public ushort Reserved1, Reserved2, Reserved3;
    public nint Value;
    public nint Record;

    public static Variant Int(int value) => new() { Vt = VT_I4, Value = value };
    public static Variant Bool(bool value) => new() { Vt = VT_BOOL, Value = value ? -1 : 0 }; // VARIANT_TRUE = -1
    public static Variant String(string value) => new() { Vt = VT_BSTR, Value = Marshal.StringToBSTR(value) };

    /// <summary>Valeur réservée « non pris en charge » d'UIA (attributs de texte).</summary>
    public static Variant NotSupported()
    {
        Marshal.ThrowExceptionForHR(LessonsAutomation.UiaGetReservedNotSupportedValue(out nint unknown));
        Marshal.AddRef(unknown); // l'appelant libère le VARIANT
        return new Variant { Vt = VT_UNKNOWN, Value = unknown };
    }
}

[GeneratedComInterface]
[Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
internal partial interface IRawElementProviderSimple
{
    ProviderOptions GetProviderOptions();
    nint GetPatternProvider(int patternId);
    Variant GetPropertyValue(int propertyId);
    nint GetHostRawElementProvider();
}

[GeneratedComInterface]
[Guid("f7063da8-8359-439c-9297-bbc5299a7d87")]
internal partial interface IRawElementProviderFragment
{
    nint Navigate(NavigateDirection direction);
    nint GetRuntimeId();
    UiaRect GetBoundingRectangle();
    nint GetEmbeddedFragmentRoots();
    void SetFocus();
    nint GetFragmentRoot();
}

[GeneratedComInterface]
[Guid("620ce2a5-ab8f-40a9-86cb-de3c75599b58")]
internal partial interface IRawElementProviderFragmentRoot
{
    nint ElementProviderFromPoint(double x, double y);
    nint GetFocus();
}

[GeneratedComInterface]
[Guid("3589c92c-63f3-4367-99bb-ada653b77cf2")]
internal partial interface ITextProvider
{
    nint GetSelection();
    nint GetVisibleRanges();
    nint RangeFromChild(nint childElement);
    nint RangeFromPoint(UiaPoint point);
    nint GetDocumentRange();
    SupportedTextSelection GetSupportedTextSelection();
}

[GeneratedComInterface]
[Guid("0dc5e6ed-3e16-4bf1-8f9a-a979878bc195")]
internal partial interface ITextProvider2 : ITextProvider
{
    nint RangeFromAnnotation(nint annotationElement);
    nint GetCaretRange([MarshalAs(UnmanagedType.Bool)] out bool isActive);
}

[GeneratedComInterface]
[Guid("5347ad7b-c355-46f8-aff5-909033582f63")]
internal partial interface ITextRangeProvider
{
    nint Clone();
    [return: MarshalAs(UnmanagedType.Bool)]
    bool Compare(nint range);
    int CompareEndpoints(TextPatternRangeEndpoint endpoint, nint targetRange, TextPatternRangeEndpoint targetEndpoint);
    void ExpandToEnclosingUnit(TextUnit unit);
    nint FindAttribute(int attributeId, Variant value, [MarshalAs(UnmanagedType.Bool)] bool backward);
    nint FindText([MarshalAs(UnmanagedType.BStr)] string text, [MarshalAs(UnmanagedType.Bool)] bool backward,
        [MarshalAs(UnmanagedType.Bool)] bool ignoreCase);
    Variant GetAttributeValue(int attributeId);
    nint GetBoundingRectangles();
    nint GetEnclosingElement();
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetText(int maxLength);
    int Move(TextUnit unit, int count);
    int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count);
    void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, nint targetRange, TextPatternRangeEndpoint targetEndpoint);
    void Select();
    void AddToSelection();
    void RemoveFromSelection();
    void ScrollIntoView([MarshalAs(UnmanagedType.Bool)] bool alignToTop);
    nint GetChildren();
}

/// <summary>
/// Fournisseurs UIA de la fenêtre des Leçons : créés au premier WM_GETOBJECT, c'est-à-dire
/// seulement quand un client UIA les demande, et déconnectés avant la destruction du HWND.
/// </summary>
internal sealed partial class LessonsAutomation : IDisposable
{
    internal const int UiaRootObjectId = -25;
    internal const int TextPatternId = 10014;
    internal const int TextPattern2Id = 10024;
    internal const int ControlTypePropertyId = 30003;
    internal const int NamePropertyId = 30005;
    internal const int HasKeyboardFocusPropertyId = 30008;
    internal const int IsKeyboardFocusablePropertyId = 30009;
    internal const int IsEnabledPropertyId = 30010;
    internal const int AutomationIdPropertyId = 30011;
    internal const int IsPasswordPropertyId = 30019;
    internal const int IsOffscreenPropertyId = 30022;
    internal const int EditControlTypeId = 50004;
    internal const int AutomationFocusChangedEventId = 20005;
    internal const int TextSelectionChangedEventId = 20014;
    internal const int TextChangedEventId = 20015;
    internal const string SurfaceAutomationId = "LessonSurface";
    private const int UiaAppendRuntimeId = 3;
    private const int UIA_E_ELEMENTNOTAVAILABLE = unchecked((int)0x80040201);

    internal static readonly Guid IidUnknown = new("00000000-0000-0000-C000-000000000046");
    internal static readonly Guid IidSimple = new("d6dd68d1-86fd-4332-8666-9abedea2d24c");
    internal static readonly Guid IidFragment = new("f7063da8-8359-439c-9297-bbc5299a7d87");
    internal static readonly Guid IidFragmentRoot = new("620ce2a5-ab8f-40a9-86cb-de3c75599b58");
    internal static readonly Guid IidTextRange = new("5347ad7b-c355-46f8-aff5-909033582f63");

    // Appels reçus sur le fil d'interface (STA), selon les règles de COM : aucun verrou.
    // Mesuré le 01/10 (LeconsAutomationTests) : sans UseComThreading, un client du même
    // processus fait arriver une partie des appels sur son propre fil.
    internal const ProviderOptions Options = ProviderOptions.ServerSideProvider | ProviderOptions.UseComThreading;

    internal static readonly StrategyBasedComWrappers Wrappers = new();

    private readonly RootProvider _root;
    private readonly SurfaceProvider _surface;
    private string? _lastText;
    private int _lastCaret = -1;
    private bool _lastSurfaceFocus;
    private bool _disposed;

    public LessonsAutomation(ILessonSurfaceHost host)
    {
        Host = host;
        _root = new RootProvider(this);
        _surface = new SurfaceProvider(this);
    }

    internal ILessonSurfaceHost Host { get; }
    internal SurfaceProvider Surface => _surface;
    internal RootProvider Root => _root;

    /// <summary>Réponse à WM_GETOBJECT pour UiaRootObjectId.</summary>
    public IntPtr OnGetObject(IntPtr wParam, IntPtr lParam)
    {
        nint root = Pointer(_root, IidSimple);
        try { return UiaReturnRawElementProvider(Host.Handle, wParam, lParam, root); }
        finally { Marshal.Release(root); }
    }

    /// <summary>
    /// Après un repeint : signale aux clients ce qui a changé depuis le précédent. Le repeint
    /// fait foi, puisque la géométrie lue par les clients est celle qu'il a relevée.
    /// </summary>
    public void RaiseChanges()
    {
        if (_disposed) return;
        var view = Host.SurfaceView;
        bool surfaceFocus = view != null && Host.SurfaceHasFocus;
        bool focusChanged = surfaceFocus != _lastSurfaceFocus;
        bool textChanged = view?.Text != _lastText;
        bool caretChanged = (view?.Caret ?? -1) != _lastCaret;
        _lastSurfaceFocus = surfaceFocus;
        _lastText = view?.Text;
        _lastCaret = view?.Caret ?? -1;

        if (!UiaClientsAreListening()) return;
        if (focusChanged && surfaceFocus)
            Raise(_surface, AutomationFocusChangedEventId);
        else if (focusChanged && Host.WindowHasFocus)
            Raise(_root, AutomationFocusChangedEventId); // Tab vers un bouton, qui n'a pas d'élément propre
        if (view == null) return;
        if (textChanged)
            Raise(_surface, TextChangedEventId);
        if (textChanged || caretChanged)
            Raise(_surface, TextSelectionChangedEventId);
    }

    private static void Raise(object provider, int eventId)
    {
        nint simple = Pointer(provider, IidSimple);
        try { UiaRaiseAutomationEvent(simple, eventId); }
        finally { Marshal.Release(simple); }
    }

    internal void ThrowIfGone()
    {
        if (_disposed || Host.Handle == IntPtr.Zero)
            throw new COMException(null, UIA_E_ELEMENTNOTAVAILABLE);
    }

    internal UiaRect ToScreen(Win32.RECT rect)
    {
        var origin = new Win32.POINT { x = rect.left, y = rect.top };
        Win32.ClientToScreen(Host.Handle, ref origin);
        return new UiaRect { Left = origin.x, Top = origin.y, Width = rect.right - rect.left, Height = rect.bottom - rect.top };
    }

    internal Win32.POINT ToClient(double x, double y)
    {
        var point = new Win32.POINT { x = (int)Math.Floor(x), y = (int)Math.Floor(y) };
        Win32.ScreenToClient(Host.Handle, ref point);
        return point;
    }

    /// <summary>Pointeur d'interface d'un objet géré, référence comptée pour l'appelant.</summary>
    internal static nint Pointer(object provider, in Guid iid)
    {
        nint unknown = Wrappers.GetOrCreateComInterfaceForObject(provider, CreateComInterfaceFlags.None);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out nint pointer));
            return pointer;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <summary>Objet géré derrière un pointeur que nous avons nous-mêmes donné ; null sinon.</summary>
    internal static T? Unwrap<T>(nint pointer) where T : class
    {
        if (pointer == 0 || Marshal.QueryInterface(pointer, in IidUnknown, out nint unknown) != 0)
            return null;
        try
        {
            return ComWrappers.TryGetObject(unknown, out object? instance) ? instance as T : null;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    internal static nint RuntimeId() => SafeArray.Ints(UiaAppendRuntimeId, 1);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IntPtr hwnd = Host.Handle;
        if (hwnd != IntPtr.Zero)
            UiaReturnRawElementProvider(hwnd, IntPtr.Zero, IntPtr.Zero, 0); // libère ce qu'UIA garde du HWND
        foreach (object provider in new object[] { _root, _surface })
        {
            nint simple = Pointer(provider, IidSimple);
            try { UiaDisconnectProvider(simple); }
            finally { Marshal.Release(simple); }
        }
    }

    [DllImport("UIAutomationCore.dll")]
    private static extern IntPtr UiaReturnRawElementProvider(IntPtr hwnd, IntPtr wParam, IntPtr lParam, nint element);

    [DllImport("UIAutomationCore.dll")]
    internal static extern int UiaHostProviderFromHwnd(IntPtr hwnd, out nint provider);

    [DllImport("UIAutomationCore.dll")]
    private static extern int UiaRaiseAutomationEvent(nint provider, int eventId);

    [DllImport("UIAutomationCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UiaClientsAreListening();

    [DllImport("UIAutomationCore.dll")]
    private static extern int UiaDisconnectProvider(nint provider);

    [DllImport("UIAutomationCore.dll")]
    internal static extern int UiaGetReservedNotSupportedValue(out nint notSupported);

    /// <summary>La fenêtre : propriétés et place données par son HWND, la surface pour enfant.</summary>
    [GeneratedComClass]
    internal sealed partial class RootProvider(LessonsAutomation owner)
        : IRawElementProviderSimple, IRawElementProviderFragment, IRawElementProviderFragmentRoot
    {
        public ProviderOptions GetProviderOptions() => Options;

        public nint GetPatternProvider(int patternId) => 0;

        public Variant GetPropertyValue(int propertyId) => default; // VT_EMPTY : le HWND répond

        public nint GetHostRawElementProvider()
        {
            owner.ThrowIfGone();
            Marshal.ThrowExceptionForHR(UiaHostProviderFromHwnd(owner.Host.Handle, out nint host));
            return host;
        }

        public nint Navigate(NavigateDirection direction) =>
            direction is NavigateDirection.FirstChild or NavigateDirection.LastChild && owner.Host.SurfaceView != null
                ? Pointer(owner.Surface, IidFragment)
                : 0;

        public nint GetRuntimeId() => 0; // celui du HWND

        public UiaRect GetBoundingRectangle() => default; // celui du HWND

        public nint GetEmbeddedFragmentRoots() => 0;

        public void SetFocus() { }

        public nint GetFragmentRoot() => Pointer(this, IidFragmentRoot);

        public nint ElementProviderFromPoint(double x, double y)
        {
            owner.ThrowIfGone();
            if (owner.Host.SurfaceView is not { } view)
                return 0;
            var point = owner.ToClient(x, y);
            bool inside = point.x >= view.Box.left && point.x < view.Box.right && point.y >= view.Box.top && point.y < view.Box.bottom;
            return inside ? Pointer(owner.Surface, IidFragment) : 0;
        }

        public nint GetFocus() =>
            owner.Host.SurfaceView != null && owner.Host.SurfaceHasFocus ? Pointer(owner.Surface, IidFragment) : 0;
    }

    /// <summary>La surface où l'on tape : un Edit qui porte TextPattern et TextPattern2.</summary>
    [GeneratedComClass]
    internal sealed partial class SurfaceProvider(LessonsAutomation owner)
        : IRawElementProviderSimple, IRawElementProviderFragment, ITextProvider2
    {
        internal LessonsAutomation Owner => owner;

        /// <summary>Texte et place des caractères, ou une ligne vide quand la surface a disparu.</summary>
        internal LessonSurfaceView View =>
            owner.Host.SurfaceView ?? new LessonSurfaceView("", "", 0, default, Array.Empty<Win32.RECT>(), null);

        public ProviderOptions GetProviderOptions() => Options;

        public nint GetPatternProvider(int patternId) =>
            patternId is TextPatternId or TextPattern2Id ? Pointer(this, IidUnknown) : 0;

        public Variant GetPropertyValue(int propertyId)
        {
            owner.ThrowIfGone();
            return propertyId switch
            {
                ControlTypePropertyId => Variant.Int(EditControlTypeId),
                NamePropertyId => Variant.String(View.Name),
                AutomationIdPropertyId => Variant.String(SurfaceAutomationId),
                IsKeyboardFocusablePropertyId => Variant.Bool(true),
                HasKeyboardFocusPropertyId => Variant.Bool(owner.Host.SurfaceView != null && owner.Host.SurfaceHasFocus),
                IsEnabledPropertyId => Variant.Bool(true),
                IsPasswordPropertyId => Variant.Bool(false),
                IsOffscreenPropertyId => Variant.Bool(owner.Host.SurfaceView == null),
                _ => default,
            };
        }

        public nint GetHostRawElementProvider() => 0;

        public nint Navigate(NavigateDirection direction) =>
            direction == NavigateDirection.Parent ? Pointer(owner.Root, IidFragment) : 0;

        public nint GetRuntimeId() => RuntimeId();

        public UiaRect GetBoundingRectangle()
        {
            owner.ThrowIfGone();
            return owner.Host.SurfaceView is { } view ? owner.ToScreen(view.Box) : default;
        }

        public nint GetEmbeddedFragmentRoots() => 0;

        public void SetFocus()
        {
            owner.ThrowIfGone();
            owner.Host.FocusSurface();
        }

        public nint GetFragmentRoot() => Pointer(owner.Root, IidFragmentRoot);

        public nint GetSelection() => SafeArray.Unknowns(CaretRange());

        public nint GetVisibleRanges()
        {
            var view = View;
            int first = 0, last = view.Text.Length;
            if (view.Cells.Length == view.Text.Length && view.Text.Length > 0)
            {
                while (first < last && view.Cells[first].right <= view.Box.left) first++;
                while (last > first && view.Cells[last - 1].left >= view.Box.right) last--;
            }
            return SafeArray.Unknowns(new TextRange(this, first, last));
        }

        public nint RangeFromChild(nint childElement) => throw new ArgumentException(null, nameof(childElement)); // aucun enfant

        public nint RangeFromPoint(UiaPoint point)
        {
            owner.ThrowIfGone();
            var view = View;
            var client = owner.ToClient(point.X, point.Y);
            int index = view.Cells.Length == view.Text.Length ? IndexAt(view, client.x) : view.Caret;
            return Pointer(new TextRange(this, index, index), IidTextRange);
        }

        public nint GetDocumentRange() => Pointer(new TextRange(this, 0, View.Text.Length), IidTextRange);

        // Un seul curseur, que seule la frappe déplace : Select le refuse ailleurs qu'au curseur.
        public SupportedTextSelection GetSupportedTextSelection() => SupportedTextSelection.Single;

        public nint RangeFromAnnotation(nint annotationElement) => throw new ArgumentException(null, nameof(annotationElement));

        public nint GetCaretRange(out bool isActive)
        {
            isActive = owner.Host.SurfaceView != null && owner.Host.SurfaceHasFocus;
            return Pointer(CaretRange(), IidTextRange);
        }

        private TextRange CaretRange()
        {
            int caret = Math.Clamp(View.Caret, 0, View.Text.Length);
            return new TextRange(this, caret, caret);
        }

        /// <summary>Index du caractère sous l'abscisse : avant sa moitié, lui ; après, le suivant.</summary>
        internal static int IndexAt(LessonSurfaceView view, int x)
        {
            for (int i = 0; i < view.Cells.Length; i++)
            {
                var cell = view.Cells[i];
                if (x < (cell.left + cell.right) / 2)
                    return i;
            }
            return view.Cells.Length;
        }
    }

    /// <summary>
    /// Plage de texte sur la ligne de la surface. Le texte est relu à chaque appel : une plage
    /// gardée par un client après un changement de ligne est bornée au nouveau texte.
    /// </summary>
    [GeneratedComClass]
    internal sealed partial class TextRange(SurfaceProvider surface, int start, int end) : ITextRangeProvider
    {
        private int _start = start;
        private int _end = end;

        internal int Start => _start;
        internal int End => _end;

        private string Text
        {
            get
            {
                string text = surface.View.Text;
                _end = Math.Clamp(_end, 0, text.Length);
                _start = Math.Clamp(_start, 0, _end);
                return text;
            }
        }

        public nint Clone() => Pointer(new TextRange(surface, _start, _end), IidTextRange);

        public bool Compare(nint range) =>
            Unwrap<TextRange>(range) is { } other && other.SameSurface(surface) && other._start == _start && other._end == _end;

        private bool SameSurface(SurfaceProvider other) => ReferenceEquals(surface, other);

        public int CompareEndpoints(TextPatternRangeEndpoint endpoint, nint targetRange, TextPatternRangeEndpoint targetEndpoint)
        {
            var target = Target(targetRange);
            return Math.Sign(Endpoint(endpoint) - target.Endpoint(targetEndpoint));
        }

        public void ExpandToEnclosingUnit(TextUnit unit)
        {
            string text = Text;
            if (unit >= TextUnit.Line)
            {
                (_start, _end) = (0, text.Length);
                return;
            }
            _start = Floor(text, unit, _start);
            _end = Next(text, unit, _start); // en fin de ligne, la plage reste vide

        }

        public nint FindAttribute(int attributeId, Variant value, bool backward) => 0; // aucun attribut

        public nint FindText(string text, bool backward, bool ignoreCase)
        {
            string content = Text;
            if (string.IsNullOrEmpty(text))
                return 0;
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string span = content[_start.._end];
            int index = backward ? span.LastIndexOf(text, comparison) : span.IndexOf(text, comparison);
            return index < 0 ? 0 : Pointer(new TextRange(surface, _start + index, _start + index + text.Length), IidTextRange);
        }

        public Variant GetAttributeValue(int attributeId) => Variant.NotSupported();

        public nint GetBoundingRectangles()
        {
            surface.Owner.ThrowIfGone();
            string text = Text;
            var view = surface.View;
            Win32.RECT? rect = Bounds(view, text.Length);
            if (rect is not { } r)
                return SafeArray.Doubles(Array.Empty<double>());
            var screen = surface.Owner.ToScreen(r);
            return SafeArray.Doubles(new[] { screen.Left, screen.Top, screen.Width, screen.Height });
        }

        /// <summary>
        /// Rectangle de la plage, borné à la surface ; pour une plage vide, la place du curseur,
        /// que la loupe suit (GetCaretRange). Null si rien n'est visible.
        /// </summary>
        internal Win32.RECT? Bounds(LessonSurfaceView view, int length)
        {
            Win32.RECT rect;
            if (_start == _end)
            {
                if (view.CaretRect is { } caret && _start == view.Caret)
                    rect = caret;
                else if (view.Cells.Length == length && length > 0)
                {
                    var cell = view.Cells[Math.Min(_start, length - 1)];
                    int x = _start < length ? cell.left : cell.right;
                    rect = new Win32.RECT { left = x, top = cell.top, right = x + 1, bottom = cell.bottom };
                }
                else
                    return null;
            }
            else if (view.Cells.Length == length)
                rect = new Win32.RECT
                {
                    left = view.Cells[_start].left,
                    top = view.Cells[_start].top,
                    right = view.Cells[_end - 1].right,
                    bottom = view.Cells[_start].bottom,
                };
            else
                rect = view.Box;

            rect.left = Math.Max(rect.left, view.Box.left);
            rect.right = Math.Min(rect.right, view.Box.right);
            return rect.right > rect.left ? rect : null;
        }

        public nint GetEnclosingElement() => Pointer(surface, IidSimple);

        public string GetText(int maxLength)
        {
            string text = Text[_start.._end];
            return maxLength >= 0 && text.Length > maxLength ? text[..maxLength] : text;
        }

        public int Move(TextUnit unit, int count)
        {
            string text = Text;
            if (count == 0)
                return 0;
            if (unit >= TextUnit.Line)
                unit = TextUnit.Document;
            bool degenerate = _start == _end;
            int pos = degenerate ? _start : Floor(text, unit, _start);
            // Une plage non vide commence au plus tard au début de la dernière unité.
            int last = degenerate || text.Length == 0 ? text.Length : Floor(text, unit, text.Length - 1);
            int moved = 0;
            for (; count > 0 && pos < last; count--, moved++)
                pos = Next(text, unit, pos);
            for (; count < 0 && pos > 0; count++, moved--)
                pos = Previous(text, unit, pos);
            _start = pos;
            _end = degenerate ? pos : Next(text, unit, pos);
            return moved;
        }

        public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
        {
            string text = Text;
            int pos = Endpoint(endpoint);
            int moved = 0;
            for (; count > 0 && pos < text.Length; count--, moved++)
                pos = Next(text, unit, pos);
            for (; count < 0 && pos > 0; count++, moved--)
                pos = Previous(text, unit, pos);
            SetEndpoint(endpoint, pos);
            return moved;
        }

        public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, nint targetRange, TextPatternRangeEndpoint targetEndpoint)
        {
            var target = Target(targetRange);
            _ = Text;
            SetEndpoint(endpoint, target.Endpoint(targetEndpoint));
        }

        public void Select()
        {
            _ = Text;
            if (_start != _end || _start != Math.Clamp(surface.View.Caret, 0, surface.View.Text.Length))
                throw new InvalidOperationException(); // UIA_E_INVALIDOPERATION : seule la frappe déplace le curseur
        }

        public void AddToSelection() => throw new InvalidOperationException();

        public void RemoveFromSelection() => throw new InvalidOperationException();

        public void ScrollIntoView(bool alignToTop) { } // la ligne défile d'elle-même jusqu'au curseur

        public nint GetChildren() => SafeArray.Unknowns();

        private TextRange Target(nint range) =>
            Unwrap<TextRange>(range) is { } target && target.SameSurface(surface)
                ? target
                : throw new ArgumentException(null, nameof(range));

        private int Endpoint(TextPatternRangeEndpoint endpoint)
        {
            _ = Text;
            return endpoint == TextPatternRangeEndpoint.Start ? _start : _end;
        }

        private void SetEndpoint(TextPatternRangeEndpoint endpoint, int position)
        {
            if (endpoint == TextPatternRangeEndpoint.Start)
            {
                _start = position;
                _end = Math.Max(_end, _start);
            }
            else
            {
                _end = position;
                _start = Math.Min(_start, _end);
            }
        }

        // Unités : caractère, mot (un mot commence après une espace), et la ligne entière pour
        // Line, Paragraph, Page et Document. Format n'a pas de mise en forme propre : il vaut
        // l'unité supérieure, le mot.
        internal static bool IsBoundary(string text, TextUnit unit, int index)
        {
            if (index <= 0 || index >= text.Length)
                return true;
            return unit switch
            {
                TextUnit.Character => !char.IsLowSurrogate(text[index]),
                TextUnit.Format or TextUnit.Word => char.IsWhiteSpace(text[index - 1]) && !char.IsWhiteSpace(text[index]),
                _ => false,
            };
        }

        internal static int Next(string text, TextUnit unit, int index)
        {
            do index++; while (index < text.Length && !IsBoundary(text, unit, index));
            return Math.Min(index, text.Length);
        }

        internal static int Previous(string text, TextUnit unit, int index)
        {
            do index--; while (index > 0 && !IsBoundary(text, unit, index));
            return Math.Max(index, 0);
        }

        internal static int Floor(string text, TextUnit unit, int index)
        {
            while (index > 0 && !IsBoundary(text, unit, index))
                index--;
            return index;
        }
    }

    /// <summary>SAFEARRAY à une dimension, créés pour l'appelant, qui les détruit.</summary>
    internal static class SafeArray
    {
        private const ushort VT_I4 = 3;
        private const ushort VT_R8 = 5;
        private const ushort VT_UNKNOWN = 13;

        public static nint Ints(params int[] values)
        {
            nint array = Create(VT_I4, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                int value = values[i];
                Check(SafeArrayPutElement(array, ref i, ref value), array);
            }
            return array;
        }

        public static nint Doubles(double[] values)
        {
            nint array = Create(VT_R8, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                double value = values[i];
                Check(SafeArrayPutElement(array, ref i, ref value), array);
            }
            return array;
        }

        /// <summary>Tableau d'IUnknown : SafeArrayPutElement compte sa propre référence.</summary>
        public static nint Unknowns(params object[] providers)
        {
            nint array = Create(VT_UNKNOWN, providers.Length);
            for (int i = 0; i < providers.Length; i++)
            {
                nint unknown = Pointer(providers[i], IidUnknown);
                try { Check(SafeArrayPutElement(array, ref i, unknown), array); }
                finally { Marshal.Release(unknown); }
            }
            return array;
        }

        private static nint Create(ushort vt, int count)
        {
            nint array = SafeArrayCreateVector(vt, 0, (uint)count);
            return array != 0 ? array : throw new OutOfMemoryException();
        }

        private static void Check(int hr, nint array)
        {
            if (hr == 0) return;
            SafeArrayDestroy(array);
            Marshal.ThrowExceptionForHR(hr);
        }

        [DllImport("oleaut32.dll")]
        private static extern nint SafeArrayCreateVector(ushort vt, int lowerBound, uint count);

        [DllImport("oleaut32.dll")]
        private static extern int SafeArrayPutElement(nint array, ref int index, ref int value);

        [DllImport("oleaut32.dll")]
        private static extern int SafeArrayPutElement(nint array, ref int index, ref double value);

        [DllImport("oleaut32.dll")]
        private static extern int SafeArrayPutElement(nint array, ref int index, nint unknown);

        [DllImport("oleaut32.dll")]
        private static extern int SafeArrayDestroy(nint array);
    }
}
