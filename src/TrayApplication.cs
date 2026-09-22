// Application system tray — interface utilisateur via Win32 API natif (pas de WinForms)
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Gère l'icône dans la zone de notification et le menu contextuel.
/// Utilise directement l'API Win32 (Shell_NotifyIcon, CreatePopupMenu, etc.)
/// pour éviter la dépendance à WinForms et permettre le trimming/AOT.
/// </summary>
sealed class TrayApplication : IDisposable
{
    // ── Window messages (spécifiques TrayApplication) ────────────
    private const uint WM_APP = 0x8000;
    private const uint WM_TRAYICON = WM_APP + 1;
    private const uint WM_APP_SEARCH = WM_APP + 2;
    private const uint WM_APP_VKBD = WM_APP + 3;
    private const uint WM_APP_STATE = WM_APP + 6;
    private const uint WM_APP_SECURE_RECHECK = WM_APP + 7;
    // Activation de toast COM re-routée du thread RPC vers le thread UI (v1.2.0).
    // wParam = 1 : action=review, cible Store ; wParam = 2 : action=review, cible
    // page feedback (répartition tirée à l'affichage — un toast antérieur sans
    // segment target= vaut cible Store, comportement historique).
    // wParam = 3 : action=autostart, relance unique du lancement au démarrage.
    private const uint WM_APP_TOAST = WM_APP + 4;

    // ── Menu IDs ────────────────────────────────────────────────────
    private const int IDM_TOGGLE = 1001;
    private const int IDM_SITE = 1003;
    private const int IDM_KEYBOARD = 1006;
    private const int IDM_SEARCH = 1007;
    internal const int IDM_BUG = 1009;
    private const int IDM_ONBOARDING = 1010;
    private const int IDM_SETTINGS = 1012;
    internal const int IDM_SUPPORT = 1013;
    internal const int IDM_FEEDBACK = 1014;
    private const int IDM_ABOUT = 1016;
    private const int IDM_EXERCISES = 1023;
    private const int IDM_GUIDE_CHANGES = 1024;
    private const int IDM_GUIDE_PDF = 1025;
    private const int IDM_CARDS = 1026;
    private const int IDM_PAUSE = 1027;
    private const int IDM_PRIVACY = 1028;
    internal const int IDM_DISCORD = 1029;
    private const int IDM_RELEASE_NOTES = 1030;
    private const int IDM_MAINTAINABLE_LAYERS = 1040;
    // Sous-menu Couches (décision S2-3 du 2026-09-02) : une entrée cochable par couche.
    private const int IDM_LAYER_GREEK = 1041;
    private const int IDM_LAYER_CYRILLIC = 1042;
    private const int IDM_LAYER_SCIENTIFIC = 1043;
    internal const int IDM_RATE_STORE = 1031;
    private const int IDM_STATS = 1032;
    private const int IDM_SWITCH_LANGUAGE = 1033;
    private const int IDM_QUIT = 1005;
    // Sous-menu compatibilité jeu (v0.9.7)
    private const int IDM_COMPAT_AUTO = 1020;
    private const int IDM_COMPAT_FORCE_ON = 1021;
    private const int IDM_COMPAT_FORCE_OFF = 1022;
    private const int IDM_COMPAT_INFO = 1034;
    private const int IDM_CHALLENGE = 1035;
    private const int IDM_AUTOSTART = 1036;
#if DEBUG
    private const int IDM_RESET_ONBOARDING = 1015;
#endif

    // ── Shell_NotifyIcon ────────────────────────────────────────────
    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON = 0x02;
    private const uint NIF_TIP = 0x04;
    private const uint NIF_INFO = 0x10;
    private const uint NIIF_INFO = 0x01;
    private const uint NIIF_WARNING = 0x02;
    // Icone fournie par l'application plutot que glyphe systeme (NIIF_ICON_MASK).
    private const uint NIIF_USER = 0x04;
    // Sans ce drapeau, Windows prend la taille SM_CXSMICON et reduit l'icone 32x32.
    private const uint NIIF_LARGE_ICON = 0x20;

    // GetSystemMetrics : taille d'icône système « large », suit l'échelle d'affichage.
    private const int SM_CXICON = 11;
    // Événements balloon reçus via uCallbackMessage (Shell32 ≥ 6.0, donc toujours sur Win10/11)
    private const uint NIN_BALLOONTIMEOUT = 0x0404;
    private const uint NIN_BALLOONUSERCLICK = 0x0405;

    // Deep link vers le volet « Donner un avis » de la fiche Microsoft Store de l'app
    private const string StoreReviewUrl = ProductIdentity.StoreReviewUrl;

    // ── Sollicitation d'avis (v1.2.0) ───────────────────────────────
    // Essai 1 — v1.3.0, décision d'Antoine du 2026-09-21 : le déclencheur n'est plus le
    // calendrier mais la PREUVE D'USAGE de ce qu'AZERTY Global apporte. Le seuil vit dans
    // UsageStats.EnrichedCharsReviewThreshold, avec le compteur qu'il borne.
    //
    // Les deux planchers de jours qu'il remplace (3 jours d'usage distincts, 3 jours
    // écoulés depuis la première frappe) disaient « l'application a servi » par un proxy.
    // Le compte de caractères enrichis le dit directement, et dit en plus QUOI a servi :
    // UsageStats.TryCategorize exclut par construction tout ce que l'AZERTY traditionnel
    // de Windows donne déjà. ⛔ Ne pas les recumuler : la 1.1.0 a fait 0 notation pour 394
    // utilisateurs actifs en juillet 2026, le risque n'est pas de trop demander.
    // Plancher de temps de frappe actif à l'essai 1, en minutes distinctes avec au moins
    // une frappe remappée (UsageStats.TotalActiveMinutes). Décision d'Antoine du
    // 2026-09-21, après observation en VM : le compte de caractères enrichis seul se
    // franchit en une poignée de secondes dès qu'un texte est riche en accents, alors
    // qu'une durée ne se fabrique pas. Les deux gardes sont cumulatives et disent deux
    // choses différentes : QUOI a servi (20 caractères enrichis) et COMBIEN (10 minutes).
    private const int ReviewPromptFirstMinActiveMinutes = 10;
    private const int ReviewPromptSecondActiveDays = 10;
    // Plancher calendaire de l'essai 2 seulement : sept jours au moins après le premier,
    // sinon le second tombe dans la même semaine et se lit comme une relance. C'est un
    // garde-fou d'espacement, pas une preuve d'usage — il survit au changement ci-dessus.
    private const int ReviewPromptSecondMinGapDays = 7;
    // Au-delà, l'utilisateur est considéré comme parti : on ne relance pas un absent.
    private const int ReviewPromptStaleDays = 3;
    // Pas de sollicitation dans la foulée d'une erreur. Internal parce que
    // ReviewSharePrompt applique le même silence au chemin partage : une seule source
    // évite que les deux valeurs divergient.
    internal const int ReviewPromptErrorCooldownHours = 48;

    // ── Menu flags ──────────────────────────────────────────────────
    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_GRAYED = 0x0001;
    private const uint MF_POPUP = 0x0010;
    private const uint MF_CHECKED = 0x0008;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_BOTTOMALIGN = 0x0020;

    // ── MessageBox ──────────────────────────────────────────────────
    private const uint MB_OK = 0x00;
    private const uint MB_ICONERROR = 0x10;
    private const uint MB_ICONINFORMATION = 0x40;

    // ═══════════════════════════════════════════════════════════════
    // Champs d'instance
    // ═══════════════════════════════════════════════════════════════
    private IntPtr _hWnd;
    private IntPtr _hIcon;
    // Handle distinct de _hIcon : UpdateIcon detruit celui du tray a chaque
    // changement d'etat, ce qui invaliderait un hBalloonIcon pointant dessus.
    private IntPtr _hBalloonIcon;
    private Win32.NOTIFYICONDATAW _nid;
    private readonly Win32.WNDPROC _wndProcDelegate; // prevent GC
    private KeyboardHook? _hook;
    private KeyMapper? _mapper;
    private Layout? _layout;
    private VirtualKeyboard? _virtualKeyboard;
    private CharacterSearch? _characterSearch;
    private OnboardingWindow? _onboarding;
    private SettingsWindow? _settings;
    private MaintainableLayersWindow? _maintainableLayersWindow;
    private LayerIndicatorWindow? _layerIndicator;
    private AboutWindow? _about;
    private UsageStatsWindow? _usageStats;
    private ToggleNotification? _toggleNotification;
    private LessonsWindow? _lessons;
    private IntPtr _lastForegroundBeforeTrayMenu;

    // Si conflit layout systeme detecte au demarrage, l'onboarding est differe et n'est
    // affiche qu'apres que l'utilisateur a clique « Garder l'app » dans LayoutConflictWindow.
    // Sinon, ce flag reste a false et l'onboarding s'affiche normalement.
    private bool _pendingOnboardingShow;
    // La sollicitation d'avis était due au démarrage mais la fenêtre d'accueil occupait
    // l'écran : elle est reprise à la fermeture de celle-ci. Jusqu'en v1.1 elle vivait
    // dans le `else` du test d'accueil et se trouvait donc perdue, définitivement, pour
    // tout utilisateur qui revoyait l'accueil à chaque démarrage.
    private bool _reviewPromptDeferred;
    private bool _activationConsent = ConfigManager.ActivationConsent;
    private bool _enabled = ConfigManager.ActivationConsent;
    private DateTimeOffset? _pauseUntilUtc;

    // Nature de la dernière balloon cliquable affichée : le clic sur une balloon est
    // routé selon cette valeur (avis → page d'avis, défi → séance). Invalidé dès qu'une
    // autre balloon la remplace ou qu'elle expire. L'avis ne concerne plus que le canal
    // balloon (hors package, ou repli si le toast COM échoue — ToastActivation, v1.2.0).
    private enum PendingBalloonKind { None, Review, Training, Announcement, AutoStart }
    private PendingBalloonKind _pendingBalloon;

    // Cible de la sollicitation d'avis en cours (canal balloon uniquement : le canal
    // toast transporte sa cible dans ses propres args, survivant au redémarrage).
    private bool _reviewTargetIsStore;

    // Serveur COM d'activation de toast enregistré (packagé uniquement). Si false,
    // la sollicitation d'avis reste une balloon classique (comportement v1.1.0).
    private bool _toastActivatorRegistered;

    // Compatibilité jeux (v0.9.7) : couche de détection foreground + désactivation auto anti-cheat
    private readonly IWin32Api _win32Api = new RealWin32Api();
    private readonly IWindowsTypingHost _typingHost = new AzertyGlobalWindowsTypingHost();
    private ForegroundMonitor? _foregroundMonitor;
    private bool _wasEnabledBeforeAutoDisable;
    private bool _suspendedForCompatibility;
    // Raison pour laquelle le hook a ete configure la derniere fois. Les quatre raisons
    // rendent toutes CompatibilityMode.DisabledAntiCheat, donc passer d'une application
    // suspendue a une autre ne change ni le mode ni _suspendedForCompatibility : sans
    // cette memoire, OnForegroundChanged ne voit aucune transition et laisse le hook
    // configure pour l'application precedente (ecart 8 de la recette VM du 2026-09-19,
    // trou de transition mesure par l'audit du 2026-09-20).
    private CompatibilitySuspendReason _appliedSuspendReason = CompatibilitySuspendReason.None;

    public TrayApplication()
    {
        // Garder une référence au delegate pour empêcher le GC de le collecter
        _wndProcDelegate = WndProcCallback;

        // Créer une fenêtre cachée pour recevoir les messages tray
        var hInstance = Win32.GetModuleHandleW(null);
        var className = ProductIdentity.WindowClass("Wnd");

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            lpszClassName = className
        };
        Win32.RegisterClassExW(ref wc);

        _hWnd = Win32.CreateWindowExW(0, className, ProductIdentity.DisplayName,
            0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hWnd == IntPtr.Zero)
        {
            // Sans fenêtre de messages, rien ne peut fonctionner (tray, timers, hook events).
            Win32.MessageBoxW(IntPtr.Zero,
                L.Tray_WindowCreationError,
                L.Common_ErrorTitle, MB_OK | MB_ICONERROR);
            Win32.PostQuitMessage(1);
            return;
        }

        // Notifications de session (verrouillage/déverrouillage, RDP) → réinstallation du hook.
        // Best-effort : un échec n'empêche pas le fonctionnement nominal.
        Win32.WTSRegisterSessionNotification(_hWnd, Win32.NOTIFY_FOR_THIS_SESSION);

        // Icône tray
        _hIcon = CreateTextIcon("AG", true);
        // L'icône balloon (logo produit) se crée à chaque émission, jamais ici :
        // NIIF_LARGE_ICON exige la taille SM_CXICON du moment, qui suit l'échelle
        // d'affichage (F6 du smoke v1.2.0 — 32 px codé en dur = bulle rejetée à 125 %).

