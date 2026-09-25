# Contre-avis, groupe 1 (audit simplicité 1.3.0, commit `f0a98ba`)

Relecture adverse, en lecture seule, des constats A-01 à A-04, M-01, M-02, M-03, M-06, M-12, A-05, F-18 et A-11. Chemins relatifs à `components/microsoft-store/`. Le dépôt a été lu à `f0a98ba` (HEAD, `src/` propre).
Une seule expérience a été lancée, dans le scratchpad, sans hook ni application : `contre-avis/pompe/` (sortie dans `contre-avis/resultats-pompe.txt`). Elle vérifie si une attente `WaitOne` sur un fil `[STAThread]` sert les messages envoyés. C'est par un message envoyé que Windows livre le rappel `WH_KEYBOARD_LL` au fil qui a posé le hook (doc Microsoft de LowLevelKeyboardProc). Résultat identique en JIT et en NativeAOT .NET 8.0.31 :
- un `SendMessage` venu d'un autre fil est traité **pendant** un `WaitOne(300)`, 0,1 ms après son envoi ;
- un `PostMessage` n'est traité qu'après la fin de l'attente.

---

## A-01 — TrayApplication, god class
**Verdict : NUANCÉ**
- Les faits sont exacts : 3 076 lignes (`git show f0a98ba:src/TrayApplication.cs | wc -l`), `WndProcCallback` en 681-1049, 13 constantes de minuterie (496-559).
- La dispersion des minuteries est **plus grande** que les « quatre endroits » annoncés. SetTimer et KillTimer apparaissent aux lignes 354, 640-656, 692/695, 912-979, 1185, 1852, 1909/1920, 2112-2115 et 2444.
- Plusieurs minuteries sont ponctuelles ou conditionnelles : SINGLECLICK prend `GetDoubleClickTime`, LAYOUT_CHECK dure 100 ms, et PAUSE, STARTUP_BALLOON et REVIEW_QUIET dépendent du contexte. Une table statique (id, période, gestionnaire) ne peut donc servir qu'au dispatch et au nettoyage, pas à l'armement.
- Le menu n'est pas « sans état partagé ». Le bloc 1577-1791 lit `_enabled`, `IsPaused`, `ShouldProcessHook`, `_foregroundMonitor`, `_mapper`, `_virtualKeyboard` et `_characterSearch`.
- Le gain de −150 lignes, « confiance élevée », est gonflé. Découper en 8 classes ajoute des champs, des constructeurs et du câblage. Les tables de dispatch retirent peut-être 40 à 60 lignes.

**Correction :** gravité majeur maintenue (la 1.4.0 est risquée à modifier). Gain ≈ 0 ± 50 lignes, confiance faible sur ce chiffre. Préciser : « table pour le dispatch et le nettoyage ; l'armement reste contextuel ». Retirer « menu sans état partagé ».

## A-02 — Sollicitation d'avis
**Verdict : NUANCÉ (léger)**
- Les faits vérifiés sont exacts :
  - plafond `2` littéral aux lignes TrayApplication:2394 et 2506 et ConfigManager:424 ;
  - `ReviewPromptStillPossible` (2387-2401) recopie 4 gardes ;
  - ReviewSharePrompt:74-76 dépend de `TrayApplication.ReviewPromptErrorCooldownHours` ;
  - `reviewPromptDone` est écrite et jamais lue. C'est voulu : le commentaire de ConfigManager:409 la garde pour le retour arrière en v1.1.
- Défaut 1 de la proposition : le « budget unique » d'une sollicitation cliquable par jour, étendu à l'annonce et à la relance du démarrage, **change le comportement**. Aujourd'hui, seuls l'avis et le rappel se croisent (TrainingReminders:63, TrayApplication:2536). La garde 2536 est d'ailleurs inerte en 1.3.0, puisque le Défi est masqué. Ce changement demande une décision d'Antoine.
- Défaut 2 : supprimer `ShouldConsumeEnrichedSignal` casse ReviewSignalConsumptionTests:29, qui le trouve par réflexion sur son nom. Les tests AZERTYGlobal ne tournent qu'en CI.

**Correction :** signaler l'extension du budget comme un choix de produit, pas comme une simplification. Ajouter ce test au risque. Gravité et gain inchangés (−150 est une borne haute, commentaires compris).

## A-03 — État d'activation implicite
**Verdict : NUANCÉ (léger)**
- Le fond tient : les trois défauts cités sont bien documentés (commentaires 197-203, 603-611, 1300-1303), et les deux branches de 2856-2867 ne diffèrent que par `ResumeOwed`.
- Erreurs mineures :
  - le titre dit « cinq champs » mais le texte en liste six ;
  - `ApplyHookState(` compte 10 appels plus la définition, pas 11 appels.
