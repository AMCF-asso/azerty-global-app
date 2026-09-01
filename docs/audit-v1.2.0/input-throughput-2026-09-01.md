# Audit v1.2.0 — vitesse de frappe et capture des entrées

Date : 2026-09-01
Périmètre : hook global, remapping, onboarding, Leçons, mode libre, recherche,
touches mortes, modificateurs, compatibilité et périphériques clavier.
Cible produit : 200 WPM, soit environ 16,67 caractères/s et 60 ms par caractère
avec la convention de 5 caractères par mot.

## Verdict intermédiaire

Le chemin logiciel nominal dispose d'une marge très importante à 200 WPM. Les
rafales automatisées n'ont perdu aucun caractère jusqu'à la limite de l'outil de
test, environ 355 WPM. Le mode Libre de la v1.2.0 installée a aussi absorbé une
rafale à 200 WPM sans correction.

Deux limites structurelles restent à traiter ou à surveiller :

1. le hook bas niveau et les notifications UI s'exécutent sur le thread principal ;
2. `UsageStats.Preload()` est appelé après l'installation du hook, ce qui laisse une
   courte fenêtre où la première frappe peut déclencher une lecture disque dans le
   callback du hook.

La validation physique clavier intégré, USB et Bluetooth reste distincte : le hook
utilisé par l'application ne fournit pas l'identité du périphérique.

## Mesures automatisées

### Moteur isolé

Sonde : `src/TypingEngine.Windows.Tests/InputThroughputAuditTests.cs`. Chaque scénario
exécute 20 000 échantillons et vérifie l'intégrité de chaque lot produit. Les appels
`SendInput` sont mockés : les chiffres mesurent le moteur et la construction des lots,
pas la latence du pilote ou de l'application cible.

| Chemin | p50 | p95 | p99 | maximum observé | Débit agrégé |
|---|---:|---:|---:|---:|---:|
| Unicode simple | 1,5 µs | 2,1 µs | 5,1 µs | 2,68 ms | 506 910 touches/s |
| Composition par touche morte | 3,3 µs | 3,9 µs | 9,7 µs | 3,19 ms | 498 962 touches/s |
| Compatibilité native avec repli Alt+code | 1,9 µs | 2,4 µs | 6,1 µs | 4,47 ms | 376 107 touches/s |

Les maxima isolés incluent les pauses du runtime et du test. Même le maximum de
4,47 ms reste très inférieur aux 60 ms disponibles à 200 WPM.

### Chaîne Windows réelle

Version installée : MSIX `1.2.0.0`, exécutable daté du 2026-08-27. Les frappes sont
injectées comme événements clavier individuels dans une fenêtre Bloc-notes vierge.

| Scénario | Envoyé | Reçu | Ordre | Cadence réelle |
|---|---:|---:|---|---:|
| Cible produit | 250 | 250 | exact | 199,83 WPM |
| Rafale accélérée | 250 | 250 | exact | 355,49 WPM |
| Rafale sans pause demandée | 500 | 500 | exact | 354,71 WPM, limite de l'outil |

Le fichier `error.log` n'a pas été créé pendant ces essais : aucune exception ou
réinstallation critique du hook n'a été journalisée.

### Fenêtre Leçons — mode Libre

Une rafale de 250 frappes a été envoyée en 15,004 s, soit 199,95 WPM. La fenêtre a
affiché `792 caractères/min`, `19 s`, `0 correction` après le délai nécessaire à la
capture d'écran. Ces valeurs sont cohérentes avec 250 caractères reçus. Le WPM affiché
à 158 inclut les secondes d'attente et de capture après la fin de la rafale ; il ne
signale pas une perte.

### Suites de tests

- `TypingEngine.Core.Tests` : 18/18 réussis ;
- `TypingEngine.Windows.Tests` : 109/109 réussis, sonde d'audit comprise ;
- tests ciblés du tampon et du passage de ligne : 4/4 réussis avant blocage du binaire
  fraîchement recompilé par Smart App Control ;
- la suite applicative complète est actuellement inexécutable localement pour la même
  raison (`0x800711C7`). La compilation de l'application réussit sans erreur.

## Chemins de saisie examinés

### Hook et remapping

`KeyboardHook.HookCallback` appelle synchroniquement : suivi des modificateurs,
abonnés `RawKeyDown`, `KeyMapper.ProcessKey`, composition, construction des événements
et `SendInput`. Le stockage des statistiques est en mémoire après préchargement.

Microsoft indique qu'un hook `WH_KEYBOARD_LL` est rappelé sur le thread qui l'a
installé, qu'il doit répondre avant `LowLevelHooksTimeout`, et qu'un dépassement peut
entraîner son retrait silencieux. Sur Windows 10 1709 et suivants, la valeur acceptée
est plafonnée à 1 000 ms. Microsoft recommande un thread dédié et le transfert du
travail non indispensable hors du callback :
https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc

La machine auditée n'a pas de valeur `LowLevelHooksTimeout` explicite dans le registre.
L'application réinstalle préventivement le hook toutes les 60 secondes ; après un retrait
silencieux, la période sans remapping peut donc atteindre 60 secondes.

### Ordre des événements injectés

Chaque chaîne produite est envoyée dans un seul lot `SendInput`. Microsoft garantit que
les événements d'un même appel sont insérés séquentiellement et ne sont pas entrelacés
avec d'autres entrées :
https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput

