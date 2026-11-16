# Audit des ressources longue durée — app AZERTY Global 1.3.0 (16/11/2026)

- **Inventaire** : 381 créations de minuteries, d'abonnements, d'objets Windows et de mémoire native dans `src/`, chacune avec sa libération ou « jamais » ([inventaire généré](audit-ressources-inventaire.md)) ; 0 site sans verdict (`--check`), contrôle bloquant dans `verify.ps1` et la CI (T2).
- **3 constats confirmés et corrigés.** C1 et C2, même cause, impact faible : redimensionner les Leçons (C1) ou le clavier virtuel (C2) remplissait le cache de polices partagé `AppFonts.Get`, qui ne détruit rien : +48 et +47 objets GDI pour 20 tailles traversées. Correctif (T1) : chaque fenêtre possède ses polices à l'échelle (`WindowFonts`) et les détruit au changement d'échelle et à la fermeture. Après : GDI −1 et +0, cache partagé immobile ; les deux tests des constats passent au vert.
- **0 fuite** sur 11 fenêtres ouvertes, peintes et fermées, puis laissées ouvertes sous changements d'écran, de couleurs et de langue, puis rouvertes : GDI 0 ; USER sans croissance d'un cycle à l'autre (un décalage ponctuel de +9 au plus, borné : Accueil +8 sur 100 cycles, +9 sur 300).
- **C3, confirmé en CI** : démarrer puis arrêter GDI+ à chaque icône du tray gardait un objet USER par icône sur le runner Windows de la CI (+10 sur 10 cycles), presque rien sur le poste (+2 sur 100). Correctif : GDI+ démarré une fois pour tout le processus (`GdiPlus`), arrêté à la sortie ; 0 démarrage de plus en dessinant.
- **Décisions d'Antoine (QCM du 16/11)** : T1 par « polices par fenêtre », T2 « dans verify ». Les deux sont faits ; C3 par « GDI+ ouvert une fois » (QCM du 16/11 au soir).

Base : `396e449`, branche `claude/ag-audit-ressources-130`. L'audit (`741c34b`, `6732f86`) ne touche que les tests et les scripts ; T2 est `dbd198e` ; seul le correctif T1 modifie `src/`.

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
   - Chaque verdict est ancré sur le fichier, l'API, le rang de l'appel identique et le texte de l'appel, pas sur un numéro de ligne : une ligne ajoutée plus haut ne le périme pas.
   - Le script échoue si un site n'a ni règle ni verdict, ou si un verdict ne correspond plus à aucun appel (appel modifié ou retiré : à relire).
4. **Banc de cycles** : `src/AZERTYGlobal.Tests/RessourcesLongueDureeTests.cs`.
   - Compteurs relevés après un échauffement : `GetGuiResources` (GDI, USER), abonnés des événements statiques, fenêtres inscrites à `DialogNavigation`.
   - On relève les compteurs après l'échauffement, à mi-parcours et après 20 cycles (`AZERTY_CYCLES_RESSOURCES` allonge). Tolérance : 5 objets. GDI, abonnés et dialogues se comparent sur tous les cycles ; USER sur la seconde moitié seulement, car Windows crée à la demande quelques objets USER une fois par processus (Accueil : +0 sur 20 cycles lancé seul, +7 sur 20 dans verify, +8 sur 100, +9 sur 300). Une fuite d'un objet par cycle fait +10 sur la seconde moitié.
   - Un dépassement USER sur la seconde moitié se confirme par une prolongation de même longueur : une fuite recroît (+10), un décalage ponctuel ne se répète pas. Cas vu dans verify le 16/11 : Couches maintenues, USER 358 → 356 à mi-parcours → 362 (+6), alors que seul il donne +0 sur 20 cycles et 24 → 33 → 35 sur 2 × 150 (palier).
   - Contrôle négatif : `Le_banc_voit_une_fuite_d_un_objet_par_cycle` garde un menu (USER) ou un pinceau (GDI) par cycle ; le banc doit échouer (mesuré : USER +10 puis +10 à la prolongation, GDI +19).

## Inventaire compté

