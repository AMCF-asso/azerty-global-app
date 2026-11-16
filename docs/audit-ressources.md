# Audit des ressources longue durée — app AZERTY Global 1.3.0 (16/11/2026)

- **Inventaire** : 387 créations de minuteries, d'abonnements, d'objets Windows et de mémoire native dans `src/`, chacune avec sa libération ou « jamais » ([inventaire généré](audit-ressources-inventaire.md)) ; 0 site sans verdict (`--check`).
- **2 constats confirmés, même cause, impact faible** : redimensionner les Leçons (C1) ou le clavier virtuel (C2) remplit le cache de polices partagé `AppFonts.Get`, qui ne détruit rien. Mesures : +48 et +47 objets GDI pour 20 tailles traversées, palier ensuite ; deux tests en échec, nommés dans les constats.
- **0 fuite** sur 11 fenêtres ouvertes, peintes et fermées, puis laissées ouvertes sous changements d'écran, de couleurs et de langue, puis rouvertes : GDI 0 ; USER sans croissance d'un cycle à l'autre (un décalage ponctuel de +9 au plus, borné : Accueil +8 sur 100 cycles, +9 sur 300).
- **Réfuté après mesure** : la fuite USER de GDI+ démarré puis arrêté à chaque icône du tray (+52 sur 100 cycles) était un artefact du banc, qui ne pompait pas les messages ; en pompant comme le tray : +2.
- **Décision attendue** : piste de correctif du ticket T1 (C1 et C2) et ajout du contrôle d'inventaire à `verify.ps1` (QCM à Antoine) ; aucun correctif du produit dans cette branche.

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
   - On relève les compteurs après l'échauffement, à mi-parcours et après 20 cycles (`AZERTY_CYCLES_RESSOURCES` allonge). Tolérance : 5 objets. GDI, abonnés et dialogues se comparent sur tous les cycles ; USER sur la seconde moitié seulement, car Windows crée à la demande quelques objets USER une fois par processus (Accueil : +0 sur 20 cycles lancé seul, +7 sur 20 dans verify, +8 sur 100, +9 sur 300). Une fuite d'un objet par cycle fait +10 sur la seconde moitié.

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

Les 22 « jamais » durent tous une fois par processus, ou par objet possédé par l'instance unique du tray, sauf une exception : `AppFonts.cs:93`, cause des constats C1 et C2.

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

### C2 — confirmé, impact faible : le cache de polices grandit avec chaque taille du clavier virtuel

- **Test en échec** : `RessourcesLongueDureeTests.Redimensionner_le_clavier_virtuel_ne_garde_pas_des_polices_pour_chaque_taille_traversee`.
- **Mesure du 16/11, 20 tailles de 0,85 à 1,25 fois la taille d'ouverture** (739 × 345 à 1087 × 508) :
  - cache `AppFonts` : 7 → 54 polices ;
  - GDI : 29 → 76, soit +47, exactement l'écart du cache ;
  - seconde passe sur les mêmes tailles : +0.
- **Cause** : le mécanisme de C1, sans plancher.
  - `PaintScale` (`VirtualKeyboard.cs:288`) suit la taille client en continu (minimum 0,1).
  - Les touches (`PaperKeyboard.cs:286-599`), le bandeau (`VirtualKeyboard.cs:1096-1115`) et les légendes demandent `AppFonts.Get` à la hauteur `taille × échelle`.
  - La fenêtre se redimensionne à la souris (`WS_THICKFRAME`) : chaque taille peinte pendant le glissé ajoute ses hauteurs.
- **Borne (estimation)** : environ 1,2 police par point d'échelle (47 pour 40 points) ; de 0,5 à 2 fois la taille d'origine, de l'ordre de 200 polices, sous le quota de 10 000. Croissance bornée, jamais rendue.
- **Ordre des tests** : le cache est commun au processus. Lancé juste après celui du clavier, le test des Leçons ne voit plus que +28, les hauteurs communes existant déjà. Les chiffres de C1 et C2 sont ceux de chaque test lancé sans l'autre avant lui.
- **Recherche des autres appelants** : toutes les polices `FontSpec` de `src/` passent par `AppFonts.Get` (`AppFonts.cs:13`). Seuls les Leçons et le clavier virtuel ont une échelle qui suit la taille de la fenêtre ; les autres fenêtres suivent le DPI (`_dpiScale`, `dpi / 96f`), d'où un jeu par écran, borné.

