// Défi du jour vu de la fenêtre Leçons (audit du 25/09, L-18) : catalogue du jour, fin de
// séance et partage du résultat, sortis de LessonsWindow qui n'en garde que l'affichage.
namespace AZERTYGlobal;

internal sealed class ChallengeSessionState
{
    private string? _lessonKey;

    /// <summary>Texte à copier : non null seulement quand la séance qui vient de s'achever
    /// est le défi commun. Les séances de prise en main dépendent de la progression
    /// individuelle, deux personnes n'y tapent pas le même extrait.</summary>
    public string? ShareText { get; private set; }
    public bool ShareCopied { get; private set; }

    /// <summary>Un résultat a été copié pendant cette ouverture de la fenêtre.</summary>
    public bool SharedThisSession { get; private set; }

    /// <summary>Catalogue des leçons, plus le module « Défi du jour » quand il est offert
    /// (Défi activé, rappels activés, banque d'extraits chargée).</summary>
    public static LessonCatalog LoadCatalog()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        if (!DailyChallenge.ShouldOfferModule(DailyChallenge.Enabled, ConfigManager.TrainingEnabled))
            return catalog;
        var challenge = DailyChallenge.BuildModule(
            DateOnly.FromDateTime(DateTime.Now), ConfigManager.TrainingSequenceIndex);
        if (challenge == null) return catalog;
        return new LessonCatalog(catalog.Modules.Append(challenge).ToArray());
    }

    /// <summary>Vrai si le module du Défi ne correspond plus à la date, à l'étape ou à la
    /// langue : fenêtre restée ouverte à cheval sur minuit, séance terminée.</summary>
    public bool IsStale()
    {
        string key = DailyChallenge.ShouldOfferModule(DailyChallenge.Enabled, ConfigManager.TrainingEnabled)
            ? $"{DateOnly.FromDateTime(DateTime.Now):yyyyMMdd}/{ConfigManager.TrainingSequenceIndex}/{L.Language}"
            : "off";
        if (_lessonKey == key) return false;
        _lessonKey = key;
        return true;
    }

    /// <summary>Force la reconstruction au prochain <see cref="IsStale"/>.</summary>
    public void Invalidate() => _lessonKey = null;

    public void ClearShare()
    {
        ShareText = null;
        ShareCopied = false;
    }

    /// <summary>
    /// Clôt la séance du jour : prépare le texte partageable, puis date la séance et fait
    /// avancer la séquence. Dans cet ordre : <see cref="TrainingReminders.MarkSessionCompleted"/>
    /// change la phase lue par le partage.
    /// </summary>
    public void Complete(LessonAttemptStats stats, int? previousBestWpm)
    {
        PrepareShare(stats, previousBestWpm);
        try { TrainingReminders.MarkSessionCompleted(DateOnly.FromDateTime(DateTime.Now)); }
        catch (Exception ex) { ConfigManager.Log("LessonsWindow.ChallengeCompleted", ex); }
    }

    private void PrepareShare(LessonAttemptStats stats, int? previousBestWpm)
    {
        ClearShare();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var session = DailyChallenge.SessionFor(today, ConfigManager.TrainingSequenceIndex);
            if (session == null || session.IsSequencePhase) return;

            // Un premier résultat n'est pas un record : sans meilleur score antérieur,
            // il n'y a rien à battre.
            bool personalBest = previousBestWpm.HasValue && stats.Wpm.HasValue &&
                                stats.Wpm.Value > previousBestWpm.Value;

            ShareText = ChallengeShare.Build(today, session.Extract.Credit, stats, personalBest);
        }
        catch (Exception ex)
        {
            ConfigManager.Log("LessonsWindow.PrepareChallengeShare", ex);
            ShareText = null;
        }
    }

    /// <summary>
    /// Copie le résultat. Le succès change le libellé du bouton et arme la sollicitation
    /// d'avis différée : quelqu'un qui partage son résultat est la personne la mieux disposée
    /// à noter l'application, et c'est le seul moment où l'application le sait de source sûre.
    /// </summary>
    public bool Copy(IntPtr owner)
    {
        if (ShareText == null) return false;
        if (!ClipboardText.TrySet(owner, ShareText)) return false;
        ShareCopied = true;
        SharedThisSession = true;
        return true;
    }

    /// <summary>Lu une fois à la fermeture de la fenêtre.</summary>
    public bool TakeSharedThisSession()
    {
        bool shared = SharedThisSession;
        SharedThisSession = false;
        return shared;
    }
}
