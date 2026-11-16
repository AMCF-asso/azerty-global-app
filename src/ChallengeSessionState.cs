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
    public bool ShareCopyFailed { get; private set; }

    /// <summary>Point d'appui du presse-papiers : les tests le remplacent.</summary>
    internal Func<IntPtr, string, bool> ClipboardWriter = ClipboardText.TrySet;

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

    /// <summary>La séance affichée est déjà comptée : la finir de nouveau ne change ni la date,
    /// ni la séquence, ni « Défis terminés » (audit UX du 09/11, L10).</summary>
    public bool AlreadyCounted => !TrainingReminders.WouldCount(_moduleDate, _moduleSequenceIndex);

    /// <summary>
    /// Record du Défi, tous jours confondus (audit UX du 09/11, L10) : la valeur gardée en
    /// config, ou mieux celle des extraits des jours passés restés dans la progression (records
    /// d'avant la 1.3.0). Null tant qu'aucun extrait n'a de vitesse.
    /// </summary>
    public static int? PersonalBestWpm(LessonProgressStore progress)
    {
        long best = Math.Max((long)ConfigManager.TrainingBestWpm, progress.BestChallengeWpm() ?? 0);
        return best > 0 ? (int)Math.Min(best, int.MaxValue) : null;
    }

    public void ClearShare()
    {
        ShareText = null;
        ShareCopied = false;
        ShareCopyFailed = false;
        _shareSource = null;
    }

    // Ce dont le texte partagé est fait, gardé pour le refaire dans une autre langue.
    private (DateOnly Date, uint SequenceIndex, LessonAttemptStats Stats, bool PersonalBest)? _shareSource;

    /// <summary>Langue changée, bilan du Défi ouvert : le texte à copier est refait dans la
    /// nouvelle langue, et le bouton propose de nouveau la copie (audit UX du 09/11, L14 : il
    /// restait dans l'ancienne langue).</summary>
    public void RefreshShareLanguage()
    {
        if (_shareSource == null) return;
        try
        {
            ShareText = BuildShare();
            ShareCopied = false;
            ShareCopyFailed = false;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("LessonsWindow.RefreshChallengeShare", ex);
        }
    }

    private string? BuildShare()
    {
        if (_shareSource is not { } source) return null;
        var session = DailyChallenge.SessionFor(source.Date, source.SequenceIndex);
        if (session == null || session.IsSequencePhase) return null;
        return ChallengeShare.Build(source.Date, session.Extract.Credit, source.Stats, source.PersonalBest);
    }

    /// <summary>
    /// Clôt la séance jouée : prépare le texte partageable, puis date la séance et fait
    /// avancer la séquence, une seule fois par séance (un rejeu ne compte pas, A23). Les deux
    /// lisent l'étape dont le module a été construit, pas celle de la config, déjà avancée
    /// après une première fin, et la séance est datée du jour du module : une fenêtre restée
    /// ouverte après minuit ne compte pas l'extrait de la veille une seconde fois, ni à la
    /// place du Défi du nouveau jour (audit UX du 09/11, L17). <paramref name="previousBestWpm"/> est le record de tous les
    /// Défis lu avant cette séance (<see cref="PersonalBestWpm"/>) ; battu ou pas encore en
    /// config, il y est écrit.
    /// </summary>
    public void Complete(LessonAttemptStats stats, int? previousBestWpm)
    {
        PrepareShare(stats, previousBestWpm);
        try
        {
            int best = Math.Max(previousBestWpm ?? 0, stats.Wpm ?? 0);
            if (best > 0 && (uint)best > ConfigManager.TrainingBestWpm)
                ConfigManager.SetTrainingBestWpm((uint)best);
        }
        catch (Exception ex) { ConfigManager.Log("LessonsWindow.ChallengeBest", ex); }
        try
        {
            TrainingReminders.RepairFutureDates(DateOnly.FromDateTime(DateTime.Now)); // jour réel, pas celui du module
            TrainingReminders.MarkSessionCompleted(_moduleDate, _moduleSequenceIndex);
        }
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

            _shareSource = (today, _moduleSequenceIndex, stats, personalBest);
            ShareText = BuildShare();
        }
        catch (Exception ex)
        {
            ConfigManager.Log("LessonsWindow.PrepareChallengeShare", ex);
            ShareText = null;
        }
    }

    /// <summary>
    /// Copie le résultat. Le succès change le libellé du bouton, rien d'autre : depuis la 1.3.0
    /// (C1), un partage ne déclenche plus la demande d'avis du Store, qui part seulement d'un clic
    /// sur « Noter l'application ».
    /// </summary>
    public bool Copy(IntPtr owner)
    {
        if (ShareText == null) return false;
        // C8.1 : un échec se dit (ShareCopyFailed), il ne laisse plus le bouton inchangé.
        bool copied = ClipboardWriter(owner, ShareText);
        ShareCopied = copied;
        ShareCopyFailed = !copied;
        return copied;
    }
}