        _nid = new Win32.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATAW>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = ProductIdentity.DisplayName + " v" + Program.Version,
            szInfo = "",
            szInfoTitle = ""
        };
        PrepareTrayIconForAdd(ref _nid);
        Win32.Shell_NotifyIconW(NIM_ADD, ref _nid);

        // Le tooltip du tray garde son texte tant que l'état ne change pas : le rafraîchir
        // explicitement quand la langue change (Paramètres ou onboarding).
        ConfigManager.AppLanguageChanged += _ => UpdateTooltip();

        // Activateur de toast COM (v1.2.0) : Windows livre le clic sur un toast à CE
        // processus au lieu de relancer l'exécutable. Packagé uniquement — hors package,
        // les balloons Shell_NotifyIcon restent le canal (aucun AUMID enregistré).
        // Le callback arrive sur un thread RPC COM → re-routage PostMessage vers le
        // thread UI avant toute action.
        if (ConfigManager.IsPackaged)
        {
            _toastActivatorRegistered = ToastActivation.Register();
            if (_toastActivatorRegistered)
            {
                ToastActivation.Activated += args =>
                {
                    if (args.Contains("action=review", StringComparison.Ordinal))
                        Win32.PostMessageW(_hWnd, WM_APP_TOAST,
                            args.Contains("target=feedback", StringComparison.Ordinal) ? (IntPtr)2 : (IntPtr)1,
                            IntPtr.Zero);
                    else if (args.Contains("action=autostart", StringComparison.Ordinal))
                        Win32.PostMessageW(_hWnd, WM_APP_TOAST, (IntPtr)3, IntPtr.Zero);
                };
            }
        }

        // Charger le layout et démarrer le hook
        try
        {
            LoadAndStart();
            // Refléter l'état réel dans le tooltip dès le démarrage : le szTip posé au
            // NIM_ADD ne portait que « AZERTY Global vX.Y.Z » (sans « — Actif »), et
            // UpdateTooltip n'était sinon appelé qu'au premier changement d'état/langue
            // (constat smoke test 2026-07-17).
            UpdateTooltip();
            CheckSystemLayout(); // peut declencher LayoutConflictWindow et set _layoutPopupOpen

            // Démarrer l'horloge « premier lancement » dès maintenant (y compris quand
            // l'onboarding s'affiche) : la sollicitation d'avis à J+7 compte depuis le
            // vrai premier lancement, pas depuis la fin de l'onboarding.
            ConfigManager.EnsureFirstRunTimestamp();

            // Premier lancement : onboarding. Lancements suivants : notification balloon.
#if DEBUG
            // En debug, toujours afficher l'onboarding pour faciliter les tests
            bool shouldShowOnboarding = true;
#else
            // Afficher le wizard tant que ex1+ex2+ex3 ne sont pas tous complétés,
            // SAUF si l'utilisateur a explicitement désactivé l'option dans les Settings
            // (priorité au choix manuel — cf. Q2 du plan UX 2026-05-02).
            bool shouldShowOnboarding = !_activationConsent || (ConfigManager.ShowOnboardingAtStartup
                                     && ConfigManager.LearningMaxStepCompleted < 3);
#endif
            if (shouldShowOnboarding)
            {
                // La sollicitation d'avis n'est plus subordonnée à l'absence d'accueil :
                // elle est différée, pas annulée (cf. _reviewPromptDeferred).
                _reviewPromptDeferred = true;
                if (_layoutPopupOpen)
                {
                    // Conflit layout systeme detecte : la mini-fenetre LayoutConflictWindow
                    // doit prendre le dessus. On differe l'affichage de l'onboarding au callback
                    // « Garder l'app ». Si l'utilisateur choisit « Quitter », l'onboarding ne
                    // s'affiche jamais (pas de flash visuel inutile).
                    _pendingOnboardingShow = true;
                }
                else
                {
                    ShowOnboardingNow();
                }
            }
            else if (!MaybeShowReviewPrompt() && !MaybeShowChallengeAnnouncement()
                     && !MaybeShowAutoStartNudge())
            {
                Win32.SetTimer(_hWnd, (UIntPtr)TIMER_STARTUP_BALLOON, STARTUP_BALLOON_DELAY_MS, IntPtr.Zero);
            }
        }
        catch (Exception ex)
        {
            // v1.1 (reliquat v1.0) : ne plus afficher ex.Message brut à l'utilisateur —
            // le détail technique va dans error.log, l'utilisateur reçoit un message
            // clair avec un chemin d'action (support).
            ConfigManager.Log("TrayApplication ctor", ex);
            Win32.MessageBoxW(IntPtr.Zero,
                L.Tray_StartupError,
                L.Common_ErrorTitle, MB_OK | MB_ICONERROR);
            Win32.PostQuitMessage(1);
        }
    }

    private void LoadAndStart()
    {
        var layout = LayoutLoader.LoadFromResource();
        _layout = layout;
        _mapper = new KeyMapper(layout, _win32Api, _typingHost);
        _mapper.StateChanged += OnStateChanged;
        _mapper.ToggleRequested += OnToggleShortcut;
        _hook = new KeyboardHook(_mapper, _typingHost);
        _hook.RawKeyDown += OnKeyPressed;
        _hook.SearchRequested += () => Win32.PostMessageW(_hWnd, WM_APP_SEARCH, IntPtr.Zero, IntPtr.Zero);
        _hook.VirtualKeyboardRequested += () => Win32.PostMessageW(_hWnd, WM_APP_VKBD, IntPtr.Zero, IntPtr.Zero);
        _hook.LayoutMayHaveChanged += OnLayoutMayHaveChanged;
        ApplyHookState();
        if (_activationConsent) _hook.Install();

        // Synchroniser l'etat interne (CapsLock + modificateurs Shift/Ctrl/Alt) avec l'etat
        // reel du systeme. Cas critique : si l'utilisateur lance l'app pendant qu'un jeu
        // tient des touches (ex. Maj pour sprinter), le keydown initial a ete manque par
        // le hook. Sans ce sync, les frappes suivantes utilisent un etat modifs faux.
        _mapper.SyncState();

        // Le moteur ne lit aucune configuration : lui pousser les réglages des
        // couches maintenables au démarrage, puis à chaque enregistrement.
        PushMaintainableLayerSettings();

        try
        {
            _characterSearch = new CharacterSearch(new TextInsertionService(_mapper.TryEmitText,
                canInsert: () => !ShouldBlockHookCompletely && _foregroundMonitor?.IsSecureInput != true,
                refreshTarget: () => _foregroundMonitor?.Recompute()));
            _characterSearch.InsertionIncomplete += () =>
                ShowBalloon(L.Layers_InsertIncompleteTitle, L.Layers_InsertIncompleteBody);
            _characterSearch.SelectionChanged += OnSearchSelectionChanged;
            _characterSearch.FallbackCopied += character =>
                ShowBalloon(L.Layers_InsertFallbackTitle, L.Layers_InsertFallbackBody(character));
        }
        catch (Exception ex)
        {
            ConfigManager.Log("Init CharacterSearch", ex);
            _characterSearch = null;
        }

        try
        {
            _virtualKeyboard = new VirtualKeyboard(layout, _characterSearch?.GetCharacterNames());
        }
        catch (Exception ex)
        {
            ConfigManager.Log("Init VirtualKeyboard", ex);
            _virtualKeyboard = null;
        }

        try
        {
            _layerIndicator = new LayerIndicatorWindow();
        }
        catch (Exception ex)
        {
            ConfigManager.Log("Init LayerIndicator", ex);
            _layerIndicator = null;
        }

        Win32.SetForegroundWindow(_hWnd);

        // Compatibilité jeux : instancier le ForegroundMonitor APRÈS création de la
        // fenêtre tray (HWND requis pour le SetTimer debounce). Mode dégradé si échec.
        try
        {
            _foregroundMonitor = new ForegroundMonitor(_win32Api, _hWnd, _typingHost);
            _foregroundMonitor.ForegroundChanged += OnForegroundChanged;
            // Résultat UIA arrivé après le budget d'attente : le snapshot sécurisé
            // est périmé, replanifier un Recompute sur la boucle de messages.
            SecureInputDetector.ResultChangedLate += OnSecureInputLateResult;
            _mapper.SetForegroundMonitor(_foregroundMonitor);
            // Le ctor de ForegroundMonitor a deja calcule un snapshot initial avant
            // l'abonnement ci-dessus : appliquer cet etat au hook immediatement.
            OnForegroundChanged();
        }
        catch (Exception ex)
        {
            ConfigManager.Log("ForegroundMonitor init", ex);
            _foregroundMonitor = null;
        }

        // Audit overrides invalides : un override forceOn sur un process désormais
        // anti-cheat (liste mise à jour par release) doit être supprimé pour la sécurité utilisateur.
        AuditCompatibilityOverridesAtStartup();
    }

    /// <summary>
    /// Au démarrage, scanner les overrides utilisateur : si un override forceOn pointe
    /// sur un process désormais listé anti-cheat (mise à jour de la liste hardcodée),
    /// supprimer l'override + bulle d'avertissement (bypass NotificationsEnabled car
    /// c'est une notification de sécurité, pas de confort).
    /// </summary>
    private void AuditCompatibilityOverridesAtStartup()
    {
        try
        {
            var overrides = ConfigManager.GetAllCompatibilityOverrides();
            var conflicting = new List<string>();
            foreach (var (proc, mode) in overrides)
            {
                if (mode == "forceOn" && GameRegistry.IsAntiCheatProcess(proc, null))
                {
                    ConfigManager.SetCompatibilityOverride(proc, null);
                    conflicting.Add(proc);
                    // Audit sécu 2026-05 SEV-A1-02 : anonymisation du process name dans le log.
                    ConfigManager.LogCompatCriticalEvent("OverrideInvalidCleanup",
                        $"removed forceOn for '{ConfigManager.AnonymizeProcessName(proc)}' (now anti-cheat-listed)");
                }
            }
            if (conflicting.Count > 0)
            {
                var list = string.Join(", ", conflicting);
                // Bypass NotificationsEnabled : on utilise Shell_NotifyIconW directement
                ShowSecurityBalloon(L.Tray_GameCompatDisabledTitle, L.Tray_GameCompatDisabledBody(list));
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("AuditCompatibilityOverridesAtStartup", ex);
        }
    }

    // Timer IDs pour la réinstallation du hook après démarrage
    private const uint TIMER_REHOOK = 9001;
    private const uint TIMER_REHOOK_2 = 9002;
    private const uint TIMER_REHOOK_3 = 9003;
    // Watchdog : Windows peut décrocher silencieusement un hook LL trop lent
    // (LowLevelHooksTimeout sous charge, reprise de veille). Réinstallation périodique
    // à identité constante — coût négligeable, récupération garantie ≤ 60 s.
    private const uint TIMER_HOOK_WATCHDOG = 9004;
    private const uint HOOK_WATCHDOG_INTERVAL_MS = 60_000;
    // Statistiques locales d'usage (v1.1) : sauvegarde différée, jamais sur le chemin
    // de la frappe. Cf. UsageStats.Flush.
    // AG130-10 : sonde de silence. Le watchdog ci-dessus repare, mais a l'aveugle et
    // en 60 s au pire — pendant lesquelles l'utilisateur tape en AZERTY natif sans
    // qu'un mot soit dit. La sonde compare le dernier rappel recu du hook a une
    // fenetre pendant laquelle une frappe a ete vue PAR AILLEURS (GetAsyncKeyState) :
    // une frappe sans rappel, c'est un hook decroche. Detection en ~4 s au lieu de 60,
    // et une trace dans le journal de compatibilite.
    private const uint TIMER_HOOK_PROBE = 9007;
    private const uint HOOK_PROBE_INTERVAL_MS = 2_000;
    private readonly HookSilenceWatchdog _hookSilence = new();
    private long _hookProbeWindowStartTicks = Environment.TickCount64;

    private const uint TIMER_STATS_FLUSH = 9005;
    private const uint STATS_FLUSH_INTERVAL_MS = 5 * 60_000;
    // Chien de garde du snapshot foreground (Écart 7, recette VM du 2026-09-19).
    // Un Alt+Tab peut laisser ForegroundMonitor figé sur la fenêtre du sélecteur :
    // GetEmitContext refuse alors toute émission, et rien ne le répare tant qu'aucun
    // événement de focus n'arrive — dans le Bloc-notes, qui n'en émet plus une fois
    // au premier plan, le remappage restait mort jusqu'à un aller-retour manuel.
    // Un tick ne coûte qu'un GetForegroundWindow ; le Recompute complet n'a lieu que
    // si le snapshot est réellement périmé. 250 ms est sous le seuil de perception
    // d'une frappe interrompue, sans peser sur la boucle de messages.
    private const uint TIMER_FOREGROUND_WATCHDOG = 9006;
    private const uint FOREGROUND_WATCHDOG_INTERVAL_MS = 250;
    private const uint TIMER_SINGLECLICK = 9010;
    private const uint TIMER_LAYOUT_CHECK = 9020;
    private const uint TIMER_PAUSE = 9030;
    // Balloon « Actif » du démarrage : émis par timer one-shot, jamais dans la foulée
    // du NIM_ADD — le shell jette en silence une bulle demandée pendant que l'entrée
    // tray s'enregistre encore (constaté au smoke v1.2.0 du 2026-08-24 : cette bulle
    // n'apparaissait jamais, quand les mêmes bulles émises plus tard s'affichent).
    private const uint TIMER_STARTUP_BALLOON = 9040;
    private const uint STARTUP_BALLOON_DELAY_MS = 1500;
    // Sollicitation d'avis au fil de la frappe (v1.3.0). Le chemin de démarrage ne voit
    // l'utilisateur qu'au lancement, c'est-à-dire au pire moment : il n'a encore rien
    // tapé. Ce timer attend qu'il vienne de franchir le seuil de caractères enrichis,
    // puis qu'il ait cessé de taper — on ne coupe pas une phrase en cours.
    //
    // ⛔ Le hook clavier ne pose PAS ce timer : il est sur le chemin critique de la
    // frappe et UsageStats.RecordEmittedText s'y interdit toute I/O. Le hook ne fait
    // qu'armer un drapeau en mémoire ; c'est ce tick, sur la boucle de messages, qui
    // lit le drapeau et décide. Le coût d'un tick est deux lectures d'entier sous lock.
    //
    // Le timer ne tourne que tant qu'une sollicitation reste possible, et se tue dès
    // qu'elle ne l'est plus. Résolution : l'affichage tombe entre 15 et 20 s après la
    // dernière frappe, ce qui est sans importance à cette échelle.
    private const uint TIMER_REVIEW_QUIET = 9050;
    private const uint REVIEW_QUIET_POLL_MS = 5_000;
    private const long REVIEW_QUIET_SILENCE_MS = 15_000;

    // Message TaskbarCreated (Explorer restart / chargement tardif au boot)
    private readonly uint _wmTaskbarCreated = Win32.RegisterWindowMessageW("TaskbarCreated");

    private bool IsPaused => _pauseUntilUtc.HasValue;
    private bool ShouldBlockHookCompletely => !_activationConsent || IsPaused || _suspendedForCompatibility;
    private bool ShouldProcessHook => _activationConsent && _enabled && !IsPaused && !_suspendedForCompatibility;

    // R3 : suspension décidée par l'utilisateur lui-même (case « forcer la
    // désactivation »), par opposition à une suspension imposée (anti-cheat, accès
    // distant, premier plan inconnu). Miroir exact de ShouldDetectShortcutsWhileBlocked :
    // les raccourcis restent détectés dans ce cas, donc leurs fenêtres doivent vivre.
    private bool IsUserOverrideSuspension =>
        ShouldServeSearchWhileBlocked(
            _suspendedForCompatibility,
            _foregroundMonitor?.CurrentSuspendReason ?? CompatibilitySuspendReason.None);

    /// <summary>
    /// R3 : la recherche de caractères suit exactement la détection des raccourcis sous
    /// suspension — seule la suspension choisie par l'utilisateur la sert. Une suspension
    /// imposée (anti-cheat, accès distant, premier plan inconnu) protège quelque chose ;
    /// un choix de confort n'a rien à protéger.
    ///
    /// ⚠️ Fonction pure et non la propriété : la propriété dépend de _foregroundMonitor,
    /// que la suite ne sait pas construire. C'est le trou du § 3 de la revue du 21/09.
    /// </summary>
    private static bool ShouldServeSearchWhileBlocked(
        bool suspendedForCompatibility, CompatibilitySuspendReason reason) =>
        suspendedForCompatibility && reason == CompatibilitySuspendReason.UserOverride;

    private void ApplyHookState(bool syncWhenActive = false, bool preservePendingDeadKey = false)
    {
        if (_mapper != null) _mapper.EmissionPaused = IsPaused;
        if (_hook == null) return;
        _hook.PassThroughAll = ShouldBlockHookCompletely;
        // Pause volontaire : garder la détection des raccourcis pour permettre la reprise
        // au clavier. Jamais pendant une désactivation anti-cheat (inertie totale voulue).
        //
        // Écart 8 de la recette VM du 2026-09-19 : une application que l'utilisateur a
        // lui-même marquée « désactivée » tombait dans cette même inertie totale, si bien
        // que Ctrl+Maj+W n'ouvrait pas la recherche de caractères et atteignait
        // l'application, qui y lisait Ctrl+W et fermait sa fenêtre — le geste attendu
        // ouvre une fenêtre, le geste réel détruisait le travail en cours. L'inertie
        // totale protège l'anti-cheat, l'accès distant et le premier plan inconnu ; un
        // choix de confort de l'utilisateur n'a rien à protéger. Le remappage, lui, reste
        // éteint : Enabled vaut toujours ShouldProcessHook, donc seule la détection des
        // raccourcis revient.
        _hook.ShortcutsWhilePassThrough = _activationConsent && ShouldDetectShortcutsWhileBlocked(
            IsPaused,
            _suspendedForCompatibility,
            _foregroundMonitor?.CurrentSuspendReason ?? CompatibilitySuspendReason.None);
        _hook.Enabled = ShouldProcessHook;
        ApplyWindowInputState();
        if (syncWhenActive && ShouldProcessHook)
            _mapper?.SyncState(preservePendingDeadKey);
    }

    private void ApplyWindowInputState()
    {
        bool paused = ShouldBlockHookCompletely;
        // R3 : sous « forcer la désactivation », le raccourci de recherche est toujours
        // détecté ; geler la saisie de la fenêtre ouvrirait une fenêtre inerte.
        _characterSearch?.SetInputPaused(paused && !IsUserOverrideSuspension);
        _lessons?.SetInputPaused(paused);
        _settings?.SetInputPaused(_activationConsent && paused);
        // Le consentement doit rester accessible avant que le moteur soit actif.
        _onboarding?.SetInputPaused(_activationConsent && paused);
    }

    /// <summary>Boucle de messages principale.</summary>
    public void Run()
    {
        // Le hook LL peut ne pas recevoir de callbacks tant que la boucle de messages
        // n'est pas active, ou au démarrage de Windows quand le système n'est pas encore
        // prêt. On planifie plusieurs réinstallations progressives pour couvrir les deux cas.
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REHOOK, 500, IntPtr.Zero);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REHOOK_2, 3000, IntPtr.Zero);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REHOOK_3, 8000, IntPtr.Zero);
        // Watchdog périodique (non tué : se répète tant que l'app vit)
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_HOOK_WATCHDOG, HOOK_WATCHDOG_INTERVAL_MS, IntPtr.Zero);
        // Sonde de silence du hook (non tué : périodique) — AG130-10
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_HOOK_PROBE, HOOK_PROBE_INTERVAL_MS, IntPtr.Zero);
        // Sauvegarde différée des statistiques locales d'usage (non tué : périodique)
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_STATS_FLUSH, STATS_FLUSH_INTERVAL_MS, IntPtr.Zero);
        // Sollicitation d'avis au fil de la frappe : seulement s'il en reste une à faire.
        if (ReviewPromptStillPossible())
            Win32.SetTimer(_hWnd, (UIntPtr)TIMER_REVIEW_QUIET, REVIEW_QUIET_POLL_MS, IntPtr.Zero);
        // Chien de garde du snapshot foreground (non tué : périodique)
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_FOREGROUND_WATCHDOG, FOREGROUND_WATCHDOG_INTERVAL_MS, IntPtr.Zero);
        // Chargement anticipé de usage-stats.json sur le thread UI : la première frappe
        // remappée ne doit déclencher aucune I/O dans le callback du hook.
        UsageStats.Preload();

        int ret;
        while ((ret = Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0)) != 0)
        {
            if (ret == -1) break; // Erreur fatale — sortir de la boucle
            // AG130-40 : la fenetre inscrite prend la navigation de dialogue. Consomme,
            // le message ne doit surtout pas repartir en dispatch — il serait livre deux fois.
            if (DialogNavigation.TryRoute(ref msg)) continue;
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Gestion des messages Windows
    // ═══════════════════════════════════════════════════════════════
    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_TRAYICON:
                    var mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
                    if (mouseMsg == Win32.WM_RBUTTONUP)
                        ShowContextMenu();
                    else if (mouseMsg == Win32.WM_LBUTTONUP)
                        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_SINGLECLICK, Win32.GetDoubleClickTime(), IntPtr.Zero);
                    else if (mouseMsg == Win32.WM_LBUTTONDBLCLK)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_SINGLECLICK);
                        if (ShouldProcessHook) _virtualKeyboard?.Toggle();
                    }
                    else if (mouseMsg == NIN_BALLOONUSERCLICK)
                    {
                        var kind = _pendingBalloon;
                        _pendingBalloon = PendingBalloonKind.None;
                        if (kind == PendingBalloonKind.Review)
                            OpenReviewTarget(_reviewTargetIsStore);
                        else if (kind == PendingBalloonKind.Training)
                        {
                            TrainingReminders.MarkReminderClicked();
                            ShowChallengeWindow();
                        }
                        else if (kind == PendingBalloonKind.Announcement)
                            ShowChallengeWindow(); // le défi s'ouvre sans opt-in depuis le 2026-08-16 :
                                                   // l'annonce mène à la séance, plus aux Paramètres
                        else if (kind == PendingBalloonKind.AutoStart)
                            EnableAutoStartFromPrompt();
                    }
                    else if (mouseMsg == NIN_BALLOONTIMEOUT)
                    {
                        if (_pendingBalloon == PendingBalloonKind.Training)
                            TrainingReminders.MarkReminderIgnored(); // 3 ignorés → arrêt définitif
                        _pendingBalloon = PendingBalloonKind.None;
                    }
                    return IntPtr.Zero;

                case WM_APP_TOAST:
                    // Clic sur un toast (activation COM re-routée du thread RPC).
                    if (wParam == (IntPtr)1)
                        OpenReviewTarget(toStore: true);
                    else if (wParam == (IntPtr)2)
                        OpenReviewTarget(toStore: false);
                    else if (wParam == (IntPtr)3)
                        EnableAutoStartFromPrompt();
                    return IntPtr.Zero;

                case WM_APP_SEARCH:
                    // R3, écart 8 : le raccourci arrive jusqu'ici sous suspension choisie
                    // (ShortcutsWhilePassThrough) ; annoncer « Désactivé » contredisait le
                    // Changelog. Le remappage, lui, reste éteint.
                    if ((ShouldProcessHook || IsUserOverrideSuspension) &&
                        _mapper?.AdvancedFeaturesSuppressed != true)
                        _characterSearch?.Toggle();
                    else if (_mapper?.AdvancedFeaturesSuppressed == true)
                        ShowBalloon(L.Layers_SearchSecureTitle, L.Layers_SearchSecureBody);
                    else if (IsPaused)
                        ShowBalloon(L.Tray_PausedTitle, L.Tray_PausedBalloonBody);
                    else
                        ShowBalloon(L.Tray_DisabledTitle, L.Tray_DisabledBalloonBody);
                    return IntPtr.Zero;

                case WM_APP_VKBD:
                    if (ShouldProcessHook)
                        _virtualKeyboard?.Toggle();
                    else if (IsPaused)
                        ShowBalloon(L.Tray_PausedTitle, L.Tray_PausedBalloonBody);
                    else
                        ShowBalloon(L.Tray_DisabledTitle, L.Tray_DisabledBalloonBody);
                    return IntPtr.Zero;

                case WM_APP_STATE:
                    Interlocked.Exchange(ref _stateRefreshQueued, 0);
                    ApplyStateChange();
                    return IntPtr.Zero;

                case WM_APP_SECURE_RECHECK:
                    // Recompute relit IsForegroundPasswordField (valeur UIA désormais
                    // fraîche) et ne notifie que si l'état effectif change.
                    _foregroundMonitor?.Recompute();
                    return IntPtr.Zero;

                case Win32.WM_COMMAND:
                    switch (wParam.ToInt32() & 0xFFFF)
                    {
                        case IDM_TOGGLE: OnToggle(); break;
                        case IDM_PAUSE:
                            if (IsPaused)
                                StopPause(expired: false);
                            else
                                ShowPauseDialogAndStart();
                            break;
                        case IDM_KEYBOARD:
                            if (ShouldProcessHook || _virtualKeyboard?.IsVisible == true)
                            {
                                _virtualKeyboard?.UsePreferredMonitorWindow(_lastForegroundBeforeTrayMenu);
                                _virtualKeyboard?.Toggle();
                            }
                            break;
                        case IDM_SEARCH:
                            if (_characterSearch?.IsVisible == true)
                                _characterSearch.Hide();
                            else if (ShouldProcessHook && _mapper?.AdvancedFeaturesSuppressed != true)
                                _characterSearch?.Show(_lastForegroundBeforeTrayMenu);
                            else if (_mapper?.AdvancedFeaturesSuppressed == true)
                                ShowBalloon(L.Layers_SearchSecureTitle, L.Layers_SearchSecureBody);
                            break;
                        case IDM_SETTINGS:
                            ShowSettingsWindow();
                            break;
                        case IDM_LAYER_GREEK:
                        case IDM_LAYER_CYRILLIC:
                        case IDM_LAYER_SCIENTIFIC:
                            ToggleMaintainableLayer(wParam.ToInt32() & 0xFFFF);
                            break;
                        case IDM_MAINTAINABLE_LAYERS:
                            if (_maintainableLayersWindow == null)
                            {
                                _maintainableLayersWindow = new MaintainableLayersWindow();
                                _maintainableLayersWindow.SettingsChanged += OnMaintainableLayerSettingsChanged;
                            }
                            _maintainableLayersWindow.Show();
                            break;
                        case IDM_GUIDE_CHANGES: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/guide"), null, null, 1); break;
                        case IDM_RELEASE_NOTES: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/nouveautes"), null, null, 1); break;
                        case IDM_GUIDE_PDF: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/assets/Prise_en_main_AZERTY_Global.pdf"), null, null, 1); break;
                        case IDM_CARDS: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/guide#cartes"), null, null, 1); break;
                        case IDM_PRIVACY: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/mentions-legales#confidentialite-securite"), null, null, 1); break;
                        case IDM_DISCORD: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.DiscordInviteUrl, null, null, 1); break;
                        case IDM_SITE: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.SiteBaseUrl, null, null, 1); break;
                        case IDM_FEEDBACK: OpenFeedback("tray-menu"); break;
                        case IDM_RATE_STORE: OnRateStoreFromMenu(); break;
                        case IDM_AUTOSTART: ToggleAutoStart(); break;
                        case IDM_BUG: OnReportBug(); break;
                        case IDM_SUPPORT: Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/soutien"), null, null, 1); break;
                        case IDM_ONBOARDING:
                            if (_onboarding == null)
                            {
                                _onboarding = CreateOnboardingWindow();
                            }
                            else
                            {
                                ConfigureOnboardingWindow(_onboarding);
                            }
                            ApplyWindowInputState();
                            _onboarding.Show();
                            break;
                        case IDM_SWITCH_LANGUAGE:
                        {
                            // Bascule directe FR↔EN. L.Language d'abord : les abonnés à
                            // AppLanguageChanged (tooltip tray, fenêtres Paramètres/bienvenue
                            // ouvertes) lisent L.* au moment de l'événement.
                            string lang = L.IsEnglish ? "fr" : "en";
                            L.Language = lang;
                            ConfigManager.SetAppLanguage(lang);
                            break;
                        }
                        case IDM_ABOUT:
                            // Titre, liens et bouton sont créés au constructeur : recréer la
                            // fenêtre si la langue a changé depuis (fenêtre masquée uniquement,
                            // même pattern que ShowLessonsWindow).
                            if (_about != null && !_about.IsVisible && _about.UiLanguage != L.Language)
                            {
                                _about.Dispose();
                                _about = null;
                            }
                            _about ??= new AboutWindow();
                            _about.Show();
                            break;
                        case IDM_STATS:
                            if (_usageStats != null && !_usageStats.IsVisible && _usageStats.UiLanguage != L.Language)
                            {
                                _usageStats.Dispose();
                                _usageStats = null;
                            }
                            _usageStats ??= new UsageStatsWindow();
                            _usageStats.StatsShared = OnStatsShared;
                            _usageStats.Show();
                            break;
                        case IDM_EXERCISES:
                            ShowLessonsWindow();
                            break;
                        case IDM_CHALLENGE:
                            ShowChallengeWindow();
                            break;
                        case IDM_COMPAT_AUTO:
                            ApplyCompatibilityOverride(null);
                            break;
                        case IDM_COMPAT_FORCE_ON:
                            ApplyCompatibilityOverride("forceOn");
                            break;
                        case IDM_COMPAT_FORCE_OFF:
                            ApplyCompatibilityOverride("forceOff");
                            break;
                        case IDM_COMPAT_INFO:
                            Win32.MessageBoxW(_hWnd, L.Tray_CompatInfoBody,
                                L.Tray_CompatInfoTitle, MB_OK | MB_ICONINFORMATION);
                            break;
