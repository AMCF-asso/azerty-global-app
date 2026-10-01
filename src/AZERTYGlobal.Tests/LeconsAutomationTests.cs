using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AZERTYGlobal;
using Xunit;
using Range = AZERTYGlobal.LessonsAutomation.TextRange;

namespace AZERTYGlobal.Tests;

/// <summary>
/// UI Automation de la ligne des Leçons (étape 3 du lot) : la logique des plages de texte,
/// puis, de bout en bout, un vrai client UIA sur un fil MTA qui lit la fenêtre créée sur un fil
/// STA, comme dans l'application.
/// </summary>
public class LeconsAutomationTests
{
    private const string Line = "Ma sœur a bon cœur.";

    /// <summary>Hôte de test : une ligne fixe, des cases de 10 px à partir de x = 100.</summary>
    private sealed class FakeHost : ILessonSurfaceHost
    {
        public IntPtr Handle { get; set; } = GetDesktopWindow(); // ClientToScreen y est l'identité
        public LessonSurfaceView? SurfaceView { get; set; }
        public bool WindowHasFocus { get; set; } = true;
        public bool SurfaceHasFocus { get; set; } = true;
        public int FocusRequests;
        public void FocusSurface() => FocusRequests++;
    }

    private static LessonSurfaceView View(string text, int caret, int boxLeft = 90, int boxRight = 400)
    {
        var cells = new Win32.RECT[text.Length];
        for (int i = 0; i < text.Length; i++)
            cells[i] = new Win32.RECT { left = 100 + 10 * i, top = 20, right = 110 + 10 * i, bottom = 56 };
        var caretRect = new Win32.RECT { left = 100 + 10 * caret, top = 20, right = 102 + 10 * caret, bottom = 56 };
        return new LessonSurfaceView("Texte à taper", text, caret, new Win32.RECT { left = boxLeft, top = 14, right = boxRight, bottom = 78 },
            cells, caretRect);
    }

    private static (LessonsAutomation Automation, FakeHost Host) Create(string text = Line, int caret = 3)
    {
        var host = new FakeHost { SurfaceView = View(text, caret) };
        return (new LessonsAutomation(host), host);
    }

    private static Range RangeOf(nint pointer)
    {
        try { return LessonsAutomation.Unwrap<Range>(pointer) ?? throw new InvalidOperationException("plage étrangère"); }
        finally { Marshal.Release(pointer); }
    }

    [Fact]
    public void LaPlageDuDocumentDonneLaLigneEntiere()
    {
        var (automation, _) = Create();
        var document = RangeOf(automation.Surface.GetDocumentRange());
        Assert.Equal(Line, document.GetText(-1));
        Assert.Equal("Ma ", document.GetText(3));
    }

    [Fact]
    public void LeCurseurEstUnePlageVideALaLettreATaper()
    {
        var (automation, host) = Create(caret: 3);
        var caret = RangeOf(automation.Surface.GetCaretRange(out bool active));
        Assert.True(active);
        Assert.Equal((3, 3), (caret.Start, caret.End));

        host.SurfaceHasFocus = false;
        RangeOf(automation.Surface.GetCaretRange(out active));
        Assert.False(active);
    }

    [Fact]
    public void LaSelectionEstLeCurseur()
    {
        var (automation, _) = Create(caret: 5);
        nint array = automation.Surface.GetSelection();
        var ranges = SafeArrays.Unknowns(array);
        Assert.Single(ranges);
        var range = RangeOf(ranges[0]);
        Assert.Equal((5, 5), (range.Start, range.End));
    }

    [Theory]
    [InlineData(0 /* Character */, 5, 6, "u")]
    [InlineData(2 /* Word */, 3, 8, "sœur ")]
    [InlineData(1 /* Format */, 3, 8, "sœur ")]
    [InlineData(3 /* Line */, 0, 19, Line)]
    [InlineData(6 /* Document */, 0, 19, Line)]
    public void ExpandToEnclosingUnitEnglobeLUnite(int unit, int start, int end, string text)
    {
        var (automation, _) = Create(caret: 5); // au milieu de « sœur »
        var range = RangeOf(automation.Surface.GetCaretRange(out _));
        range.ExpandToEnclosingUnit((TextUnit)unit);
        Assert.Equal((start, end), (range.Start, range.End));
        Assert.Equal(text, range.GetText(-1));
    }

