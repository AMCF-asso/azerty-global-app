namespace AZERTYGlobal;

internal static partial class L
{
    // ── Recherche de caractère, refonte 1.3.0 (maquette « partition du choix ») ──
    // Pied : le nombre est écrit à part, en gras (« 20 résultats affichés sur 44 »).
    public static string Search_FooterShownOf(int total) => T($" résultats affichés sur {total}", $" of {total} results shown");
    public static string Search_FooterResultSingular => T(" résultat", " result");
    public static string Search_FooterResultPlural => T(" résultats", " results");
    /// <summary>Ce que fait Entrée, à droite du pied : « Entrée insère « Ź » ».</summary>
    public static string Search_FooterInsert(string character) =>
        T($"Entrée insère « {character} »", $"Enter inserts “{character}”");
}
