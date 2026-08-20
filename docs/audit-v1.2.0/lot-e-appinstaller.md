# Lot E — `.appinstaller` et chemin de mise à jour du canal AMCF

Session du 2026-08-20. Ce document porte ce que le lot E a décidé, ce qu'il a mesuré, et ce
qu'il laisse ouvert. Le code correspondant est `scripts/gen-appinstaller.py` et son témoin
`scripts/tests/test_gen_appinstaller.py`.

## Ce que ce lot livre

Un générateur, pas un fichier. `scripts/gen-appinstaller.py` lit l'identité dans le manifeste
du bundle signé et écrit le `.appinstaller` à partir d'elle ; le même script vérifie un fichier
existant contre un bundle. **Aucun `.appinstaller` n'est commité**, et c'est voulu : le fichier
doit porter l'identité du bundle AMCF de la 1.2.0, qui n'existe pas encore — il sera signé au
lot G. Un fichier écrit d'avance porterait soit une version fausse, soit l'identité du canal
Store, et le script refuse explicitement la seconde.

## Décisions d'Antoine, 2026-08-20, par QCM avant écriture

| # | Décision | Conséquence dans le code |
|---|---|---|
| E1 | Intervalle de vérification : **24 h** | `HOURS_BETWEEN_UPDATE_CHECKS = 24`, écrit explicitement dans le fichier |
| E2 | Mise à jour **silencieuse et non bloquante** | ni `ShowPrompt` ni `UpdateBlocksActivation` — voir la mesure n°2 ci-dessous |
| E3 | **Pas** de vérification en tâche de fond | `AutomaticBackgroundTask` absent, et sa présence est signalée comme un écart |
| E4 | URL du bundle **stable, sans version** | `BUNDLE_URI = …/AZERTY_Global.msixbundle` |
| E5 | Livrable : **générateur + validateur** | le fichier n'est jamais écrit à la main |
| E6 | Accents : **vérifier avant d'écrire** | fait, voir la mesure n°1 |
| E7 | Postes 1809 : **mesurer avant de promettre** | sans objet après la mesure n°2, les deux attributs concernés n'étant pas écrits |

## Trois mesures qui cadrent le contenu du fichier

Aucune des trois ne se devine à la lecture du tableau de référence de Microsoft, et deux
contredisent ce que ce tableau laisse croire.

### 1. Les accents de l'éditeur passent en production, malgré la doc

Le `Publisher` du bundle AMCF est imposé par le sujet du certificat de signature. Relevé dans
`AppxMetadata/AppxBundleManifest.xml` du bundle 1.1.0 réellement signé, le 2026-08-20 :

```
CN=Association pour la Modernisation du Clavier Français, O=Association pour la Modernisation du Clavier Français, L=Clermont-Ferrand, S=Puy-de-Dôme, C=FR
```

Les octets mesurés sont bien de l'UTF-8 : `Fran\xc3\xa7ais`, `Puy-de-D\xc3\xb4me`. Deux pages de
la doc Microsoft se contredisent sur ce point.

- `element-appinstaller`, Remarks : « Only `encoding="UTF-8"` with no escape characters, and no
  non-ascii characters is accepted. »
- `element-main-bundle`, attribut `Publisher` : une regex de DN dont la classe de caractères est
  `[^,+="<>#;]`, qui n'exclut aucun accent.

Un `.appinstaller` public en service tranche en faveur de la regex. `MicaForEveryone.appinstaller`
publie, sous `encoding="utf-8"` et namespace `appinstaller/2017/2` :

```
Publisher="CN=Đặng Bình Minh, O=Đặng Bình Minh, L=Hà Nội, C=VN"
```

et il est référencé comme méthode d'installation officielle de ce projet (« App Installer
sideload », lien ajouté dans son README). **Ce n'est pas une preuve d'installation** : aucun
rapport de succès ni d'échec n'a été trouvé pour ce fichier, et la recherche de code GitHub
n'est pas accessible sans authentification, donc le corpus balayé est celui qu'indexent les
moteurs. La preuve reste le critère d'acceptation du lot, plus bas.