    [Fact]
    public void EnFinDeLigneLaPlageResteVide()
    {
        var (automation, _) = Create(caret: Line.Length);
        var range = RangeOf(automation.Surface.GetCaretRange(out _));
        range.ExpandToEnclosingUnit(TextUnit.Character);
        Assert.Equal((Line.Length, Line.Length), (range.Start, range.End));
    }

    [Fact]
    public void MoveParMotPasseDeMotEnMot()
    {
        var (automation, _) = Create(caret: 0);
        var range = RangeOf(automation.Surface.GetCaretRange(out _));
        range.ExpandToEnclosingUnit(TextUnit.Word);
        Assert.Equal("Ma ", range.GetText(-1));

        Assert.Equal(2, range.Move(TextUnit.Word, 2));
        Assert.Equal("a ", range.GetText(-1));
        Assert.Equal(2, range.Move(TextUnit.Word, 5)); // « bon », « cœur. » : la dernière unité
        Assert.Equal("cœur.", range.GetText(-1));
        Assert.Equal(-4, range.Move(TextUnit.Word, -9));
        Assert.Equal("Ma ", range.GetText(-1));
    }

    [Fact]
    public void UnePlageVideAvanceJusquALaFin()
    {
        var (automation, _) = Create(caret: 17);
        var range = RangeOf(automation.Surface.GetCaretRange(out _));
        Assert.Equal(2, range.Move(TextUnit.Character, 5));
        Assert.Equal((19, 19), (range.Start, range.End));
        Assert.Equal(0, range.Move(TextUnit.Character, 1));
    }

