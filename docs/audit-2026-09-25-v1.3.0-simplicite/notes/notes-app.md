# Zone app — synthèse (audit 1.3.0, `f0a98ba`, 2026-09-25)

**Vue d'ensemble.** La coquille applicative fonctionne, mais sa complexité se concentre en deux points : `TrayApplication` (3 076 l., huit responsabilités, état d'activation implicite) et le circuit des sollicitations (avis, rappel, annonce, relance : 25 conditions, 9 clés persistées, 9 drapeaux en mémoire). C'est là que la 1.4.0 sera risquée à modifier (A-01, A-02, A-03). Côté exécution, rien ne touche le disque à chaque frappe. Les coûts réels viennent du fil partagé avec le hook : écritures de config avec fsync (A-05), infobulle republiée à chaque Maj (A-08), hook posé avant le travail de démarrage (A-06). S'y ajoutent environ 4,9 réveils/s au repos (A-07).

**Points forts à garder.**
1. `UsageStats` ne fait aucune I/O par frappe : compteurs en mémoire sous verrou, `Preload` avant la boucle, flush différé de 5 min et à la fermeture, drapeau `_dirty`. Seuls des agrégats sont gardés, jamais de caractère ni d'horodatage par frappe.
2. Les décisions pures sont extraites et testables : précédence des politiques avec lecteur de registre injectable, `AppChannel.Classify`, les motifs « photographie + ShouldX » (`AutoStartNudge`, `TrainingReminders`, `ReviewSharePrompt`), `WindowSizing`, `ClassifySuspensionTransition`, `NextAnnouncement`. Le problème est leur nombre, pas leur forme.
3. Les données de l'utilisateur sont protégées. Côté config : écriture atomique avec `.tmp` par PID, refus d'écraser un fichier non chargé, et forme des clés inconnues conservée pour le retour arrière. Côté instance : double mutex `Local\` + `Global\` qualifié par SID, avec repli si `SeCreateGlobalPrivilege` manque. Enfin l'icône apparaît avant tout chargement lourd.

**Garde-fous (à ne pas toucher sans preuve).** Accord d'activation : aucun hook ni annonce avant l'accord, accueil saisissable avant l'activation (`ApplyWindowInputState`). Pause et suspensions : inertie totale sous anti-triche, accès distant ou premier plan inconnu. Noms de mutex : `BuildSingleInstanceMutexNames` est désormais un contrat avec la V2 (D19, la V2 recrée ce mutex). Compatibilité de config (`ConfigShapePreservationTests`, `ConfigManagerCompatTests`). Les minuteries de réinstallation du hook ne se retirent pas sans le journal `HookSilentlyDetached`.

**Chiffres.**
- Périmètre lu : ~9 240 lignes, dont `TrayApplication` 3 076 (1 989 de code, 838 de commentaires), `ConfigManager` 1 115, `Win32.cs` 982 (151 P/Invoke, dont 16 morts), `UsageStats` 668 et Localisation 1 010 (411 membres, 4 morts).
- 20 constats : 4 majeurs, 9 moyens, 7 mineurs. Gain cumulé estimé : −755 lignes, dont −92 prouvées par comptage (A-12, A-14, A-15, A-20).
- Mesures hors application (scratchpad/audit/bench-app, JIT .NET 8, ce PC, pas l'AOT) :
  - `Save` de config : 3,33 ms ;
  - parse de `character-index.json` (606 Ko) : 11,5 ms ;
  - `Process.GetProcesses` : 3,54 ms et ~774 Kio alloués par sonde ;
  - `JsonDocument.Parse("true")` : 0,3 µs, sans enjeu de performance.

**Réponses aux questions du mandat.**
- **Sérialisation.** Ce n'est pas du JSON maison : `ConfigManager` emploie déjà `System.Text.Json` (JsonDocument, Utf8JsonWriter). Le sac clé par clé est voulu pour le retour arrière ; seule la façon d'écrire est à simplifier, avec `JsonObject` (A-10).
- **Un seul état d'avis ?** Oui : un seul enregistrement persistant et une seule fonction de décision (A-02).
- **Win32 en double ?** Oui : 9 P/Invoke, 2 structures et 4 constantes sont doublés, sous deux classes homonymes (A-19).
- **Localisation.** Le mécanisme maison est plus simple que `.resx` pour deux langues ; seules 4 clés sont mortes (A-14, A-15).

**Signalements hors zone (non instruits).**
- `VirtualKeyboard.cs:842-845` : sauvegarde de config à chaque WM_SIZE visible.
- `MaintainableLayersWindow.cs:234-241` : 7 sauvegardes d'affilée.
- `TypingEngine.Windows/GameRegistry.cs:218-237` : `Process.GetProcesses` toutes les 5 s ; Toolhelp32 serait plus sobre.
- `LessonProgressStore.cs` : troisième copie de l'écriture atomique.
- `CharacterSearch.cs:226` : parse de 606 Ko dans le constructeur.
- `KeyMapper` lève StateChanged à chaque modificateur. Le correctif est côté consommateur (A-08).
- `Changelog.md:196` dit qu'une identité illisible retombe « hors package » ; `AppChannel.Classify` la classe en canal sobre (Amcf).
- `AvisApresSeanceTests.cs:22` recopie en littéral la constante privée `REVIEW_QUIET_SILENCE_MS`.
- La version 1.3.0 est écrite en 6 endroits (csproj, `Program.Version`, 3 attributs d'`AssemblyInfo`, manifeste MSIX), gardés par `VersionAlignmentTests`.

**Limites.**
- Lus intégralement : `TrayApplication`, `ConfigManager`, `UsageStats`, `Program`, et tous les petits fichiers du mandat.
- `Win32.cs` et `Localization/*` : inventaire par script (`app_pinvoke.py`, `app_l10n_dead.py`) et lectures ciblées, pas de lecture ligne à ligne.
- Tests : seuls `ConfigShapePreservationTests` et `ConfigManagerCompatTests` sont lus en entier ; les autres uniquement par grep et en-têtes.
- Rien n'a été exécuté dans l'application : pas de lancement, pas de tests (Application Control), pas de mesure de `Shell_NotifyIcon` ni de la durée réelle du démarrage.
- Les gains en lignes sont des estimations, sauf mention « preuve ».
