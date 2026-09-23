namespace AZERTYGlobal;

/// <summary>
/// AG130-42 — borner une fenêtre à la zone de travail de son écran.
///
/// Une fenêtre dimensionnée en multipliant sa taille de référence par le facteur DPI n'a
/// aucune raison de tenir à l'écran : à 175 % sur 1920×1080, la fenêtre des leçons demande
/// 1960×1330 px de zone client pour une zone de travail de 1920×1032. Rien n'échoue —
/// Windows crée la fenêtre, une partie est simplement hors écran, et les commandes du bas
/// deviennent inatteignables.
///
/// Le rapport de forme se conserve, sinon la mise en page rendue à l'échelle se déforme :
/// <c>UpdateRenderScaleFromCurrentClient</c> prend le plus petit des deux rapports, donc
/// écraser une seule dimension laisse une bande vide sur l'autre.
/// </summary>
static class WindowSizing
{
    /// <summary>
    /// Part de la zone de travail qu'une fenêtre peut occuper au plus. Même valeur que
    /// <c>LearningModule</c>, qui bornait déjà : les 10 % restants laissent voir qu'il y a
    /// un bureau derrière, et laissent la barre des tâches d'un écran secondaire tranquille.
    /// </summary>
    public const float MaxWorkAreaFraction = 0.9f;

    /// <summary>
    /// Réduit <paramref name="wantedW"/> × <paramref name="wantedH"/> pour tenir dans la
    /// fraction demandée de la zone de travail, à rapport de forme constant. Une taille qui
    /// tient déjà ressort inchangée.
    /// </summary>
    public static (int Width, int Height) ClampToWorkArea(
        int wantedW, int wantedH, int workW, int workH,
        float fraction = MaxWorkAreaFraction)
    {
        // Une zone de travail nulle ou négative veut dire qu'aucune mesure n'a abouti
        // (GetMonitorInfo en échec) : mieux vaut la taille voulue qu'une fenêtre de 0 px.
        if (wantedW <= 0 || wantedH <= 0 || workW <= 0 || workH <= 0)
            return (Math.Max(1, wantedW), Math.Max(1, wantedH));

        int maxW = Math.Max(1, (int)(workW * fraction));
        int maxH = Math.Max(1, (int)(workH * fraction));
        if (wantedW <= maxW && wantedH <= maxH) return (wantedW, wantedH);

        double ratio = wantedW / (double)wantedH;
        // Comparer les rapports de forme dit laquelle des deux bornes mord la première.
        if (maxW / (double)maxH > ratio)
            return (Math.Max(1, (int)(maxH * ratio)), maxH);

        return (maxW, Math.Max(1, (int)(maxW / ratio)));
    }

    /// <summary>
    /// D1 (accessibilité 1.3.0) — une valeur de référence à 96 DPI portée au DPI de l'écran,
    /// arrondie au plus proche comme <c>MulDiv</c>. Les hauteurs de police négatives de
    /// <c>CreateFontW</c> se mettent à l'échelle comme le reste. Un DPI nul ou négatif, donc
    /// une mesure qui a échoué, laisse la valeur à 100 %.
    /// </summary>
    public static int ScaleForDpi(int value, int dpi)
    {
        if (dpi <= 0) return value;
        return (int)Math.Round(value * (double)dpi / 96.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// D3 (accessibilité 1.3.0) — plus petite zone client admise pour une fenêtre
    /// redimensionnable : le plus petit rectangle au rapport <paramref name="ratio"/> qui couvre
    /// <paramref name="minW"/> × <paramref name="minH"/> (aucun rapport imposé quand il vaut 0),
    /// ramené, cadre compris, à la même fraction de la zone de travail que
    /// <see cref="ClampToWorkArea"/>.
    ///
    /// Windows applique <c>ptMinTrackSize</c> à <c>CreateWindowEx</c>, <c>MoveWindow</c> et
    /// <c>SetWindowPos</c>, pas seulement au tirage d'un bord (mesuré le 2026-09-23) : un
    /// minimum plus grand que l'écran annulait donc le plafond d'AG130-42. Les Leçons demandent
    /// 940 × 600 à 96 DPI, soit 1645 × 1050 px de zone client à 175 %, plus que la zone de
    /// travail d'un 1920 × 1080.
    /// </summary>
    public static (int Width, int Height) MinimumClientSize(int minW, int minH, double ratio,
        int nonClientW, int nonClientH, int workW, int workH, float fraction = MaxWorkAreaFraction)
    {
        int w = Math.Max(1, minW);
        int h = Math.Max(1, minH);
        if (ratio > 0)
        {
            // Même calcul que LessonsWindow.EnforceMinimumClientSize : la largeur d'abord.
            h = Math.Max(1, (int)Math.Round(w / ratio));
            if (h < minH)
            {
                h = minH;
                w = Math.Max(1, (int)Math.Round(h * ratio));
            }
        }
        if (workW <= 0 || workH <= 0) return (w, h);

        int availW = Math.Max(1, (int)(workW * fraction) - Math.Max(0, nonClientW));
        int availH = Math.Max(1, (int)(workH * fraction) - Math.Max(0, nonClientH));
        return ClampToWorkArea(w, h, availW, availH, 1f);
    }
}