| Catégorie | Créations | Détail |
|---|---|---|
| windows | 191 | CreateWindowExW 71, GetDC 29, SaveDC 12, SetWindowSubclass 11, BeginPaint 8, CreatePopupMenu 8, LoadCursorW 7, GdiplusStartup 7, … |
| gdi | 110 | CreateSolidBrush 27, CreateFontW 22, GdipCreate 20, CreatePen 16, CreateCompatibleDC 8, … |
| abonnement | 39 | event += 31, DialogNavigation.Register 7, AppDomain 1 |
| minuterie | 32 | SetTimer 25, ThreadPool 5, Threading.Timer 1, Thread 1 |
| mémoire | 15 | Marshal.AllocHGlobal 9, StringToHGlobalUni 3, GCHandle.Alloc 3 |

Verdicts sur les 381 sites :
- libéré : 223 ;
- fenêtre enfant détruite avec sa fenêtre parente : 57 ;
- minuterie de fenêtre : 24 ;
- désabonné : 16 ;
- sous-classe retirée : 11 ;
- GDI+ arrêté (à la sortie, `GdiPlus.Shutdown`) : 1 ;
- `using` : 5 ;
- curseur système partagé : 7 ;
- aucun objet gardé (pool de fils) : 5 ;
- fabriques suivies chez l'appelant : 7 ;
- **jamais** : 21 ;
- divers : 4.

Les 21 « jamais » durent tous une fois par processus, ou par objet possédé par l'instance unique du tray. Avant le correctif, il y en avait 22 : le `CreateFontW` du cache partagé, cause de C1 et C2, en était l'exception. Il est devenu la fabrique `AppFonts.Create` (`AppFonts.cs:180`) : `AppFonts.Get` garde ses polices (tailles qui ne suivent que le DPI), `WindowFonts` détruit les siennes.

## Constats

### C1 — confirmé puis corrigé : le cache de polices grandissait avec chaque échelle des Leçons

- **Test** : `RessourcesLongueDureeTests.Redimensionner_les_Lecons_ne_garde_pas_des_polices_pour_chaque_taille_traversee`, en échec avant le correctif, vert après.
- **Mesure avant, 16/11, 20 tailles de 0,85 à 1,25 fois la taille d'ouverture** :
  - 13 échelles distinctes, de 1 à 1,25 ;
  - cache `AppFonts` : 17 → 65 polices ;
  - GDI : 53 → 101, soit +48, exactement l'écart du cache ;
  - seconde passe sur les mêmes tailles : +0 ;
  - avec 100 tailles dans la même plage : +47 aussi.
- **Cause** :
  - Les Leçons mettent leur contenu à l'échelle de la fenêtre (`_windowScale`, jamais sous 1).
  - Leurs polices décrites par `FontSpec` (papier, accueil, `MiniKeyboardRenderer`, `KeyNotation`, `TablatureRenderer`, `PaperKeyboard`) prenaient la hauteur `taille × échelle`.
  - `AppFonts.Get` crée une HFONT par triplet (famille, hauteur, graisse) et la garde jusqu'à la fin du processus. C'est voulu : « Ne jamais la détruire : elle est partagée ».
  - Chaque hauteur nouvelle laissait donc une police.
  - Les 14 polices propres aux Leçons (`_hFont*`) étaient, elles, déjà détruites et recréées à chaque changement d'échelle (`RecreateScaledFonts`).
- **Borne avant correctif (estimation)** : environ 170 polices de l'échelle 1 à 1,9 (fenêtre agrandie sur l'écran 3440 × 1440), le double avec un second DPI ; moins de 400 objets sur un quota de 10 000 par processus. Pas de panne en vue, mais une mémoire GDI qui ne redescendait jamais.
- **Mesure après, même protocole** : GDI 53 → 52 → 52 (seconde passe) ; cache partagé 0 → 0 ; polices de la fenêtre 17 à la taille d'origine, 18 au plus pendant le balayage, 0 après la fermeture ; couverture des glyphes immobile (sans sa correction : 1155 → 2171 entrées).
- **Autres sources du même cache, plus petites (plausible, sans test)** : `CharacterSearch` réduit la police du glyphe pour qu'il tienne (`Math.Max(8, size × available / width)`), soit au plus 21 hauteurs par DPI ; les fenêtres à DPI fixe ajoutent un jeu par écran traversé. Bornés, laissés au cache partagé.

