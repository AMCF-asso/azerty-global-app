namespace TypingEngine.Windows;

public enum MaintainableLayerMode
{
    Inactive,
    OneShot,
    Held,
    Locked
}

/// <summary>
/// Identifie une instance de processus, y compris lorsque Windows réutilise un PID.
/// </summary>
public readonly record struct ForegroundProcessIdentity(uint ProcessId, long StartTimeTicks)
{
    public bool IsValid => ProcessId != 0;
}

public readonly record struct MaintainableLayerState(
    string? LayerId,
    MaintainableLayerMode Mode,
    ForegroundProcessIdentity Owner)
{
    public static MaintainableLayerState Inactive =>
        new(null, MaintainableLayerMode.Inactive, default);

    public bool IsActive => LayerId != null && Mode != MaintainableLayerMode.Inactive;
}

/// <summary>
/// Machine d'état pure des couches maintenables. Elle ne connaît ni le layout,
/// ni le hook, ni SendInput : KeyMapper lui fournit uniquement les événements
/// physiques et consomme l'état effectif pour résoudre les caractères.
/// </summary>
internal sealed class MaintainableLayerManager
{
    internal static readonly string[] SupportedLayers =
    {
        "dk_greek",
        "dk_cyrillic",
        "dk_scientific"
    };

    private readonly Func<long> _clockMilliseconds;
    private readonly Dictionary<ForegroundProcessIdentity, string> _lockedLayers = new();
    private readonly HashSet<string> _enabledLayers = new(StringComparer.Ordinal);

    private ForegroundProcessIdentity _foreground;
    private bool _secureInput;
    private bool _enabled;
    private int _doubleTapMilliseconds;

    private string? _pressedLayer;
    private uint _pressedScanCode;
    private ForegroundProcessIdentity _pressedOwner;
    private bool _pressedUsedAsChord;

    private string? _heldLayer;
    private ForegroundProcessIdentity _heldOwner;
    private string? _oneShotLayer;
    private ForegroundProcessIdentity _oneShotOwner;

    private string? _lastTapLayer;
    private ForegroundProcessIdentity _lastTapOwner;
    private long _lastTapAt;
    private bool _consumeEscapeKeyUp;

    public event Action? StateChanged;

    public MaintainableLayerManager(Func<long>? clockMilliseconds = null)
    {
        _clockMilliseconds = clockMilliseconds ?? (() => Environment.TickCount64);
        ApplySettings(false, Array.Empty<string>(), 500);
    }

    public MaintainableLayerState CurrentState => GetEffectiveState(_foreground);

    public bool IsSecureInput => _secureInput;

    /// <summary>Scancode du déclencheur physiquement enfoncé, null sinon. Permet au
    /// KeyMapper de promouvoir l'accord même quand la frappe part en raccourci.</summary>
    public uint? PendingTriggerScanCode => _pressedLayer != null ? _pressedScanCode : null;

    public void ApplySettings(bool enabled, IEnumerable<string> enabledLayers, int doubleTapMilliseconds)
    {
        var before = CurrentState;
        _enabled = enabled;
        _enabledLayers.Clear();
        foreach (var layer in enabledLayers)
        {
            if (SupportedLayers.Contains(layer, StringComparer.Ordinal))
                _enabledLayers.Add(layer);
        }
        _doubleTapMilliseconds = Math.Clamp(doubleTapMilliseconds, 150, 1000);

        if (!_enabled)
        {
            ResetTransient();
            _lockedLayers.Clear();
        }

        NotifyIfChanged(before);
    }

    public bool IsLayerEnabled(string layerId) =>
        _enabled && _enabledLayers.Contains(layerId);

    public void SetForeground(ForegroundProcessIdentity identity, bool secureInput)
    {
        var before = CurrentState;
        bool processChanged = identity != _foreground;
        bool securityChanged = secureInput != _secureInput;

        if (processChanged || (securityChanged && secureInput))
        {
            ResetTransient();
            ClearTapHistory();
        }

        _foreground = identity;
        _secureInput = secureInput;
        NotifyIfChanged(before);
    }

    public bool BeginTrigger(string layerId, uint scanCode)
    {
        if (_secureInput || !_foreground.IsValid || !IsLayerEnabled(layerId))
            return false;

        // Répétition automatique du keydown : garder le même déclencheur en attente.
        if (_pressedLayer != null)
            return _pressedScanCode == scanCode;

        var before = CurrentState;
        _pressedLayer = layerId;
        _pressedScanCode = scanCode;
        _pressedOwner = _foreground;
        _pressedUsedAsChord = false;

        // Une nouvelle activation remplace un one-shot encore en attente, mais ne
        // touche pas au verrouillage qui doit reprendre après un accord temporaire.
        _oneShotLayer = null;
        _oneShotOwner = default;
        NotifyIfChanged(before);
        return true;
    }

