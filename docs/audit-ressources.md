# Audit des ressources longue durée — app AZERTY Global 1.3.0 (16/11/2026)

- **Inventaire** : 387 créations de minuteries, d'abonnements, d'objets Windows et de mémoire native dans `src/`, chacune avec sa libération ou « jamais » ([inventaire généré](audit-ressources-inventaire.md)) ; 0 site sans verdict (`--check`).
- **1 constat confirmé, impact faible** : redimensionner les Leçons remplit le cache de polices partagé `AppFonts.Get`, qui ne détruit rien. Mesure : +48 objets GDI pour 13 échelles traversées, palier ensuite ; test en échec `Redimensionner_les_Lecons_ne_garde_pas_des_polices_pour_chaque_taille_traversee`.
- **0 fuite** sur 11 fenêtres ouvertes, peintes et fermées, puis laissées ouvertes sous changements d'écran, de couleurs et de langue, puis rouvertes : GDI 0, USER dans le bruit (±4) sur 20 et 100 cycles.
- **Réfuté après mesure** : la fuite USER de GDI+ démarré puis arrêté à chaque icône du tray (+52 sur 100 cycles) était un artefact du banc, qui ne pompait pas les messages ; en pompant comme le tray : +2.
- **Décision attendue** : correctif du ticket T1 et ajout du contrôle d'inventaire à `verify.ps1` (QCM à Antoine) ; aucun correctif du produit dans cette branche.

Base : `396e449`, branche `claude/ag-audit-ressources-130`. Aucun fichier de `src/` hors tests n'est modifié.

## Méthode

1. **Inventaire mécanique** : `python scripts/audit-ressources-inventaire.py --check --md docs/audit-ressources-inventaire.md`.
   - Le script recense, hors projets de tests, chaque appel qui crée une ressource. Liste des motifs : en-tête du script.
   - La liste vient des déclarations P/Invoke de `src/` filtrées par préfixe (`Create*`, `Open*`, `Load*`, `Register*`, `Alloc*`, `Begin*`, `Set*Hook`…).
   - Elle a été recoupée par grep sur les créateurs classiques absents : `OpenThemeData`, régions, `LoadImage`, `ImageList_*`, `RegisterHotKey`, `CreateDIBSection`, `SystemEvents`, `Task.Run`, `FileSystemWatcher`… Aucun de ceux-là n'existe dans `src/`.
   - Les seuls ajoutés après ce recoupement sont `Marshal.AllocHGlobal`, `StringToHGlobalUni`, `GCHandle.Alloc` et `LoadCursorW`.
2. **Libération cherchée par règle**, dans l'ordre :
   1. la même variable libérée plus bas dans la méthode ;
   2. un champ (`_x`) libéré n'importe où dans le fichier ;
   3. une fenêtre enfant (`WS_CHILD`, parent `_hWnd`) détruite avec sa fenêtre parente ;
   4. une minuterie de fenêtre (`SetTimer(_hWnd, id)`), qui meurt avec la fenêtre (même id remplacé, `KillTimer` cités) ;
   5. `-=` du même gestionnaire, `RemoveWindowSubclass`, `Unregister`, `Unhook*`, `NIM_DELETE`, `CoRevokeClassObject`, `GdiplusShutdown` du même jeton.
3. **48 sites lus à la main** : `docs/audit-ressources-verdicts.tsv`.
   - On y trouve les fabriques suivies jusqu'à chaque appelant, les objets COM, les travaux du pool de fils et les abonnements de l'instance unique du tray.
   - Le script échoue si un site n'a ni règle ni verdict, ou si un verdict ne correspond plus à une ligne.
4. **Banc de cycles** : `src/AZERTYGlobal.Tests/RessourcesLongueDureeTests.cs`.
   - Compteurs relevés après un échauffement : `GetGuiResources` (GDI, USER), abonnés des événements statiques, fenêtres inscrites à `DialogNavigation`.
   - On compare deux paliers séparés de 20 cycles (`AZERTY_CYCLES_RESSOURCES` allonge). La tolérance est de 5 objets : le bruit USER mesuré est de ±4 et ne dépend pas du nombre de cycles.

## Inventaire compté