### C2 — confirmé puis corrigé : le cache de polices grandissait avec chaque taille du clavier virtuel

- **Test** : `RessourcesLongueDureeTests.Redimensionner_le_clavier_virtuel_ne_garde_pas_des_polices_pour_chaque_taille_traversee`, en échec avant le correctif, vert après.
- **Mesure avant, 16/11, 20 tailles de 0,85 à 1,25 fois la taille d'ouverture** (739 × 345 à 1087 × 508) :
  - cache `AppFonts` : 7 → 54 polices ;
  - GDI : 29 → 76, soit +47, exactement l'écart du cache ;
  - seconde passe sur les mêmes tailles : +0.
- **Cause** : le mécanisme de C1, sans plancher.
  - `PaintScale` suit la taille client en continu (minimum 0,1).
  - Les touches (`PaperKeyboard`), le bandeau et les légendes demandaient `AppFonts.Get` à la hauteur `taille × échelle`.
  - La fenêtre se redimensionne à la souris (`WS_THICKFRAME`) : chaque taille peinte pendant le glissé ajoutait ses hauteurs.
- **Borne avant correctif (estimation)** : environ 1,2 police par point d'échelle ; de 0,5 à 2 fois la taille d'origine, de l'ordre de 200 polices.
- **Mesure après, même protocole** : GDI 29 → 29 → 29 ; cache partagé 0 → 0 ; polices de la fenêtre 7 à la taille d'origine, 8 au plus, 0 après la fermeture ; couverture des glyphes immobile (sans sa correction : 103 → 859 entrées).
- **Ordre des tests** : avant le correctif, le cache était commun au processus. Lancé juste après celui du clavier, le test des Leçons ne voyait plus que +28. Les chiffres « avant » sont ceux de chaque test lancé sans l'autre avant lui.
- **Recherche des autres appelants** : seuls les Leçons et le clavier virtuel ont une échelle qui suit la taille de la fenêtre. Les autres fenêtres suivent le DPI (`_dpiScale`, `dpi / 96f`), d'où un jeu par écran, borné. L'accueil (`OnboardingWindow`) dessine aussi `PaperKeyboard`, mais son unité vient du DPI et de la largeur de la zone de travail, pas de la taille de sa fenêtre : il reste sur le cache partagé (`fonts: null`, commenté dans le code).

### C3 — confirmé en CI puis corrigé : GDI+ démarré puis arrêté à chaque dessin

- **Où** : sept endroits démarraient GDI+ puis l'arrêtaient autour d'un dessin : l'icône du tray (`TrayIcon.RenderLogo`, `TrayIcon.FromPixels`, refaite à chaque bascule, Verr. Maj. ou pause), le logo en icône (`TrayApplication.CreateLogoIcon`, `NativeWindow`), l'À propos, les Réglages et l'Accueil.
- **Mesure sur le poste, 16/11** : sans pomper les messages, USER +52 sur 100 cycles ; en pompant comme la boucle du tray, +2 ; avec un GDI+ gardé ouvert ailleurs, 0. Le constat avait été classé réfuté sur ce seul poste.
- **Mesure en CI, 16/11 (run 43952848446, runner Windows de GitHub)** : en pompant, USER +10 sur 10 cycles puis encore +10 sur 10 de plus ; même chose par `TrayIcon.Create` (+10 puis +9) ; avec un GDI+ gardé ouvert, 0. La file de messages ne rend pas ces objets partout : une icône par bascule les accumulerait chez un utilisateur dont le Windows se comporte comme le runner.
- **Pourquoi les tests de fenêtres ne le voyaient pas** : le banc ouvre les fenêtres dans `BancCapture`, qui démarre son propre GDI+ (`CaptureSupport.cs:270`) ; leurs couples démarrage/arrêt ne faisaient donc que compter une référence de plus.
- **Correctif (choix d'Antoine du 16/11 : « l'allumer une seule fois »)** : `GdiPlus.EnsureStarted` (`GdiPlus.cs`) démarre GDI+ au premier dessin et le garde ouvert ; `Program.Main` l'arrête après la fermeture des fenêtres, qui libèrent leurs images GDI+ dans leur `Dispose`. Les sept endroits l'appellent.
- **Test** : `RessourcesLongueDureeTests.Aucun_dessin_ne_redemarre_GDIplus` (20 × 2 icônes et un À propos : 0 démarrage de plus). L'ancien test, qui démarrait et arrêtait GDI+ lui-même, mesurait Windows et non l'app : il est retiré.