    public bool EndTrigger(uint scanCode)
    {
        if (_pressedLayer == null || _pressedScanCode != scanCode)
            return false;

        var before = CurrentState;
        string layer = _pressedLayer;
        var owner = _pressedOwner;
        bool wasChord = _pressedUsedAsChord;

        _pressedLayer = null;
        _pressedScanCode = 0;
        _pressedOwner = default;
        _pressedUsedAsChord = false;
        _heldLayer = null;
        _heldOwner = default;

        if (!wasChord && owner == _foreground && !_secureInput)
        {
            if (_lockedLayers.TryGetValue(owner, out var locked) &&
                string.Equals(locked, layer, StringComparison.Ordinal))
            {
                _lockedLayers.Remove(owner);
                ClearTapHistory();
            }
            else
            {
                long now = _clockMilliseconds();
                bool isDoubleTap = string.Equals(_lastTapLayer, layer, StringComparison.Ordinal) &&
                    _lastTapOwner == owner &&
                    now >= _lastTapAt &&
                    now - _lastTapAt <= _doubleTapMilliseconds;

                if (isDoubleTap)
                {
                    // Un seul verrou par processus : une autre couche le remplace.
                    _lockedLayers[owner] = layer;
                    _oneShotLayer = null;
                    _oneShotOwner = default;
                    ClearTapHistory();
                }
                else
                {
                    _oneShotLayer = layer;
                    _oneShotOwner = owner;
                    _lastTapLayer = layer;
                    _lastTapOwner = owner;
                    _lastTapAt = now;
                }
            }
        }
        else if (wasChord)
        {
            ClearTapHistory();
        }

        NotifyIfChanged(before);
        return true;
    }

    /// <summary>
    /// Transforme un déclencheur maintenu en couche temporaire dès la première
    /// autre frappe. Retourne true lorsqu'un accord est désormais actif.
    /// </summary>
    public bool PromotePendingTriggerToHeld()
    {
        if (_pressedLayer == null || _pressedOwner != _foreground || _secureInput)
            return false;

        var before = CurrentState;
        _pressedUsedAsChord = true;
        _heldLayer = _pressedLayer;
        _heldOwner = _pressedOwner;
        ClearTapHistory();
        NotifyIfChanged(before);
        return true;
    }

    public MaintainableLayerState GetEffectiveState(ForegroundProcessIdentity identity)
    {
        if (!_enabled || _secureInput || !identity.IsValid || identity != _foreground)
            return MaintainableLayerState.Inactive;

        if (_heldLayer != null && _heldOwner == identity)
            return new(_heldLayer, MaintainableLayerMode.Held, identity);

        if (_oneShotLayer != null && _oneShotOwner == identity)
            return new(_oneShotLayer, MaintainableLayerMode.OneShot, identity);

        if (_lockedLayers.TryGetValue(identity, out var locked))
            return new(locked, MaintainableLayerMode.Locked, identity);

        return MaintainableLayerState.Inactive;
    }

    public void ConsumeOneShot()
    {
        if (_oneShotLayer == null || _oneShotOwner != _foreground)
            return;

        var before = CurrentState;
        _oneShotLayer = null;
        _oneShotOwner = default;
        ClearTapHistory();
        NotifyIfChanged(before);
    }

    public bool CancelOneShot()
    {
        if (_oneShotLayer == null)
            return false;

        var before = CurrentState;
        _oneShotLayer = null;
        _oneShotOwner = default;
        ClearTapHistory();
        NotifyIfChanged(before);
        return true;
    }

    public bool HandleEscape(bool isKeyDown)
    {
        if (!isKeyDown)
        {
            bool consume = _consumeEscapeKeyUp;
            _consumeEscapeKeyUp = false;
            return consume;
        }

        if (!_foreground.IsValid || !_lockedLayers.ContainsKey(_foreground) || _secureInput)
            return false;

        var before = CurrentState;
        _lockedLayers.Remove(_foreground);
        ResetTransient();
        ClearTapHistory();
        _consumeEscapeKeyUp = true;
        NotifyIfChanged(before);
        return true;
    }

    public void ClearAll()
    {
        var before = CurrentState;
        ResetTransient();
        ClearTapHistory();
        _lockedLayers.Clear();
        NotifyIfChanged(before);
    }

    public void ResetTransientState()
    {
        var before = CurrentState;
        ResetTransient();
        ClearTapHistory();
        NotifyIfChanged(before);
    }

    public void RemoveDeadProcessLocks(Func<ForegroundProcessIdentity, bool> isAlive)
    {
        var before = CurrentState;
        foreach (var identity in _lockedLayers.Keys.ToArray())
        {
            if (!isAlive(identity))
                _lockedLayers.Remove(identity);
        }
        NotifyIfChanged(before);
    }

    private void ResetTransient()
    {
        _pressedLayer = null;
        _pressedScanCode = 0;
        _pressedOwner = default;
        _pressedUsedAsChord = false;
        _heldLayer = null;
        _heldOwner = default;
        _oneShotLayer = null;
        _oneShotOwner = default;
    }

    private void ClearTapHistory()
    {
        _lastTapLayer = null;
        _lastTapOwner = default;
        _lastTapAt = 0;
    }

    private void NotifyIfChanged(MaintainableLayerState before)
    {
        if (before != CurrentState)
            StateChanged?.Invoke();
    }
}
