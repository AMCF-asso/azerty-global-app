# Changelog — Application AZERTY Global

## Version 1.3.0 — 21 septembre 2026

Préparée dans ce dépôt ; **non encore soumise au Microsoft Store**. La 1.2.0 ne l’a pas été non plus : pour un utilisateur venant de la 1.1.0 servie par le Store, cette version apporte aussi tout ce que liste la 1.2.0 ci-dessous. Numérotée 1.3.0 et non 1.2.1 parce qu’une réorganisation de menu est une fonctionnalité, pas un correctif (décision d’Antoine du 2026-09-19).

**Correctifs de l’audit du 24 septembre (décisions d’Antoine, rapport `docs/audit-2026-09-24-v1.3.0-bugs/rapport.md`)**

- Frappe : un caractère Unicode envoyé par un autre logiciel (VK_PACKET : Espanso, AutoHotkey `SendText`, gestionnaire de mots de passe, dictée, session RDP) n’est plus jamais remappé. Depuis `ade118f`, sur un PC où tourne un service de prise en main à distance, « (3) » sortait « ´.@ ». Témoin : `UnicodePacketTests`.
- Accueil : fermer par la croix, Échap ou Alt+F4 à l’étape 3 n’applique plus la case « Lancer au démarrage de Windows » cochée par défaut ; seul le bouton final l’applique, ou une case que l’utilisateur a lui-même modifiée. Même règle pour « Ne plus afficher ». Témoin : `OnboardingCloseDecisionTests`.
- Relancer AZERTY Global depuis le menu Démarrer quand il tourne déjà rouvre l’accueil s’il n’est pas activé, sinon affiche la bulle d’état. Une activation de notification reste silencieuse.
- Sollicitation d’avis : le premier essai ne part plus jamais du démarrage ni de la fermeture de l’accueil, seulement après une vraie frappe suivie de 15 s de silence. Une installation qui migre avec des statistiques existantes attend en plus le lendemain du premier lancement de cette version. Témoins : `ReviewPromptFirstAttemptTests`.
- Avant tout accord, l’app n’annonce plus de suspension de compatibilité (ni bulle, ni icône « suspendu », ni ligne dans error.log) ; le suivi continue, pour qu’une activation dans un jeu anti-triche reste suspendue.
- Recherche ouverte depuis le menu de l’icône : la barre des tâches n’est plus prise pour cible ; sans application valide, le caractère est copié avec notification.
- Leçons : à 150 et 175 % sur un écran 1080p, la fenêtre plafonnée à l’écran réduit aussi son contenu ; le clavier dessiné ne recouvre plus la ligne à taper ni la saisie (confirmé par le calcul, à vérifier à l’écran).
- Exercices : un Tab accidentel suivi d’une espace ne ferme plus le tutoriel ; un caractère tapé sur un bouton d’en-tête revient à l’exercice, Entrée reste l’activation clavier.
- Fenêtre Pause : « Quitter » pendant qu’elle est ouverte ferme bien l’application (le WM_QUIT est retransmis).
- Textes : « Couches maintenables » devient « Couches verrouillables » (“Lockable layers”) ; l’erreur de démarrage renvoie vers contact@azerty.global et non vers la page de dons ; la recherche « insère » ; vouvoiement du Défi ; « anti-triche », « mots/min », « 12 s », « avant l’ouverture de session », « Verr. Maj. » harmonisé, boutons des Leçons en français.
- Fiche Store : Nouveautés ramenées sous 1 500 caractères, notes de certification réécrites en anglais avec une procédure de test, description alignée FR/EN.
- Reportés en 1.3.1 : `config.json` illisible qui bloque toute sauvegarde, Verr. Maj tenu, raccourci Ctrl+Maj+Verr. Maj exécuté dans le hook, consoles de VM absentes des hôtes distants, messages des onglets Applications et Langue, manifeste sans texte anglais.

**Correctifs de la recette du 23 et du 24 septembre (décisions d’Antoine)**

- Accueil : « Lancer au démarrage de Windows » est proposée **cochée** à l’étape 3. Elle n’est appliquée qu’à la validation de cette étape, après activation. Un refus dans les paramètres Windows la laisse décochée. Décocher la case proposée compte comme un choix et éteint la relance.
- Clavier affiché (Leçons, exercices, clavier virtuel) : en anglais, les touches se lisent « Enter », « Caps Lock », « Shift » et « Space ». Le clavier virtuel se redessine au changement de langue.
- Zone de notification : l’icône est grise tant que l’app n’est pas activée, dès le démarrage. Elle était bleue alors que l’infobulle disait « Désactivé ».
- Paramètres : la fenêtre garde la taille de l’onglet Général, quel que soit l’onglet affiché.
- Prise en main à distance : sur un PC piloté à distance (Parsec, AnyDesk, TeamViewer, RustDesk, Bureau à distance Chrome), AZERTY Global remappe de nouveau les frappes reçues, une seule fois, et les exercices les reçoivent. Côté machine qui se connecte, l’app reste suspendue quand le logiciel distant est au premier plan. Depuis AG130-07 (21/09), ces frappes injectées étaient ignorées. Contrepartie : tant qu’un tel logiciel tourne, les frappes injectées par d’autres outils (clavier visuel, AutoHotkey) sont aussi remappées.

**Menu de la zone de notification — cinq blocs puis douze lignes (décisions du 2026-09-02 et du 2026-09-19)**

- Portage de la décision S2 : le menu est réordonné, et l’entrée unique à suffixe ✓ des couches maintenables devient un sous-menu « Couches ▸ » de trois entrées cochables — grec, cyrillique, scientifique.
- Les blocs séparés par un trait sont **cinq** : état, outils, apprendre, réglages et infos, puis un dernier qui porte la langue et « Quitter ». Cette ligne en annonçait quatre jusqu'au 2026-09-22 — c'est le compte de la décision S2, pas celui du menu construit (R17 de la revue de code du 2026-09-21, relevé dans `TrayApplication.ShowContextMenu`). Le menu n'a pas bougé ; seule sa description était fausse.
- Cocher une couche alors que l’interrupteur principal `maintainableLayersEnabled` est éteint l’allume ; décocher la dernière couche active l’éteint. Sans cette règle, un clic sur une couche ne produit rien de visible sur une installation neuve, puisque l’interrupteur vaut `false` par défaut. La décision S2-3 ne tranchait pas ce point.
- Le menu S2 mesurait dix-huit lignes, jugées encore trop longues en VM. « Apprendre ▸ » replie Leçons, Défi du jour et Revoir l’accueil ; « À propos et aide ▸ » replie Confidentialité et sécurité, Ressources, Retours et soutien, Noter sur le Store et À propos. Dix-huit lignes deviennent douze, **rien n’est retiré**. Le Défi du jour perd la visibilité immédiate que lui donnait la décision du 2026-08-16 : arbitrage assumé.
- ⚠️ Aucun test ne verrouille la structure du menu — `ShowContextMenu` appelle Win32 directement. La vérification est visuelle, en VM.

**Sollicitation d’avis — rattrapage de la base 1.1.0 (décision d’Antoine du 2026-09-20)**

- Le drapeau hérité `reviewPromptDone` ne vaut plus une sollicitation consommée. Une installation qui migre depuis la 1.1.0 repart avec ses **deux essais entiers**, au lieu d’un seul comme le prévoyait la règle de migration de la 1.2.0.
- Motif mesuré : la sollicitation de la 1.1.0 vivait dans le `else` du test d’affichage de l’accueil, donc une partie des installations n’a **jamais** été sollicitée ; et quand elle l’était, le tirage 50/50 l’envoyait une fois sur deux vers la page de feedback privée au lieu de la fiche Store. Un `reviewPromptDone` à true ne prouve donc pas qu’une note ait été demandée. Snapshot Store du 2026-09-02 : **0 notation sur 394 utilisateurs actifs** en juillet 2026, et 3 notes uniques hors celle de l’auteur depuis mars.
- ⚠️ Contrepartie assumée : l’utilisateur de la 1.1.0 qui a cliqué sa sollicitation et déposé sa note est **indistinguable** de celui qui l’a ignorée — la 1.1.0 n’écrit pas `reviewPromptClicked`, ce champ naît en 1.2.0. Il sera re-sollicité une fois. Les garde-fous ordinaires tiennent : plafond de deux essais sur la vie de l’installation, planchers de 3 puis 10 jours d’usage distincts, une sollicitation par jour au plus, silence de 48 heures après une erreur journalisée.
- Témoin : `ReviewPromptCount_LegacyDoneFlagNoLongerConsumesAnAttempt` attend 0 là où l’ancienne ligne rendait 1 — remettre le repli rougit ce test et lui seul. Un second test prouve que `reviewPromptClicked` reste lu.
- La priorité de la sollicitation sur le rappel du Défi du jour **survit désormais au redémarrage** : le champ en mémoire qui la portait est mort, l’état se relit sur disque. Sans ce correctif, un redémarrage laissait le rappel d’entraînement passer devant la sollicitation le même soir.
- Le premier essai ne se déclenche plus sur un plancher de jours d’usage mais sur l’usage réel de la disposition (décision d’Antoine du 2026-09-21) : vingt caractères qu’un AZERTY traditionnel ne donne pas, puis quinze secondes sans frappe, pour ne pas couper une phrase en cours. Le second essai garde ses planchers de 10 jours d’usage et de 7 jours d’écart, et le chemin du partage est inchangé. Onze témoins, chaque cas négatif doublé de sa réciproque.

**Correctifs isolés repris de `main`**

- `RepositoryUrl` pointait encore le compte personnel `AZERTYGlobal` au lieu de `AMCF-asso` ; la constante part dans le binaire Store.
- `src/lessons.json` reprend le module « course aux 30 millions » du site. Cela répare le job CI `provenance`, qui échouait parce que la copie embarquée avait dérivé de son original canonique `tester/lessons.json` — vérifié : `check-layout-provenance.py` rend 0, les 3 copies sont identiques.
- ⛔ Le reste de l’écart avec `main` n’est **pas** portable : 95 fichiers et ~17 800 insertions, qui sont la refonte graphique CH0-CH4b, c’est-à-dire la 2.0.0. Les quick wins QW-3 et QW-5 n’existent nulle part — le chantier CH5 n’a jamais été ouvert.

**Compatibilité — fin d’une suspension déclenchée par le shell Windows**

- Ouvrir la zone de notification ou cliquer la barre des tâches suffisait à suspendre le remapping, avec une bulle « suspendu par précaution » qui recouvrait l’icône de l’application — donc le seul geste prévu pour quitter. Six occurrences journalisées en trois minutes dans la VM le 2026-09-19.
- Cause établie : `explorer.exe` passe au premier plan, la fenêtre bascule vers le volet `ShellExperienceHost` entre les deux `GetForegroundWindow()` d’un même `Recompute`, et la branche de course fabriquait une identité inconnue. Faux positif sur le shell Windows, au geste le plus banal qui soit.
- Seul un suivi réellement indisponible (`IsTrackingAvailable` faux) suspend désormais. La sécurité de frappe est inchangée : `GetEmitContext()` recontrôle la fenêtre à chaque émission et refuse d’émettre dès qu’elle a bougé — c’est cette garde qui compte, pas la suspension.
- Deux tests neufs dans `src/TypingEngine.Windows.Tests/ShellRaceSuspensionTests.cs`, témoin de mutation passé : l’ancienne ligne remise, un seul rouge et le bon.
- Trace de compatibilité enrichie : mode, motif, `hasFg`, `pid` et `tracking`, émise aussi quand seul le motif change.

