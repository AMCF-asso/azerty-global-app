# Audit 1.3.0 — bugs réels et textes visibles — 24 septembre 2026

Cadrage d'Antoine (QCM du 24/09) : bugs réels sur les parcours de production ; diff 1.1.0 → candidat ; bloquants seuls corrigés en 1.3.0, le reste en 1.3.1 ; traces « IA » limitées aux textes visibles.

- Candidat audité : `ade118f` (bundle CI `01b18e11…`, run 35987701131). HEAD `b9773bf` n'ajoute que de la documentation.
- Base : `ad049fc`, dernier état avant la réconciliation 1.2. La 1.1.0 publiée a été compilée hors dépôt ; la source historique a servi pour le format de configuration.
- Méthode : quatre revues en lecture seule (moteur de frappe, cycle de vie, fenêtres, textes). Les constats retenus comme bloquants ont été relus dans le code par la session principale. Rien n'a été compilé ni exécuté : **aucun constat n'est reproduit sur machine**.
- Exclus : ce que listent déjà `audit-2026-09-22-v1.3.0/rapport.md`, la section D de `feu-vert/recette.md`, `feu-vert/accessibilite-1.3.0.md` et le Changelog 1.3.0.

## Proposés bloquants pour 1.3.0 — code (reconstruction du paquet)