- « Les fonctions pures gardent leurs tests » est **faux** si elles quittent TrayApplication. Les tests les trouvent par leur nom via `typeof(TrayApplication).GetMethod` :
  - SuspensionTransitionTests:23,36 (`ClassifySuspensionTransition`, `IsSecuritySuspension`) ;
  - SearchWhileSuspendedTests:26,37 (`ShouldDetectShortcutsWhileBlocked`, `ShouldServeSearchWhileBlocked`).
  Un déplacement les casse à l'exécution.

**Correction :** ajouter ce risque. Gain −20 à −40 lignes, confiance moyenne (et non −60, élevée). Y absorber M-12, qui décrit le même état vu du moteur.

## A-04 — Raccourci .lnk par vtable COM
**Verdict : NUANCÉ**
- Les faits sont exacts :
  - offsets et délégués en 32-105, `Enable` en 187-269 ;
  - les deux canaux publiés sont des MSIX (Distribution Entreprises.md:8-19) ;
  - le hors-package relève de la décision D8 (AppChannel.cs:14-16) ;
  - ToastActivation.cs:23-44 utilise bien `[GeneratedComInterface]` ;
  - l'installateur EXE (`components/windows-installer`) installe la disposition, pas l'application.
- Le gain dépend de la branche choisie :
  - **retrait** : ≈ −150 à −190 lignes, `GetShortcutWindowsState` et `IsShortcutDisabledInWindows` (373-411) partant aussi ;
  - **`[GeneratedComInterface]`** : ≈ −40 à −60 lignes seulement, car IShellLinkW et IPersistFile doivent être déclarées dans l'ordre de leur vtable jusqu'aux méthodes utilisées.

**Correction :** gravité « majeur si D8 → retrait, sinon moyen ». Donner les deux gains séparément.

## M-01 — Contexte de premier plan relu jusqu'à 16 fois
**Verdict : NUANCÉ**
- Les comptes sont exacts (banc, section 1 ; 16 lectures sur 6 rappels). Le lien avec `acec0dd` est **exact** : son message dit « GetEmitContext relit l'instantane… EmitText ne faisait rien ».
- En revanche, « bug réel » va trop loin. Le cas b2 n'a jamais été observé ni testé. Le commit l'écrit : « la garde b2 n'est pas couverte… la recette VM reste le seul juge ». L'audit du 20/09 (agent 01 §8.5, :177) le classe parmi les « hypothèses ».
- Dans un même rappel, seul `GetForegroundWindow()` peut varier (ForegroundMonitor.cs:121). L'instantané, lui, est écrit sur le même fil.
- **La proposition réintroduit b2.** Si l'on retire 807-818 et ne garde qu'une vérification avant `SendInput`, `_composition.Process` (820) consomme la touche morte, puis la vérification tardive refuse l'émission : le caractère est perdu. Il faut que cet échec remonte comme « non émis » et que ProcessKeyCore arbitre (`ArbitratePendingDeadKeyWhileSuspended`) puis rende `false`.
- 521-525 est bien redondant : `ClearPassedThroughKeys(emitReleases:false)` ne fait rien (sortie immédiate à la ligne 252).
- Les points d'entrée hors hook gardent chacun leur lecture : `TryEmitText` public (967, plus de 30 appels en tests), 189, 252 et 1103.

**Correction :** remplacer « un bug réel » par « une course identifiée à la lecture (acec0dd b2), jamais reproduite ». Gravité moyen. Gain ≈ −5 à −10 lignes. La proposition doit dire que la vérification tardive reprend le traitement b2.

## M-02 — KeyMapper, god class
**Verdict : CONFIRMÉ**
- Les faits sont exacts :
  - 1 569 lignes ;
  - `ProcessKeyCore` en 519-838, avec 34 `return` recomptés ;
  - 9 commits depuis `1b68caa` (`git log`).
- L'extraction d'un `InputBatchBuilder` ne touche pas `_composition`, seul champ lu par réflexion (ApplicationChangeDeadKeyTests:58, DeadKeyWhileSuspendedTests:75, PauseResumeDeadKeyTests:55).
- Réserve mineure : rapatrier `IsToggleShortcut` dans KeyboardHook oblige à exposer l'état des modificateurs, et touche TrayApplication:376 ainsi que TrayUxRegressionTests.

## M-03 — Trois tables de propriété des touches
**Verdict : NUANCÉ**
- Les faits sont exacts, mais les trois structures n'ont **pas le même cycle de vie** :
  - `_passedThroughKeys` est une quarantaine de relâchements dus, qui survit au relâchement vu pendant une suspension (515-516, 250-252) ;
  - `_keyDownOwnership` perd son entrée à ce même relâchement (514).
