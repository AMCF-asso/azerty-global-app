# Cahier des charges — AZERTY Global pour Windows

> **Cible : version 2.0.0**, première livraison du programme v2. Application Windows .NET 8 AOT distribuée via Microsoft Store (MSIX bundle x64 + ARM64) et, depuis la v1.0.0, via MSIX hors Store signé au nom de l'AMCF. Sans dépendance externe, sans droits administrateur pour l'application Store ou MSIX.

**Portée.** Ce document décrit ce que la 2.0.0 doit faire. Il ne constate aucune livraison : la publication reste conditionnelle à la validation des exigences ci-dessous et à l'acceptation du Store.

**Convention des cases.** `[x]` = comportement présent dans le code au 2026-08-24 (publié en 1.1.0, ou terminé en 1.2.0 sans être packagé). `[ ]` = attendu pour la 2.0.0 et non livré. Une case cochée n'est pas une preuve de test.

**Décisions intégrées** (arrêtées les 2 et 5 septembre 2026, non rouvertes ici) : canaux de distribution et mentions par canal ; menu de la zone de notification ; parcours d'accueil et fusion du module d'essai dans les Leçons ; charte du clavier ; compagnon complet sur la disposition Windows native. Ce qui relève des étapes ultérieures du programme v2 est listé au §2.13.

---

## 1. Objectifs

### Objectif principal
Permettre à n'importe quel utilisateur Windows d'utiliser AZERTY Global **immédiatement**, sans installation, sans droits admin, et sans alerte de sécurité.

### Cas d'usage cibles
- Salarié sur un PC d'entreprise verrouillé (pas d'admin)
- Étudiant sur un PC d'école/université
- Utilisateur qui veut tester avant d'installer
- Démo lors d'événements (CIC Innovation Awards, salons, conférences)
- **Utilisateur qui a déjà installé la disposition AZERTY Global native** dans les paramètres de langue de Windows et veut garder les outils de l'application

### Deux configurations prises en charge, sans perte de fonction

1. **AZERTY Windows traditionnel + remapping de l'application** : l'application produit les caractères d'AZERTY Global.
2. **AZERTY Global natif actif dans Windows + application en compagnon** : Windows produit la frappe de base, l'application garde tous ses outils (§2.12).

Aucune fonction n'est perdue du seul fait que la disposition native est active. La seconde configuration est un mode normal et complet du produit, pas un mode dégradé.

---

## 2. Exigences fonctionnelles

### 2.1 Remapping clavier complet

- [x] **48 touches** remappées selon la ressource `AZERTY Global 2026.json`, synchronisée depuis la disposition actuelle
- [x] **8 couches par touche** : base, shift, altgr, shift+altgr, caps, caps+shift, caps+altgr, caps+shift+altgr
- [x] **29 touches mortes** avec toutes leurs tables de transformation
- [x] **Touches mortes chaînées** : une touche morte suivie d'un caractère non reconnu doit produire le diacritique isolé + le caractère (fallback)
- [x] **Espace après touche morte** : produit le diacritique/symbole isolé

### 2.2 Verrouillage Majuscule Intelligent (Smart Caps Lock)

C'est la fonctionnalité signature d'AZERTY Global, elle doit être **parfaite**.

- [x] Caps Lock n'affecte **que les lettres** (a–z + é, è, ç, à, œ, æ, ù, ß)
- [x] Les chiffres, symboles et ponctuation ne sont **pas affectés** par Caps Lock
- [x] Caps + é → É, Caps + è → È, Caps + ç → Ç, Caps + à → À
- [x] Caps + Shift inverse le comportement (minuscule quand Caps actif)
- [x] Les couches AltGr respectent aussi le Smart Caps Lock (ex: Caps + AltGr + O → Œ au lieu de œ)
- [ ] Indicateur visuel de l'état Caps Lock (LED physique synchronisée si possible, ou icône tray)

### 2.3 Compatibilité

- [x] **Windows 10** (version 1809+) et **Windows 11**
- [x] Fonctionne avec tous les types de claviers physiques (ISO FR, ANSI US, etc.)
- [x] Compatible avec les raccourcis système (Ctrl+C/V/Z/X, Alt+Tab, Win+..., Alt+F4, etc.)
- [x] Compatible avec les raccourcis applicatifs (Ctrl+S, Ctrl+Shift+S, etc.)
- [ ] Ne doit **pas** interférer avec AltGr quand il est utilisé comme Ctrl+Alt par certaines applications
- [x] Fonctionne dans toutes les applications (navigateurs, éditeurs de texte, IDE, terminal, jeux*)