| # | Constat | Où (`ade118f`) | Preuve | Correctif minimal |
|---|---|---|---|---|
| B1 | **VK_PACKET remappé comme une touche physique.** Depuis `ade118f`, dès qu'un hôte distant est détecté (les services TeamViewer, AnyDesk, Parsec et Chrome Remote Desktop tournent en permanence dès que le logiciel est installé), un caractère Unicode injecté par Espanso, AutoHotkey `SendText`, un gestionnaire de mots de passe ou la dictée est interprété comme un scan code. `(3)` donne `´.@` ; avec Verr. Maj, l'espace donne `D`. Les exercices comptent aussi une faute. Même défaut pour l'Unicode d'une session RDP. | `src/TypingEngine.Windows/KeyboardHook.cs:258-266` (aucun filtre `0xE7`) | Relu dans le code ; contenu du `scanCode` de VK_PACKET documenté par Microsoft ; non reproduit | Après le test du marqueur : `if (hookStruct.vkCode == 0xE7) return CallNextHookEx(...)`. Ajouter un témoin, et la ligne de recette « parsecd lancé + AutoHotkey `SendText "(3) x"`, Verr. Maj éteint puis allumé ». |
| B2 | **Démarrage automatique activé sur croix ou Échap à l'étape 3 de l'accueil.** La case est cochée par défaut et `Close()` l'applique quel que soit le geste de fermeture. La note au certificateur (`msix/Fiche Store.md:329`) et le Changelog affirment l'inverse. | `src/OnboardingWindow.cs:712-737`, appelé par `:806`, `:911`, `:948` | Relu dans le code | `Close(bool validated)` : n'appliquer la case que depuis le bouton final ; sur croix ou Échap, seulement si l'utilisateur l'a modifiée. Remettre `_step3Reached = false`. |
| B3 | **Leçons : à 150 % et 175 % sur 1080p, le clavier dessiné recouvre la ligne cible et la saisie.** AG130-42 plafonne la fenêtre, puis `CaptureBaseWindowMetrics` prend la zone client déjà réduite comme référence : l'échelle vaut 1 dans une fenêtre trop basse. À 175 %, l'exercice est inutilisable. | `src/LessonsWindow.cs:403`, `:415`, `:563-575` | Relu dans le code ; recouvrement calculé, **pas vu à l'écran** | Prendre `D(BASE_WIN_W) - _nonClientW` et `D(BASE_WIN_H) - _nonClientH` comme référence. **À confirmer d'abord à l'écran** (recette B13 à 175 %). |
| B4 | **La recherche ouverte depuis le menu de l'icône insère le caractère dans la barre des tâches.** Au clic droit, le premier plan est `Shell_TrayWnd`, qui devient la cible. L'insertion rend `Inserted` : pas de copie de secours, et rien n'apparaît dans l'application. | `src/TrayApplication.cs:1676-1678` ; `src/CharacterSearch.cs:1146-1157` | Chemin relu ; le premier plan au clic est raisonné, pas vu | Refuser comme cible `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `NotifyIconOverflowWindow` et `TopLevelWindowForOverflowXamlIsland`, et remettre `_targetWindow` à zéro si la cible est invalide (on retombe sur la copie avec notification). |
| B5 | **Textes erronés dans le binaire** (à regrouper avec la même reconstruction) : l'erreur de démarrage envoie « contactez le support » vers `/soutien`, la page de dons (`L.Tray.cs:12`, `:15`) ; « Réversible depuis le menu », alors que l'entrée a été retirée du menu (`L.Tray.cs:195`) ; « Copie ton résultat » tutoie (`L.Challenge.cs:67`) ; l'accueil dit que la recherche « copie », alors qu'elle insère (`L.Onboarding.cs:64`). | voir ci-contre | Relu dans le code | Voir les réécritures en annexe A. |

## Bloquants pour la soumission — Partner Center (sans reconstruction)

| # | Constat | Action |
|---|---|---|
| P1 | Le bloc « Nouveautés » de la fiche fait **4 845 caractères en FR et 4 267 en EN, pour une limite de 1 500**. La 1.2.0 n'ayant jamais été publiée, la note doit couvrir 1.2 et 1.3. | Coller la version courte de l'annexe B (FR 1 172, EN 987 caractères). |
| P2 | Les notes de certification sont en français, s'arrêtent à la v1.0.0, n'ont pas de procédure de test et omettent les API 1.2 et 1.3 (lecture des processus toutes les 5 s pour les hôtes distants, `StoreContext` pour la notation, activateur COM des toasts, `SendInput`). Elles ne disent pas non plus que l'app vit dans la zone de notification. | Coller le texte anglais de l'annexe C, **mis à jour selon B2**. |
| P3 | La fiche EN annonce `azerty.global/support`, alors que le code ouvre `/soutien`. La confidentialité omet que « Donner mon avis » transmet les versions. La recherche est décrite comme « copie ». FR et EN divergent (section tutoriel, tilde). Les fonctions 1.2 et 1.3 (Leçons, Défi) sont absentes de la description. La typographie FR n'est pas corrigée (apostrophes droites, espaces insécables). | Corriger `msix/Fiche Store.md` avant de coller. |

## Reportés en 1.3.1 (majeurs ou mineurs, non bloquants)

- **`config.json` illisible** : `_loadFailed` bloque toute sauvegarde, donc l'accord n'est jamais mémorisé. L'app reste inactive à chaque démarrage, et la seule issue est de supprimer le fichier à la main (`ConfigManager.cs:119-130`, `:971-1014`). Probabilité faible, car l'écriture est atomique depuis la 1.1. Proposition : mettre le fichier en quarantaine plutôt que se bloquer, et prolonger A11 par une seconde relance.
- **Relance depuis Démarrer sans effet** quand l'instance tourne sans accord : pas de signal envoyé à l'instance vivante (`Program.cs:92-117`).
- **Sollicitation d'avis** dans la minute qui suit la mise à jour pour les utilisateurs 1.1 actifs, sans avoir tapé en 1.3 (`TrayApplication.cs:331`, `:1297-1302`, `:2371-2381`). Bornée à deux essais.
- **Bulles de suspension** (anti-triche, distant) affichées avant tout accord (`TrayApplication.cs:416-427`, `:2617`).
- **Consoles de VM** (client Windows Sandbox, `vmconnect.exe`) absentes de `RemoteAccessProcesses` (`GameRegistry.cs:24-33`). ⚠️ Conséquence pour la recette : **quitter l'app de l'hôte pendant une recette Sandbox**. Le constat de la section D « la touche `.` a mis en surbrillance la touche C » correspond exactement au mécanisme de B1 (U+002E = SC02E = C).
- **Raccourci Ctrl+Maj+Verr. Maj** : le travail d'interface s'exécute dans le rappel du hook, avec un risque de `LowLevelHooksTimeout` si explorer est lent (`KeyboardHook.cs:309-313` → `TrayApplication.cs:1691-1784`). Le défaut existait déjà avant.
- **Verr. Maj tenu** : `_capsLockState` s'inverse à chaque répétition automatique (`KeyMapper.cs:592-620`).
- **Exercices** : un Tab accidentel met le focus sur « Quitter », puis la première espace ferme le tutoriel (`LearningModule.cs:1415-1434`, `:1678-1693`). C'est une régression de K3 ; le correctif est court, à promouvoir en 1.3.0 si la reconstruction a lieu.
- **Paramètres** : les messages de retour des onglets Applications et Langue ne s'affichent jamais (`SettingsWindow.cs:916`).
- **`PauseDurationDialog`** avale `WM_QUIT` (`:76-85`), ce qui peut laisser un processus sans icône. Non vérifié ; le défaut existait déjà avant.
- **Textes** : « Couches maintenables » est un terme interne, « Reset stats » un anglicisme ; « WPM » et « 12s » en français ; « jour(s) » ; « anti-cheat » ; manifeste FR seul ; notations « Verr.Maj » incohérentes ; « AVANT le login » ; ton marketing de la description (9 emojis de titre, « zéro réapprentissage »).

## Vérifié sans défaut

- **Réinjection** : pas de boucle ; nos émissions portent le marqueur, exclu avant `ShouldTreatAsPhysical`.
- **Mutex et migration des clés de configuration 1.1 → 1.3** : même nom et même type, `onboardingDone` migré.
- **Cycle de vie Windows** : `TaskbarCreated`, veille, changement de session et `WM_QUERYENDSESSION` fonctionnent.
- **Interface** : aucune fuite GDI dans les huit `OnPaint` ; divisions protégées ; presse-papiers restauré.
- **Textes** : aucun identifiant interne (AG130, S2, R17) visible dans l'interface.

## Conséquence sur le calendrier

B1 à B5 changent le binaire. Le bundle `01b18e11` n'est donc plus le candidat : il faut une nouvelle CI, refaire la recette A (ciblée sur B1 à B4, plus A2, E1 et B13) et le WACK, puis soumettre. P1 à P3 se font dans Partner Center, en parallèle.

---

## Annexe A — réécritures dans le binaire

- `L.Tray.cs:12/15` : « Si le problème persiste, écrivez à contact@azerty.global. » / “If the problem persists, email contact@azerty.global.” (ou l'URL `/bug`).
- `L.Tray.cs:195` : « Vous pouvez changer ce choix dans les Paramètres. » / “You can change this in Settings.”
- `L.Challenge.cs:67` : « Copie votre résultat du jour, prêt à coller dans une conversation. »
- `L.Onboarding.cs:64` : « …tapez le nom d'un caractère : Entrée l'insère dans votre texte et le clavier virtuel montre comment le taper. »

## Annexe B — Nouveautés proposées (FR, 1 172 caractères)

> Version 1.3.0
> • Menu de l'icône plus court : « Apprendre » regroupe Leçons, Défi du jour et accueil ; « À propos et aide » regroupe confidentialité, ressources, retours et notation. Aucune fonction n'a été retirée.
> • Couches grecque, cyrillique et scientifique : un double appui verrouille l'alphabet dans l'application ouverte, Échap le libère. Chaque couche se coche dans le sous-menu « Couches ». Fonction facultative.
> • La recherche de caractères insère le caractère directement dans votre texte.
> • Défi du jour : le même extrait pour tout le monde, chaque jour, avec « Copier mon résultat » et votre record personnel.
> • Vous pouvez noter l'application sans quitter AZERTY Global.
> • Le lancement au démarrage de Windows est proposé à la fin de l'accueil.
> • Corrections : ouvrir la zone de notification ne met plus l'application en pause ; après un Alt+Tab, les caractères ne sortent plus en AZERTY classique ; en mode jeu, les caractères faits avec AltGr ou une touche morte ne se perdent plus ; le raccourci de recherche ne ferme plus la fenêtre d'une application exclue.
> • Les fenêtres Paramètres et Leçons s'adaptent à la taille et à la mise à l'échelle de l'écran.

À vérifier avant de coller : chaque « correction » doit concerner un défaut présent en 1.1.0. Retirer celles qui portent sur des défauts nés en 1.2 (jamais publiée). La version EN (987 caractères) suit le même contenu, avec les libellés de l'interface anglaise.

## Annexe C — notes de certification proposées (EN)

> AZERTY Global 1.3.0 is a notification-area (system tray) app that applies an improved French keyboard layout. After setup it has no main window.
> How to test: 1) Launch the app; the welcome window opens. Click "Activate and try" (French UI: "Activer et essayer"); the flag button switches the language. 2) The AG icon appears in the notification area (it may be in the hidden-icons overflow ^). Right-click it for the menu. 3) In Notepad: Caps Lock then é gives É; Ctrl+Shift+Caps Lock turns the layout off and on. 4) Ctrl+Shift+Q shows the virtual keyboard; Ctrl+Shift+W opens character search (Enter inserts the character).
> Technical notes:
> • Low-level keyboard hook (SetWindowsHookEx WH_KEYBOARD_LL), installed only after the user clicks Activate. Output is sent with SendInput. No DLL or code is injected into other processes.
> • Read-only process information (SetWinEventHook, OpenProcess QUERY_LIMITED_INFORMATION | VM_READ, module names, and running process names every 5 s) is used to pause in anti-cheat games and remote-desktop clients and to detect remote-control hosts. Nothing is logged by name or transmitted.
> • UI Automation IsPassword is read only to turn off advanced features in password fields.
> • The startup task is declared disabled. The last welcome step offers it pre-checked; it is registered only if the user confirms that step, and a refusal in Settings > Apps > Startup is respected.
> • No account, no telemetry, no automatic network traffic. Links open in the browser only when clicked; in-app rating uses StoreContext.RequestRateAndReviewAppAsync.
> • runFullTrust: Win32 desktop app (.NET 8 Native AOT). WACK: PASS. The optional "Blocked executables" test fails because of ShellExecuteW (used to open links), as in previously accepted versions.

La phrase sur la tâche de démarrage n'est vraie qu'une fois B2 corrigé. La dernière phrase WACK est à vérifier sur le rapport WACK du bundle final.

## Décisions d'Antoine (24/09, QCM) et exécution

- Corrigés en 1.3.0 : B1, B2, B3 (confirmé par le calcul ; vérification à l'écran à 175 % encore due), B4, B5, Tab des exercices, sollicitation d'avis (plus de premier essai au démarrage ; installation migrée : attendre le lendemain de la mise à jour), relance depuis Démarrer, bulles avant accord, `WM_QUIT` de la Pause, polissage des textes, renommage « Couches verrouillables ».
- Reportés en 1.3.1 : `config.json` illisible, Verr. Maj tenu, raccourci exécuté dans le hook, consoles de VM (consigne de recette : **quitter AZERTY Global sur l'hôte avant tout test en Sandbox**), messages des onglets Applications et Langue, manifeste sans texte anglais.
- Fiche Store réécrite (`msix/Fiche Store.md`). Nouveautés : FR 1 197 et EN 1 039 caractères. Deux marqueurs `[à vérifier …]` sont à retirer avant de coller.
- Deux choix de mise en œuvre à valider par Antoine : (1) le délai « lendemain » se réarme à chaque nouvelle version tant que le premier essai d'avis reste dû ; (2) le premier essai attend si le rappel du Défi est déjà parti le même jour.
- Vérification locale : build Release sans avertissement ; TypingEngine.Windows 257/257, Core 18/18, Python 147/147, `check-doc-versions` et `list-identity-literals` sans erreur. Les tests de l'application sont bloqués par Application Control sur ce poste : **la CI fait foi**.

## Lignes de recette à ajouter pour le nouveau candidat

1. B1 : lancer `parsecd` (ou un autre hôte distant), puis AutoHotkey `SendText "(3) x"` dans le Bloc-notes, Verr. Maj éteint puis allumé : le texte sort intact.
2. B2 : à l'étape 3, case laissée cochée, fermer par la croix, par Échap, puis par Alt+F4 : le démarrage reste désactivé dans Paramètres › Applications › Démarrage. Avec « C'est parti ! », il est activé.
3. Relance : sans accord, fermer l'accueil puis relancer depuis Démarrer : l'accueil revient. App activée : bulle « Actif ».
4. B4 : clic droit sur l'icône › Recherche › Entrée : le caractère est copié avec notification, rien n'est envoyé dans la barre des tâches.
5. B3 : Leçons à 150 % et 175 % sur 1080p : rien ne se superpose (ligne B13).
6. Exercices : Tab puis espace, le tutoriel reste ouvert ; Tab puis Entrée sur Quitter le ferme.
7. Avant accord : un jeu anti-triche au premier plan ne déclenche aucune bulle. Après activation, revenir dans le jeu : bulle de suspension et frappe non remappée.