| Catégorie | Créations | Détail |
|---|---|---|
| windows | 191 | CreateWindowExW 71, GetDC 29, SaveDC 12, SetWindowSubclass 11, BeginPaint 8, CreatePopupMenu 8, LoadCursorW 7, GdiplusStartup 7, … |
| gdi | 110 | CreateSolidBrush 27, CreateFontW 22, GdipCreate 20, CreatePen 16, CreateCompatibleDC 8, … |
| abonnement | 39 | event += 31, DialogNavigation.Register 7, AppDomain 1 |
| minuterie | 32 | SetTimer 25, ThreadPool 5, Threading.Timer 1, Thread 1 |
| mémoire | 15 | Marshal.AllocHGlobal 9, StringToHGlobalUni 3, GCHandle.Alloc 3 |

Verdicts sur les 387 sites :
- libéré : 223 ;
- fenêtre enfant détruite avec sa fenêtre parente : 57 ;
- minuterie de fenêtre : 24 ;
- désabonné : 16 ;
- sous-classe retirée : 11 ;
- GDI+ arrêté : 7 ;
- `using` : 5 ;
- curseur système partagé : 7 ;
- aucun objet gardé (pool de fils) : 5 ;
- fabriques suivies chez l'appelant : 6 ;
- **jamais** : 22 ;
- divers : 4.

Les 22 « jamais » durent tous une fois par processus, ou par objet possédé par l'instance unique du tray, sauf une exception : `AppFonts.cs:93`, le constat C1.

## Constats

### C1 — confirmé, impact faible : le cache de polices grandit avec chaque échelle des Leçons

- **Test en échec** : `RessourcesLongueDureeTests.Redimensionner_les_Lecons_ne_garde_pas_des_polices_pour_chaque_taille_traversee`.
- **Mesure du 16/11, 20 tailles de 0,85 à 1,25 fois la taille d'ouverture** :
  - 13 échelles distinctes, de 1 à 1,25 ;
  - cache `AppFonts` : 17 → 65 polices ;
  - GDI : 53 → 101, soit +48, exactement l'écart du cache ;
  - seconde passe sur les mêmes tailles : +0 ;
  - avec 100 tailles dans la même plage : +47 aussi.
- **Cause** :
  - Les Leçons mettent leur contenu à l'échelle de la fenêtre (`_windowScale`, `LessonsWindow.cs:690-705`, jamais sous 1).
  - Leurs polices décrites par `FontSpec` (`LessonsWindow.Paper.cs:19`, `LessonsWindow.Home.cs:55`, `MiniKeyboardRenderer.cs:283`, `KeyNotation`) prennent la hauteur `taille × échelle`.
  - `AppFonts.Get` (`AppFonts.cs:86-96`) crée une HFONT par triplet (famille, hauteur, graisse) et la garde jusqu'à la fin du processus. C'est voulu : « Ne jamais la détruire : elle est partagée ».
  - Chaque hauteur nouvelle laisse donc une police.
  - Les 14 polices propres aux Leçons (`_hFont*`, `LessonsWindow.cs:431-444`) sont, elles, détruites et recréées à chaque changement d'échelle (`RecreateScaledFonts`).
- **Borne (estimation)** :
  - Le palier est atteint quand toutes les hauteurs entières d'une plage d'échelles ont été vues.
  - 48 polices pour 0,25 d'échelle donnent environ 170 polices de 1 à 1,9, l'échelle d'une fenêtre agrandie sur l'écran 3440 × 1440. Le compte double si un second DPI s'y ajoute.
  - Cela fait moins de 400 objets sur un quota de 10 000 par processus. Pas de panne en vue, mais une mémoire GDI qui ne redescend jamais.
- **Autres sources du même cache, plus petites (plausible, sans test)** :
  - `CharacterSearch.cs:1550` réduit la police du glyphe pour qu'il tienne (`Math.Max(8, size × available / width)`), soit au plus 21 hauteurs par DPI ;
  - les fenêtres à DPI fixe ajoutent un jeu par écran traversé.

### Plausibles (sans test en échec)

- **P1** — `AppFonts.cs:70` : les polices embarquées sont chargées une fois par processus (`AddFontMemResourceEx`). La mémoire copiée reste allouée jusqu'à la sortie. C'est borné et non répété, donc pas un ticket.
- **P2** — `TrayApplication` n'a pas de constructeur testable sans vrai crochet clavier : ses 13 minuteries ne sont classées que par lecture. Toutes sont sur la fenêtre du tray (`SetTimer(_hWnd, id)`), donc remplacées à même id et détruites avec elle (`TrayApplication.cs:2346`). L'arrêt n'en tue que 4 explicitement (2357-2360), sans effet puisque la fenêtre les emporte.

### Réfutés