**Compatibilité et fenêtres — les trois écarts relevés en VM le 2026-09-19**

- Écart 7 — après un Alt+Tab, la fenêtre d’arrivée n’émet pas toujours `EVENT_SYSTEM_FOREGROUND` : l’instantané restait figé sur le sélecteur de tâches et toute émission était refusée, donc la frappe sortait en AZERTY traditionnel. Ajout des événements `SWITCHSTART` et `SWITCHEND`, d’une propriété `IsSnapshotStale` et d’un chien de garde de 250 ms côté hôte, qui ne recalcule que sur un instantané réellement périmé. Trois témoins neufs, vérifiés par mutation.
- Écart 8 — une application marquée « désactivée par l’utilisateur » tombait dans l’inertie totale prévue pour l’anti-cheat : Ctrl+Maj+W atteignait l’application, qui y lisait Ctrl+W et fermait sa fenêtre. Les raccourcis sont réarmés pour le seul motif `UserOverride` ; le remapping reste éteint, et l’inertie totale tient toujours pour l’anti-cheat, l’accès distant et le premier plan inconnu.
- Écart 8, complément du 2026-09-21 (AG130-06) — le correctif ci-dessus n’avait que deux branches, celle qui entre en suspension et celle qui en sort. Passer **directement** d’une application désactivée par l’utilisateur à un jeu anti-cheat, ou l’inverse, ne rappelait donc rien : les raccourcis gardaient l’état de l’application précédente, et Ctrl+Maj+W pouvait ouvrir une fenêtre sous anti-cheat. La transition se classe désormais sur le **motif** de suspension et pas seulement sur le mode ; `ClassifySuspensionTransition` et `ShouldDetectShortcutsWhileBlocked` sont des fonctions pures, 24 témoins.
- Écart 4 — la fenêtre Paramètres se dimensionnait sur deux constantes et dépassait la zone de travail en 1366×768 à 150 %, rendant le troisième bouton radio inatteignable. Elle mesure désormais son contenu, se plafonne à la zone de travail et défile.

**Accueil — le chiffre public porte sur les frappes**

- « 99 % de vos habitudes préservées » devient « 99 % de vos frappes préservées », en français comme en anglais (`habits` → `keystrokes`). Relevé par Antoine en VM sur le paquet 1.3.0.0 : la mesure porte sur les frappes, « habitudes » promettait plus large que ce qui est mesuré.

**Les cinq constats mineurs de la revue de code (R13 à R17, 2026-09-22)**

Le tableau de la revue du 2026-09-21 est soldé : R1 à R5 et R8 à R12 étaient corrigés, R6 reformulé et R7 reporté en 1.3.1. Ces cinq-là fermaient la liste.

- **Touche morte abandonnée au changement d’application (R13).** L’arbitrage écrit en 1.3.0 ne couvrait que les suspensions imposées — anti-triche, accès distant, premier plan inconnu. Un Alt+Tab d’un traitement de texte vers un navigateur ne suspend rien : la touche morte en attente survivait, et l’accent surgissait sur une lettre tapée bien plus tard, dans une autre application. Même symptôme que l’écart 5, cible ordinaire. ⛔ Ce qui décide est l’identité de la dernière **application**, jamais celle du premier plan courant : un clic sur la barre des tâches, le sélecteur Alt+Tab et nos propres fenêtres changent le premier plan sans changer d’application, et ne détruisent rien. Quinze témoins, mutation jouée — la décision neutralisée fait rougir quatre tests, et eux seuls.
- **Plus de rafale de bulles au passage par le shell (R15).** Le sélecteur de tâches appartient à `explorer.exe` et se résout en `Default` : un Alt+Tab entre deux applications suspendues produisait une sortie de suspension puis une entrée, soit deux bulles par bascule — dont une qui ignore le réglage des notifications. Un aller-retour par la barre des tâches, sans changer d’application du tout, en produisait autant. Deux règles dans une seule fonction pure : rien ne s’annonce tant que le premier plan est une surface éphémère du shell, et une fois le premier plan posé, l’annonce ne part que si elle dit autre chose que la précédente. Ce qui est dû reste dû — sortir vraiment d’un jeu annonce toujours la reprise. ⛔ Seules les bulles passent par là : le hook, l’icône, l’infobulle et les raccourcis suivent le premier plan réel sans délai. Sept témoins.
- **« Donner mon avis » emporte la version, comme « Signaler un bug » (R16).** Les quatre entrées de retour — menu du tray, sollicitation d’avis, fenêtre de statistiques, accueil — ouvraient un `/feedback` nu. Un retour sans version ne se rattache à aucun binaire, alors que la 1.1.0, la 1.2.0 et la 1.3.0 coexistent dans le parc. Un seul corps désormais pour les cinq liens, pour que l’oubli ne puisse plus porter sur une branche ; le paramètre `source=app-notification` de la sollicitation est conservé tel quel, le site le lit depuis la 1.2.0. Cinq témoins.
- **R14 — rien à corriger, une attente de recette à réécrire.** Le geste 8 supposait qu’une fenêtre élevée se classe en « premier plan inconnu ». Elle ne s’y classe pas : `OpenProcess(QUERY_LIMITED_INFORMATION)` réussit à travers l’élévation, le nom du processus est lu, le mode vaut `Default`. C’est le comportement voulu — VM-13 du 19/09 a mesuré que le remappage traverse une fenêtre élevée, et suspendre y casserait la frappe dans un terminal administrateur. Seul `EnumProcessModules` échoue en silence, ce qui prive la détection automatique de jeu de sa voie « modules » sur un processus élevé ; la détection par nom tient. L’attente du geste 8 est corrigée dans la recette.
- **R17 — le menu compte cinq blocs, pas quatre.** Le menu n’a pas bougé ; c’est cette page qui reprenait le compte de la décision S2 au lieu de celui du menu construit. Corrigé ci-dessus.

**Deux témoins creux réparés (§ 3.3 et § 3.4 de la revue)**

