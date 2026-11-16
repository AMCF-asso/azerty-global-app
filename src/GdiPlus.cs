namespace AZERTYGlobal;

/// <summary>
/// GDI+ démarré une fois pour tout le processus et arrêté à la sortie.
///
/// Avant le 16/11/2026, sept endroits le démarraient puis l'arrêtaient autour de chaque
/// dessin, dont l'icône du tray refaite à chaque bascule, Verr. Maj. ou pause. Sur le runner
/// Windows de la CI, chaque couple GdiplusStartup / GdiplusShutdown laissait un objet USER,
/// même en vidant la file de messages (+10 sur 10 cycles, run 43952848446) ; avec un GDI+
/// gardé ouvert, 0. Voir docs/audit-ressources.md, constat C3.
/// </summary>
internal static class GdiPlus
{
    private static readonly object Gate = new();
    private static IntPtr _token;

    /// <summary>Nombre de GdiplusStartup réussis depuis le lancement : 1 au plus tant que
    /// <see cref="Shutdown"/> n'est pas appelé (relu par les tests).</summary>
    internal static int Starts { get; private set; }

    /// <summary>Démarre GDI+ au premier appel et le garde ouvert ; rend vrai si GDI+ est prêt.
    /// Sans effet ni coût après le premier succès ; un échec se retente à l'appel suivant.</summary>
    public static bool EnsureStarted()
    {
        lock (Gate)
        {
            if (_token != IntPtr.Zero) return true;
            var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
            if (Win32.GdiplusStartup(out _token, ref input, IntPtr.Zero) != 0)
            {
                _token = IntPtr.Zero;
                return false;
            }
            Starts++;
            return true;
        }
    }

    /// <summary>Arrête GDI+ à la sortie du processus, après la fermeture des fenêtres, qui
    /// libèrent leurs images GDI+ dans leur Dispose. Un appel suivant à
    /// <see cref="EnsureStarted"/> le redémarrerait.</summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            if (_token == IntPtr.Zero) return;
            Win32.GdiplusShutdown(_token);
            _token = IntPtr.Zero;
        }
    }
}
