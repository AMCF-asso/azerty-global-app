using System;
using TypingEngine.Windows;

namespace AZERTYGlobal;

/// <summary>
/// Lecture de la position du curseur par les fenêtres du produit (placement sur l'écran du
/// curseur, main sur une cible cliquable, survol du drapeau). Elle passe par la couche
/// <see cref="IWin32Api"/> déjà utilisée pour SendInput : en production, la vraie souris
/// (<see cref="RealWin32Api"/>) ; sous le banc de capture, une position hors fenêtre, de sorte
/// que le banc n'a plus à déplacer la vraie souris de l'utilisateur (choix d'Antoine du 15/10).
///
/// Le remplacement est propre au fil : les fenêtres de test vivent et reçoivent leurs messages
/// sur le fil qui les crée, et deux classes de tests peuvent tourner en parallèle.
/// </summary>
internal static class CursorSource
{
    private static readonly IWin32Api Real = new RealWin32Api();

    [ThreadStatic] private static IWin32Api? t_override;

    /// <summary>Position du curseur ; faux si Windows ne la donne pas.</summary>
    internal static bool TryGet(out Win32.POINT point)
    {
        bool ok = (t_override ?? Real).GetCursorPos(out int x, out int y);
        point = new Win32.POINT { x = x, y = y };
        return ok;
    }

    /// <summary>Remplace la couche pour le fil courant jusqu'au Dispose. Réservé aux tests.</summary>
    internal static IDisposable OverrideForTests(IWin32Api api)
    {
        var scope = new OverrideScope(t_override);
        t_override = api;
        return scope;
    }

    private sealed class OverrideScope : IDisposable
    {
        private readonly IWin32Api? _previous;
        private bool _disposed;

        internal OverrideScope(IWin32Api? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            t_override = _previous;
        }
    }
}