- `MockWin32Api.ToUnicodeEx` rendait 1 en toutes circonstances : `IsDeadKeyOnLayout` ne pouvait **jamais** valoir vrai sous test, et le repli Alt+code des touches mortes natives — « ^ », « ¨ », « ~ », « ` » sur un AZERTY traditionnel, geste 19 — n’avait aucune couverture dans sa branche utile. Le mock sait désormais déclarer des touches mortes natives ; quatre témoins jouent les deux côtés de la décision, cache compris.
- ⚠️ Les deux témoins de pause volontaire de `DeadKeyWhileSuspendedTests` éprouvent une branche que la production n’atteint pas : une pause met `KeyboardHook.PassThroughAll` à vrai, et le hook est le seul appelant de `ProcessKey`. La garde reste, défensive, mais elle ne décrit pas ce que vit l’utilisateur ; la limite est désormais écrite dans le fichier, avec le pointeur vers `PauseResumeDeadKeyTests`, qui tient la vraie couture depuis R4.

**Correctifs de l’audit du 2026-09-20**

L’audit complet du candidat 1.3.0 a rendu 52 constats, dont 4 bloquants (`docs/audit-2026-09-20-v1.3.0/rapport.md`). Onze sont corrigés dans cette version, chacun avec des témoins prouvés par mutation ; les autres sont mineurs ou reportés en 1.3.1.

- **Compatibilité jeu, caractères perdus (écart 5, AG130-08).** AltGr était détecté comme RAlt+LCtrl mais émis comme un `VK_RMENU` nu, sans bit étendu ni scan code : la table `ModifierScanCode` couvre les six modificateurs et les douze émissions passent par elle. Et une touche morte en attente n’est plus perdue quand le contexte d’émission devient indisponible : elle est relâchée sur une pause volontaire, abandonnée sur une suspension de premier plan, puisque l’utilisateur a changé d’application.
- **Perte silencieuse à l’émission (AG130-09).** `SendInput` est déclaré avec `SetLastError`, et un lot refusé en entier n’est plus perdu sans trace alors que la touche physique a déjà été bloquée. `KeyMapper.SendInputs` devient le point de passage unique des sept sites d’émission, compte les refus et les journalise, le premier d’une série puis un sur cinquante.
- **Limite du diagnostic UIPI.** Un retour nul de `SendInput` est compté comme un refus. La valeur de retour et `GetLastError` ne permettent toutefois pas d’en attribuer la cause à UIPI : le journal indique un échec d’émission, sans prétendre identifier ce mécanisme. Un envoi accepté ne garantit pas davantage que l’application cible l’ait traité. La reprise des frappes refusées reste reportée en 1.3.1. Référence : [documentation Microsoft de SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput#return-value).
- **Hook décroché (AG130-10).** Windows retire silencieusement un hook bas niveau dont le rappel dépasse `LowLevelHooksTimeout`, et l’application réinstallait à l’aveugle toutes les 60 s, sans rien dire. Le dernier rappel est horodaté et une sonde de 2 s le compare aux frappes réellement vues : détection en 4 s environ, avec une ligne de journal.
- **Frappes injectées par un tiers (AG130-07).** `LLKHF_INJECTED` est lu : ce qu’injecte un autre programme — outil d’automatisation, clavier visuel de Windows, outil d’accessibilité — passe sans être remappé. Un programme qui injecte du texte connaît déjà le caractère qu’il envoie, et le clavier visuel affiche la disposition native : remapper son clic ferait mentir ses propres étiquettes.
- **Configuration (AG130-11).** Les clés inconnues gardent leur forme au lieu d’être réécrites en chaîne ; le fichier temporaire de sauvegarde porte le PID ; le mutex d’instance unique devient global. Conséquence assumée : une seconde session du même compte, bureau à distance et console ouverts en même temps, refuse de démarrer et le dit, au lieu d’écrire dans les mêmes fichiers avec le dernier écrivain gagnant.
- **Navigation clavier (AG130-40).** La boucle de messages n’appelait ni `IsDialogMessageW` ni `TranslateAcceleratorW` : les `WS_TABSTOP` étaient inertes et Entrée n’activait pas le bouton par défaut. Cinq fenêtres à contrôles s’inscrivent désormais explicitement. Les surfaces de frappe — Leçons, module d’apprentissage — en restent dehors, Tab et Entrée y étant des caractères à taper.
- **Navigation clavier, suite (revue de code du 2026-09-21, R1, R2, R5).** Trois défauts que la relecture statique du code a trouvés derrière AG130-40, aucun test de fenêtre ne pouvant les voir. La bande d’onglets de Paramètres portait `TCS_FOCUSNEVER` : au clavier seul, « Applications » et « Langue » étaient hors d’atteinte, donc la liste d’apps suspendues, les trois modes de compatibilité et le choix de langue aussi ; elle est désormais un arrêt de tabulation ordinaire, les flèches changent d’onglet. Les liens d’À propos et de Statistiques répondaient `DLGC_WANTALLKEYS` sans exception, ce qui vaut `DLGC_WANTMESSAGE` : une fois le focus sur un lien, Tab et Maj+Tab n’en sortaient plus (piège clavier, WCAG 2.1.2) ; Tab est rendu à `IsDialogMessageW`, comme les cases de raccourci le faisaient déjà. Enfin `IsDialogMessageW` convertit Échap et Entrée en `WM_COMMAND(IDCANCEL | IDOK)` sans jamais les dispatcher : les gestionnaires `WM_KEYDOWN` d’Échap des trois fenêtres étaient morts. Échap ferme à nouveau Paramètres, À propos et Statistiques ; Entrée presse le bouton focalisé dans Paramètres et Statistiques, ferme ailleurs. Décisions pures dans `DialogNavigation`, 11 témoins (`DialogNavigationKeyboardTrapTests`), mutation vérifiée : sans l’exception Tab, deux rougissent. Rapport : `docs/audit-2026-09-20-v1.3.0/revue-code-2026-09-21-sans-vm.md`.
- **Suspension choisie, touche morte et saisie sécurisée (revue de code du 2026-09-21, R3, R4, R11).** Trois contradictions de plus entre ce que la 1.3.0 annonce et ce que le code fait. L’écart 8 n’était corrigé qu’à moitié : Ctrl+Maj+W était bien avalé sous « forcer la désactivation » — plus de Ctrl+W destructeur — mais la recherche de caractères ne s’ouvrait pas pour autant, `WM_APP_SEARCH` exigeant un hook actif et répondant « Désactivé » ; le drapeau `ShortcutsWhilePassThrough` était juste, son consommateur l’ignorait. La touche morte en attente, elle, était détruite à la reprise d’une pause volontaire : `StopPause` resynchronise l’état et cette resynchronisation annulait la composition, contre la doctrine écrite dans le moteur. Enfin le raccourci du clavier virtuel n’avait pas la garde « saisie sécurisée » que la recherche portait : Ctrl+Maj+Q s’affichait au-dessus d’un champ mot de passe ; les deux raccourcis partagent désormais un seul prédicat. Décisions rendues pures et testées : `SearchWhileSuspendedTests`, `PauseResumeDeadKeyTests`, `SecureInputShortcutTests`. Rapport : `docs/audit-2026-09-20-v1.3.0/revue-code-2026-09-21-sans-vm.md`.
- **Sollicitation d’avis : un refus provisoire ne brûle plus le franchissement, et le tutoriel ne peut plus arrêter le comptage (revue de code du 2026-09-21, R8, R9).** Le déclencheur du premier essai — vingt caractères enrichis puis quinze secondes de silence — désarmait son signal avant même de demander l’affichage. Franchir le seuil à neuf minutes d’usage actif suffisait donc à perdre la sollicitation pour toute la durée du processus : le plancher des dix minutes refusait, le franchissement ne se reproduisait plus, et il fallait redémarrer l’application alors qu’il aurait suffi d’attendre une minute. Le signal n’est désormais consommé que si la sollicitation est partie, ou si plus aucune n’est possible ; tout autre refus le laisse armé pour le prochain silence. Par ailleurs, le module d’apprentissage ouvre une plage de frappe non comptée pour toute sa durée de vie, refermée à sa fermeture : si son constructeur échouait après cette ouverture — création de fenêtre, fontes, contrôles — personne ne tenait l’instance, donc personne ne refermait la plage, et **plus une seule frappe n’était comptée jusqu’au redémarrage** (statistiques, séries, sollicitation d’avis). Le constructeur referme désormais la plage avant de relancer. Six témoins neufs (`ReviewSignalConsumptionTests`, `ExcludedTypingLifetimeTests`). Rapport : `docs/audit-2026-09-20-v1.3.0/revue-code-2026-09-21-sans-vm.md`.
- **Paramètres : les onglets se traduisent, et le message de validation ne déborde plus (revue de code du 2026-09-21, R10, R12).** Changer la langue retraduisait toute la fenêtre **sauf sa bande d’onglets** : « Général / Applications / Langue » restaient en français sur une fenêtre entièrement anglaise, et réciproquement. Les libellés d’un contrôle onglet ne sont pas des textes de fenêtre : ils ne se réécrivent qu’avec un message dédié, que la fonction de retraduction n’envoyait nulle part. Dans la même fenêtre, un message de validation long — par exemple après une réinitialisation — grandissait le contenu sans que la fenêtre soit remesurée : le message débordait, le défilement ne s’armait pas, et le bas du contenu devenait inatteignable. Les deux chemins passent désormais par la même remesure que le changement de langue. ⚠️ Aucun test de fenêtre n’existe dans la suite : vérification en recette VM. Rapport : `docs/audit-2026-09-20-v1.3.0/revue-code-2026-09-21-sans-vm.md`.

- **Fenêtre Leçons (AG130-42).** Elle lisait son DPI sur le moniteur principal et ne traitait pas `WM_DPICHANGED`, seule des dix fenêtres : à 175 % sur un 1920×1080 elle demandait plus de place que l’écran n’en a. Elle suit désormais `GetDpiForWindow` et `WM_DPICHANGED`, et se plafonne à 90 % de la zone de travail.
- **Documents publics (AG130-12).** Le `README` annonçait « version 1.2.0 » et « aucun package n’a encore été produit ni soumis », trois assertions fausses ; la formule interdite « 99 % des habitudes » survivait dans trois fichiers. Le `README` entre dans le périmètre de `check-doc-versions.py`.
- **Versions des documents de parc (AG130-02).** Les sept documents surveillés déclaraient encore `version-app: 1.2.0`, ce qui faisait sortir `check-doc-versions.py` en erreur et bloquait la CI avant même de compiler. La 1.2.0 est déclarée historique.

**Accessibilité — correctifs rapides (décision d’Antoine du 2026-09-22)**

La case « testé pour l’accessibilité » de Partner Center reste décochée pour cette version. Ce lot corrige ce qui tenait en une session ; l’exposition à UI Automation des surfaces dessinées et le contraste élevé partent en 1.3.1. Détail et état : `docs/audit-2026-09-22-v1.3.0/feu-vert/accessibilite-1.3.0.md`.

- **Clavier.** La fenêtre des couches maintenables et celle du conflit de disposition répondent à Tab, Entrée et Échap ; « Passer cet exercice » s’atteint par Tab ; les boutons-icônes des Leçons montrent leur infobulle au focus clavier ; le drapeau de langue de l’accueil se tabule et s’active par Entrée ou Espace. Le focus se voit : cadre de focus sur les boutons dessinés des exercices, sur les liens de l’accueil et d’À propos, et sur le drapeau.
- **Noms lus par le Narrateur.** Le champ des heures de la Pause s’annonçait « Minutes » et celui des minutes n’avait aucun nom (mesuré) ; les champs de raccourci et la liste des apps suspendues des Paramètres, comme le champ de la Recherche, n’en avaient pas non plus. Les boutons ▲▼ de la Pause s’annoncent « Augmenter les heures », etc., en français et en anglais.
- **Mise à l’échelle.** La Pause, les couches maintenables et l’indicateur de couche suivent le DPI de l’écran. La taille minimale des Leçons dépassait l’écran dès 175 % sur un 1920×1080 : Windows l’impose jusqu’à la création de la fenêtre (mesuré), ce qui annulait le plafond d’AG130-42. Elle est bornée à la zone de travail.
- 34 cas de test neufs sur les décisions pures, prouvés par mutation ; le reste se recette, lignes B6 à B13 de `docs/audit-2026-09-22-v1.3.0/feu-vert/recette.md`.

**Vérifié**

- Corrigé et vérifié en VM le 2026-09-19 à 13:44 : explorateur, Edge et zone de notification ouverts, `error.log` n’existe pas.
- Paquet reconstruit le 2026-09-21 à 19:17 depuis le commit `63e58e2`, sur un publish x64 et ARM64 refait le jour même : le bundle du 2026-09-20 à 17:27, auquel 28 fichiers de production étaient postérieurs, est archivé et ne doit pas être installé. Les trois suites .NET en Release rendaient alors **618 tests, 0 échec, 0 ignoré** — le chiffre de 501 qui circulait datait de l’audit et était périmé de treize commits.
- Comptage à jour au commit `4918757` (lot R13-R17 inclus), mesuré en CI le 2026-09-22 à 10:19 UTC sur la branche `ci/verif` : **695 tests, 0 échec, 0 ignoré** — 18 pour `TypingEngine.Core.Tests`, 226 pour `TypingEngine.Windows.Tests`, 451 pour `AZERTYGlobal.Tests`. ⛔ Ce comptage n’a pas pu être refait sur le poste d’Antoine : Smart App Control (`VerifiedAndReputablePolicyState = 1`) refuse de charger `AZERTY Global.dll`, **y compris en Debug**, avec `0x800711C7` — les 451 tests de la suite application y échouent tous au chargement, jamais sur une assertion. Ce n’est pas nouveau : la mesure du 2026-09-07 (CH4b) avait déjà établi que les deux configurations sont murées sur ce poste, 341 échecs sur 349 de cause unique `0x800711C7`. Seul le constat du 2026-09-02, limité à la sortie Release du composant `windows-installer`, disait moins. Ce que cette version ajoute, c’est que le blocage tient toujours au 2026-09-22. ⚠️ Il est **intermittent** : le commit `4918757` note quatre refus de suite puis une acceptation sans qu’aucune entrée n’ait changé — un run local vert ne prouve donc pas qu’il ait cessé, et un run rouge ne dit rien du code. La CI est la seule preuve stable tant que ce poste est muré.
- État au 2026-09-22 à 22:40, commit `bd33c9d` (contenu de `9350947` sur `ci/verif`), [run 35779820283](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35779820283) vert : **726 tests C#** (18 / 235 / 473) et **147 tests Python**, 0 échec. Bundle attesté SHA-256 `768f13fbe9e79e98cc56009e34496916ca352f84dbf31c1350c4be6506c4250c`. BinSkim est désormais complet et bloquant ; ses alertes ont été traitées ainsi :
  - **Durcissement CET (shadow stack) sur x64** : `/CETCOMPAT` passé au linker, que le SDK NativeAOT 8 n’ajoutait pas. Mesuré dans Windows Sandbox : l’app packagée tourne avec shadow stack et CFG actifs, et rattrape normalement une exception de configuration.
  - **Quatre alertes dérogées nominativement** (Spectre, SDL, SourceLink, LTCG), seulement quand leur condition est vérifiée sur le binaire : tous les objets C/C++ concernés sont ceux, précompilés, du runtime NativeAOT et du CRT Microsoft. Détail et preuves : `docs/audit-2026-09-22-v1.3.0/feu-vert/`.
- ⚠️ L’étape `BinSkim hardening check` sort en 1 depuis au moins le run du 2026-09-22 à 08:30 : elle n’est pas bloquante (le job reste vert) et ce n’est **pas** une régression du lot R13-R17.
- `Verify-Release.ps1` code de sortie 0, et **WACK `OVERALL_RESULT=PASS`** sur `AZERTYGlobal-1.3.0.0.msixbundle`, rapport généré à 19:20:59. Vingt-trois tests passent ; le seul échec est `Blocked executables`, test **optionnel**, en échec pour les mêmes raisons connues qu’en 1.0.0 et 0.12.0.
- ⚠️ Ce qui n’est toujours pas fait : la recette VM des 55 gestes, la signature AMCF, et les trois points hors chemin d’empaquetage (AG130-04, AG130-34, AG130-50). Le paquet n’est **pas signé** et ne se diffuse à personne.

## Version 1.2.0 — 17 août 2026

Préparée et vérifiée dans ce dépôt ; **non encore soumise au Microsoft Store**. Remplacer cette ligne par la révision Store le jour de l'acceptation. Source réconciliée le 2026-08-15.

- Report de la base interne 1.1.2 et des fonctions 1.2.0 en développement dans le dépôt public canonique : interface bilingue, statistiques locales, défi quotidien facultatif, rappels d'entraînement et activation des notifications Store.
- Extraction du modèle de disposition, du parseur JSON et de la composition des touches mortes dans `TypingEngine.Core`, projet portable partagé et couvert par sa propre suite de tests.
- Extraction du remapping, du hook clavier, de l'injection Win32 et de la compatibilité jeux dans `TypingEngine.Windows`. L'application fournit désormais sa configuration, ses journaux et ses statistiques via `IWindowsTypingHost`.
**Sollicitation d'avis — refonte (décision du 2026-08-16)**

- La sollicitation n'est plus subordonnée à l'absence de fenêtre d'accueil : elle vivait dans le `else` du test d'affichage de l'accueil, si bien qu'une partie des utilisateurs n'était **jamais** sollicitée. La condition exacte était `showOnboardingAtStartup` **et** moins de trois étapes du module d'apprentissage terminées : garder l'accueil au démarrage ne suffisait pas, il fallait aussi ne l'avoir jamais dépassé. Elle est désormais différée à la fermeture de l'accueil, via un nouveau callback `OnboardingWindow.OnClosed`.
- Déclenchement sur les jours d'usage réels au lieu du calendrier : essai 1 à 3 jours d'usage distincts (plancher de 3 jours depuis la première frappe remappée), essai 2 à 10 jours d'usage distincts (plancher de 7 jours après l'essai 1). Le J+7 calendaire de la v1.1 sollicitait de la même façon celui qui tape tous les jours et celui qui avait installé puis oublié l'application.
- Deux essais au maximum sur toute la vie de l'installation, contre un seul auparavant. Le second est abandonné si le premier a été cliqué, ou si l'application n'a plus servi depuis plus de trois jours.
- Fin du tirage 50/50 entre la fiche Store et la page feedback : en packagé la cible est toujours le Store. Une sollicitation sur deux partait vers un canal privé alors que la note publique est le seul levier qui manque à la fiche. La page feedback ne sert plus qu'aux installations hors Store, qui n'ont pas de fiche à noter.
- Aucune sollicitation dans les 48 heures qui suivent une erreur journalisée.
- `reviewPromptDone` cède la place à `reviewPromptCount` (plafonné à deux), `reviewPromptLastShown` et `reviewPromptClicked`. Migration des installations v1.1 : un `reviewPromptDone` à true vaut un essai déjà consommé, sinon la v1.2.0 enverrait deux notifications supplémentaires à quelqu'un qui a déjà été sollicité.
- Textes distincts pour chaque essai, sans aucun chiffre d'usage : les statistiques restent affaire de la fenêtre « Mes statistiques ». Le second essai ajoute le cadre associatif que le premier laisse de côté. Aucun des deux n'annonce sa propre fin : la formule « c'est la dernière fois qu'on vous le demande », prévue le 16 août pour lever la crainte du harcèlement, a été retirée le 18 août au premier smoke test — annoncer un plafond fait peser la demande au lieu de l'alléger. Les deux notifications décrivent ce que le clic ouvre au lieu de l'ordonner.

**Défi du jour — sortie de l'ombre et partage (décisions du 2026-08-16)**

- L'entrée « Défi du jour » du menu de la zone de notification est désormais **toujours visible**. Elle était conditionnée à `trainingEnabled`, qui vaut `false` par défaut : sur une installation neuve la fonction n'existait donc pas visuellement, alors que le défi commun est le seul contenu identique pour tous les utilisateurs. L'opt-in ne gouverne plus que les rappels d'entraînement, qui sont des notifications et relèvent d'un consentement distinct.
- Nouveau bouton **« Copier mon résultat »** sur le récapitulatif de fin de séance, présent uniquement après le défi commun : les cinq séances de prise en main dépendent de la progression individuelle, deux personnes n'y tapent pas le même extrait et il n'y a rien à y comparer.
- Le texte copié porte la date, la vitesse, la précision, la durée, les caractères qui ont posé problème, l'attribution de l'extrait quand il en a une, et le lien du site. Format texte et non image : il se colle dans une conversation sans capture d'écran ni téléversement. Aucune statistique d'usage n'y figure — elles restent dans « Mes statistiques ».
- Le record personnel, stocké dans `lessons-progress.json` depuis la v1.0 mais jamais affiché, est enfin lu : une séance qui bat le meilleur score antérieur est signalée comme telle.
- L'annonce unique du Défi du jour aux utilisateurs existants ouvre maintenant la séance du jour au lieu des Paramètres, et son texte a été reformulé en conséquence.

**Notation intégrée au Store (décision du 2026-08-16)**

- La sollicitation d'avis et l'entrée « Noter sur le Microsoft Store » passent par la boîte de notation **intégrée** de Windows (`StoreContext.RequestRateAndReviewAppAsync`) : la note se dépose sans quitter AZERTY Global. Le lien profond `ms-windows-store://review/` imposait une bascule vers l'application Store et l'attente de son chargement ; il reste le repli automatique hors package ou en cas d'échec de l'API.
- L'API exige Windows 10 1809, soit exactement le `MinVersion` déclaré dans le manifeste. La couche WinRT est celle qui pilote déjà `StartupTask` depuis la v1.0, et la publication AOT x64 la compile et la lie sans avertissement.
- Nouveau déclencheur : copier son résultat de défi puis refermer la fenêtre présente la boîte de notation. Le partage est le signal de promotion le plus net dont dispose l'application, et c'est le seul chemin de sollicitation qui atteigne aussi ceux qui ont coupé les notifications Windows. Les garde-fous existants restent en vigueur — deux essais au maximum sur la vie de l'installation, aucun après une réponse, aucun dans les 48 heures qui suivent une erreur journalisée, un seul par jour.

