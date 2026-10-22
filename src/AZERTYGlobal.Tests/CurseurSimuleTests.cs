using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using TypingEngine.Windows.Testing;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le banc de capture ne touche plus à la vraie souris (choix d'Antoine du 15/10 : « simuler le
/// curseur »). Avant, il la garait en (0, 0) avant chaque cliché, la remettait à la fin, et la
/// déplaçait donc sous les doigts d'Antoine à chaque rendu. Ces témoins ne sont pas derrière la
/// porte AZERTY_CAPTURE : ils font un vrai cliché d'une vraie fenêtre.
///
/// Deux preuves. La première, déterministe, vaut partout : aucune fonction du produit ni du banc
/// ne peut déplacer le curseur (<see cref="AucuneFonctionNeDeplaceLeCurseur"/>). La seconde
/// observe la vraie souris pendant un rendu (<see cref="SourisImmobilePendant"/>) ; elle a besoin
/// d'un bureau d'entrée et d'une souris laissée en paix, sinon elle est sautée, jamais réussie.
///
/// Toutes les lectures de la vraie souris se font sous le contexte DPI de l'isolation (« par
/// écran ») : testhost.exe ne déclare rien, et lu sans, GetCursorPos rend des coordonnées mises
/// à l'échelle. À 125 %, la même souris immobile se lit (1442, 745) sans contexte et
/// (1803, 931) par écran (passe du 21/10, 20 h 24).
/// </summary>
public class CurseurSimuleTests
{
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    /// <summary>Mesures troublées par une entrée de l'utilisateur avant de renoncer.</summary>
    private const int Essais = 5;

    /// <summary>Calme demandé avant chaque mesure : aucune entrée de la session depuis ce délai.</summary>
    private const int CalmeMs = 1000;

    /// <summary>Attente du calme, bornée, pour l'ensemble des essais d'un test.</summary>
    private static readonly TimeSpan AttenteMax = TimeSpan.FromSeconds(20);

    /// <summary>Points d'entrée Win32 qui déplacent ou contraignent le curseur.</summary>
    private static readonly string[] DeplaceursDeCurseur =
        { "SetCursorPos", "SetPhysicalCursorPos", "mouse_event", "ClipCursor" };

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>La vraie souris, lue sous le contexte DPI de l'isolation, qu'elle soit ouverte ou non.</summary>
    private static Win32.POINT RealCursor()
    {
        IntPtr previous = BancCapture.Native.SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            if (!Win32.GetCursorPos(out var point))
                throw new MesureImpossibleException("pas de vraie souris : GetCursorPos échoue, session sans bureau d'entrée");
            return point;
        }
        finally { BancCapture.Native.SetThreadDpiAwarenessContext(previous); }
    }

    /// <summary>Instant (horloge de GetTickCount) de la dernière entrée souris ou clavier de la session.</summary>
    private static uint LastInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        Assert.True(GetLastInputInfo(ref info));
        return info.dwTime;
    }

    /// <summary>
    /// Joue <paramref name="scenario"/> et vérifie que la vraie souris est restée où elle était,
    /// à chaque lecture qu'il demande et à la fin. Chaque mesure commence après
    /// <see cref="CalmeMs"/> sans entrée de la session.
    ///
    /// SetCursorPos ne compte pas comme une entrée (sonde du 21/10, 20 h 37, après 3 s de calme) :
    /// un écart sans aucune entrée pendant la mesure est donc un déplacement par le code, et
    /// échoue aussitôt, comme une souris garée en (0, 0). Un écart avec une entrée vient de
    /// l'utilisateur : la mesure est refaite, <see cref="Essais"/> fois au plus. Sans mesure
    /// propre dans <see cref="AttenteMax"/>, le test est sauté.
    ///
    /// Limites : une souris déjà posée en (0, 0), ou un banc qui la déplacerait par SendInput
    /// (une entrée, comme celle de l'utilisateur), ne sont pas distingués ; aucun SendInput souris
    /// n'existe dans src (21/10).
    /// </summary>
    private static void SourisImmobilePendant(Action<Action> scenario)
    {
        var troubles = new List<string>();
        var attente = System.Diagnostics.Stopwatch.StartNew();
        for (int essai = 1; essai <= Essais; essai++)
        {
            while (unchecked((uint)Environment.TickCount - LastInput()) < CalmeMs)
            {
                if (attente.Elapsed > AttenteMax)
                    throw new MesureImpossibleException($"souris ou clavier utilisés sans pause de {CalmeMs} ms "
                        + $"pendant {AttenteMax.TotalSeconds} s : rien de mesuré. {string.Join(" ; ", troubles)}");
                System.Threading.Thread.Sleep(50);
            }

            uint inputBefore = LastInput();
            var before = RealCursor();
            var reads = new List<Win32.POINT>();
            scenario(() => reads.Add(RealCursor()));
            reads.Add(RealCursor());
            uint inputAfter = LastInput();

            if (reads.All(p => p.x == before.x && p.y == before.y))
                return;

            string detail = $"essai {essai} : avant ({before.x}, {before.y}), lu "
                + string.Join(", ", reads.Select(p => $"({p.x}, {p.y})"));
            // Le stationnement de l'ancien banc, que ces témoins attrapent : jamais une main d'Antoine.
            Assert.False(reads.Any(p => p.x == 0 && p.y == 0), $"la souris a été garée en (0, 0) : {detail}");
            Assert.True(inputAfter != inputBefore, $"la souris a bougé sans aucune entrée de la session, donc par le code : {detail}");
            troubles.Add($"{detail}, dernière entrée +{unchecked(inputAfter - inputBefore)} ms");
        }
        throw new MesureImpossibleException($"souris utilisée pendant chacun des {Essais} essais : rien de mesuré. "
            + string.Join(" ; ", troubles));
    }

    private static LessonsWindow OpenLessons()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var window = new LessonsWindow(layout, mapper, hook);
        if (!(bool)BancCapture.Call(window, "SelectExercise", LessonCatalogLoader.InitiationModuleId,
                LessonCatalogLoader.InitiationLessonId, 3, false)!)
            throw new InvalidOperationException("exercice d'initiation introuvable");
        return window;
    }

    /// <summary>Les Leçons, sous l'isolation et un faux curseur : la lecture directe et le placement
    /// de la fenêtre passent par la couche Win32, jamais par la vraie souris.</summary>
    private static void LesLeconsLisentLaCouche(Action? mesurer = null)
    {
        using var isolation = new BancCapture.Isolation();
        var spy = new MockWin32Api { CursorPosition = (-123, -456) };
        using (CursorSource.OverrideForTests(spy))
        {
            // La lecture directe passe par la couche : position scriptée, jamais la vraie.
            Assert.True(CursorSource.TryGet(out var point));
            Assert.Equal((-123, -456), (point.x, point.y));
            Assert.Equal(1, spy.CursorReads);

            // Et la fenêtre qui se place sur l'écran du curseur la lit aussi.
            var window = OpenLessons();
            try
            {
                BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
                Win32.ShowWindow(window.Handle, 1);
                Win32.SendMessageW(window.Handle, Win32.WM_SETCURSOR, window.Handle, (IntPtr)1 /* HTCLIENT */);
            }
            finally
            {
                BancCapture.Teardown(window);
            }
            Assert.True(spy.CursorReads > 1, $"les Leçons n'ont pas lu le curseur par la couche ({spy.CursorReads} lecture)");
        }
        mesurer?.Invoke();
    }

    [Fact]
    public void AucuneFonctionNeDeplaceLeCurseur()
    {
        var assemblies = new[]
        {
            typeof(CursorSource).Assembly,                              // produit
            typeof(TypingEngine.Windows.RealWin32Api).Assembly,         // couche Win32
            typeof(CurseurSimuleTests).Assembly,                        // banc de capture et faux Win32
        }.Distinct();

        var imports = assemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(m => m.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
            .Select(m => (Method: $"{m.DeclaringType!.FullName}.{m.Name}",
                EntryPoint: m.GetCustomAttribute<DllImportAttribute>()?.EntryPoint ?? m.Name))
            .ToList();

        // Témoin du balayage : il voit bien les imports, dont la lecture du curseur.
        Assert.Contains(imports, i => i.EntryPoint == "GetCursorPos");
        var deplaceurs = imports.Where(i => DeplaceursDeCurseur.Contains(i.EntryPoint)).Select(i => i.Method).ToList();
        Assert.True(deplaceurs.Count == 0, $"fonctions qui déplacent le curseur : {string.Join(", ", deplaceurs)}");
    }

    [FactSourisReelle]
    public void UnClicheLaisseLaVraieSourisOuElleEtait() => SourisImmobilePendant(mesurer =>
    {
        // La position de départ est lue avant l'isolation : son ouverture ne doit pas bouger la souris.
        using var isolation = new BancCapture.Isolation();
        var window = OpenLessons();
        string dir = Path.Combine(Path.GetTempPath(), "AZGCurseur_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 1);
            BancCapture.MarkActive(hwnd);

            BancCapture.Capture(hwnd, Path.Combine(dir, "curseur.png"));

            // Mesuré tant que la fenêtre existe et que l'isolation tient : l'ancien banc avait
            // garé la souris en (0, 0) à cet instant, et ne la rendait qu'au Dispose.
            mesurer();
        }
        finally
        {
            BancCapture.Teardown(window);
            Directory.Delete(dir, recursive: true);
        }
        mesurer();
    });

    [Fact]
    public void LesLeconsLisentLeCurseurParLaCoucheWin32() => LesLeconsLisentLaCouche();

    [FactSourisReelle]
    public void LesLeconsLaissentLaVraieSourisOuElleEtait() => SourisImmobilePendant(LesLeconsLisentLaCouche);

    [Fact]
    public void LIsolationRendUnCurseurSimuleEtLeRetire()
    {
        using (new BancCapture.Isolation())
        {
            Assert.True(CursorSource.TryGet(out var simulated));
            Assert.Equal((0, 0), (simulated.x, simulated.y));
        }

        // Isolation fermée : la couche rend de nouveau la vraie souris. Sans bureau d'entrée, la
        // vraie lecture échoue et la couche avec elle, là où le curseur simulé rendrait (0, 0).
        // Les deux lectures se font sous le même contexte DPI, celui du fil.
        for (int essai = 1; ; essai++)
        {
            bool realOk = Win32.GetCursorPos(out var truth);
            bool layerOk = CursorSource.TryGet(out var layer);
            bool againOk = Win32.GetCursorPos(out var truthAgain);
            Assert.Equal(realOk, layerOk);
            if (!realOk)
                return;

            // Les lectures se suivent : un écart entre les deux vraies est une souris en mouvement.
            if (againOk && truth.x == truthAgain.x && truth.y == truthAgain.y)
            {
                Assert.Equal((truth.x, truth.y), (layer.x, layer.y));
                return;
            }
            Assert.True(essai < Essais, $"souris en mouvement à chacun des {Essais} essais");
        }
    }
}
