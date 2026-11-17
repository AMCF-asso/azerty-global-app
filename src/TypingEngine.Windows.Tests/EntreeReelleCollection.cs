namespace TypingEngine.Windows.Tests;

/// <summary>
/// Les tests de vraie frappe (catégorie EntreeReelle) posent chacun un hook clavier pour tout le
/// système et injectent des touches. Joués en parallèle, le hook d'une classe remappe les touches
/// injectées par l'autre (paquets Unicode vus par un test « sans hook du produit »). Ils partagent
/// donc une collection non parallèle : une classe à la fois, et aucune autre collection en même temps.
/// </summary>
[CollectionDefinition(Nom, DisableParallelization = true)]
public sealed class EntreeReelleCollection
{
    public const string Nom = "EntreeReelle";
}