**Identité produit — moitié non localisée (décision du 2026-08-17)**

- Nouvelle classe `ProductIdentity`. 78 sites qui nommaient le produit en dur passent par une source unique : les 20 URL du site, le lien Discord, le dépôt GitHub, l'identifiant Store, le nom du binaire, le raccourci de démarrage, le dossier de configuration, la ressource du logo, les 12 noms de classes fenêtre, le mutex d'instance unique et 31 titres affichés. 77 lignes remplacées pour 77 ajoutées : substitution ligne pour ligne, aucun changement de comportement.
- Deux formes distinguées là où le code n'en avait qu'une : `DisplayName` « AZERTY Global », ce que l'utilisateur lit, et `Namespace` « AZERTYGlobal », qui est déjà le `RootNamespace` du csproj, l'`Identity Name` du MSIX et l'`Application Id`. `ConfigFolderName` reste un littéral distinct à dessein : renommer le produit ne doit pas déplacer la configuration et la progression de tout le monde.
- Le `TaskId` du `StartupTask` reste lui aussi un littéral hors de `ProductIdentity` : `AutoStart.cs` garde `"AZERTYGlobalStartup"` en dur, à l'identique du manifeste de la 1.1.0.0 servie par le Store. C'était le seul point de cette refonte capable de casser quelque chose — un `TaskId` dérivé du `Namespace` mais différent de celui qu'a enregistré la v1.1 rend la tâche orpheline à la mise à jour, et le lancement automatique cesse sans rien dire.
- `WindowClass(suffixe)` supprime une paire de littéraux que sept fenêtres dupliquaient entre `RegisterClassEx` et `UnregisterClass` — About, CharSearch, Onboarding, Settings, ToggleNotif, UsageStats et VK répétaient leur nom de classe à deux endroits, et n'en renommer qu'un laissait la classe enregistrée.
- Vérifié avant d'y toucher : rien ne dépendait de ces chaînes. Aucun appel à `FindWindow` dans le code, et les clés de position de fenêtre sont des constantes propres (`lessonsWindowBounds`, `VirtualKeyboardBoundsKey`), pas les noms de classe.
- Hors périmètre, différé après la soumission Store : les ~65 occurrences du nom enchâssées dans les phrases traduites de `Localization/`, ainsi que l'`AssemblyDescription`. Elles réécrivent du texte visible en français et en anglais.

**Sollicitation d'avis — arbitrages du 2026-08-17**

- Le partage d'un résultat n'incrémente plus que le compteur d'essais : il ne pose plus `reviewPromptClicked`. Windows ne dit jamais si l'utilisateur a déposé sa note, si bien qu'un partage suivi d'une boîte refermée aussitôt sans rien noter éteignait toute sollicitation ultérieure — le second essai était perdu pour quelqu'un qui n'avait rien donné. Le plafond de deux essais, la limite d'une sollicitation par jour et le silence de 48 heures après une erreur journalisée restent en vigueur.
- L'entrée « Noter sur le Microsoft Store » du menu de la zone de notification pose désormais `reviewPromptClicked` : y aller de soi-même est le signal d'intention le plus net dont dispose l'application, et relancer quelqu'un qui vient de jouer le jeu serait le pire des cas. Elle ne consomme pas d'essai pour autant — ce n'est pas une sollicitation que l'application s'est accordée, c'est une action de l'utilisateur.
- Le bouton de copie de « Mes statistiques », présent depuis la v1.1 sans avoir jamais rien armé, déclenche la même sollicitation différée que le partage d'un résultat de défi : même geste de promotion, mêmes garde-fous, et tir à la fermeture de la fenêtre plutôt qu'au clic, pour ne pas couper le geste en deux.

**Lancement automatique — rattrapage hors accueil (décision du 2026-08-17)**

- Nouvelle entrée « Lancer au démarrage de Windows » au premier niveau du menu de la zone de notification, cochée quand la tâche est réellement enregistrée. Hors Paramètres, le lancement automatique n'avait aucune affordance permanente.
- Relance unique lorsque l'application a servi deux jours distincts sans démarrage automatique. Deux jours d'usage sans autostart signifient que l'utilisateur l'a relancée lui-même : l'intention est déjà démontrée, on ne lui épargne que le geste. Une seule proposition sur la vie de l'installation, jamais réémise, et rien ne s'active sans un clic.
- Motif : le manifeste déclare le `StartupTask` à `Enabled="false"`, et `OnboardingWindow.Close()` ne persistait la case pré-cochée que si l'utilisateur avait atteint l'étape 3, c'est-à-dire après deux clics sur « Suivant ». Qui refermait l'accueil plus tôt — croix, Échap, « Quitter » — n'obtenait jamais le lancement automatique et ne revoyait pas l'application au démarrage suivant.
- La décision de la v0.9.7.1 reste intacte : aucune case jamais vue n'est persistée en silence. Un choix fait à la main dans le menu éteint la relance, dans un sens comme dans l'autre.
- La règle de déclenchement vit dans `AutoStartNudge`, pure et testable sans fenêtre, sur le modèle de `TrainingReminders.ShouldRemind`.

**Correctifs de l'audit de release (2026-08-18)**