    [Fact]
    public void MoveEndpointByUnitEtendLaPlage()
    {
        var (automation, _) = Create(caret: 3);
        var range = RangeOf(automation.Surface.GetCaretRange(out _));
        Assert.Equal(4, range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 4));
        Assert.Equal("sœur", range.GetText(-1));
        // Le début dépasse la fin : la fin le suit.
        Assert.Equal(1, range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Word, 1));
        Assert.Equal((8, 8), (range.Start, range.End));
    }

    [Fact]
    public void ComparaisonsEtClonage()
    {
        var (automation, _) = Create(caret: 3);
        nint document = automation.Surface.GetDocumentRange();
        nint caret = automation.Surface.GetCaretRange(out _);
        try
        {
            var doc = LessonsAutomation.Unwrap<Range>(document)!;
            Assert.True(doc.Compare(document));
            Assert.False(doc.Compare(caret));
            Assert.True(doc.CompareEndpoints(TextPatternRangeEndpoint.End, caret, TextPatternRangeEndpoint.Start) > 0);
            Assert.Equal(0, doc.CompareEndpoints(TextPatternRangeEndpoint.Start, document, TextPatternRangeEndpoint.Start));

            var clone = RangeOf(doc.Clone());
            Assert.Equal((0, Line.Length), (clone.Start, clone.End));

            doc.MoveEndpointByRange(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.Start);
            Assert.Equal((3, Line.Length), (doc.Start, doc.End));
        }
        finally
        {
            Marshal.Release(document);
            Marshal.Release(caret);
        }
    }

    [Fact]
    public void FindTextTrouveDansLaPlage()
    {
        var (automation, _) = Create();
        var document = RangeOf(automation.Surface.GetDocumentRange());
        var found = RangeOf(document.FindText("CŒUR", backward: false, ignoreCase: true));
        Assert.Equal((14, 18), (found.Start, found.End));
        Assert.Equal(0, document.FindText("CŒUR", backward: false, ignoreCase: false));
    }

    [Fact]
    public void LesRectanglesSuiventLesCasesEtLeCurseur()
    {
        var (automation, _) = Create(caret: 3);
        var word = RangeOf(automation.Surface.GetDocumentRange());
        word.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, 3);
        word.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -(Line.Length - 7));
        Assert.Equal(new double[] { 130, 20, 40, 36 }, SafeArrays.Doubles(word.GetBoundingRectangles()));

        var caret = RangeOf(automation.Surface.GetCaretRange(out _));
        Assert.Equal(new double[] { 130, 20, 2, 36 }, SafeArrays.Doubles(caret.GetBoundingRectangles()));
    }

    [Fact]
    public void LesPlagesVisiblesSArretentAuBordDeLaSurface()
    {
        var host = new FakeHost { SurfaceView = View(Line, 3, boxLeft: 125, boxRight: 205) }; // cases 3 à 10
        var automation = new LessonsAutomation(host);
        var visible = SafeArrays.Unknowns(automation.Surface.GetVisibleRanges());
        var range = RangeOf(Assert.Single(visible));
        Assert.Equal((2, 11), (range.Start, range.End)); // les cases 2 et 10 dépassent à moitié

        var document = RangeOf(automation.Surface.GetDocumentRange());
        Assert.Equal(new double[] { 125, 20, 80, 36 }, SafeArrays.Doubles(document.GetBoundingRectangles()));
    }

    [Fact]
    public void RangeFromPointDonneLeCaractereSousLePointeur()
    {
        var (automation, _) = Create();
        var range = RangeOf(automation.Surface.RangeFromPoint(new UiaPoint { X = 136, Y = 30 }));
        Assert.Equal((4, 4), (range.Start, range.End)); // après la moitié de la case 3
    }

    [Fact]
    public void SeuleLaFrappeDeplaceLeCurseur()
    {
        var (automation, _) = Create(caret: 3);
        RangeOf(automation.Surface.GetCaretRange(out _)).Select(); // au curseur : accepté
        var document = RangeOf(automation.Surface.GetDocumentRange());
        var error = Assert.Throws<InvalidOperationException>(document.Select);
        Assert.Equal(unchecked((int)0x80131509), error.HResult); // UIA_E_INVALIDOPERATION
    }

    [Fact]
    public void LaSurfaceSeDecritCommeUnChampDeSaisie()
    {
        var (automation, host) = Create();
        Assert.Equal(LessonsAutomation.EditControlTypeId, (int)automation.Surface.GetPropertyValue(LessonsAutomation.ControlTypePropertyId).Value);
        var name = automation.Surface.GetPropertyValue(LessonsAutomation.NamePropertyId);
        try { Assert.Equal("Texte à taper", Marshal.PtrToStringBSTR(name.Value)); }
        finally { Marshal.FreeBSTR(name.Value); }
        Assert.Equal(-1, (short)automation.Surface.GetPropertyValue(LessonsAutomation.HasKeyboardFocusPropertyId).Value);

        automation.Surface.SetFocus();
        Assert.Equal(1, host.FocusRequests);
    }

    [Fact]
    public void SansSurfaceLaRacineNaPasDEnfant()
    {
        var (automation, host) = Create();
        nint child = automation.Root.Navigate(NavigateDirection.FirstChild);
        Assert.NotEqual(0, child);
        Marshal.Release(child);

        host.SurfaceView = null; // récapitulatif, Paramètres
        Assert.Equal(0, automation.Root.Navigate(NavigateDirection.FirstChild));
        Assert.Equal(0, automation.Root.GetFocus());
    }

    // ── De bout en bout : un vrai client UI Automation ───────────────────────────────

    private static readonly Guid ClsidCUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    private static readonly Guid IidIUIAutomation = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
    private static readonly Guid IidTextPattern2 = new("506a921a-fcc9-409f-b23b-37eb74106872");

    /// <summary>Hôte qui relaie la fenêtre et note le fil de chaque appel du fournisseur.</summary>
    private sealed class RecordingHost(ILessonSurfaceHost inner) : ILessonSurfaceHost
    {
        public readonly HashSet<int> Threads = new();
        private T Note<T>(T value) { lock (Threads) Threads.Add(Environment.CurrentManagedThreadId); return value; }
        public IntPtr Handle => Note(inner.Handle);
        public LessonSurfaceView? SurfaceView => Note(inner.SurfaceView);
        public bool WindowHasFocus => Note(inner.WindowHasFocus);
        public bool SurfaceHasFocus => Note(inner.SurfaceHasFocus);
        public void FocusSurface() { Note(0); inner.FocusSurface(); }
    }

    private sealed record ClientReading(int ControlType, string Name, string Text, double[] CaretBounds, string? Error);

    /// <summary>
    /// Fenêtre des Leçons sur un fil STA, comme Program.Main ([STAThread]), avec l'exercice
    /// d'initiation, « Lætitia » déjà tapé, le focus et un repeint. <paramref name="body"/> y
    /// tourne ; le fil pompe ses messages tant qu'il attend, car les appels COM arrivent par là.
    /// </summary>
    private static void OnUiThread(Action<LessonsWindow, IntPtr> body)
    {
        Exception? failure = null;
        var ui = new Thread(() =>
        {
            try
            {
                using var isolation = new BancCapture.Isolation();
                var layout = LayoutLoader.LoadFromResource();
                var mapper = new KeyMapper(layout, new MockWin32Api());
                var window = new LessonsWindow(layout, mapper, new KeyboardHook(mapper));
                try
                {
                    BancCapture.Call(window, "SelectExercise", LessonCatalogLoader.InitiationModuleId,
                        LessonCatalogLoader.InitiationLessonId, 3, false);
                    foreach (char c in "Lætitia")
                        BancCapture.Call(window, "OnChar", c);
                    BancCapture.SetField(window, "_hasFocus", true);
                    IntPtr hwnd = BancCapture.Handle(window);
                    PaintNow(hwnd);
                    body(window, hwnd);
                }
                finally
                {
                    BancCapture.Teardown(window);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        ui.Join();
        Assert.Null(failure);
    }

    private static void PaintNow(IntPtr hwnd)
    {
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Pompe la file du fil d'interface jusqu'à <paramref name="done"/>, 15 s au plus.</summary>
    private static void PumpUntil(Func<bool> done, string what)
    {
        long deadline = Environment.TickCount64 + 15000;
        while (!done() && Environment.TickCount64 < deadline)
            BancCapture.Pump(1);
        if (!done())
            throw new TimeoutException($"{what} : rien en 15 s");
    }

    private static Thread StartClient(Action client)
    {
        var thread = new Thread(() => client());
        thread.SetApartmentState(ApartmentState.MTA); // comme un client UIA hors du fil d'interface
        thread.Start();
        return thread;
    }

    [Fact]
    public void UnClientUiaLitLaLigneEtLeCurseur_SurLeFilDInterface()
    {
        ClientReading? reading = null;
        LessonSurfaceView? view = null;
        Win32.POINT origin = default;
        HashSet<int>? threads = null;
        int uiThread = 0;

        OnUiThread((window, hwnd) =>
        {
            uiThread = Environment.CurrentManagedThreadId;
            view = BancCapture.Field<LessonSurfaceView?>(window, "_surfaceView");
            Win32.ClientToScreen(hwnd, ref origin);
            var recording = new RecordingHost(window);
            threads = recording.Threads;
            BancCapture.SetField(window, "_automation", new LessonsAutomation(recording));

            var client = StartClient(() => reading = ReadWithUiaClient(hwnd));
            PumpUntil(() => !client.IsAlive, "lecture du client UIA");
        });

        Assert.NotNull(reading);
        Assert.True(reading!.Error == null, reading.Error);
        Assert.Equal(LessonsAutomation.EditControlTypeId, reading.ControlType);
        Assert.Equal(view!.Name, reading.Name);
        Assert.Equal(view.Text, reading.Text);
        var caret = view.CaretRect!.Value;
        Assert.Equal(new double[] { origin.x + caret.left, origin.y + caret.top, caret.right - caret.left, caret.bottom - caret.top },
            reading.CaretBounds);
        Assert.Equal(new[] { uiThread }, threads!); // UseComThreading : le fil STA d'interface seul
    }

    /// <summary>
    /// La loupe et Narrateur suivent la frappe par les événements : une lettre tapée doit
    /// lever TextSelectionChanged sur la surface, chez un client abonné.
    /// </summary>
    [Fact]
    public void UneFrappeLeveTextSelectionChangedChezLeClient()
    {
        var events = new List<int>();
        string? error = null;
        using var registered = new ManualResetEventSlim();

        OnUiThread((window, hwnd) =>
        {
            BancCapture.SetField(window, "_automation", new LessonsAutomation(window));
            var client = StartClient(() => error = ListenForCaretMoves(hwnd, registered, events));
            PumpUntil(() => registered.IsSet || !client.IsAlive, "abonnement du client UIA");

            BancCapture.Call(window, "OnChar", ' '); // la lettre attendue après « Lætitia »
            PaintNow(hwnd);
            PumpUntil(() => !client.IsAlive, "événement du client UIA");
        });

        Assert.True(error == null, error);
        lock (events)
            Assert.Contains(LessonsAutomation.TextSelectionChangedEventId, events);
    }

    /// <summary>
    /// Client UIA minimal par la table virtuelle (UIAutomationClient.h), comme AccessibleName :
    /// IUIAutomation, IUIAutomationTreeWalker, IUIAutomationElement, IUIAutomationTextPattern2,
    /// IUIAutomationTextRange.
    /// </summary>
    private static ClientReading ReadWithUiaClient(IntPtr hwnd)
    {
        var releases = new List<IntPtr>();
        try
        {
            IntPtr uia = CreateClient(releases);
            IntPtr surface = FindSurface(uia, hwnd, releases);
            Check(Slot<GetInt>(surface, 21)(surface, out int controlType)); // get_CurrentControlType
            Check(Slot<GetPtr>(surface, 23)(surface, out IntPtr nameBstr)); // get_CurrentName
            string name = TakeBstr(nameBstr);

            Guid iidPattern = IidTextPattern2;
            Check(Slot<GetPatternAs>(surface, 14)(surface, LessonsAutomation.TextPattern2Id, ref iidPattern, out IntPtr pattern));
            if (Keep(releases, pattern) == IntPtr.Zero)
                return new ClientReading(controlType, name, "", Array.Empty<double>(), "TextPattern2 absent");

            Check(Slot<GetPtr>(pattern, 7)(pattern, out IntPtr document)); // get_DocumentRange
            Keep(releases, document);
            Check(Slot<GetText>(document, 12)(document, -1, out IntPtr textBstr)); // GetText
            string text = TakeBstr(textBstr);

            Check(Slot<GetCaret>(pattern, 10)(pattern, out _, out IntPtr caret)); // GetCaretRange
            Keep(releases, caret);
            Check(Slot<GetPtr>(caret, 10)(caret, out IntPtr rects)); // GetBoundingRectangles
            return new ClientReading(controlType, name, text, SafeArrays.Doubles(rects), null);
        }
        catch (Exception ex)
        {
            return new ClientReading(0, "", "", Array.Empty<double>(), ex.ToString());
        }
        finally
        {
            ReleaseAll(releases);
        }
    }

    /// <summary>Abonne un gestionnaire à TextSelectionChanged sur la surface, attend un
    /// événement 5 s au plus, puis se désabonne. Rend l'erreur, ou null.</summary>
    private static string? ListenForCaretMoves(IntPtr hwnd, ManualResetEventSlim registered, List<int> events)
    {
        var releases = new List<IntPtr>();
        IntPtr uia = IntPtr.Zero;
        try
        {
            uia = CreateClient(releases);
            IntPtr surface = FindSurface(uia, hwnd, releases);
            var sink = new EventSink(events); // jamais libéré : UIA peut rappeler jusqu'au désabonnement
            Check(Slot<AddHandler>(uia, 32)(uia, LessonsAutomation.TextSelectionChangedEventId, surface,
                1 /* TreeScope_Element */, IntPtr.Zero, sink.Pointer)); // AddAutomationEventHandler
            registered.Set();
            long deadline = Environment.TickCount64 + 5000;
            while (Environment.TickCount64 < deadline)
            {
                lock (events)
                    if (events.Count > 0) break;
                Thread.Sleep(20);
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
        finally
        {
            registered.Set();
            if (uia != IntPtr.Zero)
                Slot<NoArgs>(uia, 41)(uia); // RemoveAllEventHandlers
            ReleaseAll(releases);
        }
    }

    private static IntPtr CreateClient(List<IntPtr> releases)
    {
        CoInitializeEx(IntPtr.Zero, 0 /* COINIT_MULTITHREADED */);
        // Comme la loupe et Narrateur : sinon UIA ramène les coordonnées au DPI logique.
        SetThreadDpiAwarenessContext(new IntPtr(-4) /* PER_MONITOR_AWARE_V2 */);
        Guid clsid = ClsidCUIAutomation, iid = IidIUIAutomation;
        Check(CoCreateInstance(ref clsid, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, ref iid, out IntPtr uia));
        return Keep(releases, uia);
    }

    /// <summary>L'Edit parmi les enfants de la fenêtre, en vue brute : la barre de titre (proxy
    /// du HWND) vient d'abord.</summary>
    private static IntPtr FindSurface(IntPtr uia, IntPtr hwnd, List<IntPtr> releases)
    {
        Check(Slot<GetPtrArg>(uia, 6)(uia, hwnd, out IntPtr window)); // ElementFromHandle
        Keep(releases, window);
        Check(Slot<GetPtr>(uia, 16)(uia, out IntPtr walker)); // get_RawViewWalker
        Keep(releases, walker);
        Check(Slot<GetPtrArg>(walker, 4)(walker, window, out IntPtr child)); // GetFirstChildElement
        var seen = new List<int>();
        while (Keep(releases, child) != IntPtr.Zero)
        {
            Check(Slot<GetInt>(child, 21)(child, out int controlType)); // get_CurrentControlType
            if (controlType == LessonsAutomation.EditControlTypeId)
                return child;
            seen.Add(controlType);
            Check(Slot<GetPtrArg>(walker, 6)(walker, child, out child)); // GetNextSiblingElement
        }
        throw new InvalidOperationException("pas d'Edit parmi les enfants : " + string.Join(", ", seen));
    }

    private static IntPtr Keep(List<IntPtr> releases, IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
            releases.Add(pointer);
        return pointer;
    }

    private static void ReleaseAll(List<IntPtr> releases)
    {
        for (int i = releases.Count - 1; i >= 0; i--)
            Marshal.Release(releases[i]);
    }

    /// <summary>
    /// IUIAutomationEventHandler fait main : un objet COM réduit à sa table virtuelle
    /// (QueryInterface, AddRef, Release, HandleAutomationEvent), sans bloc unsafe.
    /// </summary>
    private sealed class EventSink
    {
        private delegate int QueryInterfaceFn(IntPtr self, ref Guid iid, out IntPtr result);
        private delegate uint RefCountFn(IntPtr self);
        private delegate int HandleEventFn(IntPtr self, IntPtr sender, int eventId);

        private static readonly Guid IidUnknown = new("00000000-0000-0000-C000-000000000046");
        private static readonly Guid IidHandler = new("146c3c17-f12e-4e22-8c27-f894b9b79c69");
        private static readonly List<Delegate> Alive = new(); // les trampolines vivent avec le processus

        public IntPtr Pointer { get; }

        public EventSink(List<int> events)
        {
            QueryInterfaceFn query = (IntPtr self, ref Guid iid, out IntPtr result) =>
            {
                bool known = iid == IidUnknown || iid == IidHandler;
                result = known ? self : IntPtr.Zero;
                return known ? 0 : unchecked((int)0x80004002); // E_NOINTERFACE
            };
            RefCountFn addRef = _ => 2;
            RefCountFn release = _ => 1;
            HandleEventFn handle = (_, _, eventId) =>
            {
                lock (events) events.Add(eventId);
                return 0;
            };
            var methods = new Delegate[] { query, addRef, release, handle };
            lock (Alive) Alive.AddRange(methods);
            IntPtr vtable = Marshal.AllocHGlobal(IntPtr.Size * methods.Length);
            for (int i = 0; i < methods.Length; i++)
                Marshal.WriteIntPtr(vtable, i * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(methods[i]));
            Pointer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Pointer, vtable);
        }
    }

    private static T Slot<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    private static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);

    private static string TakeBstr(IntPtr bstr)
    {
        if (bstr == IntPtr.Zero)
            return "<null>";
        try { return Marshal.PtrToStringBSTR(bstr); }
        finally { Marshal.FreeBSTR(bstr); }
    }

    private delegate int GetPtr(IntPtr self, out IntPtr result);
    private delegate int GetPtrArg(IntPtr self, IntPtr argument, out IntPtr result);
    private delegate int GetInt(IntPtr self, out int result);
    private delegate int GetPatternAs(IntPtr self, int patternId, ref Guid riid, out IntPtr result);
    private delegate int GetText(IntPtr self, int maxLength, out IntPtr result);
    private delegate int GetCaret(IntPtr self, out int isActive, out IntPtr range);
    private delegate int AddHandler(IntPtr self, int eventId, IntPtr element, int scope, IntPtr cacheRequest, IntPtr handler);
    private delegate int NoArgs(IntPtr self);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr instance);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    /// <summary>Lecture des SAFEARRAY rendus par le fournisseur, détruits après lecture.</summary>
    private static class SafeArrays
    {
        public static double[] Doubles(nint array)
        {
            try
            {
                var values = new double[Length(array)];
                Marshal.ThrowExceptionForHR(SafeArrayAccessData(array, out IntPtr data));
                try { Marshal.Copy(data, values, 0, values.Length); }
                finally { SafeArrayUnaccessData(array); }
                return values;
            }
            finally
            {
                SafeArrayDestroy(array);
            }
        }

        /// <summary>Les IUnknown du tableau, chacun avec une référence pour l'appelant.</summary>
        public static nint[] Unknowns(nint array)
        {
            try
            {
                var values = new IntPtr[Length(array)];
                Marshal.ThrowExceptionForHR(SafeArrayAccessData(array, out IntPtr data));
                try { Marshal.Copy(data, values, 0, values.Length); }
                finally { SafeArrayUnaccessData(array); }
                foreach (var value in values)
                    Marshal.AddRef(value);
                return Array.ConvertAll(values, v => (nint)v);
            }
            finally
            {
                SafeArrayDestroy(array); // libère les références du tableau
            }
        }

        private static int Length(nint array)
        {
            Marshal.ThrowExceptionForHR(SafeArrayGetLBound(array, 1, out int lower));
            Marshal.ThrowExceptionForHR(SafeArrayGetUBound(array, 1, out int upper));
            return upper - lower + 1;
        }

        [DllImport("oleaut32.dll")] private static extern int SafeArrayGetLBound(nint array, uint dimension, out int bound);
        [DllImport("oleaut32.dll")] private static extern int SafeArrayGetUBound(nint array, uint dimension, out int bound);
        [DllImport("oleaut32.dll")] private static extern int SafeArrayAccessData(nint array, out IntPtr data);
        [DllImport("oleaut32.dll")] private static extern int SafeArrayUnaccessData(nint array);
        [DllImport("oleaut32.dll")] private static extern int SafeArrayDestroy(nint array);
    }
}