- Avec une seule table `KeyOwner`, soit on perd le relâchement dû (touche collée côté GLFW/SDL), soit l'appui suivant passe pour une répétition (498-504). Il faut un état de plus ou un ensemble « relâchement dû » séparé.
- L'hétérogénéité des clés est sans effet : une touche étendue ressort en 636-637, avant 832 et avant la branche Ctrl (658).
- Aucun bug n'est imputé à ce motif.

**Correction :** gravité moyen. Gain −10 à −20 lignes, confiance faible. Proposition : `KeyOwner` pour la propriété de l'appui et une table distincte pour les relâchements dus.

## M-06 — Recompute sur le fil du hook
**Verdict : NUANCÉ (fort)**
- **La partie « modules » tient :**
  - `ResolveState` → `TryEnumProcessModules` (ForegroundMonitor:309, RealWin32Api:133-172) ;
  - le banc mesure 5,0 ms pour 136 modules ;
  - l'énumération ne pompe aucun message, donc les frappes attendent.
- **La partie UIA est réfutée.**
  - Le fil principal est `[STAThread]` (Program.cs:36), et l'appelant attend par `_done.WaitOne` (SecureInputProbe:50).
  - Sur un fil STA, cette attente pompe les messages envoyés. C'est confirmé en JIT et en AOT (voir l'en-tête).
  - Le rappel LL est donc servi pendant l'attente, de façon **réentrante**, avec l'instantané précédent.
- Conséquences pour le constat :
  - « jusqu'à 35 ms » devient « ≈ 5 ms » ;
  - le gain « l'attente de 30 ms quitte le fil du hook » tombe ;
  - la proposition (4), fail-closed immédiat, *améliorerait* ce qui se passe pendant la fenêtre de réentrance. Mais elle doublerait les Recompute par focus si elle n'est pas couplée au cache (2).
- Erreurs de citation :
  - `RealWin32Api.cs:158-231` n'existe pas, le fichier fait 178 lignes ;
  - `OpenProcess` est appelé 3 fois (lignes 60, 84, 137), pas 2.

**Correction :** titre « ≈ 5 ms par focus (énumération des modules) », gravité moyen, la fréquence des focus n'étant pas mesurée. Ajouter la réentrance comme fait nouveau.

## M-12 — Quatre booléens d'activité
**Verdict : NUANCÉ (fort, proche de réfuté)**
- Les quatre valeurs ne s'écrivent **qu'à un seul endroit**, TrayApplication.cs:597-616. Un grep ne trouve aucun autre écrivain. « Au minimum, regrouper en une méthode » est donc déjà fait : c'est `ApplyHookState`.
- Aucune course possible : les setters et le rappel tournent sur le même fil.
- L'Écart 8 était un choix de politique (UserOverride traité comme l'inertie). Un enum aurait demandé le même choix.
- La lecture de `DisabledAntiCheat` par le moteur (KeyMapper:1085-1086, via le contrôle en direct de ForegroundMonitor:121) est la ligne fail-closed entre une bascule de premier plan et l'`ApplyHookState` de l'hôte. Elle **ne doit pas** être repliée dans l'enum de l'hôte.
- Le constat oublie `RemoteHostPresent` (TrayApplication:1274).

**Correction :** fusionner dans A-03, gravité mineur, et ajouter ce garde-fou au risque.

## A-05 — config.json réécrit avec fsync à chaque réglage
**Verdict : CONFIRMÉ**
- Les faits sont exacts :
  - les setters appellent `Save()` sous verrou (923-980), avec `Flush(true)` en 1095 et `File.Replace` en 1099 ;
  - le nombre de sauvegardes par geste est juste : 4 (2052-2055), 3 (424-429), 3 (473-476), 6-7 (MaintainableLayersWindow:234-241) et jusqu'à 4 pour Paramètres (`AutoStart.Set` → `SetAutoStart`, AutoStart.cs:136, plus `MarkPromptShown` et 2 setters) ;
  - `WM_SIZE` → `SaveWindowBounds` en VirtualKeyboard:842-845.
- Une écriture de fichier ne pompe pas : le rappel LL attend donc pendant la sauvegarde. La prémisse tient.
- La durée tient aussi : 3,3 ms par sauvegarde (réplique du format, SSD de ce PC), soit ≈ 23 ms pour 7 écritures. C'est imperceptible ici ; le risque réel concerne un disque lent ou un EDR.
- Seule réserve : `WM_EXITSIZEMOVE` ne couvre ni Aero Snap ni l'agrandissement. Il faut aussi écrire à la fermeture, ce que la proposition prévoit, ou sur `SIZE_MAXIMIZED`/restauration.