- Sollicitation d'avis après un partage : la limite d'une par jour se lisait sur un champ en mémoire, remis à zéro à chaque démarrage du processus. Elle ne valait donc qu'une par session — partager un résultat, redémarrer l'application, repartager le même jour consommait les deux essais de la vie de l'installation en quelques minutes. La garde s'appuie désormais sur la date persistée.
- Le partage exige à son tour un plancher d'usage : une première frappe remappée et deux jours d'usage distincts. Ce chemin n'appliquait aucun seuil, si bien qu'un utilisateur du jour 1 qui terminait le Défi du jour et copiait son résultat recevait la boîte de notation dans l'heure. Les seuils de 3 et 10 jours de la notification ne s'y appliquent toujours pas — un partage est un geste volontaire, pas une interruption — mais l'application doit avoir servi.
- La date du dernier essai est écrite en heure locale, comme toutes ses comparaisons. En UTC, elle enregistrait la veille pour toute sollicitation affichée entre minuit et 2 heures locales, et le plancher de 7 jours du second essai s'ouvrait un jour trop tôt. Elle est maintenant fournie par l'appelant, qui a déjà calculé sa date.
- La relance du lancement automatique s'éteint aussi depuis Paramètres et depuis l'accueil, et plus seulement depuis le menu de la zone de notification. Activer puis désactiver le lancement automatique ailleurs que dans le menu laissait la relance armée, et l'application proposait deux jours plus tard d'activer ce que l'utilisateur venait de couper. Seul un changement réel de la case compte : refermer Paramètres sans y toucher n'est pas un choix.
- Le manifeste de packaging déclare enfin l'activateur COM de toast — `com:ComServer` et `windows.toastNotificationActivation` — que `ToastActivation.cs` exigeait en commentaire depuis sa création sans qu'aucune déclaration n'existe. Sans elle, Windows relance l'exécutable au clic sur un toast au lieu de livrer l'activation au processus vivant.
- Le test censé garantir cette déclaration comparait deux chaînes codées en dur sans jamais ouvrir le manifeste : il est resté vert pendant toute l'absence. Il lit désormais le fichier livré au packaging, et deux témoins prouvent qu'il échoue sur un manifeste sans déclaration comme sur un CLSID divergent.
- La règle de décision du chemin partage vit dans `ReviewSharePrompt`, pure et testable sans fenêtre, sur le modèle d'`AutoStartNudge`.

**Déployabilité en parc — lots A à F (décisions des 2026-08-19 et 2026-08-20)**

- Détection du canal d'installation : `AppChannel` distingue le canal Microsoft Store, le canal AMCF hors Store et l'exécution hors package, en lisant l'identité du paquet. Une identité illisible retombe sur le canal hors package et jamais sur le Store — se tromper dans ce sens n'active rien.
- Le canal AMCF ne demande plus rien : ni sollicitation d'avis, ni « Soutenir le projet », ni lien Discord, ni « Noter sur le Microsoft Store ». Le prédicat canonique est `AppChannel.IsSober(channel) => channel == DistributionChannel.Amcf`, et non « tout ce qui n'est pas le Store » : le canal hors package garde exactement le comportement de la 1.1.0, décision D8, qui reste à trancher.
- Le canal AMCF n'écrit plus aucune statistique sur le disque — `usage-stats.json` n'est pas créé — et la fenêtre « Mes statistiques » le dit au lieu d'afficher des zéros.
- Couche de politiques d'entreprise sous `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\AZERTYGlobal`, cinq valeurs : `Language`, `NotificationsEnabled`, `UsageStatsEnabled`, `ExternalLinksEnabled` et `ShowOnboarding`. Précédence politique, puis réglage utilisateur, puis défaut du canal. Un réglage imposé apparaît grisé, avec la mention qu'il est géré par l'organisation. `ShowOnboarding` à 1 autorise sans imposer : seul 0 contraint, pour ne pas retirer à l'utilisateur une case qu'il avait cochée.
- Modèle d'administration livré dans `entreprise/` : `AZERTYGlobal.admx`, ses libellés `fr-FR` et `en-US`, et `politiques-exemple.reg`. Un test refuse tout écart entre les cinq clés du modèle et celles que lit `PolicyManager`. Le `.reg` est en UTF-16 LE avec BOM, seul fichier du dépôt dans ce cas, parce que c'est ce que `regedit` attend.
- Le `.appinstaller` du canal AMCF est désormais généré depuis le manifeste du bundle signé par `scripts/gen-appinstaller.py`, et vérifiable contre un bundle. L'identité n'est jamais recopiée à la main, l'URL du bundle est stable et sans numéro de version, et la vérification de mise à jour est fixée à 24 heures.
- Note RGPD pour les établissements scolaires et les structures publiques, dans `entreprise/` : ce qui est écrit sur le poste, où, comment l'éteindre, et le fait que l'établissement reste responsable de traitement. Les noms de logiciels du journal de compatibilité y sont documentés comme anonymisés par HMAC-SHA256 à sel local.
- Les documents de parc perdent la prétention qu'une stratégie de groupe installe un MSIX : elle ne l'installe pas, elle lance le script qui l'installe. `Pilotes/Note informatique.md` conserve la mention exacte, précisée. Leurs liens de téléchargement passent aux URL stables du lot E.
- Nouveau `scripts/check-doc-versions.py` : les documents de parc portent un bloc de suivi machine-lisible, et le scanner refuse une version déclarée périmée, une version inconnue dans le corps, une empreinte non déclarée ou une URL de téléchargement versionnée. C'est l'absence d'un tel garde qui avait laissé la note DSI en 1.1.0 pendant six semaines. 33 tests, et dix mutations rouges dans `docs/audit-v1.2.0/witness-lot-f-versions.py`.

**Couches maintenables — grec, cyrillique, scientifique (décision du 2026-08-05, portée le 2026-08-24)**

Fonctionnalité développée et validée le 2026-08-05 dans la copie de travail Codex (163/163 tests),
restée dans un commit local jamais poussé, hors de la réconciliation du 2026-08-15. Portée dans ce
dépôt le 2026-08-24 sur l'architecture extraite : machine d'état et détection sécurisée dans
`TypingEngine.Windows`, fenêtres et service d'insertion dans l'application, chaînes bilingues dans
`Localization/L.Layers.cs`. Désactivée par défaut, activation volontaire depuis le menu, entièrement hors ligne.

- Les trois touches mortes `dk_greek` (Maj + *), `dk_cyrillic` (AltGr + *) et `dk_scientific` (AltGr + =)
  gagnent deux modes sans changer leurs tables ni leurs emplacements : appui simple = touche morte
  ponctuelle actuelle, double appui = verrouillage dans l'application active. Un déclencheur maintenu
  pendant une autre frappe vaut un appui simple — le mode « maintien » du développement initial a été
  retiré au smoke test du 2026-08-24, tenir le déclencheur en tapant du reste des doigts étant intenable.
- Le verrouillage est associé à l'instance exacte du processus (PID + instant de création) : il survit
  aux allers-retours entre applications, disparaît à la fermeture du processus et n'est jamais hérité
  par un PID réutilisé. Déverrouillage par le même déclencheur ou par un premier Échap (absorbé) ; une
  autre couche peut transformer la frappe suivante par-dessus la couche verrouillée, son double appui
  remplace le verrou.
- Le Shift ou AltGr d'activation est consommé tant qu'il reste enfoncé ; un nouveau Shift ou le
  Verr. Maj. produit les majuscules. Espace reste une espace normale en verrouillage ;
  une touche non définie dans la couche produit son caractère AZERTY Global ordinaire ; les 26 autres
  touches mortes sont inchangées. Ctrl, Alt et Windows conservent leurs raccourcis pendant une couche active.
- Nouvel indicateur discret près du curseur (couche active et mode), masqué dans les champs sécurisés
  et quand le remapping est suspendu (jeux, pause).
- Champs de mot de passe : le remapping ordinaire est conservé mais les couches, la recherche et
  l'indicateur sont suspendus. Détection native `ES_PASSWORD` complétée par UI Automation pour les
  navigateurs, exécutée sur un thread MTA dédié à attente bornée (30 ms) pour ne jamais ralentir le
  hook clavier ; seul le booléen IsPassword est consulté, jamais le contenu du champ.
- Recherche de caractères : `Entrée` insère désormais le caractère directement dans la fenêtre
  d'origine (y compris à l'ouverture depuis le menu tray), et la fenêtre de recherche reste
  ouverte comme en 1.1.0 pour enchaîner les insertions (retour du smoke test du 2026-08-24 —
  la fermeture au premier caractère surprenait) ; en cas d'échec, repli sur la copie avec
  notification explicite.
- Réglages persistants (couches actives, délai du double appui 150-1000 ms, indicateur) via la
  nouvelle fenêtre « Couches maintenables » ; les configurations existantes migrent avec la
  fonctionnalité désactivée. La case « export volontaire de diagnostics » de la version Codex
  n'est pas reprise : aucun code ne consommait ce consentement (audit du 2026-08-24).
- Les mises à jour d'icône tray, d'infobulle et d'indicateur déclenchées depuis le hook clavier partent
  désormais en différé coalescé sur la boucle de messages — plus aucun appel bloquant dans `WH_KEYBOARD_LL`.
- Adaptations de portage vs la version Codex : le moteur ne lit aucune configuration (l'application
  pousse ses réglages par `ApplyMaintainableLayerSettings`), les textes passent par `L.Layers.cs`
  (FR/EN), le nom de classe fenêtre par `ProductIdentity.WindowClass`, et la garde Win s'appuie sur
  l'état déjà suivi par `TrackModifiers`. Les trois suites passent à **18 + 107 + 287 = 412 tests**
  (376 avant le portage, incluant les 21 tests de notifications du 2026-08-23).

**Revue de release 1.2.0**

- Version applicative portée de `1.1.2` à `1.2.0`, manifeste de packaging de `1.0.0.0` à `1.2.0.0` (le Store sert `1.1.0.0`). `Program.cs` et `AssemblyInfo.cs` étaient restés en `1.1.2` : l'infobulle du tray et les rapports de bug se seraient annoncés en 1.1.2, et `Verify-Release.ps1` bloquait dessus.
- 422 tests xUnit passent sur les trois suites — 296 applicatifs, 108 moteur Windows, 18 moteur portable (mesure du 2026-08-27, suites Debug locales). Le chiffre de 250 annoncé le 17 août avait été figé avant huit tests ajoutés le même jour au parseur de disposition ; l'audit de release et ses correctifs l'ont porté à 282, puis les lots de déployabilité en parc, la refonte des notifications et le portage des couches maintenables à 412, et les correctifs de l'audit final du 24 août à 421, puis le smoke des 24-27 août — retrait du mode « maintien », correctif des bulles d'état — à 422. Build Release : 0 avertissement, 0 erreur.
- Quatre tests comparent `Program.Version` aux attributs d'assembly à chaque exécution de la suite. La dérive qui a bloqué les portes de release ce jour-là ne se voyait que dans `Verify-Release.ps1`, une levée à la fois et au moment du packaging ; elle apparaît désormais en CI, avant. Le csproj et `AppxManifest.xml` restent couverts par le script, hors de portée d'un test qui ne connaît que l'assembly compilé.
- Le package n'embarque plus les visuels de la fiche Store : `Pack-MSIX.ps1` copie désormais une liste blanche — les quatre logos que le manifeste référence et leurs variantes `.scale-200` — au lieu du dossier `Assets/` entier. Chaque `.msix` perd 3,4 Mo de captures, posters et gabarits internes (audit du 2026-08-24).
- La fenêtre « Mes statistiques » ne réserve plus les 130 pixels de la section « Défi du jour » quand celle-ci ne se dessine pas (rappels inactifs et aucun défi complété) : la hauteur suit le même prédicat que le rendu et se recalcule à chaque ouverture. Constaté par Antoine au premier lancement du smoke test du 2026-08-24.
- Les bulles d'état (« Actif » au démarrage, pause, fin de pause, reprise après suspension) s'affichent désormais réellement : `Shell_NotifyIconW` les rejetait en silence sur tout écran à échelle non-100 %, `NIIF_LARGE_ICON` exigeant une icône exactement à `SM_CXICON` — 40 px à 125 % — là où `CreateTextIcon` la produisait en 32 px codés en dur. L'icône balloon est désormais le logo du produit (favicon embarqué), rendue à la taille système du moment et recréée à chaque émission, avec repli sur l'icône texte si le chargement échoue ; les bulles de sécurité (`NIIF_WARNING`, sans icône custom) n'ont jamais été touchées. Le timer one-shot de 1,5 s posé le 24/08 sur la bulle « Actif » (diagnostic `NIM_ADD`, invalidé depuis) reste en place comme garde d'état. Cause isolée par builds diagnostics et compteurs shell pendant le smoke test du 2026-08-27.
- La fenêtre « Couches maintenables » efface son fond : sa classe n'avait pas de brosse (`hbrBackground` nul) et, au-dessus d'un jeu plein écran, elle laissait voir la scène et dessinait ses contrôles en double. Brosse de classe alignée sur les autres fenêtres, statics et cases posés dessus en transparence (constaté sous Trackmania, smoke test du 2026-08-24).
- Artefacts du 2026-08-27 : bundle `AZERTYGlobal-1.2.0.0.msixbundle` reconstruit pendant le smoke test avec le correctif des bulles d'état (commit `146a93e`), vérifié par `Verify-Release.ps1` — empreinte `97B2E5A1…`, 6 738 046 octets. Remplace le bundle final du 2026-08-24 (`A69ACB8F…`, 6 736 787 octets), qui portait les sept commits de l'audit final, les trois premiers correctifs du smoke et le retrait du mode « maintien » ; celui-ci remplaçait le bundle du matin (`C7621B01…`, 6 735 511 octets), packé sur des publications antérieures aux commits de l'audit. Le bundle **n'est pas signé** : ni WACK, ni signature AMCF, ni soumission Partner Center à cette date, et la validation manuelle §7 du cahier des charges reste due avant tout envoi.
- ⚠️ Trois vérifications de `Verify-Release.ps1` se sautent en silence dans ce dépôt — TO-DO, contexte app et contexte projet, tous hors dépôt. Un avertissement, pas un échec : le script ne garantit donc que ce qu'il a réellement lu.

