namespace TypingEngine.Windows.Tests;

/// <summary>
/// Constat C1.2 (suite ME-06) : CharsStartedBy, la longueur du préfixe de texte dont un événement au
/// moins est parti quand SendInput n'accepte qu'une partie du lot. Les bornes sont celles que
/// BuildAndSendText relève : (événements construits, caractères consommés) au début de chaque
/// caractère, puis à la fin du texte. Une paire de substitution consomme deux caractères ; un
/// substitut isolé n'en produit aucun événement.
/// </summary>
public class CharsStartedByTests
{
    private static List<(int Events, int Chars)> Bornes(params (int, int)[] bornes) => bornes.ToList();

    // « abc » : deux événements par caractère.
    private static readonly List<(int Events, int Chars)> TroisLettres = Bornes((0, 0), (2, 1), (4, 2), (6, 3));

    [Theory]
    [InlineData(1u, 1)]   // coupure entre l'appui et le relâchement du premier
    [InlineData(2u, 1)]   // coupure entre deux caractères : le second n'a pas commencé
    [InlineData(3u, 2)]   // appui du second parti
    [InlineData(4u, 2)]
    [InlineData(5u, 3)]   // appui du troisième parti
    public void Texte_de_plusieurs_caracteres_coupe_en_milieu_de_texte(uint envoyes, int attendu)
    {
        Assert.Equal(attendu, KeyMapper.CharsStartedBy(TroisLettres, envoyes));
    }

    [Fact]
    public void Aucun_evenement_parti_n_annonce_aucun_caractere()
    {
        Assert.Equal(0, KeyMapper.CharsStartedBy(TroisLettres, 0));
    }

    // « a » + substitut isolé + « b » : le substitut isolé ne produit aucun événement (BuildAndSendText
    // le saute), les bornes du caractère suivant sont donc égales aux siennes.
    private static readonly List<(int Events, int Chars)> AvecSubstitutIsole = Bornes((0, 0), (2, 1), (2, 2), (4, 3));

    [Theory]
    [InlineData(1u, 1)]   // seul « a » a commencé : le substitut isolé n'est pas compté
    [InlineData(2u, 1)]   // « a » est complet, ni le substitut isolé ni « b » n'ont commencé
    [InlineData(3u, 3)]   // « b » a commencé : le préfixe contient le substitut isolé qui le précède
    public void Caractere_sans_evenement_n_est_compte_que_s_un_suivant_a_commence(uint envoyes, int attendu)
    {
        Assert.Equal(attendu, KeyMapper.CharsStartedBy(AvecSubstitutIsole, envoyes));
    }

    [Fact]
    public void Substitut_isole_en_fin_de_texte_n_est_jamais_annonce_dans_un_lot_partiel()
    {
        // « a » + substitut isolé final : deux événements en tout ; coupé à un, seul « a » est parti.
        var bornes = Bornes((0, 0), (2, 1), (2, 2));
        Assert.Equal(1, KeyMapper.CharsStartedBy(bornes, 1));
    }

    [Fact]
    public void Substitut_isole_en_tete_est_inclus_des_que_le_suivant_a_commence()
    {
        var bornes = Bornes((0, 0), (0, 1), (2, 2));
        Assert.Equal(2, KeyMapper.CharsStartedBy(bornes, 1));
    }

    // Une paire de substitution (un point de code hors plan de base) : quatre événements (deux
    // appuis, deux relâchements), deux caractères consommés ; coupée où que ce soit, elle est annoncée
    // entière, jamais à moitié.
    [Theory]
    [InlineData(1u, 2)]
    [InlineData(3u, 2)]
    [InlineData(4u, 2)]   // paire complète, « b » n'a pas commencé
    [InlineData(5u, 3)]
    public void Paire_de_substitution_coupee_est_annoncee_entiere(uint envoyes, int attendu)
    {
        var bornes = Bornes((0, 0), (4, 2), (6, 3));
        Assert.Equal(attendu, KeyMapper.CharsStartedBy(bornes, envoyes));
    }
}
