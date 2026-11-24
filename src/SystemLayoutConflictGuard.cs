namespace AZERTYGlobal;

/// <summary>
/// Décide s'il faut afficher la modale de conflit avec la disposition système.
/// Au démarrage : avertit si la disposition système est active et l'alerte activée.
/// En cours de session (après Ctrl+Maj) : avertit uniquement lors d'une vraie bascule
/// (inactive → active), jamais si la disposition était déjà active (ex. Ctrl+Maj+clic).
/// </summary>
internal sealed class SystemLayoutConflictGuard
{
    private bool _lastActive;

    public bool EvaluateStartup(bool isSystemLayoutActive, bool warningEnabled)
    {
        _lastActive = isSystemLayoutActive;
        return isSystemLayoutActive && warningEnabled;
    }

    public bool EvaluateAfterSwitch(bool isSystemLayoutActive, bool warningEnabled)
    {
        bool justActivated = isSystemLayoutActive && !_lastActive;
        _lastActive = isSystemLayoutActive;
        return justActivated && warningEnabled;
    }
}