### Plausibles (sans test en échec)

- **P1** — `AppFonts.cs:70` : les polices embarquées sont chargées une fois par processus (`AddFontMemResourceEx`). La mémoire copiée reste allouée jusqu'à la sortie. C'est borné et non répété, donc pas un ticket.
- **P2** — `TrayApplication` n'a pas de constructeur testable sans vrai crochet clavier : ses 13 minuteries ne sont classées que par lecture. Toutes sont sur la fenêtre du tray (`SetTimer(_hWnd, id)`), donc remplacées à même id et détruites avec elle (`TrayApplication.cs:2346`). L'arrêt n'en tue que 4 explicitement (2357-2360), sans effet puisque la fenêtre les emporte.

### Réfutés

Par mesure (tests verts, `RessourcesLongueDureeTests`) :
- **Fenêtres ouvertes, peintes et fermées (11 fenêtres)** : GDI 0, USER sans croissance sur la seconde moitié, abonnés statiques et dialogues inscrits stables, sur 20 et 100 cycles. Le +7 USER de l'Accueil vu une fois dans verify est le décalage ponctuel décrit dans la méthode : +9 sur 300 cycles.
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
| T1 | faible | C1 et C2 : polices du cache `AppFonts` gardées pour chaque taille des Leçons et du clavier virtuel |
| T2 | prévention | `audit-ressources-inventaire.py --check` dans `verify.ps1`, pour qu'une création sans libération connue échoue en CI |

- **T1, pistes de correctif** (choix au QCM, aucun appliqué) :
  1. Chaque fenêtre à échelle continue (Leçons, clavier virtuel) possède ses polices à l'échelle : un jeu par fenêtre, détruit au changement d'échelle et à la fermeture, comme les 14 `_hFont*` des Leçons. Les dessinateurs partagés (`PaperKeyboard`, `MiniKeyboardRenderer`, `KeyNotation`, `TablatureRenderer`) reçoivent ce jeu au lieu d'appeler `AppFonts.Get`, qui ne sert plus qu'aux tailles fixes par DPI. Corrige C1 et C2 ; les deux tests passent au vert.
  2. Arrondir les échelles à des paliers (par exemple 5 %) : moins de hauteurs, mais la croissance reste ; les tests ne passent au vert que si la plage tombe sous la tolérance.
  3. Un cache à plafond qui détruit les polices non demandées depuis le dernier changement d'échelle. Risque : une HFONT encore sélectionnée dans un DC ou donnée à un contrôle par `WM_SETFONT` serait détruite sous lui.
- **T2** : environ 2 s de plus par exécution. Chaque nouvelle création de ressource demanderait alors une règle ou une ligne de verdict.

## Vérification

- Banc `RessourcesLongueDuree` : 39 tests, 37 réussis, 2 en échec voulus (C1, C2).
- `scripts/verify.ps1 -Etape "tests-core,tests-windows,tests-app"` (16/11, Windows PowerShell 5.1) :

| Étape | Référence 396e449 | Cette branche |
|---|---|---|
| tests-core | 18/18 | 18/18 |
| tests-windows | 404/404 | 404/404 |
| tests-app | 1993 réussis, 3 ignorés, 0 échec, sur 1996 | 2030 réussis, 3 ignorés, 2 échecs (C1, C2), sur 2035 |

  Les 39 tests ajoutés expliquent l'écart (1996 + 39 = 2035). Les seuls échecs sont les tests des constats C1 et C2 ; les 3 ignorés sont les mêmes (`CurseurSimuleTests`). Mesure du 16/11, 19 h 37.
- `python scripts/audit-ressources-inventaire.py --check` : 387 sites, 0 sans verdict, 0 verdict périmé.