> *Jeux : idéalement pouvoir désactiver le remapping temporairement (voir §2.9)

### 2.4 Interface utilisateur

L'interface est **bilingue français / anglais**, le français étant la langue par défaut du public cible. Chaque texte visible existe dans les deux langues ; la langue se change depuis le menu et depuis Paramètres › Préférences, et se verrouille par la politique `Language`.

- [x] **Icône dans la zone de notification** (system tray) avec distinction visuelle actif/inactif
- [x] **Double-clic sur l'icône** : ouvre le clavier virtuel (geste le plus naturel)
- [ ] **Clic droit** sur l'icône : menu contextuel organisé en **quatre blocs par intention** — *État*, *Outils*, *Apprendre*, *Réglages et infos* — puis la langue et Quitter. Ordre cible (18 entrées de premier niveau) :

  ```
  Désactiver / Activer                     Ctrl+Maj+Verr.Maj
  Mettre en pause… / Reprendre maintenant
  ────────
  Clavier virtuel                          (raccourci affiché)
  Rechercher un caractère                  (raccourci affiché)
  Couches ▸   [Grec ☐ · Cyrillique ☐ · Scientifique ☐ · ── · Configurer…]
  ────────
  Leçons
  Défi du jour
  Revoir l'accueil
  ────────
  Paramètres
  Compatibilité des applications ▸         (grisée sans application détectée)
  Confidentialité & sécurité
  Statistiques
  Ressources ▸
  Retours et soutien ▸                     (canal AMCF : avis + bug seulement)
  Noter sur le Microsoft Store             (canal Store seulement)
  À propos
  ────────
  Switch to English / Passer en français   (grisée sous politique)
  Quitter
  ```