## Version 1.1.0 — 23 juillet 2026

Publiée sur le Microsoft Store en `1.1.0.0` (révision Store `2026-07-23T22:11:18Z`). Le package a été produit hors de ce dépôt : aucun tag ni commit de release ne lui correspond ici, et le code n'a rejoint la branche canonique qu'avec le commit `452aab0` du 2026-08-15. Entrée reconstituée le 2026-08-16 à partir des notes de version publiées (FR et EN), du code réconcilié et de l'inspection de l'application installée depuis le Store, pas d'un diff de release.

**Interface bilingue**

- Interface complète en anglais, en plus du français.
- Changement de langue à chaud depuis la fenêtre de bienvenue, le menu de la zone de notification ou les Paramètres.

**Statistiques locales**

- Nouvelle fenêtre « Mes statistiques » : jours d'utilisation, séries, temps de frappe actif et caractères spéciaux produits avec AZERTY Global.
- Calcul et stockage exclusivement sur l'appareil, sans télémétrie réseau. Aucune frappe ni aucun texte n'est enregistré ni transmis.
- Bouton de copie volontaire d'un résumé lisible des statistiques dans le presse-papiers.

**Avis et retours**

- Accès direct aux avis, aux retours et à la communauté depuis l'application.
- Sollicitation d'avis unique 7 jours après le premier lancement (`MaybeShowReviewPrompt`), cible tirée à 50/50 entre le volet d'avis du Store et la page feedback du site, marquée comme faite dès l'affichage. Vérifié sur une installation Store 1.1.0.0 : `firstRunTimestamp` et `reviewPromptDone` sont écrits dans `config.json`.

**Recherche de caractères**

- Prise en charge des noms anglais et de nombreux alias supplémentaires liés aux langues.

**Fiabilité**

- Comportement fiabilisé après une mise en veille ou une session de bureau à distance.
- Messages d'erreur plus clairs et correctifs d'interface divers.

## Version 1.0.0 — 29 juin 2026

Version stable de lancement Microsoft Store. Cette version reprend la RC interne `0.12.0`, validée le 2026-06-26, puis reconstruite et publiée en `1.0.0` le 2026-06-29. Le MSIX hors Store signé AMCF reste un livrable séparé.

**Module Leçons**

- Ajout d'une fenêtre `Leçons` accessible depuis le tray, avec catalogue embarqué `lessons.json`, progression locale et mode libre non persistant.
- Ajout d'un renderer clavier commun utilisé par le clavier virtuel, l'onboarding et les leçons.
- Ajout du moteur `LessonTypingSession`, du chargement de catalogue, des indices et du stockage `lessons-progress.json`.

**Correctifs pré-test manuel**

- Les leçons reprennent la gestion `WM_SYSCHAR` / `WM_SYSKEYDOWN` et la résolution positionnelle déjà utilisée par l'onboarding pour les dispositions sous-jacentes non-AZERTY.
- Le pass-through des lettres identiques reste actif même avec Verr. Maj. afin de préserver les raccourcis applicatifs comme `K` sur YouTube.
- Les échecs de sauvegarde de `config.json` sont maintenant journalisés.

**Validation Store 1.0.0**

- Bundle Store 1.0.0.0 reconstruit le 2026-06-29 (`msix/AZERTYGlobal-1.0.0.0.msixbundle`) pour x64 + ARM64.
- `scripts/Verify-Release.ps1` PASS : hashes des exécutables publiés identiques aux exécutables embarqués dans le bundle.
- WACK 1.0.0 PASS ; `DPIAwarenessValidation` PASS, optional `Blocked executables` non bloquant conservé.
- Package accepté et publié par Microsoft le 2026-06-29.
- SHA-256 du bundle Store : `E6BC370052CDFF26F8F3C6BD2526C338A749B67A2F48BE24B175C71C672C9855`.

## Version 0.11.2 — 3 juin 2026

**Exercice de typographie**

- Phrase de l'exercice 4 remplacée par : `Lætitia demande « d'où vient ce chef-d'œuvre… » — elle l'approuve à 100 %.`

**Correctifs pré-publication Store**

- Mode compatibilité jeux : les combos natives utilisent désormais de vrais événements scancode (`KEYEVENTF_SCANCODE`) pour les applications qui bindent les touches physiquement.
- Désactivation anti-cheat : la notification de sécurité reste affichée même si les notifications standard sont désactivées.
- Journaux locaux : anonymisation du nom de process dans le log debug compat et suppression du chemin complet `learning-tweaks.json`.

## Version 0.11.1 — 28 mai 2026

**Correctif dispositions système non-AZERTY**

- Correction du pass-through clavier quand la fenêtre cible utilise une disposition système non-AZERTY, notamment QWERTY US.
- Les touches physiques restent pilotées par scancode : `D01` produit bien `a` au lieu de laisser passer `q`, et `E01` produit bien `&` au lieu de laisser passer `1`.
- Le pass-through reste conservé quand la disposition de la fenêtre cible produit déjà le bon caractère.
- Correction associée pour les raccourcis `Ctrl+touche` : `Ctrl+D01` sous QWERTY envoie bien `Ctrl+A`, pas `Ctrl+Q`.

## Version 0.11.0 — 20 mai 2026

**Synchronisation avec la disposition actuelle**

- Ressources embarquées synchronisées avec la disposition actuelle : `AZERTY Global 2026.json` et `character-index.json`.
- Mise à jour des raccourcis : `#` en alternative développeur sur AltGr + :, `^` sur AltGr + I, backtick vif sur AltGr + L, Latin étendu sur AltGr + 6, tiret insécable sur Maj + AltGr + 6.
- Espaces insécables alignées : espace fine insécable sur AltGr + Espace, espace insécable sur Maj + AltGr + Espace.
- Recherche de caractères mise à jour avec 1034 entrées d'index, dont 1005 caractères Unicode et 29 touches mortes.
- Ajout d'un script durable de synchronisation des ressources depuis le site, avec validation des raccourcis critiques.

## Version 0.10.0 — 8 mai 2026

**Audit sécurité indépendant**

- Hardening binaire : Control Flow Guard (CFG) activé sur les binaires AOT x64 et ARM64. Build déterministe explicite.
- Robustesse renforcée : gestion d'erreurs défensive sur le hook clavier (try/catch sur le callback bas niveau) et les allocations mémoire natives (try/finally sur 5 sites `Marshal.AllocHGlobal`).
- Privacy : logs locaux désormais limités (pas de stack traces complètes ni de paths utilisateur dans `error.log`) et noms de process anonymisés via HMAC-SHA256 dans les events critiques de compatibilité.
- Isolation hook : marker d'injection randomisé au démarrage (au lieu d'une valeur fixe), mutex d'instance unique préfixé `Local\` + suffixé SID utilisateur (anti-squat).
- CI GitHub Actions ajoutée (build reproductible x64+ARM64 + tests + Pack-MSIX + Verify-Release + BinSkim hardening + attestation SLSA L1).
- Hygiène repo : suppression d'un fichier doublon `OnboardingWindow (# Name clash...)` issu d'un conflit de sync Proton Drive.

Aucun changement fonctionnel utilisateur visible. Audit complet : `Archives/audits/2026-05/reports/AUDIT-SECURITY-v0.10.0.md`.

## Version 0.9.8 — 5 mai 2026

**Menu tray — entrée « Exercices »**

- Nouvelle entrée `Exercices` dans le menu de la zone de notification (entre `Rechercher un caractère` et le séparateur). Ouvre le `LearningModule` en mode replay : démarre toujours à l'exercice 1, parcourt les 4 exercices normaux puis la page de choix avant les 2 exercices bonus, comme l'onboarding initial.
- Mode replay sans side-effect sur la progression : la valeur `learningMaxStepCompleted` du fichier de configuration n'est jamais modifiée par cette voie. La progression sauvegardée reste celle du premier passage onboarding.
- Garde-fou : si la fenêtre d'onboarding est en cours d'utilisation OU si une autre instance de `LearningModule` est déjà ouverte, le clic est ignoré (no-op) — pas de doublon d'instance.

**Notification toggle — suppression du doublon**

- Suppression de la balloon Windows (zone de notification, en bas à droite) lors des bascules `Ctrl+Maj+Verr.Maj` : elle faisait doublon avec la mini-fenêtre flottante en haut à droite (`ToggleNotification`, ajoutée en v0.9.7) qui était déjà plus visible et plus lisible. Reste désormais seule la fenêtre flottante.
- La balloon de démarrage de l'app (rappel du raccourci `Ctrl+Maj+Verr.Maj` au lancement) est conservée — elle a un rôle pédagogique différent.

## Version 0.9.7 — Avril-Mai 2026

**Caps Lock — refonte complète (smoke test in-game, mai 2026)**