Par mesure (tests verts, `RessourcesLongueDureeTests`) :
- **Fenêtres ouvertes, peintes et fermées (11 fenêtres)** : GDI 0, USER dans le bruit, abonnés statiques et dialogues inscrits stables, sur 20 et 100 cycles.
- **Fenêtres laissées ouvertes** sous `WM_DPICHANGED` 96/144, `WM_SYSCOLORCHANGE`, `WM_SETTINGCHANGE` et une langue qui alterne fr/en (11 fenêtres) : idem.
- **Même instance fermée puis rouverte** (9 fenêtres que le tray garde en un exemplaire) : idem.
- **Leçons recréées sur le KeyMapper et le KeyboardHook partagés**, comme le fait `EnsureLessonsWindow` : 0 abonné laissé sur le moteur après 23 fenêtres. Le premier banc donnait un moteur neuf à chaque cycle et ne pouvait pas le voir.
- **ForegroundMonitor créé puis disposé 100 fois** : 300 crochets WinEvent posés, 300 retirés.
- **Icône du tray refaite 100 fois** (`TrayIcon.Create`, `CreateTextIcon`) : GDI 0, USER +4 et 0.
- **GDI+ démarré puis arrêté**, à chaque icône et à chaque fenêtre :
  - sans pomper les messages, USER +52 sur 100 cycles, encore là après 2 s d'attente ;
  - en pompant entre deux cycles comme la boucle du tray, +2 ;
  - avec un GDI+ gardé ouvert ailleurs, 0.
  - GDI+ libère ses objets USER par la file de messages du fil qui l'arrête. Tous les appels de `src/` passent par un fil qui a une boucle de messages.

Par lecture (le site et la libération sont dans l'inventaire) :
- recrochetage du clavier (`KeyboardHook.cs:171-186`, l'ancien crochet est décroché) ;
- icône et bulle du tray (ancienne icône détruite, `TrayApplication.cs:2553`, `2952`) ;
- Mutex (`using var` jusqu'à la fin de Main) ;
- objets COM : `AccessibleName`, `AutoStart` et `ToastActivation` relâchés en `finally` ou à l'arrêt ; UI Automation rendu à l'appelant selon la convention COM ;
- `OpenProcess` (`CloseHandle` en `finally`) ;
- pinceaux de classe : l'ancien est détruit, le dernier part avec `UnregisterClass` (12 classes) ;
- fils de la sonde de saisie sécurisée : au plus 1 vivant et 3 abandonnés ;
- travaux du pool : une seule demande en vol par garde `Interlocked` ;
- `DevRects.cs:158` : build Debug seulement.

## Tickets par impact

| Ticket | Impact | Objet |
|---|---|---|
| T1 | faible | C1 : polices du cache `AppFonts` gardées pour chaque échelle des Leçons |
| T2 | prévention | `audit-ressources-inventaire.py --check` dans `verify.ps1`, pour qu'une création sans libération connue échoue en CI |

- **T1, pistes de correctif** (choix au QCM, aucun appliqué) :
  1. Les Leçons possèdent leurs polices à l'échelle de la fenêtre, comme leurs 14 `_hFont*`, et `AppFonts.Get` ne sert qu'aux tailles fixes par DPI.
  2. Arrondir `_windowScale` à des paliers, ce qui réduit le nombre de hauteurs sans supprimer la croissance.
  3. Un cache à plafond qui détruit les polices non utilisées depuis le dernier changement d'échelle.
  - Le test C1 passe au vert avec la piste 1 ; avec la piste 2 seulement si la plage traversée tombe sous la tolérance.
- **T2** : environ 2 s de plus par exécution. Chaque nouvelle création de ressource demanderait alors une règle ou une ligne de verdict.

## Vérification

- Banc `RessourcesLongueDuree` : 38 tests, 37 réussis, 1 en échec (C1, voulu).
- `scripts/verify.ps1 -Etape "tests-core,tests-windows,tests-app"` (16/11, Windows PowerShell 5.1) :

| Étape | Référence 396e449 | Cette branche |
|---|---|---|
| tests-core | 18/18 | 18/18 |
| tests-windows | 404/404 | 404/404 |
| tests-app | 1993 réussis, 3 ignorés, 0 échec, sur 1996 | 2030 réussis, 3 ignorés, 1 échec (C1), sur 2034 |

  Les 38 tests ajoutés expliquent l'écart (1996 + 38 = 2034). Le seul échec est le test du constat C1 ; les 3 ignorés sont les mêmes (`CurseurSimuleTests`).
- `python scripts/audit-ressources-inventaire.py --check` : 387 sites, 0 sans verdict, 0 verdict périmé.