- [ ] **Trois raccourcis affichés seulement** : activer/désactiver, clavier virtuel, rechercher un caractère
- [ ] **« Compatibilité des applications » toujours visible** : sans application détectée au premier plan, les trois modes sont grisés et « Comprendre la compatibilité… » reste actif
- [ ] **« Lancer au démarrage de Windows » quitte le menu** : le réglage vit dans Paramètres › Préférences
- [ ] **À propos affiche le canal après la version**, sur les trois canaux : « Version 2.0.0 · Microsoft Store » / « · AMCF » / « · hors paquet »
- [x] **Raccourci clavier** pour activer/désactiver rapidement : Ctrl+Maj+Verr.Maj
- [x] **Notification au lancement** : bulle discrète confirmant l'activation
- [x] **Notification Caps Lock** : retour visuel discret quand Caps Lock change d'état (bulle ou changement d'icône tray), car le Smart Caps Lock modifie le comportement attendu
- [x] **Retour visuel touche morte** : indication discrète quand une touche morte est active (ex: bulle "^" ou changement d'icône), même sans le clavier virtuel ouvert — sinon l'utilisateur ne sait pas si sa frappe a été "avalée"
- [x] **Fichier de config portable** : les préférences utilisateur (position clavier virtuel, always on top, etc.) sont sauvegardées dans un fichier à côté de l'exe (ex: `config.json`)
- [x] Pas de fenêtre principale imposante — discret, minimaliste

### 2.5 Première utilisation (onboarding)

L'utilisateur qui lance l'application pour la première fois doit comprendre immédiatement ce qui se passe. L'accueil tient le rôle d'**écran de consentement** au regard de la politique Microsoft Store 10.2.8 (apps qui modifient le comportement système).

**Un seul parcours d'exercices en 2.0.0.** Le module d'essai autonome disparaît : ses 6 exercices deviennent la **séance 0** de la fenêtre Leçons, qui est désormais le seul moteur d'exercices de l'application.

#### Deux séquences d'accueil, une par canal

- [ ] **Canal Store (et hors paquet) — 3 écrans** : ① ce que change l'application + consentement 10.2.8, les 5 changements sur un seul écran → ② essayer la séance 0, ou passer → ③ préférences (langue, démarrage automatique, ne plus afficher)
- [ ] **Canal AMCF — 4 écrans, consentement en tête** : ① consentement dédié (« installée par votre organisation, voici ce que l'application change sur vos touches », bouton « J'ai compris ») → ② les 5 changements → ③ essayer la séance 0, ou passer → ④ préférences, grisées sous politique
- [ ] **Les ressources sortent de l'accueil** : elles vivent dans le menu (§2.4)
- [ ] **« Essayer maintenant » ouvre la fenêtre Leçons sur la séance 0 et ferme l'accueil** ; « Revoir l'accueil » permet d'y revenir depuis le menu
- [ ] **« Passer » mène à l'écran des préférences**, en lien discret sous « Essayer maintenant »
- [x] **Esc et la croix** ferment l'accueil
- [ ] **Réaffichage au démarrage** tant que moins de 3 exercices de la séance 0 sont faits et que « ne plus afficher » est décochée ; politique `ShowOnboarding` inchangée
- [x] **Sélecteur de langue** dans l'en-tête, décliné dans les deux thèmes, avec un nom accessible sur le contrôle

#### Séance 0 — 6 exercices, dans la fenêtre Leçons

- [x] 4 exercices obligatoires (premier É, ponctuation, e-mail, typographie française)
- [x] 2 exercices bonus, passables (ligne de code, mots étrangers), signalés par une pastille « Bonus »
- [ ] **Page de fin dédiée**, distincte du récapitulatif standard des leçons : ① les 3 gestes du quotidien avec leurs raccourcis (désactiver, clavier virtuel, rechercher un caractère) ; ② incitation au « Défi du jour » et, si la case est décochée, au lancement au démarrage ; ③ le guide utilisateur imprimable en premier, puis les liens `/guide`, `/faq` et `/compatibilite` du site
- [x] Clavier intégré : caractère principal, AltGr discret pour les lettres, grille 2×2 pour les symboles
- [x] Légende : « Maj. — Verr. Maj. — AltGr — Touche morte » avec leurs codes couleur

#### Migration depuis les versions 1.x

- [ ] Une progression 1.x de N exercices (0 à 6) marque **les N premiers exercices de la séance 0 comme faits** ; l'ancien compteur n'est plus jamais écrit, seulement lu
- [ ] Une installation 1.x ayant terminé les 6 exercices ne se voit **jamais re-proposer** la séance 0 ; une installation à 0 ou sans valeur n'est pas affectée

### 2.6 Clavier virtuel (visualiseur de disposition)

Fenêtre affichant la disposition AZERTY Global de manière interactive, similaire au testeur du site azerty.global.

- [x] **Affichage des 48 touches** avec tous les caractères visibles par couche
- [x] **Réactif aux modificateurs** : l'affichage change en temps réel selon les touches enfoncées :
  - État normal → caractères base
  - Shift enfoncé → caractères shift
  - AltGr enfoncé → caractères AltGr
  - Shift+AltGr → caractères Shift+AltGr
  - Caps Lock actif → caractères caps
- [x] **Réactif aux touches mortes** : quand une touche morte est active, le clavier virtuel montre les transformations possibles (ex: après `^`, afficher `â ê î ô û` sur les touches correspondantes)
- [x] **Code couleur** pour distinguer les couches (ex: blanc = base, bleu = AltGr, orange = touche morte active)
- [x] **Fenêtre redimensionnable** et repositionnable, reste au premier plan (option "always on top")
- [x] **Raccourci clavier** pour afficher/masquer rapidement (ex: Ctrl+Alt+F11)
- [x] **Pas d'interception des clics** : cliquer sur une touche du clavier virtuel ne tape pas le caractère (affichage uniquement, pas un clavier à l'écran)

### 2.7 Recherche de caractère

Permet à l'utilisateur de trouver comment taper n'importe quel caractère disponible dans AZERTY Global.

- [x] **Champ de recherche** accessible depuis le menu tray ou par raccourci clavier (ex: Ctrl+Alt+F10)
- [x] **Recherche par caractère** : coller ou taper `É` → affiche "Caps Lock + é" ou "^ + E"
- [x] **Recherche par nom** : taper "e accent aigu" ou "euro" ou "guillemet" → affiche le caractère et sa combinaison
- [x] **Résultat visuel** : montre la combinaison de touches sous forme lisible (ex: `AltGr + W → «`) et surligne la touche sur le clavier virtuel si celui-ci est ouvert
- [x] **Données issues de `character-index.json`** : réutilise le fichier existant du site qui contient déjà `unicodeNameFr`, `frenchAliases`, `methods` avec flag `recommended` — pas besoin de reconstruire ces données
- [x] **Méthode recommandée en priorité** : affiche d'abord la méthode marquée `"recommended": true` dans le JSON
- [x] **Insertion directe** : `Entrée` restaure la fenêtre d'origine (mémorisée aussi à l'ouverture depuis le menu tray) et insère le caractère via le moteur d'émission, la fenêtre de recherche restant ouverte comme en 1.1.0 pour enchaîner les insertions ; en cas d'échec (fenêtre disparue, focus refusé), repli sur la copie avec notification explicite
- [x] **Suspension dans les champs sécurisés** : le raccourci et l'entrée de menu sont inertes quand un champ de mot de passe a le focus