- **Désynchronisation entre l'état Caps Lock interne et Windows** corrigée : la frappe `Caps Lock + lettre` puis lancement d'un exercice produisait des majuscules permanentes à cause d'un état désynchros. `_capsLockState` est désormais resynchronisé avec `GetKeyState(0x14)` à chaque frappe non-modifier (`KeyMapper.ProcessKey`), et `RequestCapsLockOff` vérifie l'état Windows réel avant de toggler.
- **Modificateurs Shift/Ctrl/Alt résiduels** corrigés : si l'application est lancée pendant qu'un jeu tient des touches (ex. Maj pour sprinter), le keydown initial était manqué et des frappes ultérieures sortaient en majuscule. `SyncState` appelle désormais `CleanupStaleModifiers()` ; `SyncState` lui-même est invoqué au démarrage de l'app et au retour de focus du LearningModule.
- **Suppression du toggle Caps Lock physique** dans `BuildVkComboInputs` (mode NativeCombo) : auparavant chaque frappe en Caps Lock ON injectait `VK_CAPITAL down/up` deux fois, ce qui spammait la notification Windows « Verr. Maj. activé/désactivé » dans Minecraft, Trackmania, etc. Désormais on inverse logiquement `needsShift` (Caps Lock + Shift s'annulent côté Windows) — sans toucher physiquement à Caps Lock.
- **Détection dynamique « Caps Lock affecte ce VK ? »** via `ToUnicodeEx` (avec/sans état Caps Lock simulé, flags=1 sans consommer le dead-key state). Cache par `(vk, hkl)`. Couvre exactement les touches affectées (lettres A-Z, rangée numérique, ponctuation OEM en AZERTY) et exclut celles qui ne le sont pas (VK_OEM_102 `<>`). Bug `<` qui devenait `>` en Caps Lock corrigé.

**Touches mortes natives — fallback Alt+code**

- Les caractères qui sont eux-mêmes des dead keys sur le layout natif (`^` `¨` `~` `` ` `` en AZERTY traditionnel) faisaient entrer Windows en mode dead-key lors de l'injection en mode NativeCombo, ce qui consommait le caractère sans l'afficher (workaround Tab nécessaire dans Trackmania). `BuildNativeComboInputs` détecte désormais via `IsDeadKeyOnLayout` (`ToUnicodeEx` renvoie -1) et fait fallback automatique sur Alt+code, qui bypass complètement le système dead-key Windows.

**Compatibilité — détection foreground**

- `ForegroundMonitor.Recompute` ignore désormais les transitions vers `explorer.exe`, `SearchHost.exe`, `StartMenuExperienceHost.exe`, `ShellExperienceHost.exe`, `TextInputHost.exe` — effets de bord du clic sur l'icône tray ou de la touche Windows. Sans ce filtre, le sous-menu « Compatibilité » affichait `SearchHost.exe` ou `explorer.exe` au lieu du jeu réel.
- Le PID de notre propre application n'est plus ignoré : quand la fenêtre du LearningModule prend le focus, le mode redevient correctement `Default` (au lieu d'hériter d'un `NativeCombo` parasite d'un jeu antérieur), ce qui empêche l'AltGr+N (`~`) d'être consommé en mode dead-key dans nos exercices.
- Sous-menu « Compatibilité » dans le menu tray filtré quand le foreground est notre propre app (plus d'item « Compatibilité — AZERTY Global.exe »).

**Retour visuel pendant les jeux fullscreen — `ToggleNotification`**

- Nouvelle mini-fenêtre TOPMOST en haut à droite (240×56 px logiques, opacité ~94 %, auto-fermeture 2 s) qui affiche « AZERTY Global activé » (vert) ou « AZERTY Global désactivé » (gris) à chaque toggle via `Ctrl+Maj+Verr.Maj`. Permet de voir l'état du remapping en borderless windowed quand l'icône de la zone de notification est cachée par le jeu. Angle mort accepté en exclusive fullscreen.
- **Garde anti-cheat** : la fenêtre TOPMOST ne s'affiche **jamais** quand un process protégé par anti-cheat kernel-level est au foreground (Valorant, Fortnite, CoD, etc.). Évite tout risque qu'un anti-cheat scanne l'overlay et le flagge comme cheat tiers.

**LearningModule — finitions**

- Le module force `RequestCapsLockOff()` à l'ouverture : tous les exercices commencent désormais avec Caps Lock désactivé, peu importe l'état hérité du contexte extérieur. Empêche que l'utilisateur arrive sur l'exo 1 « Activez Verr. Maj. » avec Verr. Maj. déjà actif.
- Suffixe « (Bonus) » en doré-orangé (`#E29400`) à la suite du titre des exercices facultatifs (ex5, ex6) — remplace l'ancienne pill orange peu lisible. La couleur dorée évite la confusion avec le vert utilisé pour la progression.
- Page de choix fin d'exercice : navigation par flèches (`←` / `↑` = Recommencer ; `→` / `↓` = Suivant ; `Esc` = Quitter).
- Écran final « Bravo ! » : flèches `→` / `↓` + `Esc` ferment la fenêtre (équivalent au bouton Terminer). Bouton Terminer repositionné — aligné à droite (largeur 140 px), juste au-dessus du clavier, pour ne plus chevaucher le sous-titre « Vous maîtrisez les bases d'AZERTY Global. ».
- Tooltip de la touche Backspace désactivée passé sur 2 lignes pour la lisibilité.
- Masquage des caractères secondaires peu utilisés sur le clavier virtuel des exercices (point en chef, point souscrit, double aigu, double grave, corne, crochet, brève, brève inversée, barre oblique/horizontale, macron, latin étendu, cédille, virgule souscrite, alphabet phonétique, rond en chef, symboles scientifiques, caron, ogonek, alphabet cyrillique, symboles divers `→`, guillemet-apostrophe ouvrant, soft hyphen, arobase alternatif sur AltGr+E10, guillemets doubles `“ ”`). Ces caractères restent visibles dans le **tooltip de chaque touche** au survol.
- Tooltips uniformisés : tous les noms de caractères et de touches mortes sont en MAJUSCULES (cohérence visuelle). Format des dead keys : `TOUCHE MORTE + nom` (ex. « TOUCHE MORTE SYMBOLES DIVERS » au lieu de « FLÈCHE VERS LA DROITE »). Override pour `’` qui s'affiche désormais comme « APOSTROPHE TYPOGRAPHIQUE » (au lieu du nom Unicode officiel « GUILLEMET-APOSTROPHE FERMANT »).

**Wizard d'accueil — finitions UX**

- Étape 1 : libellé du bandeau passé de « Version bêta » à « **Phase de tests** » + point ajouté après « donnez votre avis ». Espacement entre le titre « Votre clavier est maintenant amélioré » et la barre de progression réduit (24→12 px).
- Étape 1 : la phrase rassurante « Cette application améliore votre clavier. Aucune frappe n'est enregistrée ni transmise. » est désormais sur une seule ligne avec une fonte dédiée à scaling proportionnel calibré (`-(int)Math.Round(17 * dpiScale / 1.75)` — 10 px à 100 % DPI, 17 px à 175 % DPI).
- Hauteur de la fenêtre wizard réduite de 810 → 770 px (compacité).
- Étape 3 : checkboxes « Lancer au démarrage de Windows » et « Ne plus afficher cet écran au démarrage » désormais cochées par défaut à chaque ouverture (recommandation). Lien « Donner son avis sur la bêta » renommé « Donner son avis sur AZERTY Global » (cohérence avec le menu tray).
- Navigation par flèches sur les 3 étapes (sous-classe `ButtonArrowSubclassProc` sur les boutons Next/Prev/Try) : `↓` / `→` = bouton principal de l'étape (Essayer maintenant / Suivant / C'est parti) ; `↑` / `←` = bouton Précédent (étapes 2 et 3) ; `Esc` = fermer.

**AboutWindow — refonte**

- Hauteur réduite de 320 → 230 px, largeur passée de 420 → 500 px pour faire tenir les liens.
- Description simplifiée : « Disposition clavier améliorée pour les francophones. »
- Ligne « Édité par l'AMCF » + ligne secondaire fusionnées en une seule : « Édité par l'**Association pour la Modernisation du Clavier Français (AMCF)** » avec le nom complet en lien cliquable vers la page HelloAsso de l'association.
- Lien « azerty.global » renommé « Site web ». Lien « Licence EUPL 1.2 » enrichi en « Licence EUPL 1.2 (open source) » — la mention « Licence : EUPL 1.2 (open source) » au-dessus est supprimée (redondance).

**LayoutConflictWindow — wording**

- Mention de la suppression de la disposition système reformulée pour être indépendante de la langue de Windows : « Enlève AZERTY Global de la liste des dispositions chargées dans les options de langue (Paramètres Windows → Heure et langue → Langue → Options de la langue concernée). » (au lieu de « désinstalle le pack « Français — AZERTY Global » »).

**Tutoiement / vouvoiement — stratégie**

- L'OnboardingWindow et le LearningModule (sas d'accueil) **vouvoient** l'utilisateur, en cohérence avec le site web public.
- Toutes les autres fenêtres et messages (LayoutConflictWindow, SettingsWindow, TrayApplication notifications, AutoStart erreurs) **tutoient** l'utilisateur.

**Settings — libellés**

- « Notifications (activé / désactivé) » → « Notifications ».
- « Lancer au démarrage de Windows (recommandé) » → « Lancer au démarrage de Windows ».
- « Afficher la fenêtre de bienvenue au démarrage » → « Fenêtre de bienvenue au démarrage ».
- MessageBox de confirmation « Réinitialiser raccourcis » réécrite sur 2 lignes pour rendre la boîte plus compacte.

**Menu tray — corrections**

- L'item « Donner son avis sur AZERTY Global » pointe désormais vers `https://azerty.global/beta` (au lieu de `/feedback`) tant que la phase de retours est en cours.

**Tests automatisés**

- Le test `BuildVkComboInputs_CapsLockActive_AndShiftCombo_TogglesCapsAround` qui validait l'ancien comportement (toggle Caps Lock physique) a été remplacé par `BuildVkComboInputs_CapsLockActive_NoPhysicalToggle` qui valide l'absence d'event `VK_CAPITAL` injecté.
- 77/77 tests xUnit passent. Build Release AOT x64 + ARM64 : 0 warning, 0 error.

---

**Wizard d'accueil — affichage conditionnel et choix utilisateur**

- Le wizard d'accueil ne s'affiche plus systématiquement à chaque démarrage : il reste affiché tant que les 3 premiers exercices n'ont pas tous été complétés. Une fois ces 3 exercices validés, l'application démarre directement en arrière-plan avec une bulle de notification discrète.
- Nouvelle option dans Paramètres : « Afficher la fenêtre de bienvenue au démarrage » — permet à l'utilisateur de désactiver manuellement le wizard à tout moment, même si les exercices ne sont pas terminés.
- État de progression persisté dans la configuration utilisateur (`learningMaxStepCompleted`) pour traverser les redémarrages.
- L'étape 1 du wizard reste au premier plan (`topmost`) pour maximiser la visibilité des 5 améliorations. Les étapes 2 et 3 ne le sont plus, pour permettre de consulter les ressources mentionnées (guide, Discord, bêta) en parallèle d'un navigateur.

**Menu de la zone de notification — réorganisation**

- Nouvelle entrée « À propos » en dessous des Paramètres : ouvre une mini-fenêtre custom avec version, licence EUPL 1.2, mention de l'AMCF, et 3 liens cliquables (site, code source GitHub, licence).
- Sous-menu « Compatibilité « process » » déplacé sous « À propos ». Le séparateur qui le suivait n'apparaît plus quand aucun process foreground n'est détecté (plus de séparateur orphelin).
- Libellé « Signaler un bug » enrichi en « Signaler un bug (version + OS) » pour clarifier les données techniques transmises au support.

**Conflit avec disposition système AZERTY Global — popup éclairée**

