using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 8 de l'analyse des machines à états (recherche de caractère,
/// presse-papiers). Chaque test affirme l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot8Tests
{
    private static CharacterSearch CreateWithoutTarget() =>
        new(new TextInsertionService(_ => default, _ => false));

    private static void PressEnterOnFirstResult(CharacterSearch search)
    {
        search.Show();
        var edit = BancCapture.Field<IntPtr>(search, "_hEdit");
        Win32.SetWindowTextW(edit, "e");
        Assert.True(BancCapture.Field<System.Collections.IList>(search, "_filteredResults").Count > 0);
        BancCapture.SetField(search, "_selectedIndex", 0);
        BancCapture.Call(search, "InsertSelectedCharacter");
    }

    // Constat C8.1
    // Insertion impossible (aucune cible) puis presse-papiers refusé : Entrée ne dit rien et le
    // caractère n'arrive nulle part. L'échec de la copie de secours doit être annoncé.
    [Fact]
    public void Recherche_EchecDeLInsertionEtDeLaCopie_EstAnnoncé()
    {
        using var isolation = new BancCapture.Isolation();
        var search = CreateWithoutTarget();
        try
        {
            var failures = new List<string>();
            var copies = new List<string>();
            search.FallbackCopyFailed += failures.Add;
            search.FallbackCopied += copies.Add;
            search.ClipboardWriter = (_, _) => false;

            PressEnterOnFirstResult(search);

            Assert.Single(failures);
            Assert.False(string.IsNullOrEmpty(failures[0]));
            Assert.Empty(copies);
        }
        finally { BancCapture.Teardown(search); }
    }

    // Constat C8.1
    // Après un échec annoncé, une seconde Entrée retente la copie : l'échec ne verrouille rien.
    [Fact]
    public void Recherche_SecondeEntrée_RetenteLaCopieApresUnEchec()
    {
        using var isolation = new BancCapture.Isolation();
        var search = CreateWithoutTarget();
        try
        {
            var failures = new List<string>();
            var copies = new List<string>();
            search.FallbackCopyFailed += failures.Add;
            search.FallbackCopied += copies.Add;
            int calls = 0;
            search.ClipboardWriter = (_, _) => ++calls > 1;

            PressEnterOnFirstResult(search);
            BancCapture.Call(search, "InsertSelectedCharacter");

            Assert.Single(failures);
            Assert.Single(copies);
            Assert.Equal(failures[0], copies[0]);
        }
        finally { BancCapture.Teardown(search); }
    }

    // Constat C8.1
    // « Copier mon résultat » du Défi : quand le presse-papiers refuse, le bouton le dit
    // (ShareCopyFailed) au lieu de rester inchangé ; un clic suivant qui réussit efface l'échec.
    [Fact]
    public void Defi_CopieRefusée_SeDitPuisSeRattrape()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var challenge = new ChallengeSessionState();
        challenge.LoadCatalog();
        challenge.Complete(new LessonAttemptStats(), previousBestWpm: null);
        Assert.NotNull(challenge.ShareText);

        bool accept = false;
        string? written = null;
        challenge.ClipboardWriter = (_, text) => { if (accept) written = text; return accept; };

        Assert.False(challenge.Copy(IntPtr.Zero));
        Assert.False(challenge.ShareCopied);
        Assert.True(challenge.ShareCopyFailed, "l'échec de la copie n'est visible nulle part");

        accept = true;
        Assert.True(challenge.Copy(IntPtr.Zero));
        Assert.True(challenge.ShareCopied);
        Assert.False(challenge.ShareCopyFailed);
        Assert.Equal(challenge.ShareText, written);
    }

    // Constat C8.1
    // OpenClipboard échoue tant qu'une autre application tient le presse-papiers : quelques essais
    // rapprochés, le dernier réussit.
    [Fact]
    public void Presse_papiers_Ouverture_RéessaieJusquAuSuccès()
    {
        int calls = 0;
        var sleeps = new List<int>();
        bool opened = ClipboardText.OpenWithRetry(_ => ++calls == ClipboardText.OpenAttempts,
            (IntPtr)1, sleeps.Add);

        Assert.True(opened);
        Assert.Equal(ClipboardText.OpenAttempts, calls);
        Assert.Equal(ClipboardText.OpenAttempts - 1, sleeps.Count);
    }

    // Constat C8.1
    // Le nouvel essai est borné : un presse-papiers qui reste tenu rend false après le dernier
    // essai, sans attente après lui.
    [Fact]
    public void Presse_papiers_Ouverture_EstBornée()
    {
        int calls = 0;
        var sleeps = new List<int>();
        bool opened = ClipboardText.OpenWithRetry(_ => { calls++; return false; }, (IntPtr)1, sleeps.Add);

        Assert.False(opened);
        Assert.Equal(ClipboardText.OpenAttempts, calls);
        Assert.Equal(ClipboardText.OpenAttempts - 1, sleeps.Count);
        Assert.True(sleeps.Sum() <= 200, "l'attente cumulée bloque le fil d'interface");
    }

    // Constat C8.1
    // Un presse-papiers libre ne coûte aucune attente.
    [Fact]
    public void Presse_papiers_Ouverture_ReussieDuPremierCoup_NAttendPas()
    {
        var sleeps = new List<int>();
        Assert.True(ClipboardText.OpenWithRetry(_ => true, (IntPtr)1, sleeps.Add));
        Assert.Empty(sleeps);
    }
}
