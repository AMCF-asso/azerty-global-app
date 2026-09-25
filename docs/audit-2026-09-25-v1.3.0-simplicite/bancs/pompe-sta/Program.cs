using System.Diagnostics;
using System.Runtime.InteropServices;

// Reproduit le motif de SecureInputProbe.Query : le fil principal [STAThread] attend
// _done.WaitOne(timeout) pendant qu'un autre fil lui envoie un message.
// Question : le message est-il traité PENDANT l'attente (attente pompante) ou après ?
static unsafe class P
{
    const uint WM_APP = 0x8000;
    static readonly Stopwatch Sw = Stopwatch.StartNew();
    static double sentHandledAt = -1, postedHandledAt = -1;
    static int depthDuringWait = 0;
    static volatile bool inWait;
    static bool sentDuringWait, postedDuringWait;

    [UnmanagedCallersOnly]
    static nint WndProc(nint h, uint msg, nint w, nint l)
    {
        if (msg == WM_APP + 1) { sentHandledAt = Sw.Elapsed.TotalMilliseconds; sentDuringWait = inWait; return 1; }
        if (msg == WM_APP + 2) { postedHandledAt = Sw.Elapsed.TotalMilliseconds; postedDuringWait = inWait; return 1; }
        return DefWindowProcW(h, msg, w, l);
    }

    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine($"Runtime {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, AOT={!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
        Console.WriteLine($"Apartment du fil principal : {Thread.CurrentThread.GetApartmentState()}");
        var cls = new WNDCLASSW { lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WndProc, hInstance = GetModuleHandleW(null), lpszClassName = "ContreAvisPompe" };
        if (RegisterClassW(ref cls) == 0) { Console.WriteLine("RegisterClass échoue"); return; }
        nint hwnd = CreateWindowExW(0, "ContreAvisPompe", "", 0, 0, 0, 0, 0, new nint(-3) /*HWND_MESSAGE*/, 0, cls.hInstance, 0);
        if (hwnd == 0) { Console.WriteLine("CreateWindow échoue"); return; }

        var never = new AutoResetEvent(false);
        double sendReturnedAt = -1, sendStartedAt = -1;
        var sender = new Thread(() =>
        {
            Thread.Sleep(50);
            PostMessageW(hwnd, WM_APP + 2, 0, 0);
            sendStartedAt = Sw.Elapsed.TotalMilliseconds;
            SendMessageW(hwnd, WM_APP + 1, 0, 0);
            sendReturnedAt = Sw.Elapsed.TotalMilliseconds;
        }) { IsBackground = true };
        sender.SetApartmentState(ApartmentState.MTA);

        double t0 = Sw.Elapsed.TotalMilliseconds;
        sender.Start();
        inWait = true;
        bool signaled = never.WaitOne(300);
        inWait = false;
        double t1 = Sw.Elapsed.TotalMilliseconds;

        // Pomper ensuite pour libérer un SendMessage resté en attente.
        var deadline = Sw.Elapsed.TotalMilliseconds + 200;
        while (Sw.Elapsed.TotalMilliseconds < deadline)
        {
            while (PeekMessageW(out MSG m, 0, 0, 0, 1)) { TranslateMessage(ref m); DispatchMessageW(ref m); }
            Thread.Sleep(1);
        }
        sender.Join(500);
        Console.WriteLine($"WaitOne(300) : début {t0:F1} ms, fin {t1:F1} ms (durée {t1 - t0:F1} ms, signalé={signaled})");
        Console.WriteLine($"SendMessage : envoyé à {sendStartedAt:F1} ms, traité à {sentHandledAt:F1} ms, rendu à {sendReturnedAt:F1} ms ; traité PENDANT l'attente = {sentDuringWait}");
        Console.WriteLine($"PostMessage : traité à {postedHandledAt:F1} ms ; traité PENDANT l'attente = {postedDuringWait}");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSW { public uint style; public nint lpfnWndProc; public int cbClsExtra, cbWndExtra; public nint hInstance, hIcon, hCursor, hbrBackground; public string? lpszMenuName; public string lpszClassName; }
    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern ushort RegisterClassW(ref WNDCLASSW c);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern nint CreateWindowExW(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32")] static extern nint DefWindowProcW(nint h, uint m, nint w, nint l);
    [DllImport("user32")] static extern nint SendMessageW(nint h, uint m, nint w, nint l);
    [DllImport("user32")] static extern bool PostMessageW(nint h, uint m, nint w, nint l);
    [DllImport("user32")] static extern bool PeekMessageW(out MSG m, nint h, uint min, uint max, uint remove);
    [DllImport("user32")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32")] static extern nint DispatchMessageW(ref MSG m);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern nint GetModuleHandleW(string? name);
}