### 2.8 Fonctionnalités supplémentaires (v1)

- [x] **Compensation des DK système** : gérer les conflits avec les touches mortes de l'AZERTY Windows sous-jacent (^, ¨, `, ~) — essentiel car l'utilisateur a probablement l'AZERTY traditionnel comme disposition système. Le hook doit intercepter les DK système avant qu'elles ne soient traitées par Windows, puis appliquer le comportement AZERTY Global.
- [x] **Nettoyage auto des modificateurs** : réinitialiser l'état si un modificateur reste "bloqué" après quelques secondes d'inactivité (ex: Alt enfoncé dans un alt-tab interrompu)

### 2.9 Compatibilité jeux (livré en v0.9.7)

L'application reste active dans les jeux pour permettre à l'utilisateur de continuer à taper en français correctement (chat, recherche d'items modés, langues étrangères) tout en garantissant que les frappes injectées ne cassent ni les bindings de gameplay ni les anti-cheats.

- [x] **Désactivation automatique sur les jeux protégés par anti-cheat kernel-level** (Valorant, League of Legends, Fortnite, Apex Legends, Call of Duty Warzone/Black Ops, R6 Siege, PUBG, Escape from Tarkov, Genshin Impact, Honkai Star Rail, Roblox, FACEIT, Battlefield 2042, The Finals, Delta Force, Marvel Rivals, Helldivers 2, etc.) avec bulle d'information à l'utilisateur. Réactivation automatique à la fermeture du jeu. Liste hardcodée mise à jour à chaque release. Sources : rapports Gemini et Perplexity du 2026-04-26.
- [x] **Combo native ciblée pour les jeux compatibles** : pour les jeux qui filtrent les frappes synthétiques (Minecraft Java + mods comme JEI, jeux Unity, SDL, GLFW, DirectInput…), l'application injecte les caractères via une combinaison de touches natives du clavier sous-jacent au lieu d'un VK_PACKET ignoré. Détection automatique via les modules chargés (`glfw3.dll`, `lwjgl_glfw.dll`, `SDL2.dll`, `UnityPlayer.dll`, `dinput8.dll`, `allegro-5.2.dll`, etc.).
- [x] **Alt+code pour les caractères inaccessibles sur le layout natif** (`É`, `«»`, `–`, `œ`, etc.) : injection via la séquence `Alt+0XXX` du Numpad pour préserver Smart Caps Lock et la typographie typographique en jeu.
- [x] **Override utilisateur par application** dans le menu de la zone de notification (`Auto`, `Forcer compatibilité jeu`, `Forcer désactivation`). Refus de l'override `forceOn` sur un jeu protégé par anti-cheat (sécurité utilisateur). Audit automatique au démarrage : un override `forceOn` sur un jeu nouvellement ajouté à la liste anti-cheat est supprimé avec bulle d'avertissement.
- [x] **Filet de sécurité contre les "stuck keys"** : émission de keyup synthétiques pour toutes les touches en pass-through avant tout reset interne (toggle off/on, désactivation auto). Évite que le personnage continue à avancer après réactivation manuelle.

### 2.10 Couches supplémentaires — grec, cyrillique, scientifique

> Ces couches ont **deux modes : ponctuel et verrou**. Le mode « maintien physique » a été retiré au smoke test du 2026-08-24 et n'est pas réintroduit en 2.0.0 ; le terme « couches maintenables » employé jusque-là ne le remet pas au programme.

Étend les trois touches mortes alphabétiques sans modifier leurs emplacements ni leurs tables (`AZERTY Global 2026.json` reste la source unique). Fonctionnalité désactivée par défaut, activation volontaire depuis le menu tray, entièrement hors ligne. Développée le 2026-08-05, portée sur l'architecture extraite le 2026-08-24.

- [x] **Appui simple** : touche morte ponctuelle actuelle (comportement historique inchangé)
- [x] **Déclencheur maintenu pendant une autre frappe** : vaut un appui simple — la première frappe est transformée, les suivantes redeviennent ordinaires (mode « maintien » retiré au smoke test du 2026-08-24 : tenir le déclencheur en tapant du reste des doigts était intenable)
- [x] **Double appui** : verrouillage dans le processus actif ; délai configurable 150–1000 ms (défaut : double-clic système)
- [x] **Déverrouillage** : même déclencheur ou premier Échap (absorbé) ; verrou associé au PID + instant de création (jamais hérité par un PID réutilisé, purgé à la mort du processus)
- [x] **Substitution** : pendant un verrou, l'appui simple d'une autre couche transforme la frappe suivante puis rend le verrou ; son double appui remplace le verrou
- [x] **Modificateurs** : Ctrl, Alt et Windows conservent les raccourcis ; le Shift/AltGr d'activation est consommé ; un nouveau Shift ou Verr. Maj. produit les majuscules
- [x] **Espace** : espace normale en verrouillage ; touche non définie → caractère AZERTY Global ordinaire ; les 26 autres touches mortes inchangées
- [x] **Indicateur visuel** près du caret (couche + mode), désactivable, masqué dans les champs sécurisés et quand le remapping est suspendu
- [x] **Champs de mot de passe** : remappage ordinaire conservé, mais couches, recherche et indicateur suspendus — détection `ES_PASSWORD` + UI Automation (navigateurs) sur thread dédié, jamais dans le hook clavier
- [x] **Réglages persistants** avec migration : configurations existantes conservées, couches désactivées

### 2.11 Accessibilité

L'accessibilité fait partie des exigences de la 2.0.0, pas d'un lot ultérieur. Des captures d'écran ne la prouvent pas : la validation se fait au clavier et au lecteur d'écran (§7).

- [ ] **Tout se fait au clavier** sur les fenêtres livrées : ordre de tabulation cohérent, aucune souris obligatoire, Échap ferme
- [ ] **Focus toujours visible**, y compris sur les contrôles peints à la main
- [ ] **Nom accessible** sur chaque contrôle interactif, en particulier ceux qui n'affichent qu'une image
- [ ] **Contraste** conforme sur les deux thèmes ; thème contraste élevé de Windows pris en charge
- [ ] **Mise à l'échelle** : 100 %, 125 % et au-delà sans texte coupé ni contrôle inatteignable
- [ ] **Thème clair/sombre suivant le système**, icône de la zone de notification comprise

### 2.12 Compagnon complet sur la disposition Windows native

Exigence produit centrale de la 2.0.0 : **aucune fonction n'est perdue quand la disposition AZERTY Global native est active dans Windows.**

- [ ] **Saisie ordinaire :** Windows produit les caractères de la disposition native ; l'application ne les remappe pas une seconde fois, y compris Maj, AltGr, Verr. Maj et les séquences de touches mortes
- [ ] **Outils conservés :** clavier virtuel, recherche, insertion et copie, aide aux séquences, leçons, progression, statistiques et réglages fonctionnent à l'identique. Arrêter le remapping de base ne vaut pas arrêter l'observation nécessaire aux fonctions activées
- [ ] **Couches supplémentaires :** grec, cyrillique et scientifique restent disponibles en ponctuel et en verrou, sans doublon et sans laisser une touche morte native en attente à la sortie d'une couche
- [ ] **Détection de la disposition** de la fenêtre de premier plan, suivie après Win+Espace, après un changement dans les Paramètres Windows et après un changement d'application
- [ ] **État affiché distinguant trois choses** : la disposition système active, le remapping de base et les fonctions supplémentaires. La présence de la disposition native n'est pas un conflit bloquant, et le mode compagnon ne s'affiche pas comme un simple « Désactivé »
- [ ] **Transitions propres :** changement de fenêtre, bascule de disposition, pause et fermeture laissent un état cohérent ; fermer l'application laisse la disposition native utilisable

Les restrictions des écrans protégés (fenêtres élevées, anti-triche, Raw Input) restent documentées comme limites du système : elles ne justifient pas de griser des outils qui fonctionnent sans injection.

### 2.13 Hors périmètre de la 2.0.0

Ces sujets ne sont pas abandonnés ; ils relèvent des étapes suivantes du programme v2, et aucune date n'est prise ici.

| Sujet | Statut |
|---|---|
| Atelier typographique, recherche enrichie et favoris, exercices adaptatifs | Étape 2 |
| Profils : charger d'autres dispositions (QWERTY Français, QWERTY Global) ; Compose personnel ; extraits de texte ; couches de navigation et pavé numérique ; couches personnelles | Étape 3, sur le format d'échange OKLM |
| Panneau scientifique (formules structurées, exports LaTeX / MathML / UnicodeMath) | Étape 4 |
| Applications macOS et Linux | Étape 5 |
| **Vérification de mise à jour** | **Abandonnée pour la 2.0.0** (décision du 2026-09-02) : le Store met à jour seul, l'appinstaller couvre le canal AMCF, et la 2.0.0 ne fait **aucun appel réseau** |
| **Liste anti-cheat téléchargée périodiquement** | **Abandonnée** : elle imposerait un appel réseau ; la liste reste embarquée et mise à jour à chaque version |
| Service en ligne, compte obligatoire, synchronisation automatique, IA générative, scripts ou extensions exécutables | Hors engagement du produit |

---

## 3. Exigences non-fonctionnelles

### 3.1 Sécurité et confiance

- [x] **Zéro droits administrateur** requis
- [ ] **Réduire les alertes SmartScreen / Smart App Control** : signature AMCF via Artifact Signing opérationnelle, réputation éditeur/fichier à construire
- [ ] **Zéro faux positif antivirus** (choix de technologie non flaggée + soumission aux éditeurs AV)
- [x] **Pas de keylogger** : l'application ne doit jamais enregistrer, stocker ou transmettre les frappes
- [ ] **Aucun accès réseau** : la 2.0.0 n'émet aucune requête. Les seuls liens sortants sont ceux que l'utilisateur clique lui-même (site, guide, dépôt), ouverts dans son navigateur
- [x] **Open source** (EUPL 1.2) : le code est auditable

### 3.2 Performance

- [x] **Latence imperceptible** : < 1ms de délai ajouté par frappe
- [ ] **Empreinte mémoire** : < 20 Mo en RAM
- [ ] **Pas de CPU visible** dans le Gestionnaire des tâches en usage normal
- [ ] Démarrage rapide : < 2 secondes

### 3.3 Autonomie système

- [x] **Exécutable unique** AOT autonome (~5 Mo x64, ~5 Mo ARM64)
- [x] Pas de dépendance externe à installer (.NET 8 compilé en code natif via PublishAot)
- [x] Pas d'écriture dans le registre Windows
- [x] Configuration utilisateur localisée : `%LocalAppData%\AZERTY Global\config.json` en mode MSIX, à côté de l'exe en mode unpackaged

### 3.4 Distribution

- [x] **Microsoft Store** : MSIX bundle x64 + ARM64 (~11 Mo bundle, ~5 Mo par architecture)
- [x] **MSIX hors Store signé AMCF** via Microsoft Artifact Signing pour les environnements sans accès Microsoft Store — produit et signé le 2026-06-30
- [x] **Mise à jour du canal AMCF par `.appinstaller`** ; le canal Store est mis à jour par le Store
- [ ] **Un seul binaire pour les trois canaux**, le canal étant reconnu à l'exécution ; le canal AMCF ne sollicite ni avis, ni don, ni Discord
- [ ] **L'installeur EXE signé AMCF installe la *disposition* Windows**, pas l'application : les deux produits restent distincts
- [x] Le JSON `AZERTY Global 2026.json` est embarqué dans le binaire comme ressource, synchronisée depuis la disposition actuelle

---

## 4. Architecture technique — Contraintes

### Ce que doit faire l'application techniquement

1. **Installer un low-level keyboard hook** (`SetWindowsHookEx` avec `WH_KEYBOARD_LL`) pour intercepter les frappes
2. **Mapper les scancodes** vers les caractères selon la couche active (base/shift/altgr/caps)
3. **Gérer l'état** : Caps Lock on/off, touche morte active, modificateurs enfoncés
4. **Émettre les caractères** via une méthode fiable (SendInput, SendKeys, ou injection Unicode directe)
5. **Afficher une icône tray** avec menu contextuel

### Contraintes Windows connues

- Les hooks `WH_KEYBOARD_LL` fonctionnent sans admin mais ont un timeout de ~300ms imposé par Windows (LowLevelHooksTimeout) — le traitement doit être rapide
- Certaines applications en mode élevé (admin) ne reçoivent pas les hooks d'un processus non-admin → limitation connue, à documenter
- Les jeux en mode DirectInput/Raw Input peuvent bypasser les hooks → limitation connue
- UAC : si une fenêtre admin est au premier plan, le hook ne s'applique pas

---

## 5. Comparatif des technologies candidates

| Critère | AutoHotkey v2 | C# / .NET | Rust | Go |
|---------|:---:|:---:|:---:|:---:|
| **Hook clavier sans admin** | ✅ | ✅ | ✅ | ✅ |
| **Risque faux positifs AV** | ⛔ Très élevé | ✅ Faible | ✅ Faible | ✅ Faible |
| **Exe unique (single-file)** | ✅ | ✅ (.NET 8 AOT) | ✅ | ✅ |
| **Taille exe** | ~1–2 Mo | ~5–15 Mo (AOT) | ~2–5 Mo | ~5–10 Mo |
| **Runtime requis** | Aucun (compilé) | Aucun (AOT) ou .NET 8+ | Aucun | Aucun |
| **GUI / System Tray** | ✅ Natif | ✅ WinForms/WPF | ⚠️ Bibliothèques tierces | ⚠️ Bibliothèques tierces |
| **Facilité de dev** | ✅ Simple | ✅ Simple | ⚠️ Courbe d'apprentissage | ⚠️ Moins adapté GUI Windows |
| **Écosystème Windows** | Bon | Excellent (natif Microsoft) | Bon | Moyen |
| **Maintenance long terme** | ⚠️ Communauté réduite | ✅ Support Microsoft | ✅ Communauté active | ✅ Communauté active |
| **Signable (Trusted Signing)** | ✅ | ✅ | ✅ | ✅ |

### Décision : C# / .NET 8 AOT ✅

- **Exe unique autonome** (~8-12 Mo) sans aucune dépendance ni runtime
- **GUI native** Windows (WinForms) pour le system tray et le clavier virtuel
- **Faible risque AV** (binaire .NET natif, pas de bytecode interprété)
- **Écosystème Microsoft** cohérent avec Azure Trusted Signing
- **Maintenance** : C# est très répandu, facilite les contributions futures

---

## 6. Données d'entrée

La ressource d'entrée embarquée est `AZERTY Global 2026.json`, copie synchronisée depuis la disposition actuelle, qui contient :
- **48 touches** avec jusqu'à 8 couches chacune
- **29 touches mortes** avec leurs tables de transformation complètes
- Les conventions du Smart Caps Lock

L'application doit lire ce JSON au démarrage et construire ses tables de mapping en mémoire.

---

## 7. Critères de validation

### Tests minimaux avant publication

**Remapping :**
- [x] Toutes les lettres a–z produisent le bon caractère (base + shift + caps)
- [x] É, È, Ç, À fonctionnent avec Caps Lock
- [x] Les 5 touches mortes principales fonctionnent (circonflexe, tréma, aigu, grave, tilde)
- [x] Touche morte + caractère non reconnu → diacritique isolé + caractère (fallback)
- [x] Les symboles de programmation fonctionnent (AltGr + D/F/G/H/J/K → { } \ | [ ])
- [x] Les guillemets français fonctionnent (AltGr + W/X → « »)
- [x] œ et æ fonctionnent (AltGr + O/A)
- [x] Espaces insécables : fine insécable en AltGr + Espace, insécable en Maj + AltGr + Espace
- [x] Les raccourcis Ctrl+C/V/Z/X ne sont pas cassés
- [x] Compensation DK système : ^ puis e produit ê (pas ^e ou ^^e)

**Interface :**
- [x] L'application se lance et se ferme proprement
- [x] L'icône tray s'affiche et le menu contextuel fonctionne
- [x] Double-clic sur l'icône ouvre le clavier virtuel
- [x] Le clavier virtuel réagit aux modificateurs (Shift, AltGr, Caps)
- [x] Le clavier virtuel réagit aux touches mortes actives
- [x] La recherche de caractère fonctionne (par caractère et par nom)
- [ ] L'accueil se réaffiche au démarrage tant que moins de 3 exercices de la séance 0 sont faits et que « ne plus afficher » est décochée (§2.5)

**Performance :**
- [x] L'application ne consomme pas de CPU au repos
- [x] Latence de frappe imperceptible

**Compagnon sur la disposition native (2.0.0)** — chaque scénario est comparé dans les deux configurations du §1, sur une machine où la disposition native est réellement installée. Les tests de moteur et la comparaison de fichiers ne suffisent pas :

- [ ] Frappe ordinaire, accents, symboles, Maj/AltGr/Verr. Maj → résultat conforme à la définition canonique, sans caractère doublé, perdu ou transformé deux fois
- [ ] Touche morte native enchaînée avec une couche supplémentaire → composition correcte, aucun état résiduel après désactivation de la couche
- [ ] Recherche, copie et insertion dans la fenêtre cible → insertion unique, au bon point d'insertion
- [ ] Clavier virtuel et aide aux séquences → modificateurs, touche morte active et méthode proposée cohérents avec la saisie réelle
- [ ] Leçons et progression → la saisie native est reconnue ; réussite, vitesse et précision évaluées selon les mêmes règles
- [ ] Statistiques → activité comptée selon les mêmes règles, sans dépendre du nombre de caractères réinjectés
- [ ] Couches grecque, cyrillique et scientifique → activation ponctuelle, verrou, sortie et raccourcis
- [ ] Bascule de fenêtre et de disposition, pause, fermeture → aucune double transformation ; la saisie native reste correcte après fermeture

**Accueil, fusion et migration (2.0.0) :**

- [ ] L'accueil affiche la séquence du canal courant (3 écrans Store, 4 écrans AMCF), dans les deux langues
- [ ] « Essayer maintenant » ouvre les Leçons sur la séance 0 et ferme l'accueil ; « Passer » mène aux préférences
- [ ] Une configuration 1.x à 6 exercices ne re-propose jamais la séance 0 ; une configuration à 0 ou absente n'est pas affectée
- [ ] La page de fin de séance 0 affiche les 3 gestes, l'incitation et les liens prévus

**Accessibilité (2.0.0)** — sur les fenêtres livrées, au clavier et au lecteur d'écran, pas sur des captures :

- [ ] Parcours complet au clavier de chaque fenêtre, focus visible à chaque étape
- [ ] Passe Accessibility Insights et lecture au Narrateur sans défaut bloquant
- [ ] Rendu correct en thème clair, sombre et contraste élevé, à 100 % et 125 %

**Canaux et installation (2.0.0) :**

- [ ] Installation, mise à niveau depuis une 1.x, conservation des réglages et retour à une version utilisable, sur les deux canaux
- [ ] À propos affiche le bon canal ; le canal AMCF n'affiche ni sollicitation d'avis, ni don, ni Discord
- [ ] La version affichée correspond au paquet réellement servi (empreinte, taille, notes de version)
- [ ] `Verify-Release.ps1` PASS et **WACK PASS** sur le bundle final, avant toute soumission
- [ ] **Installation par `.appinstaller` sur une machine propre**, sans droits administrateur, puis mise à jour par ce même canal
- [ ] **Smoke test sur un vrai paquet AMCF**, pas seulement en tests unitaires : aucune sollicitation d'avis, de don ni de Discord, statistiques éteintes par défaut, et les réglages imposés par une stratégie de groupe apparaissent **grisés** avec la mention qui l'explique

**Couches supplémentaires (validation manuelle avant publication de la fonctionnalité)** — à dérouler dans Word/Excel, Chrome, Edge, Firefox et VS Code :

- [ ] Maj+* puis `a` → α ; maintien Maj+* + `abc` → αbc (l'accord vaut un appui simple, seule la première frappe est transformée) ; double appui → verrou (indicateur « verrou »), Espace reste une espace, Échap déverrouille et est absorbé une seule fois
- [ ] Pendant un verrou : Ctrl+C/V, Alt+Tab et Win+E restent intacts ; un nouveau Maj produit Α ; un appui AltGr+* rend la frappe suivante cyrillique puis retombe sur le verrou grec ; un double appui AltGr+* bascule le verrou vers le cyrillique
- [ ] Champ de mot de passe (login réel dans les 3 navigateurs + `<input type="password">` local) : frappes remappées normalement, aucune couche, indicateur masqué, raccourci de recherche inerte
- [ ] Recherche depuis le raccourci ET depuis le menu tray : Entrée insère au point d'insertion d'origine et la fenêtre de recherche reste ouverte ; fenêtre cible fermée avant Entrée → notification « copié »
- [ ] Verrou dans Word → VS Code (pas de couche) → retour Word (verrou revenu) → fermeture puis réouverture de Word (verrou disparu)

---

*Dernière mise à jour : 2026-09-05 — mise au niveau du programme v2, cible 2.0.0.*