### Plausibles (sans test en échec)

- **P1** — `AppFonts.EnsureLoaded` : les polices embarquées sont chargées une fois par processus (`AddFontMemResourceEx`). La mémoire copiée reste allouée jusqu'à la sortie. C'est borné et non répété, donc pas un ticket.
- **P2** — `TrayApplication` n'a pas de constructeur testable sans vrai crochet clavier : ses 13 minuteries ne sont classées que par lecture. Toutes sont sur la fenêtre du tray (`SetTimer(_hWnd, id)`), donc remplacées à même id et détruites avec elle (`TrayApplication.cs:2346`). L'arrêt n'en tue que 4 explicitement (2357-2360), sans effet puisque la fenêtre les emporte.

### Réfutés

Par mesure (tests verts, `RessourcesLongueDureeTests`) :
- **Fenêtres ouvertes, peintes et fermées (11 fenêtres)** : GDI 0, USER sans croissance sur la seconde moitié, abonnés statiques et dialogues inscrits stables, sur 20 et 100 cycles. Le +7 USER de l'Accueil vu une fois dans verify est le décalage ponctuel décrit dans la méthode : +9 sur 300 cycles.
- **Fenêtres laissées ouvertes** sous `WM_DPICHANGED` 96/144, `WM_SYSCOLORCHANGE`, `WM_SETTINGCHANGE` et une langue qui alterne fr/en (11 fenêtres) : idem.
- **Même instance fermée puis rouverte** (9 fenêtres que le tray garde en un exemplaire) : idem.
- **Leçons recréées sur le KeyMapper et le KeyboardHook partagés**, comme le fait `EnsureLessonsWindow` : 0 abonné laissé sur le moteur après 23 fenêtres. Le premier banc donnait un moteur neuf à chaque cycle et ne pouvait pas le voir.
- **ForegroundMonitor créé puis disposé 100 fois** : 300 crochets WinEvent posés, 300 retirés.
- **Icône du tray refaite 100 fois** (`TrayIcon.Create`, `CreateTextIcon`) : GDI 0, USER +4 et 0 sur le poste (sur le runner de la CI, voir C3).

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

| Ticket | Impact | État |
|---|---|---|
| T1 | faible | fait : C1 et C2 corrigés par les polices par fenêtre |
| T3 | faible à moyen selon le Windows | fait : C3 corrigé, GDI+ démarré une fois (`GdiPlus`) |
| T2 | prévention | fait : `verify.ps1 -Etape ressources`, étape de la CI (`dbd198e`) |