En attendant, le script écrit les accents tels quels et **refuse tout échappement** : il
signale toute séquence `&…;` dans le fichier, et refuse de générer si le `Publisher` contient
un caractère qui obligerait à en écrire un (`&`, `<`, `>`, `"`). Le nombre de caractères
non-ASCII est affiché à chaque exécution, pour que le test sur machine propre sache ce qu'il
éprouve : `ç (U+00E7) × 2, ô (U+00F4) × 1`.

### 2. `ShowPrompt` est inerte pour cette application, et le silencieux est le défaut

`element-onlaunch`, Remarks, littéral :

> Setting the `ShowPrompt="true"` attribute currently shows a prompt for UWP applications but
> not for desktop applications that have been packaged in a Windows app package (that is,
> desktop applications that use the Desktop Bridge). For desktop applications, this
> functionality provides a silent update; the same default functionality provided by the
> OnLaunch element.

AZERTY Global est exactement ce cas : application desktop empaquetée en MSIX. La décision E2 est
donc obtenue **en n'écrivant rien**. Deux conséquences.

- Écrire les deux attributs n'aurait rien changé au comportement, et aurait introduit une
  dépendance à Windows 10 1903 — alors que le manifeste cible `10.0.17763` (1809).
- La question des postes 1809 tombe avec eux : `ShowPrompt` et `UpdateBlocksActivation` sont les
  seuls éléments du fichier introduits après 1809. `OnLaunch` est de 1709,
  `HoursBetweenUpdateChecks` de 1803. Rien dans le fichier produit n'exige plus que 1803.

Le document DSI ne doit malgré tout **pas** promettre le comportement d'un poste 1809 avant que
le smoke test l'ait constaté : la citation ci-dessus dit ce que fait `ShowPrompt`, pas ce que
fait une version de Windows antérieure à son existence.

### 3. Pas de préfixe `s4:`, et la doc ne s'accorde pas avec elle-même

Le tableau de `element-onlaunch` écrit `s4:HoursBetweenUpdateChecks`, `s4:ShowPrompt`,
`s4:UpdateBlocksActivation`. Mais `element-update-settings` déclare `s4` =
`…/appinstaller/2018` (fonctionnalités de 1809), tandis que `element-onlaunch` déclare le même
`s4` = `…/appinstaller/2021` (21H2). Les deux pages ne peuvent pas avoir raison ensemble.

Le fichier de production cité plus haut écrit `<OnLaunch HoursBetweenUpdateChecks="0" />`, sans
préfixe, sous `xmlns="http://schemas.microsoft.com/appx/appinstaller/2017/2"`. C'est cette forme
qui est reprise. La vérification lit d'ailleurs les attributs par leur nom local, donc un fichier
préfixé passerait aussi : le choix porte sur ce qui est écrit, pas sur ce qui est accepté.

## Fichier produit

Exemple obtenu depuis le bundle 1.1.0 réellement signé — **exemple, pas fichier à publier** : la
1.2.0 en produira un autre, avec sa propre version.

```xml
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2017/2"
              Uri="https://download.azerty.global/AZERTY_Global.appinstaller"
              Version="1.1.0.0">
  <MainBundle Name="AZERTYGlobal.AZERTYGlobal"
              Publisher="CN=Association pour la Modernisation du Clavier Français, O=Association pour la Modernisation du Clavier Français, L=Clermont-Ferrand, S=Puy-de-Dôme, C=FR"
              Version="1.1.0.0"
              Uri="https://download.azerty.global/AZERTY_Global.msixbundle" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="24" />
  </UpdateSettings>
</AppInstaller>
```

## Ce que la vérification refuse

`validate()` rend la liste des écarts entre un fichier et le bundle qu'il prétend décrire. Chaque
contrôle porte sur un fichier qui reste un XML valide et qui casse ailleurs : à l'installation, ou
en s'installant sans jamais se mettre à jour.

1. BOM UTF-8 en tête, fins de ligne CRLF.
2. Toute séquence d'échappement XML.
3. Élément racine autre que `AppInstaller`, XML illisible.
4. `Uri` ou `Version` de la racine qui divergent.
5. `MainPackage` au lieu de `MainBundle`, ou plus d'un `MainBundle`.
6. `Name`, `Publisher`, `Version` du `MainBundle` qui ne sont pas **littéralement** ceux du
   manifeste du bundle — c'est le piège n°3 du plan.
