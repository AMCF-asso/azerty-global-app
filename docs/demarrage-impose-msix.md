# Démarrage imposé dans un paquet MSIX : faisabilité (1.3.0)

Résumé :

- Question (segmentation du 2026-11-17, décision 3) : une structure peut-elle imposer le lancement d'AZERTY Global à l'ouverture de session ?
- Verdict : impossible de **forcer** ce lancement pour tous les utilisateurs d'un poste depuis un paquet MSIX, sans moyen non documenté.
- Choix d'Antoine du 2026-11-17 (QCM de la session de code) : stratégie `AutoStartEnabled` gérée par l'application, avec ses deux limites écrites.
- Code : `src/AutoStart.cs` (`ApplyPolicy`), appelé au démarrage par `src/TrayApplication.cs` ; tests `SegmentationStrategiesTests`.
- Preuve documentaire, pages Microsoft lues le 2026-11-17. Aucun essai sur un poste : voir E10 de la fiche d'essais 1.3.0.

## Sources

1. Le manifeste n'active la tâche qu'au premier lancement par l'utilisateur. `desktop:StartupTask Enabled` : « enable the startup behavior when the app is first launched ».
   https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop-startuptask
2. Seule voie à l'installation : `rescap5:ImmediateRegistration="true"`, qui exige la capacité personnalisée `Microsoft.nonUserConfigurableStartupTasks_8wekyb3d8bbwe` (même page). Cette capacité est délivrée par Microsoft. Elle activerait aussi le démarrage pour tout le monde par défaut, Store compris, contre la règle « rien par défaut ». Écartée.
3. L'utilisateur garde la main : « Once enabled, the user is in control » ; `RequestEnableAsync` ne passe pas outre son choix.
   https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask
4. `StartupTaskState.EnabledByPolicy` (4) existe, mais aucune page Microsoft ne donne de stratégie ou de clé pour le poser.
   https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptaskstate ; question restée sans réponse : https://techcommunity.microsoft.com/discussions/msix-discussions/control-startuptask-with-gpo/3427040
5. Seul mécanisme concret trouvé : `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\<PFN>\<TaskId>`, DWORD `State` = 4. Il est par utilisateur, non documenté par Microsoft et écrasé à chaque installation du paquet.
   https://ubuntu.com/wsl/docs/stable/howto/enforce-agent-startup-remotely-registry/
   C'est un contournement : exclu.

## Ce que fait la 1.3.0

À chaque lancement, l'application lit `AutoStartEnabled` sous `HKLM\SOFTWARE\Policies\AZERTYGlobal` :

- à 1, elle active sa propre tâche de démarrage ;
- à 0, elle la coupe ;
- dans les deux cas, la case des Paramètres est grisée.

Limites :

- À 1, l'effet ne vient qu'après un premier lancement par chaque utilisateur.
- À 1, un refus exprimé dans Windows (Gestionnaire des tâches, Paramètres > Démarrage) reste respecté. L'application le journalise (`AutoStartPolicyNotApplied` dans `error.log`), sans boîte.
- À 0, l'effet est complet.
