# A1 : l'app 1.3.0 ne démarre pas dans Windows Sandbox (diagnostic du 2026-11-01)

Candidat : `AZERTYGlobal-1.3.0.0.msixbundle`, SHA-256 `22f4b361…20d5` (bundle CI `agci`).
Preuves : `../evidence-a1/installation-essai1-sleep25.txt`, `../evidence-a1/installation-essai2-sac-coupe.txt`.

## Cause (prouvée)

Smart App Control (SAC) est en application dans la VM du Sandbox : `VerifiedAndReputablePolicyState = 1`
(l'hôte est à 0 depuis le 2026-09-29). L'exe du paquet, signé par un certificat jetable, est refusé.

- Essai 1 (installation + `Start-Sleep 25`) : `Add-AppxPackage` réussit (événement AppX 478/400), aucun processus
  `AZERTY*` après 25 s. Lancement direct de `AZERTY Global.exe` : « An Application Control policy has blocked this file. »
- Essai 2 (même script + coupure de SAC) : `VerifiedAndReputablePolicyState` 1 → 0,
  `CiTool --refresh` « Operation Successful », app lancée en < 25 s (pid 15156, shadow stack ON, CFG ON).

## Hypothèses

- H1 « délai trop court » : écartée. 25 s ne change rien tant que SAC est à 1.
- H2 « erreur de lancement AUMID » : écartée. Le lancement direct de l'exe est bloqué de la même façon.
- H3 « l'installateur coupait déjà SAC » : fausse. `git log -S VerifiedAndReputable` ne trouve rien dans ce dossier ;
  la phrase de la mémoire projet (2026-09-29, 2026-10-22) décrivait un état attendu, pas le code.
- Non élucidé : pourquoi les recettes du 23/09 et du 23/10 passaient (le SAC de la VM peut avoir changé avec une
  mise à jour de l'image du Sandbox ; non vérifié).

## Correctif (installer-candidat.ps1)

Avant le lancement : si SAC ≠ 0, écrire 0 dans `HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy`, `CiTool --refresh`,
journaliser l'état. Attente portée à 25 s (non nécessaire d'après l'essai 2, qui a démarré vite : non testé à 8 s) et
diagnostic ajouté en cas d'échec (événements, lancement direct, code de sortie). Effet limité à la VM jetable.

## Reste

- Rejouer `lancer-recette.ps1 -Auto` complet avec ce script (R20 à R75) : non fait ici.
- Le SAC du Sandbox bloque aussi tout utilisateur réel avec SAC actif (déjà noté dans la mémoire du 2026-09-29).