- **T1, correctif retenu (QCM du 16/11)** :
  - `WindowFonts` (`AppFonts.cs`) : les polices d'une fenêtre à échelle continue, créées à la demande, toutes détruites quand l'échelle change (`At`) et à la fermeture (`Dispose`). Les Leçons (`_scaledFonts`) et le clavier virtuel (`_fonts`) en ont un chacun.
  - `FontScale` (échelle + propriétaire) remplace le `float` d'échelle dans les dessinateurs partagés (`PaperKeyboard`, `MiniKeyboardRenderer`, `KeyNotation`, `TablatureRenderer`). Sans conversion implicite : chaque appelant d'un dessinateur choisit `WindowFonts.At(échelle)` ou `FontScale.Shared(échelle)` (DPI seul, cache partagé), et le compilateur a listé chacun de ces appels.
  - Hors dessinateurs, `FontSpec.Of(…, float)` reste pour les tailles qui ne suivent que le DPI ; ses appelants ont été relus un par un : `_dpiScale` ou `dpi / 96f` partout (À propos, Réglages, Accueil, Recherche de caractère, Conflit, Statistiques, couches, indicateur, notification). Dans les fichiers des Leçons, les seules polices décrites passent par `PaperFonts` ou `HomeFonts` (`LessonsWindow.Paper.cs:23`, `LessonsWindow.Home.cs:59`) ou héritent du propriétaire d'un style (indice, infobulle) : toutes les pages, peintes ou non par le banc, ont donc leurs polices dans le jeu de la fenêtre.
  - Couverture des glyphes (`PaperText.Coverage`) : indexée par la `FontSpec` entière, elle gardait une entrée par hauteur traversée et par grappe, et, depuis le correctif, le jeu de polices de chaque fenêtre fermée. Elle est désormais indexée par famille et graisse, dont seule dépend la table des glyphes. Les deux tests le vérifient (couverture immobile pendant le balayage).
  - `WindowFonts` n'a pas de verrou : il vérifie en Debug qu'un seul fil l'appelle, celui de sa fenêtre (un fournisseur UIA ou une minuterie du pool qui l'atteindrait échouerait dans les tests lancés en Debug, comme `dotnet test` en local ; `verify.ps1` compile en Release, où `Debug.Assert` disparaît, comme celle d'`AppFonts.Get`).
  - `AppFonts.Get` refuse en Debug une police à propriétaire (`Debug.Assert`) et ne sert plus qu'aux tailles qui ne suivent que le DPI.
  - Sûreté de la destruction : les deux fenêtres dessinent dans un DC mémoire créé et détruit à chaque peinture (le DC propre du clavier virtuel ne reçoit qu'un `BitBlt`) ; aucune police d'un `WindowFonts` ne reste sélectionnée entre deux messages, et l'échelle ne change pas pendant une peinture.
  - Les deux tests des constats vérifient en plus : cache partagé immobile, couverture des glyphes immobile, jeu de la fenêtre borné (au plus la tolérance au-dessus du jeu d'origine), jeu vide après la fermeture. Ils peignent la page affichée à l'ouverture ; les autres pages sont couvertes par la relecture ci-dessus.
  - Pistes écartées : arrondir l'échelle à des paliers (moins de hauteurs, croissance restante) ; cache à plafond (risque de détruire une HFONT encore sélectionnée ou donnée par `WM_SETFONT`).
- **T2** : environ 2 s de plus par exécution. Chaque nouvelle création de ressource demande une règle ou une ligne de verdict ; 7 témoins (`scripts/tests/test_audit_ressources_inventaire.py`).

## Vérification

- Banc `RessourcesLongueDuree` : 41 tests, tous verts (dont C1, C2 et les 2 contrôles négatifs).
- Sans la correction de `PaperText.Coverage` (fichier de `HEAD` remis le temps d'un essai), les tests de C1 et C2 échouent sur la couverture des glyphes : la nouvelle assertion voit bien la croissance.
- `scripts/verify.ps1 -Etape "ressources,temoins,tests-core,tests-windows,tests-app"` (16/11, 20 h 46, Windows PowerShell 5.1) :

| Étape | Référence 396e449 | Cette branche |
|---|---|---|
| ressources, temoins | (absente), — | 387 sites, 0 sans verdict, 0 périmé ; 166 témoins |
| tests-core, tests-windows | 18/18, 404/404 | 18/18, 404/404 |
| tests-app | 1993 réussis, 3 ignorés, sur 1996 | 2034 réussis, 3 ignorés, 0 échec, sur 2037 |

  Les 41 tests du banc expliquent l'écart (1996 + 41 = 2037) ; les 3 ignorés sont les mêmes (`CurseurSimuleTests`).
- Passage précédent (même jour, avant la prolongation USER) : 1 échec, `Couches maintenues` USER +6 sur la seconde moitié, réfuté comme fuite (voir Méthode), d'où la prolongation et le contrôle négatif.