#if DEBUG
                        case IDM_RESET_ONBOARDING:
                            ConfigManager.LogCrashTraceDebug("IDM_RESET_ONBOARDING: enter");
                            if (_onboarding == null)
                            {
                                ConfigManager.LogCrashTraceDebug("IDM_RESET_ONBOARDING: creating new OnboardingWindow");
                                _onboarding = CreateOnboardingWindow();
                            }
                            else
                            {
                                ConfigureOnboardingWindow(_onboarding);
                            }
                            ConfigManager.LogCrashTraceDebug($"IDM_RESET_ONBOARDING: Mapper={(_onboarding.Mapper != null)}, Hook={(_onboarding.Hook != null)}, AppLayout={(_onboarding.AppLayout != null)}");
                            _onboarding.ResetState();
                            ApplyWindowInputState();
                            ConfigManager.LogCrashTraceDebug("IDM_RESET_ONBOARDING: ResetState done, calling Show");
                            _onboarding.Show();
                            ConfigManager.LogCrashTraceDebug("IDM_RESET_ONBOARDING: Show returned");
                            break;
#endif
                        case IDM_QUIT: OnExit(); break;
                    }
                    return IntPtr.Zero;

                case Win32.WM_TIMER:
                    var timerId = (uint)wParam.ToInt64();
                    if (timerId == TIMER_REHOOK || timerId == TIMER_REHOOK_2 || timerId == TIMER_REHOOK_3)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)timerId);
                        ReinstallHook();
                    }
                    else if (timerId == TIMER_SINGLECLICK)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_SINGLECLICK);
                        ShowContextMenu();
                    }
                    else if (timerId == TIMER_LAYOUT_CHECK)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_LAYOUT_CHECK);
                        CheckForegroundLayout();
                    }
                    else if (timerId == TIMER_PAUSE)
                    {
                        if (_pauseUntilUtc.HasValue && DateTimeOffset.UtcNow >= _pauseUntilUtc.Value)
                            StopPause(expired: true);
                        else
                            UpdateTooltip();
                    }
                    else if (timerId == TIMER_STARTUP_BALLOON)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_STARTUP_BALLOON);
                        // Si l'état a bougé entre-temps (suspension, pause), la bulle
                        // d'état correspondante est déjà passée : ne pas la recouvrir.
                        if (ShouldProcessHook)
                            ShowBalloon(L.Tray_ActiveTitle, L.Tray_ActiveBalloonBody);
                    }
                    else if (timerId == TIMER_HOOK_WATCHDOG)
                    {
                        // Timer récurrent (pas de KillTimer) : réinstallation à identité
                        // constante, sans nudge foreground (correctif audit 2026-07 M2).
                        ReinstallHook(nudgeForeground: false);
                    }
                    else if (timerId == TIMER_HOOK_PROBE)
                    {
                        // Timer récurrent : sonde de silence du hook (AG130-10).
                        ProbeHookSilence();
                    }
                    else if (timerId == TIMER_STATS_FLUSH)
                    {
                        // Timer récurrent : sauvegarde différée des statistiques locales.
                        UsageStats.Flush();
                        // Rappel Défi du jour (v1.2.0) : décision pure à chaque tick, tous
                        // les gardes (opt-in, un par jour, fenêtre horaire) sont dedans.
                        MaybeShowTrainingReminder();
                    }
                    else if (timerId == TIMER_REVIEW_QUIET)
                    {
                        // Timer récurrent tant qu'une sollicitation reste possible.
                        MaybeShowReviewAfterQuietTyping();
                    }
                    else if (timerId == TIMER_FOREGROUND_WATCHDOG)
                    {
                        // Timer récurrent (pas de KillTimer). Le test est une seule
                        // lecture GetForegroundWindow ; le recalcul complet n'a lieu
                        // que sur un snapshot réellement périmé (Écart 7).
                        if (_foregroundMonitor?.IsSnapshotStale == true)
                            _foregroundMonitor.Recompute();
                    }
                    else if (timerId == ForegroundMonitor.TIMER_FOREGROUND_DEBOUNCE)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)ForegroundMonitor.TIMER_FOREGROUND_DEBOUNCE);
                        _foregroundMonitor?.Recompute();
                    }
                    return IntPtr.Zero;

                case Win32.WM_INPUTLANGCHANGE:
                    // Le layout système a changé (ex: Win+Espace) → invalider le cache HKL
                    _foregroundMonitor?.Recompute();
                    break; // laisser DefWindowProc traiter aussi

                case Win32.WM_POWERBROADCAST:
                    // Reprise de veille : le hook LL a pu être décroché silencieusement
                    // pendant la transition (correctif audit 2026-07 M2).
                    {
                        int powerEvent = wParam.ToInt32();
                        if (powerEvent == Win32.PBT_APMRESUMEAUTOMATIC || powerEvent == Win32.PBT_APMRESUMESUSPEND)
                            ReinstallHook(nudgeForeground: false);
                    }
                    break; // laisser DefWindowProc répondre TRUE

                case Win32.WM_WTSSESSION_CHANGE:
                    // Déverrouillage / (re)connexion console ou RDP → réinstaller le hook
                    // (correctif audit 2026-07 M2 ; item TO-DO « RDP » v1.0).
                    {
                        int sessionEvent = wParam.ToInt32();
                        if (sessionEvent == Win32.WTS_SESSION_UNLOCK ||
                            sessionEvent == Win32.WTS_CONSOLE_CONNECT ||
                            sessionEvent == Win32.WTS_REMOTE_CONNECT)
                            ReinstallHook(nudgeForeground: false);
                    }
                    return IntPtr.Zero;

                case Win32.WM_QUERYENDSESSION:
                    return (IntPtr)1; // ne jamais bloquer l'arrêt/déconnexion

                case Win32.WM_ENDSESSION:
                    // Arrêt/déconnexion confirmé : nettoyage coopératif (hook, tray, keyup
                    // synthétiques) — permet aussi aux mises à jour MSIX de se dérouler
                    // proprement (correctif audit 2026-07 m7).
                    if (wParam != IntPtr.Zero)
                        Cleanup();
                    return IntPtr.Zero;

                case Win32.WM_DESTROY:
                    Win32.PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    // TaskbarCreated : Explorer a (re)démarré — réenregistrer l'icône et le hook
                    if (_wmTaskbarCreated != 0 && msg == _wmTaskbarCreated)
                    {
                        PrepareTrayIconForAdd(ref _nid);
                        Win32.Shell_NotifyIconW(NIM_ADD, ref _nid);
                        ReinstallHook();
                        return IntPtr.Zero;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Réinstalle le keyboard hook (nouveau SetWindowsHookEx) SANS changer l'identité de
    /// l'objet KeyboardHook : les abonnements RawKeyDown de l'onboarding, des leçons et
    /// du clavier virtuel restent valides (correctif audit 2026-07 M1). L'état
    /// PassThroughAll/Enabled est porté par l'objet et n'a pas besoin d'être réappliqué.
    /// </summary>
    /// <param name="nudgeForeground">
    /// true (timers de démarrage, TaskbarCreated) : activer la fenêtre pour que le thread
    /// soit associé au système d'input — sans cet appel, le hook LL fraîchement posé peut ne
    /// pas recevoir d'événements au boot. false (watchdog, reprise de veille, session) :
    /// ne jamais toucher au foreground en cours de session.
    /// </param>
    /// <summary>
    /// AG130-10 — une frappe vue sans rappel du hook vaut hook décroché.
    ///
    /// La sonde ne parle que quand le hook est censé travailler : en pause, suspendu
    /// pour compatibilité ou désactivé, le silence est voulu et ne prouve rien. Le
    /// compteur de série est alors remis à zéro pour qu'une série entamée avant la
    /// pause ne se poursuive pas au travers.
    /// </summary>
    private void ProbeHookSilence()
    {
        if (!_activationConsent) return;
        long windowStart = _hookProbeWindowStartTicks;
        _hookProbeWindowStartTicks = Environment.TickCount64;

        if (_hook == null || !ShouldProcessHook)
        {
            // Consommer quand même les bits de GetAsyncKeyState : sans cela, les
            // touches pressées pendant la pause seraient rendues à la première sonde
            // d'après et feraient croire à une frappe dans une fenêtre vide.
            KeyboardHook.AnyKeyPressedSinceLastPoll();
            _hookSilence.Suspend();
            return;
        }

        bool keyPressObserved = KeyboardHook.AnyKeyPressedSinceLastPoll();
        if (!_hookSilence.Observe(keyPressObserved, _hook.LastCallbackTicks, windowStart))
            return;

        ConfigManager.LogCompatCriticalEvent("HookSilentlyDetached",
            $"action=reinstall ; probes={HookSilenceWatchdog.DefaultConsecutiveProbes}" +
            $" ; windowMs={HOOK_PROBE_INTERVAL_MS} ; reinstalls={_hookSilence.ReinstallCount}");

        // Sans nudge foreground : la sonde tire en pleine frappe de l'utilisateur,
        // lui voler le focus serait pire que le mal (même motif que l'audit 2026-07 M2).
        ReinstallHook(nudgeForeground: false);
    }

    private void ReinstallHook(bool nudgeForeground = true)
    {
        if (_hook == null || !_activationConsent) return;

        // Les échecs de SetWindowsHookEx sont journalisés dans Reinstall() lui-même,
        // y compris le cas « ancien hook conservé » (retour true) invisible d'ici.
        if (!_hook.Reinstall())
            return;

        if (!nudgeForeground) return;

        // Activer la fenêtre pour que le thread soit associé au système d'input
        // Sans cet appel, le hook LL est installé mais ne reçoit pas d'événements.
        // ATTENTION : ne PAS voler le focus si une autre fenetre de NOTRE process l'a deja
        // (OnboardingWindow ou LearningModule en cours d'utilisation par l'utilisateur).
        // Sinon, les TIMER_REHOOK_2 (3s) et TIMER_REHOOK_3 (8s) volent le focus de la fenetre
        // Exercices apres 2-3s puis 5s plus tard, declenchant l'overlay « Cliquez pour reprendre ».
        IntPtr fg = Win32.GetForegroundWindow();
        bool ourProcessHasFocus = false;
        if (fg != IntPtr.Zero)
        {
            Win32.GetWindowThreadProcessIdOut(fg, out uint fgPid);
            ourProcessHasFocus = fgPid == (uint)Environment.ProcessId;
        }
        if (!ourProcessHasFocus)
            Win32.SetForegroundWindow(_hWnd);
    }

    // ═══════════════════════════════════════════════════════════════
    // Détection double remapping
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Vérifie si un layout donné est AZERTY Global en testant le Smart Caps Lock.
    /// Sur AZERTY Global, Verr.Maj + é → É, + ç → Ç, + à → À.
    /// Sur AZERTY standard, Verr.Maj + é → 2, + ç → 9, + à → 0.
    /// Utilise ToUnicodeEx avec le HKL spécifié — aucun accès registre.
    /// </summary>
    private static bool IsLayoutAZERTYGlobal(IntPtr hkl)
    {
        var keyState = new byte[256];
        keyState[0x14] = 0x01; // VK_CAPITAL toggled ON
        var buf = new System.Text.StringBuilder(8);

        // 3 tests indépendants sur la signature du Smart Caps Lock
        (uint scancode, char expected)[] tests =
        {
            (0x03, 'É'), // Verr.Maj + é/2 → É (AZERTY Global) vs 2 (standard)
            (0x0A, 'Ç'), // Verr.Maj + ç/9 → Ç (AZERTY Global) vs 9 (standard)
            (0x0B, 'À'), // Verr.Maj + à/0 → À (AZERTY Global) vs 0 (standard)
        };

        foreach (var (scancode, expected) in tests)
        {
            uint vk = Win32.MapVirtualKeyExW(scancode, 1, hkl); // MAPVK_VSC_TO_VK
            if (vk == 0) return false;

            buf.Clear();
            int result = Win32.ToUnicodeEx(vk, scancode, keyState, buf, buf.Capacity, 0, hkl);
            if (result < 0) // touche morte inattendue → consommer
                Win32.ToUnicodeEx(vk, scancode, keyState, buf, buf.Capacity, 0, hkl);
            if (result != 1 || buf[0] != expected)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Avertit l'utilisateur si le layout système actif est déjà AZERTY Global.
    /// Appelé au démarrage (vérifie le layout de notre thread).
    /// </summary>
    private void CheckSystemLayout()
    {
        IntPtr hkl = Win32.GetKeyboardLayout(0);
        if (!IsLayoutAZERTYGlobal(hkl)) return;
        ShowLayoutConflictPopup(isAtStartup: true);
    }

    /// <summary>
    /// Appelé quand Ctrl+Shift est relâché sans 3e touche.
    /// Planifie une vérification après 100ms (laisser Windows finir le switch).
    /// </summary>
    private void OnLayoutMayHaveChanged()
    {
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_LAYOUT_CHECK, 100, IntPtr.Zero);
    }

    /// <summary>
    /// Vérifie le layout du thread au premier plan après un potentiel Ctrl+Shift switch.
    /// </summary>
    private void CheckForegroundLayout()
    {
        IntPtr hwndFg = Win32.GetForegroundWindow();
        uint threadId = Win32.GetWindowThreadProcessId(hwndFg, IntPtr.Zero);
        IntPtr hkl = Win32.GetKeyboardLayout(threadId);

        if (!IsLayoutAZERTYGlobal(hkl)) return;
        ShowLayoutConflictPopup(isAtStartup: false);
    }

    /// <summary>
    /// Affiche la fenetre custom de conflit avec le layout systeme. Garde-fou pour
    /// eviter l'empilement de popups si l'utilisateur fait Ctrl+Shift plusieurs fois.
    /// La fenetre est topmost (WS_EX_TOPMOST) et propose un choix eclaire :
    /// quitter l'app (cas mot de passe Windows) ou la garder (cas confort post-login).
    /// </summary>
    private bool _layoutPopupOpen;
    private LayoutConflictWindow? _layoutConflictWindow;
    private void ShowLayoutConflictPopup(bool isAtStartup)
    {
        if (_layoutPopupOpen) return; // popup deja affichee — pas de spam
        _layoutPopupOpen = true;
        Win32.SetForegroundWindow(_hWnd);
        _layoutConflictWindow?.Dispose();
        _layoutConflictWindow = new LayoutConflictWindow(
            isAtStartup,
            onQuit: () =>
            {
                _layoutPopupOpen = false;
                OnExit();
            },
            onKeep: () =>
            {
                _layoutPopupOpen = false;
                _layoutConflictWindow?.Dispose();
                _layoutConflictWindow = null;
                // Si l'onboarding etait differe (conflit detecte au demarrage), l'afficher
                // maintenant que l'utilisateur a confirme vouloir garder l'app.
                if (_pendingOnboardingShow)
                {
                    _pendingOnboardingShow = false;
                    ShowOnboardingNow();
                }
            });
        _layoutConflictWindow.Show();
    }

    /// <summary>
    /// Cree et affiche l'OnboardingWindow avec les references injectees. Factorise pour
    /// permettre l'affichage immediat (cas standard) ou differe (apres LayoutConflictWindow).
    /// </summary>
    private void ShowOnboardingNow()
    {
        _onboarding ??= CreateOnboardingWindow();
        ApplyWindowInputState();
        _onboarding.Show();
    }

    private OnboardingWindow CreateOnboardingWindow()
    {
        var onboarding = new OnboardingWindow();
        ConfigureOnboardingWindow(onboarding);
        // Qui voit l'onboarding voit l'opt-in Défi du jour (étape 3) : l'annonce
        // post-mise à jour destinée aux utilisateurs existants n'a plus lieu d'être.
        try { ConfigManager.SetChallengeAnnounceDone(); } catch { }
        return onboarding;
    }

    private bool ActivateWithConsent()
    {
        if (_activationConsent) return true;
        if (_hook == null) return false;
        try
        {
            // Installer en mode transparent, puis activer après le choix explicite.
            _hook.Install();
            ConfigManager.AcceptActivation();
            _activationConsent = true;
            _enabled = true;
            ApplyHookState(syncWhenActive: true);
            UpdateIcon();
            UpdateTooltip();
            return true;
        }
        catch (Exception ex)
        {
            if (!_activationConsent) _hook.Dispose();
            ConfigManager.Log("Activation", ex);
            Win32.MessageBoxW(_hWnd, L.Tray_StartupError, L.Common_ErrorTitle, MB_OK | MB_ICONERROR);
            return false;
        }
    }

    private void ConfigureOnboardingWindow(OnboardingWindow onboarding)
    {
        onboarding.Mapper = _mapper;
        onboarding.Hook = _hook;
        onboarding.AppLayout = _layout;
        onboarding.OpenLessonsRequested = ShowLessonsWindow;
        onboarding.OnClosed = OnOnboardingClosed;
        onboarding.ActivationRequested = ActivateWithConsent;
    }

    /// <summary>
    /// Reprend la sollicitation d'avis différée au démarrage parce que la fenêtre
    /// d'accueil occupait l'écran. Sans ce relais, tout utilisateur qui revoit l'accueil à
    /// chaque démarrage ne serait jamais sollicité.
    /// </summary>
    private void OnOnboardingClosed()
    {
        if (!_activationConsent || !_reviewPromptDeferred) return;
        _reviewPromptDeferred = false;
        MaybeShowReviewPrompt();
    }

    private void ShowLessonsWindow()
    {
        if (!_activationConsent) { ShowOnboardingNow(); return; }
        if (!EnsureLessonsWindow()) return;
        ApplyWindowInputState();
        _lessons!.Show();
    }

    /// <summary>
    /// Crée la fenêtre Leçons si besoin et lui rattache ses rappels. Retourne false si
    /// l'application n'est pas encore en état de l'ouvrir.
    ///
    /// Le catalogue de leçons est résolu dans la langue courante au parse : si la langue a
    /// changé depuis la création de la fenêtre, on la recrée (fenêtre masquée uniquement).
    /// </summary>
    private bool EnsureLessonsWindow()
    {
        if (_mapper == null || _hook == null || _layout == null) return false;
        if (_lessons != null && !_lessons.IsVisible && _lessons.CatalogLanguage != L.Language)
        {
            _lessons.Dispose();
            _lessons = null;
        }
        if (_lessons == null)
        {
            _lessons = new LessonsWindow(_layout, _mapper, _hook);
            _lessons.ChallengeShared = OnChallengeShared;
        }
        return true;
    }

    private void ShowSettingsWindow()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow();
            _settings.ShortcutChanged = () => _hook?.ReloadShortcuts();
            // Un override modifié depuis la section Apps suspendues doit
            // s'appliquer immédiatement au process foreground courant.
            _settings.CompatibilityOverridesChanged = () => _foregroundMonitor?.Recompute();
        }
        ApplyWindowInputState();
        _settings.Show();
    }

    /// <summary>
    /// Annonce unique du Défi du jour aux utilisateurs existants (décision 2026-07-30) :
    /// une seule balloon post-mise à jour 1.2.0, cliquable vers les Paramètres, jamais
    /// réémise — exception ponctuelle assumée à la doctrine zéro harcèlement. Les nouveaux
    /// utilisateurs découvrent l'opt-in à l'étape 3 de l'onboarding, pas ici.
    /// Retourne true si l'annonce a été émise (l'appelant saute alors la balloon « Actif »).
    /// </summary>
    private bool MaybeShowChallengeAnnouncement()
    {
        try
        {
            if (ConfigManager.ChallengeAnnounceDone) return false;
            if (ConfigManager.TrainingEnabled) return false; // déjà inscrit, rien à annoncer
            if (!ConfigManager.NotificationsEnabled) return false;

            ConfigManager.SetChallengeAnnounceDone(); // avant l'affichage : jamais deux fois,
                                                      // même si la balloon échoue ensuite
            ShowBalloon(L.Tray_ChallengeAnnounceTitle, L.Tray_ChallengeAnnounceBody);
            _pendingBalloon = PendingBalloonKind.Announcement;
            return true;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("MaybeShowChallengeAnnouncement", ex);
            return false;
        }
    }

    /// <summary>
    /// Relance unique du lancement automatique (décision 2026-08-17). Le manifeste déclare
    /// le StartupTask à Enabled="false" et l'accueil ne persiste sa case qu'à l'étape 3 :
    /// qui referme le wizard avant n'a jamais l'autostart et relance l'application à la
    /// main. On lui propose une fois, jamais deux. La règle de décision vit dans
    /// <see cref="AutoStartNudge"/>, testable sans fenêtre.
    /// Retourne true si la proposition a été émise (l'appelant saute la balloon « Actif »).
    /// </summary>
    private bool MaybeShowAutoStartNudge()
    {
        try
        {
            if (!AutoStartNudge.ShouldPrompt(AutoStartNudge.Snapshot())) return false;

            // Marqué avant l'affichage : jamais deux fois, même si la balloon échoue
            // ensuite — même doctrine que l'annonce du Défi du jour.
            AutoStartNudge.MarkPromptShown();

            if (ConfigManager.IsPackaged && _toastActivatorRegistered &&
                ToastActivation.TryShowToast(L.Tray_AutoStartNudgeTitle,
                    L.Tray_AutoStartNudgeBody, L.Tray_ToastBtnEnable, "action=autostart"))
                return true;

            ShowBalloon(L.Tray_AutoStartNudgeTitle, L.Tray_AutoStartNudgeBody);
            _pendingBalloon = PendingBalloonKind.AutoStart;
            return true;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("MaybeShowAutoStartNudge", ex);
            return false;
        }
    }

    /// <summary>Clic sur la proposition : on active et on confirme. Un échec est dit,
    /// pas avalé — l'utilisateur vient de demander quelque chose.</summary>
    private void EnableAutoStartFromPrompt()
    {
        try
        {
            if (AutoStart.Set(true))
                ShowBalloon(L.Tray_AutoStartEnabledTitle, L.Tray_AutoStartEnabledBody);
            else
                ShowBalloon(L.Common_ErrorTitle, AutoStart.GetFailureMessage());
        }
        catch (Exception ex)
        {
            ConfigManager.Log("EnableAutoStartFromPrompt", ex);
        }
    }

    /// <summary>Bascule « Lancer au démarrage » depuis le menu de la zone de notification.
    /// Un choix fait à la main éteint la relance, dans un sens comme dans l'autre.</summary>
    private void ToggleAutoStart()
    {
        try
        {
            bool target = !AutoStart.IsRegistered;
            if (!AutoStart.Set(target))
            {
                ShowBalloon(L.Common_ErrorTitle, AutoStart.GetFailureMessage());
                return;
            }
            AutoStartNudge.MarkPromptShown();
            if (target)
                ShowBalloon(L.Tray_AutoStartEnabledTitle, L.Tray_AutoStartEnabledBody);
        }
        catch (Exception ex)
        {
            ConfigManager.Log("ToggleAutoStart", ex);
        }
    }

    /// <summary>Ouvre la fenêtre Leçons directement sur la séance Défi du jour
    /// (clic sur le rappel ou entrée du menu tray).</summary>
    private void ShowChallengeWindow()
    {
        if (!EnsureLessonsWindow()) return;
        ApplyWindowInputState();
        if (!_lessons!.ShowChallenge())
            _lessons.Show(); // banque indisponible : fenêtre Leçons normale plutôt que rien
    }

    /// <summary>
    /// Émet la balloon « Défi du jour » si la décision de cadence l'autorise (opt-in,
    /// fenêtre horaire, un rappel par jour, priorité avis J+7, signaux locaux).
    /// Appelé toutes les 5 min par TIMER_STATS_FLUSH — la décision est pure et bon marché.
    /// </summary>
    private void MaybeShowTrainingReminder()
    {
        try
        {
            if (!ConfigManager.NotificationsEnabled) return;
            var now = DateTime.Now;
            if (!TrainingReminders.ShouldRemind(now, TrainingReminders.Snapshot()))
                return;

            TrainingReminders.MarkReminderShown(DateOnly.FromDateTime(now));
            ShowBalloon(L.Challenge_ReminderTitle, L.Challenge_ReminderBody);
            _pendingBalloon = PendingBalloonKind.Training;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("MaybeShowTrainingReminder", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Actions utilisateur
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Entrées du sous-menu « Retours et soutien », dans l'ordre d'affichage. Le canal sobre
    /// perd « Soutenir le projet » et Discord (D3) et garde « Donner mon avis » et
    /// « Signaler un bug » (D4) : deux entrées sur quatre, jamais un sous-menu vide.
    ///
    /// La liste existe pour que le menu soit asservi à une donnée qu'un test peut lire. Trois
    /// AppendMenuW gardés par un if auraient donné un test qui vérifie que Store est Store.
    /// </summary>
    internal static int[] FeedbackMenuEntries(DistributionChannel channel,
        bool? externalLinksPolicy = null) =>
        PolicyManager.ExternalLinksEnabled(externalLinksPolicy, channel)
            ? new[] { IDM_SUPPORT, IDM_FEEDBACK, IDM_DISCORD, IDM_BUG }
            : new[] { IDM_FEEDBACK, IDM_BUG };

    /// <summary>Entrées de premier niveau du bloc de retours : « Noter sur le Microsoft
    /// Store », absente du canal sobre (D3). Rendue comme liste pour la même raison que
    /// <see cref="FeedbackMenuEntries"/>.</summary>
    internal static int[] FeedbackTopLevelEntries(DistributionChannel channel,
        bool? externalLinksPolicy = null) =>
        PolicyManager.ExternalLinksEnabled(externalLinksPolicy, channel)
            ? new[] { IDM_RATE_STORE }
            : Array.Empty<int>();

    /// <summary>Libellé d'une entrée du bloc de retours. Séparé de la liste d'entrées : la
    /// liste est pure et se teste, les libellés dépendent de la langue chargée.</summary>
    private static string FeedbackMenuLabel(int id) => id switch
    {
        IDM_SUPPORT => L.Tray_MenuSupportProject,
        IDM_FEEDBACK => L.Tray_MenuGiveFeedback,
        IDM_DISCORD => L.Stats_LinkDiscord,
        IDM_BUG => L.Tray_MenuReportBug,
        IDM_RATE_STORE => L.Tray_MenuRateStore,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    private void ShowContextMenu()
    {
        var hMenu = Win32.CreatePopupMenu();
        var kbdKey = ConfigManager.GetShortcutDisplayName(ConfigManager.ShortcutVirtualKeyboardVk);
        var searchKey = ConfigManager.GetShortcutDisplayName(ConfigManager.ShortcutCharacterSearchVk);
        // Menu en quatre blocs d'intention (décision S2 du 2026-09-02, portée en v1.2.0 le
        // 2026-09-19) : État · Outils · Apprendre · Réglages et infos, puis Langue et Quitter.
        // Les IDM_* ne changent pas ; seuls l'ordre, les regroupements et trois libellés bougent.

        // ── État ────────────────────────────────────────────────────
        Win32.AppendMenuW(hMenu, MF_STRING, IDM_TOGGLE,
            _enabled ? L.Tray_MenuDisable : L.Tray_MenuEnable);
        uint pauseFlags = _enabled || IsPaused ? MF_STRING : MF_STRING | MF_GRAYED;
        Win32.AppendMenuW(hMenu, pauseFlags, IDM_PAUSE,
            IsPaused ? L.Tray_MenuResumeNow : L.Tray_MenuPauseEllipsis);
        Win32.AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

        // ── Outils ──────────────────────────────────────────────────
        uint kbdFlags = ShouldProcessHook || _virtualKeyboard?.IsVisible == true ? MF_STRING : MF_STRING | MF_GRAYED;
        Win32.AppendMenuW(hMenu, kbdFlags, IDM_KEYBOARD,
            _virtualKeyboard?.IsVisible == true ? L.Tray_MenuHideVirtualKeyboard(kbdKey) : L.Tray_MenuVirtualKeyboard(kbdKey));
        uint searchFlags = (ShouldProcessHook && _mapper?.AdvancedFeaturesSuppressed != true) ||
            _characterSearch?.IsVisible == true ? MF_STRING : MF_STRING | MF_GRAYED;
        Win32.AppendMenuW(hMenu, searchFlags, IDM_SEARCH, L.Tray_MenuSearchCharacter(searchKey));

        // Couches ▸ (S2-3) : l'entrée unique à suffixe ✓/… devient un sous-menu de trois
        // couches cochables. Les libellés sont repris tels quels de la fenêtre des couches,
        // aucun texte neuf hors le titre du sous-menu et « Configurer… ».
        bool layersOn = ConfigManager.MaintainableLayersEnabled;
        var hLayersMenu = Win32.CreatePopupMenu();
        Win32.AppendMenuW(hLayersMenu,
            MF_STRING | (layersOn && ConfigManager.MaintainableGreekEnabled ? MF_CHECKED : 0u),
            IDM_LAYER_GREEK, L.Layers_LabelGreek);
        Win32.AppendMenuW(hLayersMenu,
            MF_STRING | (layersOn && ConfigManager.MaintainableCyrillicEnabled ? MF_CHECKED : 0u),
            IDM_LAYER_CYRILLIC, L.Layers_LabelCyrillic);
        Win32.AppendMenuW(hLayersMenu,
            MF_STRING | (layersOn && ConfigManager.MaintainableScientificEnabled ? MF_CHECKED : 0u),
            IDM_LAYER_SCIENTIFIC, L.Layers_LabelScientific);
        Win32.AppendMenuW(hLayersMenu, MF_SEPARATOR, 0, null);
        Win32.AppendMenuW(hLayersMenu, MF_STRING, IDM_MAINTAINABLE_LAYERS, L.Tray_MenuLayersConfigure);
        Win32.AppendMenuW(hMenu, MF_STRING | MF_POPUP, (nuint)hLayersMenu, L.Tray_MenuLayers);
        Win32.AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

        // ── Apprendre ───────────────────────────────────────────────
        // Replié en sous-menu le 2026-09-19 : le menu à 18 lignes restait trop long à lire.
        // ⚠️ Le défi du jour perd la visibilité immédiate que lui donnait la décision du
        // 2026-08-16 — arbitrage assumé par Antoine au profit de la lisibilité du menu.
        var hLearnMenu = Win32.CreatePopupMenu();
        Win32.AppendMenuW(hLearnMenu, MF_STRING, IDM_EXERCISES, L.Tray_MenuLessons);
        // Défi du jour : toujours visible depuis la décision du 2026-08-16. L'entrée était
        // conditionnée à `trainingEnabled`, qui vaut false par défaut : sur une installation
        // neuve la fonction n'existait donc pas visuellement, alors que le défi commun est
        // le seul contenu identique pour tous les utilisateurs — le seul comparable, et le
        // seul partageable. L'opt-in ne gouverne plus que les rappels d'entraînement, qui
        // sont des notifications et relèvent d'un consentement distinct.
        Win32.AppendMenuW(hLearnMenu, MF_STRING, IDM_CHALLENGE, L.Tray_MenuChallenge);
        Win32.AppendMenuW(hLearnMenu, MF_STRING, IDM_ONBOARDING, L.Tray_MenuWelcomeWindow);
        Win32.AppendMenuW(hMenu, MF_STRING | MF_POPUP, (nuint)hLearnMenu, L.Tray_MenuLearn);
        Win32.AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

        // ── Réglages et infos ───────────────────────────────────────
        // « Lancer au démarrage de Windows » ne figure plus ici (S2-8) : le réglage vit dans
        // Paramètres › Préférences, qui porte déjà la case (`SettingsWindow.cs`). IDM_AUTOSTART
        // reste déclaré et gardé : l'accueil et les notifications l'empruntent encore.
        Win32.AppendMenuW(hMenu, MF_STRING, IDM_SETTINGS, L.Tray_MenuSettings);

        // Compatibilité des applications ▸ (S2-5) : toujours visible. Sans application
        // détectée au premier plan, les trois choix sont grisés et « Comprendre la
        // compatibilité… » reste actif — auparavant le sous-menu entier disparaîssait, ce
        // qui se lisait comme une fonctionnalité absente plutôt qu'inapplicable.
        // On filtre notre propre process : « Compatibilité — AZERTY Global.exe » n'a pas de sens.
        _compatibilityMenuProcessName = _foregroundMonitor?.LastApplicationProcessName;
        _compatibilityMenuFullPath = _foregroundMonitor?.LastApplicationFullPath;
        var fgProc = _compatibilityMenuProcessName;
        bool fgIsOwnApp = !string.IsNullOrEmpty(fgProc) &&
            string.Equals(fgProc, ProductIdentity.ExecutableName, StringComparison.OrdinalIgnoreCase);
        bool hasTarget = !string.IsNullOrEmpty(fgProc) && !fgIsOwnApp;
        var hSubMenu = Win32.CreatePopupMenu();
        if (hasTarget)
        {
            Win32.AppendMenuW(hSubMenu, MF_STRING | MF_GRAYED, 0, L.Tray_MenuActiveApp(fgProc));
            Win32.AppendMenuW(hSubMenu, MF_SEPARATOR, 0, null);
        }
        bool fgIsRemoteAccess = hasTarget && GameRegistry.IsRemoteAccessProcess(fgProc);
        uint targetFlags = hasTarget ? MF_STRING : MF_STRING | MF_GRAYED;
        Win32.AppendMenuW(hSubMenu, targetFlags, IDM_COMPAT_AUTO, L.Tray_MenuCompatAuto);
        Win32.AppendMenuW(hSubMenu,
            hasTarget && !fgIsRemoteAccess ? MF_STRING : MF_STRING | MF_GRAYED,
            IDM_COMPAT_FORCE_ON, L.Tray_MenuCompatForceOn);
        Win32.AppendMenuW(hSubMenu, targetFlags, IDM_COMPAT_FORCE_OFF, L.Tray_MenuCompatForceOff);
        if (hasTarget)
        {
            // Marquer la radio active
            var ovr = fgIsRemoteAccess ? null : ConfigManager.GetCompatibilityOverride(fgProc);
            uint activeId = ovr switch
            {
                "forceOn" => IDM_COMPAT_FORCE_ON,
                "forceOff" => IDM_COMPAT_FORCE_OFF,
                _ => IDM_COMPAT_AUTO
            };
            Win32.CheckMenuRadioItem(hSubMenu, IDM_COMPAT_AUTO, IDM_COMPAT_FORCE_OFF, activeId, Win32.MF_BYCOMMAND);
        }
        Win32.AppendMenuW(hSubMenu, MF_SEPARATOR, 0, null);
        Win32.AppendMenuW(hSubMenu, MF_STRING, IDM_COMPAT_INFO, L.Tray_MenuCompatInfo);
        Win32.AppendMenuW(hMenu, MF_STRING | MF_POPUP, (nuint)hSubMenu, L.Tray_MenuAppCompat);

        Win32.AppendMenuW(hMenu, MF_STRING, IDM_STATS, L.Stats_Title);

        // À propos et aide ▸ (2026-09-19) : confidentialité, ressources, retours, notation
        // et « À propos », soit cinq lignes de premier niveau, tiennent désormais sous une
        // seule. Rien n'est retiré : tout reste atteignable en un clic de plus.
        var hAboutHelpMenu = Win32.CreatePopupMenu();
        Win32.AppendMenuW(hAboutHelpMenu, MF_STRING, IDM_PRIVACY, L.Tray_MenuPrivacySecurity);

        // Ressources et liens externes
        var hResourcesMenu = Win32.CreatePopupMenu();
        Win32.AppendMenuW(hResourcesMenu, MF_STRING, IDM_GUIDE_PDF, L.Tray_MenuPrintableGuide);
        Win32.AppendMenuW(hResourcesMenu, MF_STRING, IDM_SITE, L.About_LinkSite);
        Win32.AppendMenuW(hResourcesMenu, MF_STRING, IDM_GUIDE_CHANGES, L.Tray_MenuFiveChanges);
        Win32.AppendMenuW(hResourcesMenu, MF_STRING, IDM_CARDS, L.Tray_MenuKeyboardCards);
        Win32.AppendMenuW(hResourcesMenu, MF_STRING, IDM_RELEASE_NOTES, L.Tray_MenuWhatsNew);
        Win32.AppendMenuW(hAboutHelpMenu, MF_STRING | MF_POPUP, (nuint)hResourcesMenu, L.Tray_MenuResources);

        var hFeedbackMenu = Win32.CreatePopupMenu();
        var channel = AppChannel.Current;
        var externalLinksPolicy = PolicyManager.Current.ExternalLinks;
        foreach (int id in FeedbackMenuEntries(channel, externalLinksPolicy))
            Win32.AppendMenuW(hFeedbackMenu, MF_STRING, (nuint)id, FeedbackMenuLabel(id));
        Win32.AppendMenuW(hAboutHelpMenu, MF_STRING | MF_POPUP, (nuint)hFeedbackMenu, L.Tray_MenuFeedbackSupport);
        // « Noter sur le Microsoft Store » au premier niveau, sous « Retours et soutien »
        // (S2-6, confirmé le 2026-09-02) : l'action la plus utile au projet, en un clic.
        // Absente du canal sobre : rien n'y renvoie vers le Store (D3).
        foreach (int id in FeedbackTopLevelEntries(channel, externalLinksPolicy))
            Win32.AppendMenuW(hAboutHelpMenu, MF_STRING, (nuint)id, FeedbackMenuLabel(id));

        Win32.AppendMenuW(hAboutHelpMenu, MF_STRING, IDM_ABOUT, L.Tray_MenuAbout);
        Win32.AppendMenuW(hMenu, MF_STRING | MF_POPUP, (nuint)hAboutHelpMenu, L.Tray_MenuAboutHelp);
        Win32.AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

#if DEBUG
        Win32.AppendMenuW(hMenu, MF_STRING, IDM_RESET_ONBOARDING, L.Tray_MenuResetOnboardingDebug);
#endif
        // Bascule de langue directe juste avant Quitter : libellé dans la langue cible,
        // cf. L.Tray_MenuSwitchLanguage. Langue imposée par une politique : l'entrée reste
        // visible mais grisée, comme les autres entrées que l'état courant interdit. La
        // retirer ferait croire à une fonctionnalité disparue plutôt qu'à une décision de
        // la structure. S2-2 : le même réglage existe aussi dans Paramètres › Préférences.
        uint languageFlags = PolicyManager.LanguageIsManagedNow ? MF_STRING | MF_GRAYED : MF_STRING;
        Win32.AppendMenuW(hMenu, languageFlags, IDM_SWITCH_LANGUAGE, L.Tray_MenuSwitchLanguage);
        Win32.AppendMenuW(hMenu, MF_STRING, IDM_QUIT, L.Tray_MenuQuit);

        Win32.GetCursorPos(out var pt);
        _lastForegroundBeforeTrayMenu = Win32.GetForegroundWindow();
        if (_lastForegroundBeforeTrayMenu == _hWnd)
            _lastForegroundBeforeTrayMenu = IntPtr.Zero;
        Win32.SetForegroundWindow(_hWnd);
        Win32.TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, pt.x, pt.y, _hWnd, IntPtr.Zero);
        Win32.PostMessageW(_hWnd, 0, IntPtr.Zero, IntPtr.Zero); // WM_NULL — fermeture propre du menu
        Win32.DestroyMenu(hMenu);
    }

    /// <summary>
    /// Handler du raccourci clavier Ctrl+Maj+Verr.Maj (via KeyMapper.ToggleRequested).
    /// Pendant une pause volontaire, le raccourci REPREND (équivalent « Reprendre
    /// maintenant ») au lieu de basculer _enabled — sinon il terminerait la pause ET
    /// désactiverait, ce qui contredirait l'intention « réactiver » de l'utilisateur.
    /// Le menu tray (IDM_TOGGLE) appelle OnToggle directement et garde sa sémantique.
    /// </summary>
    private void OnToggleShortcut()
    {
        if (IsPaused)
        {
            StopPause(expired: false);
            return;
        }
        OnToggle();
    }

    private void OnToggle()
    {
        if (_hook == null) return;
        if (!_activationConsent) { ShowOnboardingNow(); return; }

        // Une suspension de compatibilité surclasse l'état global : l'utilisateur doit
        // quitter l'application concernée ou remettre son override sur Auto.
        if (!_enabled && _suspendedForCompatibility)
        {
            var procName = _foregroundMonitor?.CurrentProcessName ?? L.Tray_ThisGameFallback;
            var procDisplay = FormatProcessName(procName);
            var reason = _foregroundMonitor?.CurrentSuspendReason ?? CompatibilitySuspendReason.UnknownForeground;
            if (reason == CompatibilitySuspendReason.RemoteAccess)
            {
                ShowSecurityBalloon(L.Tray_ForceRefusedTitle, L.Tray_ForceOnRemoteRefused);
                ConfigManager.LogCompatCriticalEvent("RemoteAccessToggleRefused",
                    $"process={ConfigManager.AnonymizeProcessName(procName)}, attempted=enable");
            }
            else if (reason == CompatibilitySuspendReason.AntiCheat)
            {
                ShowSecurityBalloon(L.Tray_ToggleRefusedTitle(procDisplay), L.Tray_AntiCheatRefusedBody);
                // Audit sécu 2026-05 SEV-A1-02 : anonymisation du process name dans le log.
                ConfigManager.LogCompatCriticalEvent("AntiCheatToggleRefused",
                    $"process={ConfigManager.AnonymizeProcessName(procName)}, attempted=enable");
            }
            else if (reason == CompatibilitySuspendReason.UserOverride)
            {
                ShowBalloon(L.Tray_SuspendedInTitle(procDisplay), L.Tray_UserOverrideToggleRefused);
            }
            else
            {
                ShowSecurityBalloon(L.Tray_PrecautionTitle, L.Tray_SuspendedUnknownForeground);
            }
            return;
        }

        if (_enabled)
            _mapper?.ClearPassedThroughKeys();

        if (IsPaused)
        {
            _pauseUntilUtc = null;
            Win32.KillTimer(_hWnd, (UIntPtr)TIMER_PAUSE);
        }

        _enabled = !_enabled;

        ApplyHookState(syncWhenActive: _enabled);

        // Si l'utilisateur désactive manuellement pendant qu'on est en désactivation auto,
        // annuler le « rétablir auto à la sortie du jeu » : il a explicitement choisi off.
        if (!_enabled && _suspendedForCompatibility)
            _wasEnabledBeforeAutoDisable = false;

        // Resynchroniser l'état quand on réactive (CapsLock a pu changer pendant la désactivation)
        if (_enabled && !_suspendedForCompatibility)
            _mapper?.SyncState();

        // Fermer le clavier virtuel et la recherche quand on désactive
        if (!_enabled)
        {
            if (_virtualKeyboard?.IsVisible == true) _virtualKeyboard.Hide();
            if (_characterSearch?.IsVisible == true) _characterSearch.Hide();
        }

        UpdateIcon();
        UpdateTooltip();

        // La balloon Windows au toggle a ete supprimee en v0.9.7.1 : elle faisait doublon
        // avec ToggleNotification (haut-droite). La balloon de demarrage de l'app, qui
        // rappelle le raccourci, est conservee dans le constructeur.

        // Mini-fenetre TOPMOST en haut a droite (visible en borderless windowed quand
        // l'icone tray est cachee par le jeu — angle mort accepte en exclusive fullscreen).
        // Garde anti-cheat : pas d'overlay tiers quand un jeu anti-cheat kernel-level est
        // au foreground — risque de detection comme cheat / trainer (fausse alerte).
        var suspendReason = _foregroundMonitor?.CurrentSuspendReason ?? CompatibilitySuspendReason.None;
        if (!IsSecuritySuspension(suspendReason))
        {
            if (_toggleNotification == null) _toggleNotification = new ToggleNotification();
            _toggleNotification.Show(_enabled);
        }
    }

    private void ShowPauseDialogAndStart()
    {
        if (!_enabled)
            return;

        var duration = PauseDurationDialog.Show(_hWnd);
        if (duration.HasValue)
            StartPause(duration.Value);
    }

    private void StartPause(TimeSpan duration)
    {
        int totalMinutes = Math.Clamp((int)Math.Round(duration.TotalMinutes), 1, 1439);
        _pauseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(totalMinutes);
        _mapper?.ClearPassedThroughKeys();
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_PAUSE, 1000, IntPtr.Zero);
        ApplyHookState();
        UpdateIcon();
        UpdateTooltip();
        ShowBalloon(L.Tray_PausedForDurationTitle(FormatDuration(totalMinutes)), L.Tray_PausedForDuration);
    }

    private void StopPause(bool expired)
    {
        bool wasPaused = IsPaused;
        _pauseUntilUtc = null;
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_PAUSE);
        // R4 : doctrine d'ArbitratePendingDeadKeyWhileSuspended — une pause volontaire
        // conserve la touche morte, seule une suspension imposée l'abandonne. SyncState
        // l'annulait à la reprise, ce que le produit ne tenait donc pas.
        ApplyHookState(syncWhenActive: true, preservePendingDeadKey: true);
        UpdateIcon();
        UpdateTooltip();

        if (wasPaused)
            ShowBalloon(expired ? L.Tray_PauseEndedTitle : L.Tray_PauseStoppedTitle, L.Tray_PauseOverBody);
    }

    /// <summary>
    /// Nom de process destiné à l'affichage : l'extension exécutable est retirée.
    /// <c>ForegroundMonitor.CurrentProcessName</c> rend « Minecraft.Windows.exe » quand les
    /// titres de notification doivent montrer « Minecraft.Windows ». La valeur brute reste
    /// seule utilisée pour GameRegistry, les overrides de compatibilité et les journaux
    /// anonymisés — la couper là fausserait une comparaison ou une empreinte.
    /// </summary>
    private static string FormatProcessName(string proc) =>
        proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? proc[..^4] : proc;

    private static string FormatDuration(int totalMinutes)
    {
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (hours == 0) return $"{minutes} min";
        return L.IsEnglish ? $"{hours} hr {minutes:00} min" : $"{hours} h {minutes:00}";
    }

    private string FormatPauseRemaining()
    {
        if (!_pauseUntilUtc.HasValue)
            return "00:00";

        var remaining = _pauseUntilUtc.Value - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        int totalHours = (int)remaining.TotalHours;
        if (totalHours > 0)
            return $"{totalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        return $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private bool _lastCapsState;
    private int _stateRefreshQueued;

    /// <summary>
    /// Déclenché depuis le callback WH_KEYBOARD_LL à chaque transition d'état
    /// (touche morte, couche, CapsLock). Ne rien faire de coûteux ici :
    /// Shell_NotifyIcon (tooltip) et SetWindowPos (indicateur) sont des appels
    /// bloquants qui retarderaient le hook — tout part en différé, coalescé,
    /// sur la boucle de messages.
    /// </summary>
    private void OnStateChanged()
    {
        if (_hWnd == IntPtr.Zero) return;
        if (Interlocked.Exchange(ref _stateRefreshQueued, 1) == 0)
            Win32.PostMessageW(_hWnd, WM_APP_STATE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Travail UI réel de OnStateChanged, exécuté hors du hook.</summary>
    private void ApplyStateChange()
    {
        bool caps = _mapper?.CapsLockActive == true;
        if (caps != _lastCapsState)
        {
            _lastCapsState = caps;
            UpdateIcon();
        }
        UpdateTooltip();
        if (_mapper != null)
        {
            // Indicateur masqué dans les champs sécurisés ET quand le hook est
            // suspendu (pause, anti-cheat) : la couche ne produirait rien.
            _layerIndicator?.Update(_mapper.MaintainableLayerState,
                _mapper.AdvancedFeaturesSuppressed || !ShouldProcessHook);
        }
        RefreshVirtualKeyboard();
    }

    private void OnKeyPressed(uint scancode)
    {
        _virtualKeyboard?.NotifyKeyPress(scancode);
        // Repositionnement de l'indicateur (le caret a bougé) : différé hors du
        // hook via le même message coalescé que les changements d'état.
        if (_mapper?.MaintainableLayerState.IsActive == true)
            OnStateChanged();
    }

    private void OnSearchSelectionChanged(CharacterSearch.MethodData? method)
    {
        _virtualKeyboard?.HighlightMethod(method);
    }

    /// <summary>Lit les réglages des couches et les pousse au moteur.</summary>
    private void PushMaintainableLayerSettings()
    {
        if (_mapper == null) return;
        var enabledLayers = new List<string>(3);
        if (ConfigManager.MaintainableGreekEnabled) enabledLayers.Add("dk_greek");
        if (ConfigManager.MaintainableCyrillicEnabled) enabledLayers.Add("dk_cyrillic");
        if (ConfigManager.MaintainableScientificEnabled) enabledLayers.Add("dk_scientific");
        _mapper.ApplyMaintainableLayerSettings(
            ConfigManager.MaintainableLayersEnabled,
            enabledLayers,
            ConfigManager.MaintainableDoubleTapMilliseconds);
    }

    /// <summary>
    /// Bascule une couche depuis le sous-menu Couches (S2-3). Les trois couches sont
    /// gardées par l'interrupteur principal `maintainableLayersEnabled`, qui vaut false
    /// par défaut : cocher une couche alors qu'il est éteint l'allume, et décocher la
    /// dernière couche active l'éteint. Sans cela, un clic sur une couche ne produirait
    /// rien de visible sur une installation neuve.
    /// </summary>
    private void ToggleMaintainableLayer(int id)
    {
        bool master = ConfigManager.MaintainableLayersEnabled;
        bool greek = master && ConfigManager.MaintainableGreekEnabled;
        bool cyrillic = master && ConfigManager.MaintainableCyrillicEnabled;
        bool scientific = master && ConfigManager.MaintainableScientificEnabled;

        switch (id)
        {
            case IDM_LAYER_GREEK: greek = !greek; break;
            case IDM_LAYER_CYRILLIC: cyrillic = !cyrillic; break;
            case IDM_LAYER_SCIENTIFIC: scientific = !scientific; break;
            default: return;
        }

        ConfigManager.SetMaintainableGreekEnabled(greek);
        ConfigManager.SetMaintainableCyrillicEnabled(cyrillic);
        ConfigManager.SetMaintainableScientificEnabled(scientific);
        ConfigManager.SetMaintainableLayersEnabled(greek || cyrillic || scientific);
        OnMaintainableLayerSettingsChanged();
    }

    private void OnMaintainableLayerSettingsChanged()
    {
        PushMaintainableLayerSettings();
        ApplyStateChange();
    }

    /// <summary>Appelé depuis le thread de travail UIA — se limite à un PostMessage.</summary>
    private void OnSecureInputLateResult()
    {
        if (_hWnd != IntPtr.Zero)
            Win32.PostMessageW(_hWnd, WM_APP_SECURE_RECHECK, IntPtr.Zero, IntPtr.Zero);
    }

    private void RefreshVirtualKeyboard()
    {
        if (_mapper == null || _virtualKeyboard == null) return;
        _virtualKeyboard.UpdateState(
            _mapper.ShiftDown,
            _mapper.AltGrDown,
            _mapper.CtrlDown,
            _mapper.AltDown,
            _mapper.CapsLockActive,
            _mapper.DisplayDeadKey);
    }

    private void OnReportBug() => OpenDiagnosticPage("/bug", "app");

    /// <summary>
    /// Ouvre la page de retour, version et OS compris (R16). Le paramètre <c>source</c>
    /// déjà présent dans l'URL de la sollicitation d'avis est conservé tel quel : le site
    /// le lit depuis la 1.2.0, et le renommer casserait sa mesure.
    /// </summary>
    private void OpenFeedback(string source, string path = "/feedback") =>
        OpenDiagnosticPage(path, source);

    private static void OpenDiagnosticPage(string path, string source) =>
        Win32.ShellExecuteW(IntPtr.Zero, "open",
            ProductIdentity.DiagnosticUrl(path, Program.Version, ProductIdentity.OsDescription(), source),
            null, null, 1);

    private void OnExit()
    {
        Cleanup();
        Win32.DestroyWindow(_hWnd);
    }

    /// <summary>Nettoyage unique du hook, de l'icône tray et des handles GDI.</summary>
    private bool _cleaned;
    private void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;

        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_PAUSE);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HOOK_WATCHDOG);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_HOOK_PROBE);
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_STATS_FLUSH);
        UsageStats.Flush(); // dernière sauvegarde avant fermeture
        Win32.WTSUnRegisterSessionNotification(_hWnd);
        _mapper?.ClearPassedThroughKeys();
        SecureInputDetector.ResultChangedLate -= OnSecureInputLateResult;
        _foregroundMonitor?.Dispose(); _foregroundMonitor = null;
        _hook?.Dispose(); _hook = null;
        _virtualKeyboard?.Dispose(); _virtualKeyboard = null;
        _characterSearch?.Dispose(); _characterSearch = null;
        _onboarding?.Dispose(); _onboarding = null;
        _settings?.Dispose(); _settings = null;
        _maintainableLayersWindow?.Dispose(); _maintainableLayersWindow = null;
        _layerIndicator?.Dispose(); _layerIndicator = null;
        _about?.Dispose(); _about = null;
        _usageStats?.Dispose(); _usageStats = null;
        _toggleNotification?.Dispose(); _toggleNotification = null;
        _lessons?.Dispose(); _lessons = null;
        _layoutConflictWindow?.Dispose(); _layoutConflictWindow = null;
        ToastActivation.Unregister();
        Win32.Shell_NotifyIconW(NIM_DELETE, ref _nid);
        if (_hIcon != IntPtr.Zero)
        {
            Win32.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
        if (_hBalloonIcon != IntPtr.Zero)
        {
            Win32.DestroyIcon(_hBalloonIcon);
            _hBalloonIcon = IntPtr.Zero;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Icône et notifications
    // ═══════════════════════════════════════════════════════════════
    private static void PrepareTrayIconForAdd(ref Win32.NOTIFYICONDATAW data)
    {
        // Les appels NIM_MODIFY remplacent uFlags par le seul champ mis à jour.
        // Après un redémarrage d'Explorer, NIM_ADD doit donc réannoncer explicitement
        // le callback, l'icône et le tooltip pour reconstruire une entrée tray complète.
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    }

    /// <summary>
    /// Bulle et journal décrivant la suspension en cours. Partagée par l'entrée en
    /// suspension et par le changement de raison : les deux doivent dire la même chose,
    /// puisque l'utilisateur voit la même protection s'appliquer.
    /// </summary>
    private void AnnounceSuspension(CompatibilitySuspendReason reason, string procName, string procDisplay)
    {
        switch (reason)
        {
            case CompatibilitySuspendReason.UnknownForeground:
                ShowSecurityBalloon(L.Tray_PrecautionTitle, L.Tray_SuspendedUnknownForeground);
                ConfigManager.LogCompatCriticalEvent("UnknownForegroundSuspended", "action=disable");
                break;
            case CompatibilitySuspendReason.RemoteAccess:
                ShowSecurityBalloon(L.Tray_SuspendedDuringTitle(procDisplay), L.Tray_DisabledForRemoteAccess);
                ConfigManager.LogCompatCriticalEvent("RemoteAccessDetected",
                    $"process={ConfigManager.AnonymizeProcessName(procName)}, action=disable");
                break;
            case CompatibilitySuspendReason.UserOverride:
                ShowBalloon(L.Tray_SuspendedInTitle(procDisplay), L.Tray_DisabledByUserOverride);
                ConfigManager.LogCompatEvent("UserOverrideApplied",
                    $"process={ConfigManager.AnonymizeProcessName(procName)}, action=disable");
                break;
            default:
                ShowSecurityBalloon(L.Tray_SuspendedInTitle(procDisplay), L.Tray_DisabledForAntiCheat);
                ConfigManager.LogCompatCriticalEvent("AntiCheatDetected",
                    $"process={ConfigManager.AnonymizeProcessName(procName)}, action=disable");
                break;
        }
    }

    /// <summary>
    /// Le hook doit-il continuer a detecter les raccourcis alors qu'il laisse tout passer ?
    /// Fonction pure : c'est la propriete de securite de l'ecart 8, et le seul point ou
    /// elle se decide.
    ///
    /// Vrai dans deux cas seulement — une pause volontaire (pour permettre la reprise au
    /// clavier) et une suspension que l'utilisateur a lui-meme configuree. Toute suspension
    /// de securite (anti-triche, acces distant, premier plan inconnu) exige l'inertie
    /// totale : y detecter un raccourci, c'est lire les frappes dans un jeu qui bannit
    /// pour cela. Une pause qui tombe pendant une suspension de securite ne la leve pas.
    /// </summary>
    private static bool ShouldDetectShortcutsWhileBlocked(
        bool isPaused,
        bool suspendedForCompatibility,
        CompatibilitySuspendReason reason)
    {
        if (suspendedForCompatibility)
            return reason == CompatibilitySuspendReason.UserOverride;
        return isPaused;
    }

    private static bool IsSecuritySuspension(CompatibilitySuspendReason reason) =>
        reason is CompatibilitySuspendReason.AntiCheat
            or CompatibilitySuspendReason.RemoteAccess
            or CompatibilitySuspendReason.UnknownForeground;

    /// <summary>Transition de suspension a appliquer au hook.</summary>
    private enum SuspensionTransition
    {
        /// <summary>Rien a faire : l'etat du hook correspond deja au premier plan.</summary>
        None,
        /// <summary>Entree en suspension depuis un premier plan ordinaire.</summary>
        Enter,
        /// <summary>Toujours suspendu, mais pour une autre raison qu'a la configuration precedente.</summary>
        ReasonChanged,
        /// <summary>Sortie de suspension.</summary>
        Leave
    }

    /// <summary>
    /// Classe la transition a appliquer. Fonction pure, sans etat : c'est le seul endroit
    /// ou la decision se prend, et le seul qui soit testable sans instancier TrayApplication.
    ///
    /// Le troisieme cas est celui que la version a deux branches ratait. AntiCheat,
    /// RemoteAccess, UnknownForeground et UserOverride rendent tous DisabledAntiCheat :
    /// entre deux applications suspendues, ni « mode == DisabledAntiCheat &amp;&amp; !suspendu »
    /// ni « mode != DisabledAntiCheat &amp;&amp; suspendu » n'est vrai, donc ApplyHookState()
    /// n'etait jamais rappele et ShortcutsWhilePassThrough gardait la valeur calculee pour
    /// l'application precedente — dans les deux sens : shortcuts encore detectes dans un jeu
    /// anti-triche apres un override utilisateur, et Ctrl+Maj+W fuyant vers l'application
    /// apres un jeu anti-triche.
    /// </summary>
    private static SuspensionTransition ClassifySuspensionTransition(
        CompatibilityMode mode,
        bool suspendedForCompatibility,
        CompatibilitySuspendReason appliedReason,
        CompatibilitySuspendReason currentReason)
    {
        bool suspendNow = mode == CompatibilityMode.DisabledAntiCheat;
        if (suspendNow && !suspendedForCompatibility) return SuspensionTransition.Enter;
        if (!suspendNow && suspendedForCompatibility) return SuspensionTransition.Leave;
        if (suspendNow && currentReason != appliedReason) return SuspensionTransition.ReasonChanged;
        return SuspensionTransition.None;
    }

    private void UpdateIcon()
    {
        var oldIcon = _hIcon;

        // Déterminer le texte et le style de l'icône selon l'état
        bool active = ShouldProcessHook;
        bool capsLock = _mapper?.CapsLockActive == true && active;
        string iconText = "AG";

        _hIcon = CreateTextIcon(iconText, active, capsLock, _suspendedForCompatibility);

        _nid.hIcon = _hIcon;
        _nid.uFlags = NIF_ICON;
        Win32.Shell_NotifyIconW(NIM_MODIFY, ref _nid);

        if (oldIcon != IntPtr.Zero) Win32.DestroyIcon(oldIcon);
    }

    private void UpdateTooltip()
    {
        var parts = new List<string> { ProductIdentity.DisplayName + " v" + Program.Version };
        if (_suspendedForCompatibility)
            parts.Add(L.Tray_TooltipSuspendedCompat);
        else if (IsPaused)
            parts.Add(L.Tray_TooltipPaused(FormatPauseRemaining()));
        else if (!_enabled)
            parts.Add(L.Tray_TooltipDisabled);
        else
        {
            parts.Add(L.Tray_TooltipActive);
            if (_mapper?.CapsLockActive == true)
                parts.Add(L.Tray_TooltipCapsLock);
            if (_mapper?.ActiveDeadKey != null)
                parts.Add(L.Tray_TooltipDeadKey(GetDeadKeySymbol(_mapper.ActiveDeadKey)));
            var layerState = _mapper?.MaintainableLayerState ?? MaintainableLayerState.Inactive;
            if (layerState.Mode is MaintainableLayerMode.Locked)
                parts.Add(L.Layers_TooltipLayer(GetMaintainableLayerLabel(layerState.LayerId)));
        }
        _nid.szTip = string.Join(" — ", parts);
        _nid.uFlags = NIF_TIP;
        Win32.Shell_NotifyIconW(NIM_MODIFY, ref _nid);
    }

    /// <summary>Retourne le symbole d'affichage d'une touche morte (partagé avec LearningModule).</summary>
    internal static string GetDeadKeySymbol(string deadKeyName)
    {
        return DisplayGlyph.ForStandaloneMark(deadKeyName switch
        {
            "dk_circumflex"       => "^",
            "dk_diaeresis"        => "¨",
            "dk_acute"            => "´",
            "dk_grave"            => "`",
            "dk_tilde"            => "~",
            "dk_dot_above"        => "˙",
            "dk_dot_below"        => ".",
            "dk_double_acute"     => "˝",
            "dk_double_grave"     => "̏",
            "dk_horn"             => "̛",
            "dk_hook"             => "̉",
            "dk_caron"            => "ˇ",
            "dk_ogonek"           => "˛",
            "dk_breve"            => "˘",
            "dk_inverted_breve"   => "̑",
            "dk_stroke"           => "/",
            "dk_horizontal_stroke"=> "−",
            "dk_macron"           => "¯",
            "dk_extended_latin"   => "ə",
            "dk_cedilla"          => "¸",
            "dk_comma"            => ",",
            "dk_phonetic"         => "ʁ",
            "dk_ring_above"       => "˚",
            "dk_greek"            => "µ",   // U+00B5 MICRO SIGN (cohérent layout AZ Global)
            "dk_cyrillic"         => "я",   // minuscule (cohérent web)
            "dk_misc_symbols"     => "→",
            "dk_scientific"       => "±",
            "dk_currencies"       => "¤",
            "dk_punctuation"      => "§",
            _ => "◌"  // DOTTED CIRCLE — fallback identique au web
        });
    }

    /// <summary>
    /// Sollicitation d'avis : deux essais au maximum sur toute la vie de l'installation,
    /// déclenchés par l'usage réel et non par le calendrier. Quelqu'un qui a installé
    /// l'application puis l'a oubliée n'a rien à en dire ; le J+7 calendaire de la v1.1
    /// le sollicitait quand même.
    ///
    /// Essai 1 (v1.3.0) : <see cref="UsageStats.EnrichedCharsReviewThreshold"/> caractères
    /// que l'AZERTY traditionnel de Windows ne donne pas. Aucun plancher de jours — le
    /// compte de caractères dit ce qu'un compte de jours ne disait pas : ce qui a servi.
    /// Essai 2 : <see cref="ReviewPromptSecondActiveDays"/> jours d'usage distincts, et
    /// <see cref="ReviewPromptSecondMinGapDays"/> jours au moins après l'essai 1 — sans ce
    /// plancher les deux notifications tombent dans la même semaine et la seconde n'a rien
    /// de neuf à dire.
    ///
    /// Deux chemins y mènent : le démarrage de l'application, et
    /// <see cref="MaybeShowReviewAfterQuietTyping"/>, qui attend le franchissement du seuil
    /// puis une pause dans la frappe. Les gardes ci-dessous valent pour les deux.
    ///
    /// Le second essai est abandonné si le premier a été cliqué (l'utilisateur a répondu,
    /// peu importe ce qu'il a fait ensuite) ou si l'application n'a plus servi depuis plus
    /// de <see cref="ReviewPromptStaleDays"/> jours. Aucune sollicitation dans les
    /// <see cref="ReviewPromptErrorCooldownHours"/> heures qui suivent une erreur
    /// journalisée : on ne demande pas un avis à quelqu'un qui vient d'avoir un problème.
    ///
    /// En packagé la cible est toujours la fiche Store — le tirage 50/50 de la v1.1
    /// envoyait une sollicitation sur deux vers un canal privé, alors que la note publique
    /// est le seul levier qui manque. Hors package, toujours la page feedback, faute de
    /// fiche à noter.
    ///
    /// Marquée comme faite dès l'affichage : une notification manquée consomme l'essai,
    /// mais il en reste un second, ce que la v1.1 n'offrait pas.
    /// Retourne true si la notification a été affichée.
    /// </summary>
    /// <summary>
    /// Reste-t-il une sollicitation d'avis à faire sur cette installation ? Ne lit que des
    /// réglages persistés, jamais l'usage : sert à décider si le timer de quiétude a une
    /// raison de tourner, pas si la sollicitation doit partir.
    /// </summary>
    private static bool ReviewPromptStillPossible()
    {
        try
        {
            return PolicyManager.ExternalLinksEnabledNow
                && ConfigManager.NotificationsEnabled
                && !ConfigManager.ReviewPromptClicked
                && ConfigManager.ReviewPromptCount < 2;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("ReviewPromptStillPossible", ex);
            return false;
        }
    }

    /// <summary>
    /// Le franchissement du seuil enrichi doit-il être consommé après une tentative de
    /// sollicitation ? Décision pure, sans état ni I/O, pour rester éprouvable.
    ///
    /// Oui dans deux cas seulement : la sollicitation est partie — elle ne se rejoue pas —
    /// ou plus aucune sollicitation n'est possible, auquel cas le signal ne sert plus à
    /// rien. Tout autre refus est provisoire : un plancher encore atteignable dans cette
    /// session de processus (minutes actives, caractères enrichis, fenêtre après erreur,
    /// jours actifs). Le signal reste alors armé et le prochain silence retentera.
    ///
    /// Avant le 2026-09-22, le drapeau était consommé avant même la tentative : franchir
    /// le seuil à 9 minutes actives brûlait la sollicitation pour toute la session de
    /// processus, le plancher des 10 minutes n'étant jamais rattrapé avant un
    /// redémarrage (R8 de la revue du 2026-09-21).
    /// </summary>
    internal static bool ShouldConsumeEnrichedSignal(bool promptShown, bool stillPossible)
        => promptShown || !stillPossible;

    /// <summary>
    /// Sollicitation d'avis déclenchée par la frappe (v1.3.0, décision du 2026-09-21).
    ///
    /// Part quand l'utilisateur vient de franchir le seuil de caractères enrichis — ceux
    /// que l'AZERTY traditionnel de Windows ne donne pas — et qu'il a cessé de taper
    /// depuis <see cref="REVIEW_QUIET_SILENCE_MS"/> millisecondes. C'est le seul moment où
    /// la valeur du produit vient d'être ressentie et où l'interruption ne coupe rien.
    ///
    /// Le drapeau marque une TRANSITION observée dans cette session de processus, jamais
    /// un état : quelqu'un qui démarre déjà au-dessus du seuil ne le lève pas, et c'est le
    /// chemin de démarrage qui le rattrape. Il n'est désarmé que si la sollicitation est
    /// partie, ou si plus aucune n'est possible : voir
    /// <see cref="ShouldConsumeEnrichedSignal"/>.
    /// </summary>
    private void MaybeShowReviewAfterQuietTyping()
    {
        try
        {
            // Plus rien à solliciter : le timer n'a plus de raison de tourner.
            if (!ReviewPromptStillPossible())
            {
                Win32.KillTimer(_hWnd, (UIntPtr)TIMER_REVIEW_QUIET);
                return;
            }

            if (!UsageStats.EnrichedThresholdCrossed) return;
            // Encore en train de taper : on repasse dans cinq secondes.
            if (UsageStats.MillisecondsSinceLastRemap < REVIEW_QUIET_SILENCE_MS) return;
            // Accueil ouvert : l'avis est différé, pas annulé — même règle qu'au démarrage.
            if (_reviewPromptDeferred) return;

            bool shown = MaybeShowReviewPrompt();
            if (ShouldConsumeEnrichedSignal(shown, ReviewPromptStillPossible()))
                UsageStats.ClearEnrichedThresholdSignal();
        }
        catch (Exception ex)
        {
            ConfigManager.Log("MaybeShowReviewAfterQuietTyping", ex);
        }
    }

    private bool MaybeShowReviewPrompt()
    {
        try
        {
            // Canal sobre : aucune sollicitation d'avis (D3). Second garde nécessaire et non
            // redondant — le chemin par partage a le sien dans ReviewSharePrompt, les deux
            // sont séparés depuis le 2026-08-18, et éteindre celui-ci n'éteint pas l'autre.
            //
            // Depuis le lot C, la condition est « les liens externes sont-ils permis » : elle
            // dit strictement plus que le canal sobre, une politique pouvant aussi les
            // éteindre sur le canal Store. Cette sollicitation ouvre la fiche Store, donc
            // elle tombe avec eux.
            if (!PolicyManager.ExternalLinksEnabledNow) return false;
            if (!ConfigManager.NotificationsEnabled) return false;

            int already = ConfigManager.ReviewPromptCount;
            if (already >= 2 || ConfigManager.ReviewPromptClicked) return false;

            var lastError = ConfigManager.LastErrorUtc;
            if (lastError.HasValue &&
                DateTime.UtcNow - lastError.Value < TimeSpan.FromHours(ReviewPromptErrorCooldownHours))
                return false;

            // Sans première frappe remappée, l'application n'a jamais servi : rien à noter.
            var firstRemap = UsageStats.FirstRemapDate;
            if (firstRemap == null) return false;

            var today = DateOnly.FromDateTime(DateTime.Now);
            int activeDays = UsageStats.ActiveDaysCount;
            int attempt = already + 1;

            if (attempt == 1)
            {
                // v1.3.0 : preuve d'usage par les caractères qu'AZERTY Global apporte, et
                // non par un compte de jours. Remplace les deux planchers calendaires.
                if (UsageStats.TotalSpecialCharsCount < UsageStats.EnrichedCharsReviewThreshold)
                    return false;
                // ... et l'application doit avoir servi assez longtemps. Sans ce plancher,
                // un seul texte accentué suffit à solliciter.
                if (UsageStats.TotalActiveMinutes < ReviewPromptFirstMinActiveMinutes)
                    return false;
            }
            else
            {
                if (activeDays < ReviewPromptSecondActiveDays) return false;
                // Null pour une installation migrée depuis la v1.1, qui ne connaissait pas
                // cette date : l'essai 1 y est forcément ancien, le plancher est acquis.
                var lastShown = ConfigManager.ReviewPromptLastShown;
                if (lastShown.HasValue &&
                    today.DayNumber - lastShown.Value.DayNumber < ReviewPromptSecondMinGapDays)
                    return false;
                var lastActive = UsageStats.LastActiveDate;
                if (lastActive == null ||
                    today.DayNumber - lastActive.Value.DayNumber > ReviewPromptStaleDays)
                    return false;
            }

            // La date persistée ici est aussi ce qui fait primer l'avis sur le rappel
            // Défi du jour : TrainingReminders la relit dans ses signaux.
            ConfigManager.RecordReviewPromptShown(today);
            bool toStore = ConfigManager.IsPackaged;
            string title = L.Tray_ReviewPromptTitle(attempt);
            string body = toStore ? L.Tray_ReviewPromptBodyStore(attempt) : L.Tray_ReviewPromptBodyFeedback(attempt);

            // v1.2.0 : en packagé avec activateur COM enregistré, passer par un vrai toast
            // (clic livré au processus vivant, plus aucune seconde instance). Repli balloon
            // si l'enregistrement ou l'affichage échoue.
            if (ConfigManager.IsPackaged && _toastActivatorRegistered &&
                ToastActivation.TryShowToast(title, body, L.Tray_ToastBtnRate,
                    toStore ? "action=review&target=store" : "action=review&target=feedback"))
                return true;

            ShowBalloon(title, body);
            _pendingBalloon = PendingBalloonKind.Review;
            _reviewTargetIsStore = toStore;
            return true;
        }
        catch (Exception ex)
        {
            ConfigManager.Log("MaybeShowReviewPrompt", ex);
            return false;
        }
    }

    /// <summary>
    /// Cible du clic sur la sollicitation d'avis. Le volet d'avis Store n'est ouvert
    /// que pour une install packagée (les autres ne peuvent en général pas noter) ;
    /// le paramètre source=app-notification permet d'attribuer les retours côté site.
    /// </summary>
    private void OpenReviewTarget(bool toStore)
    {
        // L'utilisateur a répondu à la sollicitation : plus aucune relance, quoi qu'il
        // fasse ensuite sur le Store — nous n'avons aucun moyen de le savoir, et lui
        // redemander après qu'il a joué le jeu serait le pire des cas.
        try { ConfigManager.SetReviewPromptClicked(); } catch (Exception ex) { ConfigManager.Log("SetReviewPromptClicked", ex); }

        if (toStore && ConfigManager.IsPackaged)
            OpenStoreReview();
        else
            OpenFeedback("app-notification", "/feedback?source=app-notification");
    }

    /// <summary>
    /// Ouvre la notation. La boîte intégrée du Store est tentée d'abord : elle recueille
    /// la note sans quitter AZERTY Global, là où <see cref="StoreReviewUrl"/> impose une
    /// bascule vers l'application Store et l'attente de son chargement. Repli sur le lien
    /// profond dès que l'API n'est pas disponible — installation hors Store, Store absent
    /// du système, ou refus de l'API.
    /// </summary>
    private void OpenStoreReview()
    {
        if (StoreReview.TryShow(ReviewOwnerWindow(), StoreReviewUrl)) return;
        Win32.ShellExecuteW(IntPtr.Zero, "open", StoreReviewUrl, null, null, 1);
    }

    /// <summary>
    /// « Noter sur le Microsoft Store » depuis le menu de la zone de notification. Aller
    /// chercher l'entrée soi-même est le signal d'intention le plus net dont dispose
    /// l'application : plus aucune sollicitation ensuite, et relancer quelqu'un qui vient
    /// de jouer le jeu serait le pire des cas (arbitrage du 2026-08-17). Le compteur
    /// d'essais n'est pas touché : cette ouverture n'est pas un essai que l'application
    /// s'est accordé, c'est une action de l'utilisateur.
    /// </summary>
    private void OnRateStoreFromMenu()
    {
        try { ConfigManager.SetReviewPromptClicked(); }
        catch (Exception ex) { ConfigManager.Log("OnRateStoreFromMenu", ex); }
        OpenStoreReview();
    }

    /// <summary>
    /// Fenêtre propriétaire de la boîte de notation. L'API exige un HWND du processus ;
    /// une fenêtre visible est préférée quand l'utilisateur en a une sous les yeux, sinon
    /// la fenêtre de la zone de notification, masquée mais bien réelle.
    /// </summary>
    private IntPtr ReviewOwnerWindow()
    {
        if (_lessons?.IsVisible == true && _lessons.Handle != IntPtr.Zero)
            return _lessons.Handle;
        return _hWnd;
    }

    /// <summary>
    /// L'utilisateur vient de copier son résultat de défi puis de refermer la fenêtre.
    /// Le geste de partage est le signal de promotion le plus net dont dispose
    /// l'application : il vaut mieux que n'importe quel seuil de jours, et c'est le seul
    /// chemin de sollicitation qui atteint aussi ceux qui ont coupé les notifications
    /// Windows — la sollicitation par toast, elle, ne leur parvient jamais.
    ///
    /// Les garde-fous de <see cref="MaybeShowReviewPrompt"/> restent en vigueur : deux
    /// essais au maximum sur la vie de l'installation, aucun après une réponse, aucun dans
    /// les 48 heures qui suivent une erreur journalisée, un seul par jour.
    /// </summary>
    private void OnChallengeShared() => MaybeShowReviewAfterShare("challenge");

    /// <summary>Résumé de statistiques copié puis fenêtre refermée. Le bouton de copie
    /// existe depuis la v1.1 sans avoir jamais rien armé, alors que c'est le même geste de
    /// promotion que le partage d'un résultat de défi (décision du 2026-08-17).</summary>
    private void OnStatsShared() => MaybeShowReviewAfterShare("stats");

    /// <summary>
    /// Un partage vient d'aboutir : ouvrir la boîte de notation Store si les gardes le
    /// permettent. La règle de décision vit dans <see cref="ReviewSharePrompt"/>, pure et
    /// testable sans fenêtre — R1 et R3 de l'audit v1.2.0 sont restés invisibles tant
    /// qu'elle était mêlée à l'affichage.
    /// </summary>
    private void MaybeShowReviewAfterShare(string source)
    {
        try
        {
            var signals = ReviewSharePrompt.Snapshot();
            if (!ReviewSharePrompt.ShouldPrompt(signals)) return;

            if (!StoreReview.TryShow(_hWnd, StoreReviewUrl)) return;

            // Consommé une fois la boîte lancée. `TryShow` ne dit que « opération
            // lancée » : la boîte a besoin de la boucle de messages de ce thread, donc
            // rien ne l'attend (voir StoreReview) — mais ses deux chemins d'échec
            // ouvrent le lien profond du Store, si bien qu'un essai n'est jamais
            // consommé sans issue pour l'utilisateur.
            //
            // Un essai, pas la vie de l'installation (arbitrage du 2026-08-17). La boîte
            // s'est affichée, mais Windows ne dit jamais si l'utilisateur a déposé sa note :
            // poser aussi `reviewPromptClicked` faisait qu'un seul partage — même refermé
            // aussitôt sans rien noter — éteignait toute sollicitation ultérieure. Le
            // plafond de deux essais, la règle « une seule par jour » sur la date
            // persistée, les seuils d'usage et le silence de 48 h après une erreur
            // restent les garde-fous, tous réunis dans ReviewSharePrompt.
            ConfigManager.RecordReviewPromptShown(signals.Today);
        }
        catch (Exception ex)
        {
            ConfigManager.Log($"TrayApplication.MaybeShowReviewAfterShare({source})", ex);
        }
    }

    /// <summary>Libellé localisé d'une couche maintenable (indicateur, infobulle).</summary>
    internal static string GetMaintainableLayerLabel(string? layerId) => layerId switch
    {
        "dk_greek" => L.Layers_LabelGreek,
        "dk_cyrillic" => L.Layers_LabelCyrillic,
        "dk_scientific" => L.Layers_LabelScientific,
        _ => ""
    };

    private void ShowBalloon(string title, string text)
    {
        if (!ConfigManager.NotificationsEnabled) return;
        _pendingBalloon = PendingBalloonKind.None; // toute nouvelle balloon remplace la précédente
        RefreshBalloonIcon();
        _nid.uFlags = NIF_INFO;
        _nid.szInfoTitle = title;
        _nid.szInfo = text;
        _nid.hBalloonIcon = _hBalloonIcon;
        _nid.dwInfoFlags = NIIF_USER | NIIF_LARGE_ICON;
        Win32.Shell_NotifyIconW(NIM_MODIFY, ref _nid);
    }

    /// <summary>
    /// (Re)crée l'icône balloon à la taille SM_CXICON du moment. NIIF_LARGE_ICON fait
    /// rejeter Shell_NotifyIconW en silence quand l'icône n'a pas exactement cette
    /// taille, qui suit l'échelle d'affichage — 40 px à 125 % (F6, smoke v1.2.0).
    /// Recréation à chaque bulle : l'échelle peut changer en cours de session.
    /// </summary>
    private void RefreshBalloonIcon()
    {
        int size = Win32.GetSystemMetrics(SM_CXICON);
        var fresh = CreateLogoIcon(size);
        if (fresh == IntPtr.Zero)
            fresh = CreateTextIcon("AG", true, size: size);
        if (_hBalloonIcon != IntPtr.Zero)
            Win32.DestroyIcon(_hBalloonIcon);
        _hBalloonIcon = fresh;
    }

    /// <summary>Logo du produit (favicon embarqué) en HICON à la taille demandée, fond
    /// transparent — même recette GDI+ que l'icône de fenêtre d'AboutWindow. Zéro si un
    /// maillon échoue : l'appelant retombe sur l'icône texte.</summary>
    private static IntPtr CreateLogoIcon(int size)
    {
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        if (Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero) != 0)
            return IntPtr.Zero;
        IntPtr logo = IntPtr.Zero, bmp32 = IntPtr.Zero;
        try
        {
            logo = GdiImageLoader.LoadFromEmbeddedResource(typeof(TrayApplication), ProductIdentity.LogoResourceName);
            if (logo == IntPtr.Zero) return IntPtr.Zero;
            if (Win32.GdipCreateBitmapFromScan0(size, size, 0, 0x0026200A, IntPtr.Zero, out bmp32) != 0) return IntPtr.Zero;
            if (Win32.GdipGetImageGraphicsContext(bmp32, out IntPtr g) != 0) return IntPtr.Zero;
            Win32.GdipSetSmoothingMode(g, 4);
            Win32.GdipSetInterpolationMode(g, 7);
            Win32.GdipDrawImageRectI(g, logo, 0, 0, size, size);
            Win32.GdipDeleteGraphics(g);
            if (Win32.GdipCreateHBITMAPFromBitmap(bmp32, out IntPtr hBmp, 0x00000000) != 0) return IntPtr.Zero;
            // Lignes du masque 1 bpp alignées au mot : 6 octets par ligne à 40 px, pas 5.
            var maskBits = new byte[GdiHelpers.MonochromeMaskByteCount(size, size)];
            IntPtr hMask = Win32.CreateBitmap(size, size, 1, 1, maskBits);
            var iconInfo = new Win32.ICONINFO { fIcon = true, hbmMask = hMask, hbmColor = hBmp };
            IntPtr hIcon = Win32.CreateIconIndirect(ref iconInfo);
            Win32.DeleteObject(hMask);
            Win32.DeleteObject(hBmp);
            return hIcon;
        }
        finally
        {
            if (bmp32 != IntPtr.Zero) Win32.GdipDisposeImage(bmp32);
            if (logo != IntPtr.Zero) Win32.GdipDisposeImage(logo);
            Win32.GdiplusShutdown(token);
        }
    }

    /// <summary>
    /// Bulle de notification de sécurité. Bypass <see cref="ConfigManager.NotificationsEnabled"/>
    /// car cette information ne doit pas être manquée par l'utilisateur (anti-cheat,
    /// override invalide cleanup, etc.).
    /// </summary>
    private void ShowSecurityBalloon(string title, string text)
    {
        _pendingBalloon = PendingBalloonKind.None; // toute nouvelle balloon remplace la précédente
        _nid.uFlags = NIF_INFO;
        _nid.szInfoTitle = title;
        _nid.szInfo = text;
        _nid.dwInfoFlags = NIIF_WARNING;
        Win32.Shell_NotifyIconW(NIM_MODIFY, ref _nid);
    }

    /// <summary>
    /// Handler du changement de mode foreground. Désactive auto si entrée dans un process
    /// anti-cheat (avec bulle explicative), réactive auto à la sortie (avec bulle de retour).
    /// Ne touche PAS <see cref="_enabled"/> qui reflète la volonté manuelle utilisateur :
    /// on n'agit que sur <see cref="KeyboardHook.Enabled"/>.
    /// </summary>
    private void OnForegroundChanged()
    {
        if (_foregroundMonitor == null || _hook == null || _mapper == null) return;
        _mapper.RefreshForegroundContext();
        var mode = _foregroundMonitor.CurrentMode;
        var procName = _foregroundMonitor.CurrentProcessName ?? "";
        var procDisplay = FormatProcessName(procName);
        var reason = _foregroundMonitor.CurrentSuspendReason;

        switch (ClassifySuspensionTransition(mode, _suspendedForCompatibility, _appliedSuspendReason, reason))
        {
            case SuspensionTransition.Enter:
            {
                // Entrée dans une application qui impose une suspension de compatibilité.
                if (_enabled && ShouldProcessHook)
                {
                    _wasEnabledBeforeAutoDisable = true;
                    // La cible est déjà active : conserver les relâchements jusqu'à la reprise sûre.
                    _mapper.ClearPassedThroughKeys(emitReleases: false);
                }
                _suspendedForCompatibility = true;
                _appliedSuspendReason = reason;
                ApplyHookState();
                UpdateIcon();
                UpdateTooltip();
                break;
            }

            case SuspensionTransition.ReasonChanged:
            {
                // Toujours suspendu, mais plus pour la même raison : le hook est encore
                // configuré pour l'application précédente. Sans ce cas, un override
                // utilisateur suivi d'un jeu anti-triche laissait ShortcutsWhilePassThrough
                // à true dans le jeu, et l'ordre inverse le laissait à false, où Ctrl+Maj+W
                // atteignait l'application au lieu d'ouvrir la recherche de caractères.
                _appliedSuspendReason = reason;
                ApplyHookState();
                UpdateIcon();
                UpdateTooltip();
                break;
            }

            case SuspensionTransition.Leave:
            {
                // Sortie de l'application suspendue : réactivation si on était actif avant.
                _suspendedForCompatibility = false;
                _appliedSuspendReason = CompatibilitySuspendReason.None;
                if (_wasEnabledBeforeAutoDisable && _enabled)
                {
                    ApplyHookState(syncWhenActive: true);
                    _announcementState.ResumeOwed = true;
                }
                else
                {
                    // syncWhenActive aussi ici : si _enabled est true (ex. pause expirée
                    // pendant la partie), le hook redevient actif et l'état CapsLock/modifs/DK
                    // doit être resynchronisé (correctif audit 2026-07 m1). No-op si inactif.
                    ApplyHookState(syncWhenActive: true);
                }
                _wasEnabledBeforeAutoDisable = false;
                UpdateIcon();
                UpdateTooltip();
                break;
            }
        }

        AnnounceCompatibilityStateIfChanged(procName, procDisplay);

        // L'indicateur de couche dépend du process foreground, de l'état sécurisé
        // et de ShouldProcessHook (modifié ci-dessus) : rafraîchir en dernier.
        ApplyStateChange();
    }

    /// <summary>Ce que l'utilisateur a déjà appris, et ce qu'on lui doit encore.</summary>
    internal struct CompatibilityAnnouncementState
    {
        public CompatibilitySuspendReason AnnouncedReason;
        public ForegroundProcessIdentity AnnouncedApplication;

        /// <summary>Une reprise a eu lieu mais n'a pas encore été annoncée, parce que le
        /// premier plan du moment était une surface du shell.</summary>
        public bool ResumeOwed;
    }

    internal enum CompatibilityAnnouncement { None, Suspension, Resumed }

    private CompatibilityAnnouncementState _announcementState;

    /// <summary>
    /// Décide si l'état de compatibilité doit être annoncé à l'utilisateur (R15 de la revue
    /// du 2026-09-21).
    ///
    /// Un Alt+Tab entre deux applications suspendues passait par le sélecteur de tâches, qui
    /// appartient à <c>explorer.exe</c> et se résout en <c>Default</c> : la sortie de
    /// suspension déclenchait « à nouveau actif », l'arrivée déclenchait la bulle de sécurité,
    /// et l'utilisateur prenait deux bulles par bascule — dont une qui ignore le réglage des
    /// notifications. Un aller-retour par la barre des tâches, sans changer d'application du
    /// tout, en produisait autant.
    ///
    /// Deux règles, et elles suffisent. Tant que le premier plan est une surface éphémère du
    /// shell, rien ne s'annonce : cet état-là ne dure pas, et le prochain événement dira la
    /// vérité. Et une fois le premier plan posé, l'annonce ne part que si elle dit autre chose
    /// que la précédente — même motif dans la même application, l'utilisateur le sait déjà.
    ///
    /// ⛔ Seules les bulles passent par ici. Le hook, l'icône, l'infobulle et les raccourcis
    /// suivent le premier plan réel sans délai, y compris sur le shell : c'est ce que le
    /// correctif de la course avec le shell a établi en 1.3.0, et une annonce retardée n'est
    /// pas une protection retardée.
    /// </summary>
    internal static CompatibilityAnnouncement NextAnnouncement(
        ref CompatibilityAnnouncementState state,
        bool transientShellForeground,
        CompatibilitySuspendReason current,
        ForegroundProcessIdentity currentApplication)
    {
        // Tant que le premier plan est une surface éphémère du shell, rien ne s'annonce :
        // cet état ne dure pas, et le prochain événement dira la vérité. Ce qui est dû
        // reste dû.
        if (transientShellForeground) return CompatibilityAnnouncement.None;

        if (current == state.AnnouncedReason && currentApplication == state.AnnouncedApplication)
            return CompatibilityAnnouncement.None;

        state.AnnouncedReason = current;
        state.AnnouncedApplication = currentApplication;

        if (current != CompatibilitySuspendReason.None)
        {
            state.ResumeOwed = false;
            return CompatibilityAnnouncement.Suspension;
        }

        if (!state.ResumeOwed) return CompatibilityAnnouncement.None;
        state.ResumeOwed = false;
        return CompatibilityAnnouncement.Resumed;
    }

    private void AnnounceCompatibilityStateIfChanged(string procName, string procDisplay)
    {
        var reason = _suspendedForCompatibility
            ? _appliedSuspendReason : CompatibilitySuspendReason.None;
        var application = _suspendedForCompatibility
            ? (_foregroundMonitor?.LastApplicationIdentity ?? default) : default;

        switch (NextAnnouncement(ref _announcementState,
                    _foregroundMonitor?.IsTransientShellForeground == true, reason, application))
        {
            case CompatibilityAnnouncement.Suspension:
                AnnounceSuspension(reason, procName, procDisplay);
                break;
            case CompatibilityAnnouncement.Resumed:
                ShowBalloon(L.Tray_ActiveAgainTitle, L.Tray_ActiveAgain);
                break;
        }
    }

    /// <summary>
    /// Applique un override utilisateur (Auto/forceOn/forceOff) sur le process foreground actuel.
    /// Refuse forceOn sur process anti-cheat (sécurité utilisateur) avec bulle explicative.
    /// </summary>
    private string? _compatibilityMenuProcessName;
    private string? _compatibilityMenuFullPath;

    private void ApplyCompatibilityOverride(string? mode)
    {
        var proc = _compatibilityMenuProcessName;
        if (string.IsNullOrEmpty(proc)) return;

        if (mode == "forceOn" && GameRegistry.IsRemoteAccessProcess(proc))
        {
            ShowSecurityBalloon(L.Tray_ForceRefusedTitle, L.Tray_ForceOnRemoteRefused);
            return;
        }

        if (mode == "forceOn" && GameRegistry.IsAntiCheatProcess(proc, _compatibilityMenuFullPath))
        {
            ShowSecurityBalloon(L.Tray_ForceRefusedTitle, L.Tray_AntiCheatRefusedBody);
            return;
        }

        ConfigManager.SetCompatibilityOverride(proc, mode);
        _foregroundMonitor?.Recompute();
    }

    /// <summary>
    /// Crée une icône 32x32 avec texte sur fond coloré.
    /// Bleu = actif, gris = inactif. Barre orange en bas si CapsLock.
    /// </summary>
    private static IntPtr CreateTextIcon(string text, bool active, bool capsLock = false, bool autoDisabled = false, int size = 32)
    {

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBitmap = Win32.CreateCompatibleBitmap(hdcScreen, size, size);
        var hBitmapOld = Win32.SelectObject(hdc, hBitmap);

        // Fond coloré (COLORREF = 0x00BBGGRR)
        uint bgColor = active ? 0x00D47800u : 0x00808080u; // Bleu Windows / Gris
        var hBrush = Win32.CreateSolidBrush(bgColor);
        var rect = new Win32.RECT { left = 0, top = 0, right = size, bottom = size };
        Win32.FillRect(hdc, ref rect, hBrush);
        Win32.DeleteObject(hBrush);

        // Indicateur CapsLock : barre orange en bas de l'icône
        if (capsLock)
        {
            var capsBar = new Win32.RECT { left = 0, top = size - 5, right = size, bottom = size };
            var hCapsBrush = Win32.CreateSolidBrush(0x0000A5FFu); // Orange (BBGGRR)
            Win32.FillRect(hdc, ref capsBar, hCapsBrush);
            Win32.DeleteObject(hCapsBrush);
        }

        // Indicateur "désactivé auto pour anti-cheat" : carré rouge en bas-droite
        if (autoDisabled)
        {
            var dot = new Win32.RECT { left = size - 12, top = size - 12, right = size - 2, bottom = size - 2 };
            var hRedBrush = Win32.CreateSolidBrush(0x000000FFu); // Rouge (BBGGRR)
            Win32.FillRect(hdc, ref dot, hRedBrush);
            Win32.DeleteObject(hRedBrush);
        }

        // Adapter la taille de police : plus petite pour les symboles longs, plus grande pour "AG"
        int fontSize = (text.Length <= 2 ? 22 : 18) * size / 32;
        // Remonter légèrement le texte si la barre CapsLock est présente
        var textRect = capsLock
            ? new Win32.RECT { left = 0, top = 0, right = size, bottom = size - 4 }
            : rect;

        var hFont = Win32.CreateFontW(fontSize, 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        var hFontOld = Win32.SelectObject(hdc, hFont);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, 0x00FFFFFFu); // Blanc
        Win32.DrawTextW(hdc, text, text.Length, ref textRect, Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

        Win32.SelectObject(hdc, hFontOld);
        Win32.DeleteObject(hFont);
        Win32.SelectObject(hdc, hBitmapOld);

        // Masque (tout noir = tout opaque)
        var maskBits = new byte[GdiHelpers.MonochromeMaskByteCount(size, size)];
        var hMask = Win32.CreateBitmap(size, size, 1, 1, maskBits);

        var iconInfo = new Win32.ICONINFO
        {
            fIcon = true,
            hbmMask = hMask,
            hbmColor = hBitmap
        };
        var hIcon = Win32.CreateIconIndirect(ref iconInfo);

        // Nettoyage GDI
        Win32.DeleteObject(hMask);
        Win32.DeleteObject(hBitmap);
        Win32.DeleteDC(hdc);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        return hIcon;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
