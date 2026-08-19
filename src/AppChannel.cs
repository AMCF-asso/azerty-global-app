// Canal de distribution — v1.2.0.
//
// Un seul binaire est publié sur deux canaux (décision D1 du 2026-08-19), et il reconnaît le
// sien à l'exécution (D2). Aucun changement de packaging n'est nécessaire : les deux canaux
// portent déjà deux identités MSIX distinctes, parce que l'identité d'un paquet doit égaler le
// sujet du certificat qui le signe. Le bundle hors Store est signé au nom de l'association,
// le paquet Store au nom de son identité Partner Center.
//
// La comparaison porte sur le *package family name*, jamais sur la chaîne d'éditeur : le family
// name est « <Name>_<PublisherId> », où PublisherId est un condensé déterministe de cette
// chaîne. C'est donc une comparaison de chaînes, sans marshalling de structure, donc sans
// risque sous Native AOT.
//
// Le canal hors package garde le comportement d'aujourd'hui, à l'identique : la décision D8 du
// 2026-08-19 remet son cas à plus tard. Il figure dans l'énumération pour que la bascule soit
// une ligne le jour où elle est tranchée, pas pour être traité maintenant.

namespace AZERTYGlobal;

/// <summary>Canal par lequel cette instance a été installée.</summary>
internal enum DistributionChannel
{
    /// <summary>Microsoft Store. Seul canal qui sollicite un avis et propose ses liens
    /// externes.</summary>
    Store,

    /// <summary>Bundle MSIX signé par l'association, distribué hors Store. Comportement
    /// sobre.</summary>
    Amcf,

    /// <summary>Exécutable hors package. Comportement inchangé, décision D8.</summary>
    Unpackaged,
}

/// <summary>
/// Reconnaît le canal de distribution de l'instance courante. Détecté une fois, au premier
/// accès : l'identité d'un paquet ne change pas en cours d'exécution.
/// </summary>
static class AppChannel
{
    /// <summary>Canal de cette instance.</summary>
    public static DistributionChannel Current => _override ?? Detected;

    private static DistributionChannel? _override;

    private static DistributionChannel Detected { get; } = Detect();

    private static DistributionChannel Detect()
    {
        // Les deux faits viennent de deux appels Windows indépendants, à dessein : IsPackaged dit
        // s'il y a un paquet, la lecture du nom dit lequel. Déduire le premier du succès de la
        // seconde ferait passer un paquet dont le nom reste illisible pour une exécution hors
        // paquet, donc pour le comportement d'aujourd'hui, sollicitations comprises — l'inverse
        // exact de la direction d'échec voulue plus bas.
        bool packaged = ConfigManager.IsPackaged;
        ConfigManager.TryGetPackageFamilyName(out var familyName);
        return Classify(packaged, familyName);
    }

    /// <summary>
    /// Décide du canal à partir des deux seuls faits observables. Fonction pure, sans état :
    /// c'est elle que les tests éprouvent, ce qui leur évite de toucher au canal du processus.
    /// </summary>
    /// <param name="packaged">false quand l'app ne tourne pas dans un package MSIX, ou quand
    /// son family name est resté illisible.</param>
    /// <param name="familyName">Family name du package courant, null hors package.</param>
    internal static DistributionChannel Classify(bool packaged, string? familyName)
    {
        if (!packaged)
            return DistributionChannel.Unpackaged;

        // Comparaison insensible à la casse : Windows compare les identités de paquet sans
        // tenir compte de la casse, et une casse inattendue ne doit pas faire passer le canal
        // Store pour un canal inconnu.
        if (string.Equals(familyName, ProductIdentity.StorePackageFamilyName,
                StringComparison.OrdinalIgnoreCase))
        {
            return DistributionChannel.Store;
        }

        // Tout paquet inconnu — family name illisible compris — est traité comme le canal
        // sobre. La direction d'échec est le silence, jamais une sollicitation de trop.
        return DistributionChannel.Amcf;
    }

    /// <summary>
    /// Hook de test : force le canal jusqu'au <c>Dispose</c>, qui restaure la valeur
    /// précédente — y compris quand le corps du <c>using</c> lève. À n'utiliser que dans le
    /// projet de tests via InternalsVisibleTo.
    ///
    /// La restauration est portée par le scope et non par l'appelant : un override statique
    /// sans remise à zéro traverse toute la suite dans un seul processus, et rend un test vert
    /// en isolation mais rouge dans la suite entière.
    /// </summary>
    internal static IDisposable OverrideForTests(DistributionChannel channel)
    {
        var scope = new OverrideScope(_override);
        _override = channel;
        return scope;
    }

    private sealed class OverrideScope : IDisposable
    {
        private readonly DistributionChannel? _previous;
        private bool _disposed;

        internal OverrideScope(DistributionChannel? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _override = _previous;
        }
    }
}
