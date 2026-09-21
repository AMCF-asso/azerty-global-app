# Recette VM du paquet v1.3.0

**État : aucun scénario ci-dessous n'a été exécuté.** Cette grille est à jouer par Antoine dans la VM `AZERTY-Test` ; la session qui l'écrit n'a aucun accès à cette VM.

Elle couvre les trois écarts corrigés le 20 septembre **et** les onze correctifs de l'audit du 2026-09-20 livrés le 21 (`docs/audit-2026-09-20-v1.3.0/rapport.md`). La grille d'origine `recette-vm.md` reste le protocole de la 1.2.0 : VM-01 à VM-23 et RET-01 à RET-09 s'y lisent toujours, cette grille ne les remplace pas.

## Le paquet à installer

⛔ **À remplir après la reconstruction, pas avant.** Le bundle `1.3.0.0` présent dans `msix/` date du 2026-09-20 à 17:27 et **28 fichiers de production lui sont postérieurs** : il ne contient aucun des correctifs de l'audit. `Pack-MSIX.ps1` refuse désormais de l'empaqueter en l'état (`scripts/Assert-PublishFreshness.ps1`, commit `394fdf7`).

| | |
|---|---|
| Fichier | `msix/AZERTYGlobal-1.3.0.0.msixbundle` |
| Taille | *(à relever)* |
| Construit le | *(à relever)* |
| SHA-256 de `AZERTY Global.exe` (x64) | *(à relever)* |
| SHA-256 de `AZERTY Global.exe` (ARM64) | *(à relever)* |
| Commit de construction | *(à relever)* |
| Architectures | x64 et ARM64 |
| Branche | `release/1.2.0-notation-store` — **sans** les 95 fichiers de refonte graphique de `origin/main`, qui sont la 2.0.0 (décision d'Antoine du 2026-09-20) |

Vérifier l'empreinte avant d'installer, et la comparer à celle que rend `Verify-Release.ps1` :

```powershell
Set-Location "D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
Get-FileHash "msix\AZERTYGlobal-1.3.0.0.msixbundle" -Algorithm SHA256
```

## Ce qui a changé, et donc ce qui peut casser

Par ordre de risque décroissant. Les sections A à C portent sur des chemins de frappe, ce sont celles qui décident de la soumission.

| | Correctif | Risque introduit | Section |
|---|---|---|---|
| Écart 8 + AG130-06 | Les raccourcis sont réarmés pour la seule désactivation choisie, et la transition se classe sur le **motif** | Un raccourci qui fonctionnerait là où l'inertie totale doit tenir | A |
| Écart 5 / AG130-08 | AltGr émis avec scan code et bit étendu ; touche morte en attente arbitrée à la suspension | Un caractère AltGr faux, ou une touche morte perdue là où elle tenait | B |
| Écart 7 | Chien de garde de 250 ms sur instantané périmé | Un recalcul plus fréquent, donc une bascule au mauvais moment | C |
| AG130-09 / 10 / 07 | `SendInput` compté, hook décroché détecté, frappes injectées ignorées | Une frappe légitime prise pour une injection | D |
| AG130-11 | Mutex d'instance unique devenu **global** | Deuxième session Windows du même compte : démarrage refusé | E |
| Écart 4, AG130-42, AG130-40 | Fenêtres mesurées, DPI suivi, navigation clavier | Fenêtre mal dimensionnée, Tab volé à une surface de frappe | F |
| Menu, accueil, sollicitation | Douze lignes, « frappes », deux essais rendus | Aucun test ne verrouille le menu : **vérification visuelle obligatoire** | G |

---

## A. Écart 8 et AG130-06 — les raccourcis sous suspension

Le correctif du 20/09 n'avait que deux branches : entrée en suspension et sortie. Passer **directement** d'une application désactivée par l'utilisateur à un jeu anti-cheat, ou l'inverse, ne rappelait rien, et `Ctrl+Maj+W` gardait l'état de l'application précédente (`TrayApplication.cs:2006`, `ClassifySuspensionTransition`). Les gestes 4 à 8 sont ceux que l'audit a ajoutés ; ⛔ **ils sont le cœur de cette recette, les jouer même si le reste passe.**

| # | Geste | Attendu |
|---|---|---|
| 1 | Paramètres → ajouter le Bloc-notes aux applications désactivées, mode « forcer la désactivation ». | Le Bloc-notes apparaît dans la liste. |
| 2 | Dans le Bloc-notes, avec du texte **non enregistré**, presser `Ctrl+Maj+W`. | La **recherche de caractères s'ouvre**. La fenêtre ne se ferme pas, aucune invite d'enregistrement. C'est l'écart destructeur d'origine. |
| 3 | Toujours dans le Bloc-notes désactivé, taper du texte ordinaire. | Aucun remappage : le correctif rend les raccourcis, pas la frappe. |
| 4 | **Sans repasser par une fenêtre ordinaire**, Alt+Tab du Bloc-notes désactivé **directement** vers un jeu anti-cheat déjà lancé. Presser `Ctrl+Maj+W`. | **Rien ne se passe.** Si la recherche s'ouvre, AG130-06 n'est pas corrigé dans ce sens : **bloquant**. |
| 5 | Sens inverse : du jeu anti-cheat **directement** vers le Bloc-notes désactivé. Presser `Ctrl+Maj+W`. | La recherche **s'ouvre**. Si rien ne se passe, AG130-06 n'est pas corrigé dans l'autre sens : **bloquant**. |
| 6 | Répéter 4 et 5 en alternance, cinq allers-retours, sans jamais passer par une fenêtre ordinaire. | Le comportement suit la fenêtre d'arrivée à chaque fois, sans latence perceptible. |
| 7 | Même paire d'allers-retours avec une session d'accès distant (Parsec, Bureau à distance) à la place du jeu. | Identique : rien sous accès distant, recherche sous override utilisateur. |
| 8 | Un premier plan **inconnu** (fenêtre d'un installeur, écran UAC), presser `Ctrl+Maj+W`. | **Rien ne se passe** — `UnknownForeground` est une suspension de sécurité au même titre. |
| 9 | Retirer le Bloc-notes de la liste des désactivées, `Ctrl+Maj+W`. | La recherche s'ouvre, et le remappage est **revenu** dans le Bloc-notes. |

## B. Écart 5 / AG130-08 — compatibilité jeu, caractères perdus

⚠️ **Cet écart était déclaré bloquant et non corrigé dans la version précédente de cette grille. Il est corrigé depuis le 2026-09-21** (`Changelog.md`, section « Correctifs de l'audit du 2026-09-20 ») : `ModifierScanCode` couvre les six modificateurs, et `ArbitratePendingDeadKeyWhileSuspended` (`KeyMapper.cs:1038`) arbitre la touche morte en attente. Les deux mécanismes candidats du rapport sont traités ; cette section les départage en réel. Elle rouvre du même coup **VM-05 (volet forceOn)**, **VM-07 (volet `NativeCombo`)** et **VM-08**, tous trois restés partiels ou non testés faute de ce chemin.

Activer le journal de compatibilité avant de commencer, et le joindre au compte rendu.

### B.1 — AltGr émis correctement

| # | Geste | Attendu |
|---|---|---|
| 10 | Mode « forcer la compatibilité jeu » (`forceOn`), disposition Windows Français (France). Taper `@`. | `@` sort. C'était l'un des deux caractères muets de l'écart 5. |
| 11 | Toujours en `forceOn`, taper les autres caractères à AltGr de la disposition : `#`, `{`, `}`, `[`, `]`, `|`, `\`, `€`. | Tous sortent, aucun muet, aucun doublé. |
| 12 | Frappe de contrôle `abcdef` **immédiatement** après. | `abcdef` intact — aucun modificateur resté artificiellement enfoncé. |
| 13 | Reprise de VM-07 en `NativeCombo` : touche point d'AZERTY Global avec Maj gauche, Maj droite, puis les deux. | `;` dans les trois cas, puis `abcdef` intact. |
| 14 | Reprise de VM-08 : un caractère qui exige le repli Alt+code, NumLock allumé puis éteint. | Caractère exact ; NumLock et les modificateurs retrouvent leur état initial. |

### B.2 — la touche morte en attente

| # | Geste | Attendu |
|---|---|---|
| 15 | En `forceOn` : accent circonflexe puis `a`. Répéter dix fois. | `â` les dix fois. « circonflexe + a rend a » était la signature de l'écart. |
| 16 | Taper l'accent circonflexe, **mettre en pause volontairement** (raccourci ou menu), reprendre, puis taper `a`. | La touche morte **attend** : `â` sort. L'utilisateur n'a pas changé de fenêtre. |
| 17 | Taper l'accent circonflexe, puis **Alt+Tab vers une autre application**, y taper `a`. | `a` **nu** dans la seconde fenêtre, et aucun accent qui resurgit plus tard dans la première. La touche morte est abandonnée à dessein. |
| 18 | Taper l'accent circonflexe, Alt+Tab vers un jeu anti-cheat, revenir, taper `a`. | `a` nu — même règle, la suspension de premier plan annule la composition. |
| 19 | Reprise de VM-05 complète en `forceOn` : circonflexe puis a / e / espace, tréma puis e / i / espace, accent grave puis a / u / espace. Puis les mêmes avec Verr. Maj. | Minuscules : `â ê ^`, `ë ï ¨`, `à ù` et l'accent grave seul. Majuscules : `Â Ê ^`, `Ë Ï ¨`, `À Ù` et l'accent grave seul. Espace rend la touche morte seule. |
| 20 | Le même geste répété vingt fois de suite sans rien changer. | **Vingt fois le même résultat.** « Même geste, deux résultats » était l'autre signature ; une seule divergence rouvre l'écart. |

## C. Écart 7 — le remappage survit à un Alt+Tab

| # | Geste | Attendu |
|---|---|---|
| 21 | Bloc-notes et Explorateur ouverts. Taper dans le Bloc-notes. | Les caractères AZERTY Global sortent. |
| 22 | Alt+Tab vers l'Explorateur, Alt+Tab retour, taper **immédiatement** sans cliquer. | Les caractères sortent **du premier coup**. C'est le geste qui échouait. |
| 23 | Répéter cinq fois, en variant la vitesse (Alt+Tab bref, Alt maintenu avec plusieurs Tab). | Aucun échec sur les cinq. |
| 24 | Ouvrir puis fermer la zone de notification, cliquer la barre des tâches, taper aussitôt. | **Aucune bulle « suspendu par précaution »**, remappage actif. Six occurrences en trois minutes le 19/09. |
| 25 | Mettre en pause puis reprendre au clavier (reprise de VM-18). | La reprise fonctionne — l'écart 8 touche ce même chemin. |

## D. Émission et hook — AG130-09, AG130-10, AG130-07

| # | Geste | Attendu |
|---|---|---|
| 26 | Taper dans une fenêtre lancée **en tant qu'administrateur** depuis une session ordinaire. | Échec compréhensible ou repli explicite, **et une ligne dans `error.log`** : un lot refusé par UIPI ne se perd plus sans trace. Aucun faux succès (reprise de VM-13). |
| 27 | Verrouiller la session pendant une frappe soutenue, déverrouiller, reprendre. | Aucun texte rejoué, aucune touche bloquée, refus journalisé. |
| 28 | Ouvrir le clavier visuel de Windows, cliquer des touches. | Les caractères sortent **dans la disposition native, non remappés** — le clavier visuel affiche ses propres étiquettes, les faire mentir serait le défaut. |
| 29 | Coller du texte, et faire taper du texte par un outil d'automatisation s'il y en a un. | Texte intact, non remappé. |
| 30 | Frappe continue pendant deux minutes, machine chargée (compilation en fond). | Aucun décrochage silencieux ; si le hook décroche, une ligne de journal paraît **en 4 s environ** et le remappage revient. |

## E. Instance unique globale — AG130-11

⚠️ Changement de comportement assumé, à voir de ses yeux avant la soumission.

| # | Geste | Attendu |
|---|---|---|
| 31 | Application lancée en console, ouvrir **une seconde session du même compte** en Bureau à distance et l'y lancer. | Le second démarrage **refuse et le dit**. Il n'écrit pas dans les mêmes fichiers. |
| 32 | Fermer la première instance, relancer dans la seconde session. | Démarrage normal, réglages et progression intacts. |
| 33 | Configuration contenant une clé inconnue ; ouvrir et fermer les Paramètres. | La clé inconnue **garde sa forme** (nombre, booléen), elle n'est pas réécrite en chaîne. |

## F. Fenêtres — écart 4, AG130-42, AG130-40

À jouer en changeant la résolution et la mise à l'échelle entre chaque ligne.

| # | Résolution et échelle | Attendu |
|---|---|---|
| 34 | Paramètres en 1920×1080 à 100 % | Fenêtre entière visible, **aucune barre de défilement**, le bouton radio « forcer la désactivation » visible sans rien faire. |
| 35 | Paramètres en 1366×768 à 150 % | Fenêtre plafonnée à la hauteur de l'écran, **barre de défilement présente**, les trois boutons radio atteignables. |
| 36 | Paramètres en 1366×768 à 175 % | Idem, rien ne sort par le bas. |
| 37 | **Leçons** en 1920×1080 à 175 % | La fenêtre tient dans l'écran, plafonnée à 90 % de la zone de travail (AG130-42). C'était la seule des dix à lire le DPI du moniteur principal. |
| 38 | Molette, puis ascenseur, puis Page suivante dans les deux fenêtres | Le contenu défile dans les trois cas, le tracé suit les contrôles. |
| 39 | FR → EN dans les Paramètres | La fenêtre se remesure, rien n'est coupé, le bas reste atteignable. |
| 40 | Déplacer chaque fenêtre d'un écran 100 % vers un écran 150 % | Elle se remesure, ne dépasse pas la zone de travail du nouvel écran. |
| 41 | **Tab et Entrée** dans les cinq fenêtres à contrôles (Paramètres, À propos, Accueil, notifications, statistiques) | Le focus circule, il est **visible**, Entrée active le bouton par défaut (AG130-40). |
| 42 | **Tab et Entrée** dans Leçons et le module d'apprentissage | Tab et Entrée sont **tapés comme des caractères**, pas capturés par la navigation. Exclusion voulue. |
| 43 | Ouvrir/fermer les principales fenêtres trente fois (reprise de VM-19) | Pas de crash, pas de perte d'images, pas de croissance continue des ressources graphiques. |

## G. Interface — vérification visuelle, aucun test ne la couvre

⛔ `ShowContextMenu` appelle Win32 directement : **rien en machine ne verrouille la structure du menu.** Cette section est la seule preuve qui existera.

| # | Geste | Attendu |
|---|---|---|
| 44 | Ouvrir le menu de la zone de notification, **compter les lignes**. | **Douze**, en quatre blocs : état, outils, apprentissage, application. |
| 45 | Dérouler « Couches ▸ » | Trois entrées cochables : grec, cyrillique, scientifique. |
| 46 | Sur une **installation neuve**, cocher une couche alors que l'interrupteur principal est éteint. | La couche s'allume **et** l'interrupteur principal avec elle. Sans cette règle le clic ne produit rien de visible. |
| 47 | Décocher la dernière couche active. | L'interrupteur principal s'éteint. |
| 48 | Dérouler « Apprendre ▸ » et « À propos et aide ▸ » | Leçons, Défi du jour, Revoir l'accueil d'un côté ; Confidentialité et sécurité, Ressources, Retours et soutien, Noter sur le Store, À propos de l'autre. **Rien n'a disparu.** |
| 49 | Fenêtre d'accueil, en français puis en anglais | « 99 % de vos **frappes** préservées » / « keystrokes ». ⛔ La formule « habitudes » / « habits » ne doit apparaître nulle part. |
| 50 | Mise à jour depuis une installation **1.1.0** portant `reviewPromptDone` à true | Les **deux** essais de sollicitation sont disponibles, le compteur n'est pas à 1 (reprise de RET-06). |
| 51 | Premier essai : taper vingt caractères qu'un AZERTY traditionnel ne donne pas, puis s'arrêter quinze secondes | La sollicitation paraît **après** le silence, elle ne coupe pas une phrase en cours. |
| 52 | Redémarrer la machine le même soir, avec un rappel de Défi du jour en attente | La sollicitation passe **devant** le rappel — l'état se relit sur disque. |
| 53 | Copier un résultat de défi, fermer la fenêtre | La boîte de notation intégrée paraît. L'annuler ne consomme pas le second essai. |
| 54 | « Donner mon avis » et « Signaler un bug » depuis le paquet final (reprise de RET-07) | Bonne page, **version 1.3.0 correcte**, aucune information personnelle envoyée à l'insu de l'utilisateur. |
| 55 | Narrateur, contraste élevé, agrandissement, clavier seul (reprise de RET-08) | Commandes identifiables et atteignables, focus visible, dialogues quittables. |

---

## Ce que cette grille ne couvre toujours pas

- Les scénarios de la recette d'origine jamais joués : **VM-14, VM-15, VM-20, VM-22, VM-23** — configuration absente ou invalide, progression de version inconnue, champ de mot de passe, désinstallation-réinstallation, retours partiels de `SendInput` instrumentés. VM-19 est repris en partie au geste 43, VM-05/07/08 le sont en section B.
- **ARM64** : le bundle en contient une tranche, aucune exécution ARM64 n'a jamais eu lieu. Une compilation croisée n'est pas une preuve. ⛔ Sans cette exécution, la soumission promet une architecture que rien n'a vérifiée.
- La **veille et la reprise** de session (VM-18 n'est repris qu'en pause clavier au geste 25).
- Les **constats reportés en 1.3.1** de l'audit du 2026-09-20, hors périmètre de ce paquet.

## Porte de décision

Reprise de `recette-vm.md` §5, avec les seuils propres à ce paquet.

**Bloquant, suspend la soumission :** un geste 4 à 8 qui se comporte à l'envers (AG130-06 rouvert) ; un caractère muet ou une touche morte perdue en section B (écart 5 rouvert) ; un raccourci qui atteint une application sous anti-cheat ou accès distant ; une frappe injectée vers une cible suspendue ; une fenêtre inatteignable ; un échec d'installation ou de mise à jour.

**Ne suspend pas, mais se note :** un défaut de mise en page qui n'empêche aucune action, un texte imparfait, une latence perceptible sans perte de caractère.

Pour chaque scénario, renseigner **PASS / FAIL / NON TESTÉ**, la date, le SHA-256 du paquet, la configuration, la preuve. Un résultat manquant reste « non testé » — ⛔ un bundle qui existe, une compilation verte et un ancien compte rendu ne valent aucune de ces 55 lignes.

## Côté machine, ce qui est déjà prouvé

⛔ **Les trois suites sont à rejouer en Release sur le paquet reconstruit** : les chiffres ci-dessous datent de l'audit du 20/09, treize commits les précèdent.

- 501 tests verts en Debug comme en Release au 2026-09-20 (18 Core / 157 Windows / 326 application), **0 ignoré**. Le nombre a augmenté depuis : AG130-06 apporte 24 témoins, l'écart 5 et les autres correctifs les leurs. Relever le compte réel, ne pas reprendre 501.
- Build Release sans avertissement de trimming ni d'AOT côté analyseurs ; aucun paquet vulnérable sur les trois projets livrés.
- **Aucun appel réseau sortant** dans `src/` : 0 `HttpClient`, `WebRequest`, `Socket`, `Process.Start` — seules 12 ouvertures d'URL constantes par `ShellExecuteW` et l'API d'avis du Store.
- `usage-stats.json` sans caractère, séquence ni horodatage par frappe ; noms de processus hachés (HMAC) dans `error.log`.
- Manifeste cohérent : CLSID des toasts identique au code et tenu par un test, 4 assets aux bonnes tailles, arm64 réellement ARM64, `runFullTrust` seule capacité.
- `ClassifySuspensionTransition` et `ShouldDetectShortcutsWhileBlocked` sont des fonctions pures, 24 témoins, mutation jouée.
- `Pack-MSIX.ps1` refuse un publish plus ancien que les sources (`394fdf7`), 6 témoins dont 2 négatifs, 2 mutations jouées.

Rien de tout cela ne prouve le comportement Windows réel : c'est ce que ces 55 gestes mesurent.
