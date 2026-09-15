namespace TypingEngine.Windows;

/// <summary>Intention de réparation d'un lot incomplet, recalculée à chaque tentative.</summary>
internal sealed class InputRecovery
{
    private readonly Win32.INPUT[] _inputs;
    private bool? _numLockTarget;
    private bool _deferred;

    internal InputRecovery(Win32.INPUT[] inputs, bool numLockBefore)
    {
        _inputs = inputs;
        int toggles = inputs.Count(i => i.u.ki.wVk == Win32.VK_NUMLOCK && (i.u.ki.dwFlags & 2) == 0);
        if (toggles > 0) _numLockTarget = numLockBefore ^ ((toggles & 1) != 0);
    }

    internal void MarkDeferred() => _deferred = true;
    internal void CancelNumLockRestoration() => _numLockTarget = null;

    internal Win32.INPUT[] Build(IWin32Api api, Func<ushort, bool> physicallyDown)
    {
        var releases = new List<Win32.INPUT>();
        var restores = new List<Win32.INPUT>();
        var seen = new HashSet<(ushort Vk, ushort Scan, uint Flags)>();
        foreach (var original in _inputs)
        {
            var input = original;
            var k = input.u.ki;
            if (k.wVk == Win32.VK_NUMLOCK) continue; // Géré avec son état basculé ci-dessous.
            var identity = (k.wVk, k.wVk == 0 ? k.wScan : (ushort)0, k.dwFlags & ~2u);
            if (!seen.Add(identity)) continue;
            if (IsModifier(k.wVk))
            {
                // Immédiatement, le hook connaît les modificateurs physiques.
                // Après un délai, ne pas remettre down un modificateur déjà relâché.
                bool down = physicallyDown(k.wVk);
                if (_deferred) down &= (api.GetAsyncKeyState(k.wVk) & 0x8000) != 0;
                input.u.ki.dwFlags = identity.Item3 | (down ? 0u : 2u);
                if (down) restores.Add(input);
                else releases.Add(input);
            }
            else
            {
                // N événements acceptés ne garantit pas un préfixe documenté :
                // relâcher tous les caractères du lot, jamais rejouer leurs keydowns.
                input.u.ki.dwFlags |= 2;
                releases.Add(input);
            }
        }
        if (_numLockTarget is bool target)
        {
            var input = _inputs.First(i => i.u.ki.wVk == Win32.VK_NUMLOCK);
            input.u.ki.dwFlags |= 2;
            releases.Add(input);
            if (((api.GetKeyState((int)Win32.VK_NUMLOCK) & 1) != 0) != target)
            {
                input.u.ki.dwFlags &= ~2u;
                releases.Add(input);
                input.u.ki.dwFlags |= 2;
                releases.Add(input);
            }
        }
        releases.AddRange(restores);
        return releases.ToArray();
    }

    private static bool IsModifier(ushort vk) =>
        vk is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;
}