### Onboarding et Leçons

Le tampon historique d'une seule sortie physique a été remplacé par une file FIFO dans
les deux fenêtres. Plusieurs `RawKeyDown` arrivés avant les `WM_CHAR` correspondants ne
s'écrasent plus. Les entrées expirent après 1 000 ms et sont purgées avant ajout ou
résolution.

Le délai bloquant de 300 ms entre les lignes de Leçons a été supprimé. La validation du
dernier caractère et l'ouverture de la ligne suivante sont maintenant atomiques dans
`TypeCharAndAdvanceLine`.

### Mode Libre

Le texte visible est limité à 220 caractères. Cette borne empêche le coût des insertions,
suppression et rendu de croître avec la durée de la session. Les compteurs continuent au-delà
de cette prévisualisation.

### Recherche de caractères

Chaque `EN_CHANGE` relit le champ, normalise la requête, évalue 1 034 entrées, trie les
résultats, redimensionne la fenêtre et invalide son rendu. Aucun debounce n'est appliqué.
Ce chemin reste raisonnable à 200 WPM sur le volume actuel, mais il n'est pas instrumenté
et sa marge n'est pas démontrée comme celle du moteur.

## Périphériques

`KBDLLHOOKSTRUCT` contient le virtual-key, le scancode, les flags, l'horodatage et une
information supplémentaire, mais aucun identifiant de périphérique :
https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-kbdllhookstruct

Conséquences :

- clavier intégré, USB et Bluetooth suivent le même code après livraison par Windows ;
- l'application ne peut ni mesurer ni attribuer une perte à un périphérique précis ;
- polling, latence radio, économie d'énergie et rollover doivent être testés physiquement ;
- un macro-pad ou clavier virtuel est couvert s'il émet des événements clavier Windows ;
  une manette ou un périphérique propriétaire sans émulation clavier ne l'est pas.

Inventaire courant : quatre interfaces HID Logitech `VID_046D` sont présentes. Aucun
clavier Bluetooth n'est actuellement énuméré ; seuls les énumérateurs Bluetooth Microsoft
sont visibles.

## Risques et recommandations

### R1 — hook sur le thread UI — risque moyen

À 200 WPM, aucune saturation n'est mesurée. En revanche, une pause exceptionnelle du thread
principal peut dépasser le budget Windows et faire retirer silencieusement le hook. Les
invalidations UI sont asynchrones, mais les abonnés `RawKeyDown`, la logique du mapper et
`SendInput` restent dans le callback.

Recommandation : ajouter une télémétrie strictement locale et agrégée du callback
(`count`, p50/p95/p99/max, nombre de réinstallations), sans enregistrer les touches. Ensuite,
déporter les notifications visuelles vers `PostMessage` et réserver le callback à la décision
de blocage, au remapping et à l'injection.

### R2 — préchargement après installation du hook — risque faible, correction simple

`_hook.Install()` est exécuté à `TrayApplication.cs:338`, alors que
`UsageStats.Preload()` n'arrive qu'à `:513`. Une frappe dans cet intervalle peut faire passer
`EnsureLoaded()` et sa lecture JSON sur le chemin critique.

Recommandation : déplacer `UsageStats.Preload()` avant `_hook.Install()`.

### R3 — recherche synchrone complète — risque faible

Le volume actuel reste petit, mais le coût augmente avec `character-index.json` et chaque
frappe déclenche un tri et un redimensionnement.

Recommandation : mesurer p95/p99 du filtre ; si p99 dépasse 5 ms, différer uniquement le
rendu ou introduire un debounce très court qui ne bloque jamais la réception des caractères.

### R4 — validation physique encore nécessaire

La preuve logicielle ne couvre pas le rollover, la radio Bluetooth, un dongle USB chargé,
la reprise après veille, ni les événements envoyés par un firmware propriétaire.

Un compteur local agrégé a été ajouté dans `docs/audit-v1.2.0/tools/` : il sépare les
événements physiques et injectés sans conserver les touches. Son auto-test de 10 pressions
virtuelles F24 compte exactement 10 key-down et 10 key-up injectés. Un premier relevé sur
le Logitech G213 USB a compté 68 key-down et 69 key-up physiques sur une fenêtre de
30 secondes, dont environ 15 secondes de saisie annoncée. L'écart d'un événement se situe
sur la frontière temporelle du relevé ; ce passage valide le compteur, mais pas encore la
chaîne bout en bout ni une cadence physique de 200 WPM.

Le passage bout en bout propre sur le G213 compte ensuite 114 key-down et 114 key-up.
Le mode Libre contient 106 caractères sans correction ; les huit key-down restants sont
exactement les sept caractères de la réponse `terminé` puis Entrée, saisis après le passage
dans cette conversation pendant que le compteur global terminait. Résultat : 106/106
caractères reçus par Leçons, aucune perte observée, à environ 85 WPM physiques. Le plafond
de 200 WPM reste couvert par les rafales synthétiques ; il n'a pas été atteint manuellement
sur ce passage matériel.

Matrice restante : clavier intégré à 200 WPM, Logitech USB à 200 WPM, clavier Bluetooth à
200 WPM, puis macro-pad/clavier logiciel si disponibles. Pour isoler le matériel de
l'application, compléter l'outil par un comptage simultané Raw Input par périphérique et
par le nombre de caractères accepté par la fenêtre, toujours sans conserver leur contenu.