## F-18 — Fermer une fenêtre réécrit config.json
**Verdict : NUANCÉ**
- Doublon d'A-05 : même motif, et la grille demande de regrouper.
- Le compte pour Paramètres est **faux**. `Close()` appelle toujours `AutoStart.Set` (SettingsWindow.cs:1231), qui finit par `ConfigManager.SetAutoStart` (AutoStart.cs:136). Il y a donc au moins 3 écritures par fermeture, pas 2, et « de 2 à 0 sans changement » n'est vrai que si `AutoStart.Set` devient lui aussi conditionnel.

**Correction :** fusionner dans A-05 et corriger le compte.

## A-11 — usage-stats.json perd les clés inconnues
**Verdict : NUANCÉ (sur la proposition)**
- Les faits sont exacts :
  - lecture (563-576) et écriture (636-649) de 14 clés nommées seulement ;
  - trois copies de l'écriture atomique : ConfigManager 1095/1099, UsageStats 652/656, LessonProgressStore 289.
- Défaut de la proposition : aujourd'hui, la lecture est tolérante champ par champ (`GetIntProp` rend 0 sur un mauvais type, 591-595). Un désérialiseur source-généré lève `JsonException` au moindre type inattendu. Le catch (579) remet alors tout à zéro, et le flush suivant écrase le fichier entier.
- Constat plus grave que le retour arrière, **plausible**, non reproduit :
  - une `IOException` à la lecture remet les compteurs à zéro (583) ;
  - `SaveLocked` n'a pas la garde `_loadFailed` de ConfigManager:1055 ;
  - la première frappe arme `_dirty`, et le flush suivant écrase les vraies statistiques.

**Correction :** garder la lecture champ par champ et conserver les clés inconnues par clonage de la racine (comme ConfigManager:995-1012). Ajouter la garde « ne jamais écraser un fichier non chargé ». Gravité moyen maintenue.

---

## Récapitulatif

| Id | Verdict | Gravité proposée | Gain corrigé | Point décisif |
|---|---|---|---|---|
| A-01 | NUANCÉ | majeur (inchangé) | ≈ 0 ± 50 l., confiance faible | minuteries dispersées en ≥ 9 sites ; le menu lit l'état d'activation |
| A-02 | NUANCÉ léger | majeur (inchangé) | −150 (borne haute) | « budget unique » = changement de produit ; test par réflexion |
| A-03 | NUANCÉ léger | majeur (inchangé) | −20 à −40, confiance moyenne | tests par réflexion sur `typeof(TrayApplication)` ; absorber M-12 |
| A-04 | NUANCÉ | majeur si D8 → retrait, sinon moyen | −150/−190 (retrait) ou −40/−60 (COM généré) | gain selon la branche D8 |
| M-01 | NUANCÉ | moyen (était majeur) | ≈ −5 à −10 | b2 jamais observé ; la proposition le réintroduit |
| M-02 | CONFIRMÉ | majeur | 0 | — |
| M-03 | NUANCÉ | moyen (était majeur) | −10 à −20, confiance faible | quarantaine ≠ propriété de l'appui |
| M-06 | NUANCÉ fort | moyen (était majeur) | ≈ 5 ms par focus ; pas 35 ms | l'attente UIA pompe : hook servi, en réentrance (testé JIT + AOT) |
| M-12 | NUANCÉ fort | mineur, fusion dans A-03 | ≈ 0 | déjà une seule méthode ; garde fail-closed moteur à garder |
| A-05 | CONFIRMÉ | moyen | inchangé | I/O sans pompe, 3,3 ms par écriture sur ce PC |
| F-18 | NUANCÉ | fusion dans A-05 | — | Paramètres : ≥ 3 écritures, pas 2 |
| A-11 | NUANCÉ (proposition) | moyen | inchangé | un désérialiseur strict remet tout à zéro ; garde `_loadFailed` absente |

## Non vérifié
- Le rappel `WH_KEYBOARD_LL` lui-même n'a pas été observé pendant l'attente. Poser un hook était interdit ; `SendMessage` sert de substitut, sur la foi de la doc Microsoft.
- Un `WinEvent` hors contexte peut-il être livré pendant cette même attente ? Dans ce cas, un Recompute imbriqué pourrait écrire un instantané plus ancien. Non testé, car il faudrait `SetWinEventHook`.
- Fréquence réelle des `EVENT_OBJECT_FOCUS`, durée d'une sauvegarde sur disque dur ou sous EDR, suite `AZERTYGlobal.Tests` (Application Control).
- Ce contre-avis partage les biais du modèle de l'audit. La réentrance du fil du hook (M-06) et la garde b2 (M-01) méritent la passe par l'autre fournisseur avant toute décision d'architecture.