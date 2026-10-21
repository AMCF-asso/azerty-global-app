// Défi du jour vu de la fenêtre Leçons (audit du 25/09, L-18) : catalogue du jour, fin de
// séance et partage du résultat, sortis de LessonsWindow qui n'en garde que l'affichage.
namespace AZERTYGlobal;

internal sealed class ChallengeSessionState
{
    private string? _lessonKey;
    // Étape de la séquence dont le module affiché a été construit : la séance jouée, même
    // si la séquence a avancé depuis (rejeu par « Recommencer », audit 1.3.0, A23).
    private uint _moduleSequenceIndex;
    private DateOnly _moduleDate = DateOnly.FromDateTime(DateTime.Now);
    // Défi demandé (menu, annonce, rappel) : offert pour cette ouverture même sans l'opt-in
    // d'entraînement, qui ne garde que sa place permanente dans le catalogue (A23).
    private bool _requested;

    /// <summary>Texte à copier : non null seulement quand la séance qui vient de s'achever
    /// est le défi commun. Les séances de prise en main dépendent de la progression
    /// individuelle, deux personnes n'y tapent pas le même extrait.</summary>
    public string? ShareText { get; private set; }
    public bool ShareCopied { get; private set; }

    /// <summary>Un résultat a été copié pendant cette ouverture de la fenêtre.</summary>
    public bool SharedThisSession { get; private set; }

    /// <summary>Le module du Défi entre-t-il dans le catalogue ? Opt-in d'entraînement, ou
    /// Défi demandé pour cette ouverture : sans cela, le menu « Défi du jour » et l'annonce,
    /// faite justement à qui n'a pas l'opt-in, ouvraient les Leçons ordinaires (A23).</summary>
    private bool Offered =>
        DailyChallenge.ShouldOfferModule(DailyChallenge.Enabled, ConfigManager.TrainingEnabled) ||
        (_requested && DailyChallenge.Enabled);

    /// <summary>Catalogue des leçons, plus le module « Défi du jour » quand il est offert
    /// (Défi activé, rappels activés ou Défi demandé, banque d'extraits chargée).</summary>
    public LessonCatalog LoadCatalog()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        if (!Offered) return catalog;
        _moduleSequenceIndex = ConfigManager.TrainingSequenceIndex;
        _moduleDate = DateOnly.FromDateTime(DateTime.Now);
        var challenge = DailyChallenge.BuildModule(_moduleDate, _moduleSequenceIndex);
        if (challenge == null) return catalog;
        return new LessonCatalog(catalog.Modules.Append(challenge).ToArray());
    }

    /// <summary>Vrai si le module du Défi ne correspond plus à la date, à l'étape ou à la
    /// langue : fenêtre restée ouverte à cheval sur minuit, séance terminée.</summary>
    public bool IsStale()
    {
        string key = Offered
            ? $"{DateOnly.FromDateTime(DateTime.Now):yyyyMMdd}/{ConfigManager.TrainingSequenceIndex}/{L.Language}"
            : "off";
        if (_lessonKey == key) return false;
        _lessonKey = key;
        return true;
    }

    /// <summary>Force la reconstruction au prochain <see cref="IsStale"/>.</summary>
    public void Invalidate() => _lessonKey = null;

    /// <summary>Le Défi est demandé (menu, annonce, rappel) : offert jusqu'à la fermeture.</summary>
    public void Request()
    {
        _requested = true;
        Invalidate();
    }

    /// <summary>Fenêtre fermée : sans opt-in, le module quitte le catalogue à la prochaine
    /// ouverture.</summary>
    public void EndRequest() => _requested = false;

    public void ClearShare()
    {
        ShareText = null;
        ShareCopied = false;
    }

    /// <summary>
    /// Clôt la séance jouée : prépare le texte partageable, puis date la séance et fait
    /// avancer la séquence, une seule fois par séance (un rejeu ne compte pas, A23). Les deux
    /// lisent l'étape dont le module a été construit, pas celle de la config, déjà avancée
    /// après une première fin.
    /// </summary>
    public void Complete(LessonAttemptStats stats, int? previousBestWpm)
    {
        PrepareShare(stats, previousBestWpm);
        try { TrainingReminders.MarkSessionCompleted(DateOnly.FromDateTime(DateTime.Now), _moduleSequenceIndex); }
        catch (Exception ex) { ConfigManager.Log("LessonsWindow.ChallengeCompleted", ex); }
    }

    private void PrepareShare(LessonAttemptStats stats, int? previousBestWpm)
    {
        ClearShare();
        try
        {
            // L'extrait joué : celui du jour où le module a été construit.
            var today = _moduleDate;
            var session = DailyChallenge.SessionFor(today, _moduleSequenceIndex);
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