- Si l'application détecte qu'une disposition système AZERTY Global est déjà installée, elle ouvre désormais une mini-fenêtre custom (au lieu d'une `MessageBox` standard) qui présente le trade-off entre les deux solutions :
  1. Garder la disposition système (nécessaire pour taper avec AZERTY Global avant le login Windows : mot de passe, écran de verrouillage, UAC, BitLocker)
  2. Garder l'application (clavier virtuel et recherche de caractère, plus user-friendly post-login)
- Si la fenêtre de bienvenue devait s'afficher au démarrage, son ouverture est différée jusqu'à ce que l'utilisateur ait choisi « Garder l'application ». Évite que le wizard recouvre la mini-fenêtre d'explication.

**Refonte du mini-onboarding**

- Bouton « Essayer maintenant » : largeur dimensionnée dynamiquement selon le texte (corrige la troncature visible « ssayer maintenar »).
- Instruction des exercices : passage à un gris foncé `#404040` (contraste ~9:1) pour une lisibilité nette sur fond clair.
- Mention de confidentialité ajoutée à l'étape 1 : « Cette application améliore votre clavier. Aucune frappe n'est enregistrée ni transmise. »
- Comportement « Essayer maintenant » revu : on reste sur l'étape 1 ; le bouton se transforme en « Suivant » à la sortie des exercices (au lieu d'avancer silencieusement à l'étape 2).
- Fenêtre wizard agrandie de 750 → 810 px de hauteur pour absorber la nouvelle mention.

**Module d'apprentissage**

- Renommage « Étape 1/6 » → « Exercice 1/6 » pour distinguer du wizard d'accueil 3 étapes.
- Exercices 5 et 6 (facultatifs) : pill « Bonus » à côté du titre pour signaler qu'ils sont skippables.
- Renommage du bouton « Passer cette étape » → « Passer cet exercice ».
- Page de fin enrichie : titre « Bravo ! » en grande police orange + sous-titre « Vous maîtrisez les bases d'AZERTY Global. ».
- Légende du clavier en bas : « Maj. — Verr. Maj. — AltGr — Touche morte » avec leurs codes couleur respectifs.
- Caractères AltGr du clavier des exercices désormais en bleu accent (cohérence avec le testeur du site web).
- Reformulation des instructions des exercices 1 et 2 pour être plus explicites :
  - Exercice 1 : « Activez Verr. Maj. puis tapez sur la lettre é »
  - Exercice 2 : « Gardez le Verrouillage Majuscule activé pour taper cette phrase »
- La touche Backspace est désormais grisée pendant les exercices avec un tooltip dédié au survol : « Désactivé pendant les exercices — continue de taper, l'erreur se corrige toute seule ». Évite la confusion quand l'utilisateur appuie par réflexe sur Retour arrière après une erreur.

**Wizard d'accueil — étape 3 simplifiée**

- Retrait du lien « S'entraîner avec les leçons de frappe » (doublon avec les exercices intégrés).
- Retrait de la note d'avertissement « Le testeur en ligne nécessite de désactiver temporairement l'application. » (jugée disruptive).
- Conservation des liens Guide, Bêta et Discord.

**Bugs corrigés**

- Couleur des touches mortes (`CLR_DK_RESULT`) : corrigée d'une valeur hex à 9 chiffres invalide vers le vert intentionné `#339900`.

**Compatibilité jeux**

Refonte majeure de la couche d'injection pour résoudre les problèmes de compatibilité avec les jeux qui filtrent les frappes synthétiques (Minecraft Java, mods comme JEI, jeux Unity, SDL, GLFW…).

- **Saut impossible en sprint** (Maj+Z+Espace dans Minecraft) : la barre d'espace est désormais en pass-through même quand Shift est maintenu, puisque sa sortie ne dépend pas du Shift. Le jeu reçoit un vrai `WM_KEYDOWN VK_SPACE`.
- **« Touche fantôme »** après usage du raccourci `Ctrl+Maj+Verr.Maj` pendant qu'une touche était maintenue (personnage continuant à avancer ou aller à gauche dans les jeux) : un keyup synthétique est désormais émis pour chaque touche en pass-through avant de purger l'état interne, évitant que l'app cible ne perçoive la touche comme toujours enfoncée.
- **`Ctrl + lettre` dans les jeux qui bindent par position physique** (Minecraft via GLFW, SDL, DirectInput) : si la touche physique correspond déjà au bon VK natif, on laisse passer la frappe d'origine au lieu d'injecter une touche synthétique. Corrige `Ctrl+A` (drop d'item dans l'inventaire Minecraft).
- **Combo native pour les caractères injectés en jeu** : quand un jeu compatible (Minecraft, Trackmania, jeux Unity, SDL, etc.) est au premier plan, les caractères AZERTY Global (`@`, `#`, accents, guillemets typo) sont désormais injectés via une combinaison de touches natives du clavier sous-jacent. Marche dans les chats et tous les champs de saisie modés (notamment la recherche d'items JEI dans Minecraft, qui était cassée auparavant).
- **Alt+code automatique pour les caractères inaccessibles** sur le layout natif (`É`, `«»`, `–`, `œ`, etc.) : injection via la séquence `Alt+0XXX` du Numpad pour permettre la frappe en jeu sans perdre la fonctionnalité Smart Caps Lock ni les guillemets typographiques.
- **Désactivation automatique sur jeux protégés par anti-cheat kernel-level** (Valorant, League of Legends, Fortnite, Apex Legends, Call of Duty, R6 Siege, PUBG, Tarkov, Genshin Impact, Honkai Star Rail, Roblox, FACEIT, Battlefield 2042, The Finals, Counter-Strike 2, Marvel Rivals, Helldivers 2, etc.) : AZERTY Global se met automatiquement en pause à l'ouverture du jeu pour éviter tout risque de bannissement, avec une bulle d'information ; réactivation automatique à la fermeture.
- **Le raccourci `Ctrl+Maj+Verr.Maj` est désormais refusé pendant la désactivation auto anti-cheat** : tant qu'un jeu protégé est au premier plan, l'utilisateur ne peut pas réactiver AZERTY Global, même via raccourci. Une bulle de sécurité explique le refus. Évite les bannissements accidentels.
- **Sous-menu de compatibilité par application** dans le menu de la zone de notification : permet de forcer la compatibilité jeu, ou la désactivation totale, pour une application précise détectée au premier plan. La désactivation utilisateur sur un process protégé par anti-cheat est refusée par sécurité.
- **Fonctionnement correct en RDP, VPN et applications qui simulent AltGr via `Ctrl+Alt`** : la séquence Alt+code utilisée pour injecter les caractères inaccessibles (`É`, `«»`, `–`, `œ`…) relâche désormais correctement les modificateurs physiques tenus dans ce mode. Auparavant l'application cible recevait `Ctrl+Alt+0XXX` au lieu de `Alt+0XXX`, ce qui pouvait déclencher des raccourcis au lieu de produire le caractère.

**Architecture interne**

- Refonte modulaire : nouvelle couche `IWin32Api` permettant l'injection de dépendances et facilitant la maintenance future.
- Suite de tests automatisés (~70 tests xUnit) couvrant la liste anti-cheat, la persistance des overrides utilisateur, la détection de mode, et la construction des séquences d'injection (combo native, Alt+code, fallback Unicode).
- Rotation automatique du journal d'erreurs à 5 Mo (au lieu de la troncature à 1 Mo précédente).

**Outils internes (build DEBUG uniquement)**

- Nouvelle entrée dans le menu tray « 🛠 Réinitialiser onboarding » pour faciliter les tests visuels du parcours.

## Version 0.9.6 — Avril 2026

**Consolidation**
- Audit complet de l'architecture et du code (16 fichiers, ~7 500 lignes).
- Aucun bug bloquant identifié — version de consolidation sans changement fonctionnel.

## Version 0.9.5 — Avril 2026

**Fiabilité de la publication Store**
- Alignement des métadonnées de release sur `0.9.5` côté application et `0.9.5.0` côté package Store.
- Ajout d'un contrôle de cohérence pour la chaîne `publish -> msix -> documentation`.

**Lancement automatique plus fiable**
- Les fenêtres de paramètres et d'accueil relisent désormais l'état réel de Windows au lieu d'un cache local.
- Les messages d'erreur distinguent correctement le mode MSIX du mode non packagé.

**Recherche de caractère**
- La copie dans le presse-papiers ne signale plus un succès sans validation réelle de `SetClipboardData`.
- La fenêtre gère maintenant les changements de DPI en recalculant polices, layout et taille.

**Robustesse interne**
- Le hook clavier peut être réinstallé sans fenêtre de coupure visible.
- Les composants auxiliaires (`CharacterSearch`, `VirtualKeyboard`) n'empêchent plus le remapping de démarrer s'ils échouent isolément.
- Nettoyage des `JsonDocument` temporaires et protection du log fatal contre les erreurs d'écriture.

## Version 0.9 — Mars 2026

**Démarrage automatique avec Windows**
- AZERTY Global peut maintenant se lancer automatiquement au démarrage de Windows, sans droits administrateur et sans modifier le registre.

**Raccourcis clavier personnalisables**
- Les raccourcis pour ouvrir le clavier virtuel et la recherche de caractère sont désormais configurables pour éviter les conflits avec vos autres applications.

**Meilleure compatibilité**
- Correction d'un problème où certaines touches mortes de l'AZERTY traditionnel pouvaient interférer avec la saisie.
- Les touches de modification (Maj, AltGr) ne restent plus « bloquées » dans de rares cas.

**Préparation Microsoft Store**
- AZERTY Global sera bientôt disponible sur le Microsoft Store pour une installation encore plus simple.

---

## Version 0.8 — Mars 2026

**Recherche de caractère**
- Nouveau : trouvez n'importe quel caractère en tapant son nom en français (« e accent aigu », « euro », « tiret cadratin »…) ou en collant directement le caractère recherché.
- Le résultat indique clairement la combinaison de touches à utiliser (ex : AltGr + E → €).
- La recherche surligne automatiquement les touches correspondantes sur le clavier virtuel.

---

## Version 0.7 — Mars 2026

**Clavier virtuel**
- Nouveau : un clavier virtuel affiche en temps réel les caractères disponibles selon les touches enfoncées (Maj, AltGr, Verrouillage Majuscule).
- Le clavier s'adapte quand vous appuyez sur une touche morte pour montrer les caractères accentués possibles.
- Fenêtre redimensionnable, repositionnable et toujours visible si vous le souhaitez.

**Écran d'accueil**
- Au premier lancement, un écran d'accueil explique les bases : AZERTY Global est actif, l'icône est dans la barre des tâches, et un raccourci clavier ouvre le clavier virtuel.

**Menu amélioré**
- L'icône dans la barre des tâches donne accès au clavier virtuel, à la recherche de caractère et au site azerty.global.

---

## Version 0.6 — Mars 2026

**Premier exécutable autonome**
- AZERTY Global est désormais un fichier unique qui fonctionne sans installation ni dépendance.

---

## Version 0.5 — Mars 2026

**Première version**
- Prise en charge complète de la disposition AZERTY Global 2026 avec ses 8 couches de caractères.
- Verrouillage Majuscule intelligent : n'affecte que les lettres, pas les chiffres ni les symboles.
- Icône dans la barre des tâches pour activer ou quitter le programme.
- Une seule instance peut tourner à la fois.

---

*Dernière mise à jour : 2026-09-21*