7. `Uri` du bundle qui diverge.
8. `UpdateSettings` absent : le poste n'irait jamais chercher de mise à jour, contre D7.
9. `AutomaticBackgroundTask` présent, contre E3.
10. `HoursBetweenUpdateChecks` absent, hors bornes 0-255, ou différent de la décision.
11. `UpdateBlocksActivation="true"` sans `ShowPrompt="true"` — la doc le refuse explicitement.

Et à la génération, deux refus francs plutôt qu'un fichier d'apparence correcte : identité du
canal Store, et version qui n'est pas en notation à quatre segments.

## Témoin

`scripts/tests/test_gen_appinstaller.py`, 29 tests, dont 18 mutations qui doivent toutes rougir.
Pas de script `witness-lot-e.py` à côté, contrairement aux lots B, C et D : leurs témoins
devaient muter des fichiers `.cs` réels et les restaurer, alors qu'ici la génération et la
vérification sont deux fonctions pures sur du texte. Les mutations tiennent donc dans la suite de
tests, sans toucher au dépôt.

Deux tests portent la charge de preuve du reste :

- `test_identite_du_bundle_reel` confronte le littéral accentué employé par tous les autres tests
  au manifeste du bundle signé. Sans lui, la suite entière pourrait s'accorder sur une chaîne
  plausible et fausse, et rester verte en refusant le cas réel. Il se saute — il ne rougit pas —
  quand le bundle est absent, celui-ci vivant dans le composant `website`.
- `test_fichier_genere_ne_signale_rien` est la réciproque des 18 mutations : une vérification qui
  refuse tout les passerait toutes.

## Reste à faire — côté serveur, hors de ce dépôt

Le fichier vit sur `download.azerty.global`, servi par le Worker Cloudflare
`components/website/workers/download-msix/`. **Rien n'a été modifié là** : le composant est
revendiqué par une autre session, qui a reçu la demande. Deux objets à ajouter à `FILES` —
`AZERTY_Global.appinstaller` en `application/appinstaller`, et `AZERTY_Global.msixbundle`, nom
stable. Quatre pièges mesurés dans ce Worker le 2026-08-20, avant toute édition.

1. **`CACHE_CONTROL = 'public, max-age=31536000, immutable'` est appliqué à tous les fichiers.**
   Sur une URL stable, c'est fatal : App Installer relirait pendant un an un `.appinstaller`
   périmé, et la mise à jour ne se déclencherait jamais. Ces deux entrées veulent un
   `Cache-Control` court, propre à elles.
2. **`Content-Disposition: attachment` est posé sur tous les fichiers.** Sur un `.appinstaller`,
   cela force le téléchargement au lieu de le passer à App Installer — le symptôme même que le
   plan attribue au seul type MIME. L'en-tête doit être omis pour cette entrée.
3. **`sha256` et `expectedSize` sont codés en dur par fichier.** Un nom stable change de contenu
   à chaque release : les deux valeurs devront suivre, sinon l'en-tête `X-AZERTY-Global-SHA256`
   annonce un hash faux.
4. Le `.appinstaller` n'existera qu'au lot G, avec le bundle signé. Rien ne doit être mis en
   ligne avant.

## Critère d'acceptation — non tenu à ce jour

Le plan est explicite : sans cette manipulation, le lot n'est pas fini, il est écrit.

1. Machine propre, installation par le `.appinstaller` servi depuis `download.azerty.global`.
   C'est là, et seulement là, que la question des accents du `Publisher` est tranchée.
2. Publication d'une version supérieure — bundle signé, `.appinstaller` régénéré, les deux objets
   remplacés dans R2.
3. Constat de la mise à jour effective sur la machine, sans intervention de l'utilisateur.
4. Sur un poste Windows 10 1809 si l'on en trouve un, constat de ce qui s'affiche au lancement
   pendant une mise à jour. Tant que ce constat n'existe pas, le document DSI reste muet sur ce
   cas.
