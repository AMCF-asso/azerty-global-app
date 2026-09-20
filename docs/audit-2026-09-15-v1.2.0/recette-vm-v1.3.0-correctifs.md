# Recette VM du paquet v1.3.0 — vérification des trois correctifs du 20 septembre

**État : aucun scénario ci-dessous n'a été exécuté.** Cette grille est à jouer par Antoine dans la VM `AZERTY-Test` ; la session qui l'écrit n'a aucun accès à cette VM.

## Le paquet à installer

| | |
|---|---|
| Fichier | `msix/AZERTYGlobal-1.3.0.0.msixbundle` |
| Taille | 6,48 Mo |
| Construit le | 2026-09-20 à 17:27 |
| SHA-256 de `AZERTY Global.exe` (x64) | `DF69897528D0171E37CD8E65D6A0E6E82BAE425B01DC546CDABBC11FAE6DED25` |
| Architectures | x64 et ARM64 |
| Branche | `release/1.2.0-notation-store` — **sans** les 32 commits de `origin/main` (décision d'Antoine du 2026-09-20) |
| Bundle précédent sauvegardé | `Archives/msix-previous/by-version/1.3.0.0/…-20260920-172722-….msixbundle` |

Vérifier l'empreinte avant d'installer :

```bash
Get-FileHash "msix\AZERTYGlobal-1.3.0.0.msixbundle" -Algorithm SHA256
```

## Ce qui a changé, et donc ce qui peut casser

Trois correctifs, dans cet ordre de risque décroissant :

1. **Écart 7 — Alt+Tab** : un chien de garde de 250 ms recalcule le contexte quand la fenêtre de premier plan ne correspond plus au dernier instantané. Risque introduit : un recalcul plus fréquent qu'avant, donc un scénario de suspension qui basculerait au mauvais moment.
2. **Écart 8 — `Ctrl+Maj+W` dans une application désactivée** : les raccourcis sont réarmés pour la seule désactivation *choisie* par l'utilisateur. Risque introduit : un raccourci qui fonctionnerait là où l'inertie totale doit tenir (anti-cheat, accès distant).
3. **Écart 4 — fenêtre Paramètres** : la fenêtre mesure son contenu, se plafonne à la zone de travail et défile. Risque introduit : une fenêtre mal dimensionnée, ou un contenu inatteignable dans l'autre sens.

## Les gestes, dans l'ordre

Noter pour chacun : obtenu / attendu, et une capture si l'écart est visuel.

### Écart 7 — le remappage survit à un Alt+Tab

| # | Geste | Attendu |
|---|---|---|
| 1 | Ouvrir le Bloc-notes et une seconde fenêtre (Explorateur). Taper dans le Bloc-notes pour confirmer que le remappage est actif. | Les caractères AZERTY Global sortent. |
| 2 | Alt+Tab vers l'Explorateur, puis Alt+Tab retour vers le Bloc-notes. Taper **immédiatement**, sans cliquer dans la fenêtre. | Les caractères sortent **du premier coup**. C'est le geste qui échouait. |
| 3 | Répéter cinq fois d'affilée, en variant la vitesse (Alt+Tab bref, et Alt maintenu avec plusieurs Tab). | Aucun échec sur les cinq. |
| 4 | Alt+Tab vers une application de la liste « désactivées », puis retour au Bloc-notes. | Remappage éteint dans l'application désactivée, **rétabli** au retour. |

### Écart 8 — `Ctrl+Maj+W` ne ferme plus la fenêtre

| # | Geste | Attendu |
|---|---|---|
| 5 | Paramètres → ajouter le Bloc-notes à la liste des applications désactivées, mode « forcer la désactivation ». | Le Bloc-notes apparaît dans la liste. |
| 6 | Dans le Bloc-notes, avec du texte non enregistré, presser `Ctrl+Maj+W`. | La **recherche de caractères s'ouvre**. La fenêtre du Bloc-notes ne se ferme pas, aucune invite d'enregistrement. C'est l'écart destructeur. |
| 7 | Toujours dans le Bloc-notes désactivé, taper du texte ordinaire. | Aucun remappage : le correctif rend les raccourcis, pas la frappe. |
| 8 | Lancer un jeu avec anti-cheat (ou tout processus de la liste anti-cheat), presser `Ctrl+Maj+W`. | **Rien ne se passe.** L'inertie totale doit tenir ici — si la recherche s'ouvre, le correctif est allé trop loin, c'est bloquant. |
| 9 | Ouvrir une session d'accès distant (Parsec, Bureau à distance), presser `Ctrl+Maj+W`. | **Rien ne se passe**, même raison. |

### Écart 4 — la fenêtre Paramètres tient à l'écran

À jouer en changeant la résolution et la mise à l'échelle de la VM entre chaque ligne.

| # | Résolution et échelle | Attendu |
|---|---|---|
| 10 | 1920×1080 à 100 % | Fenêtre entière visible, **aucune barre de défilement**, le bouton radio « forcer la désactivation » est visible sans rien faire. |
| 11 | 1366×768 à 150 % | Fenêtre plafonnée à la hauteur de l'écran, **barre de défilement présente**, les trois boutons radio de compatibilité atteignables en défilant. |
| 12 | 1366×768 à 175 % | Idem, rien ne sort de l'écran par le bas. |
| 13 | Molette de souris dans la fenêtre, puis glisser l'ascenseur, puis Page suivante | Le contenu défile dans les trois cas, le tracé suit les contrôles (pas de panneau resté en place). |
| 14 | Changer la langue FR → EN dans les Paramètres | La fenêtre se redimensionne à la langue, rien n'est coupé, le bas reste atteignable. |
| 15 | Déplacer la fenêtre d'un écran 100 % vers un écran 150 % (si la VM a deux écrans) | La fenêtre se remesure, ne dépasse pas la zone de travail du nouvel écran. |

### Non-régression rapprochée

| # | Geste | Attendu |
|---|---|---|
| 16 | Les cinq changements de caractères dans un éditeur et un navigateur | Résultat exact, rien d'ajouté ni de perdu (reprise de VM-04). |
| 17 | Touches mortes `^`, tréma, accent grave, en alternant Verr. Maj et AltGr | Texte attendu, état de composition intact (reprise de VM-05). |
| 18 | Mettre en pause puis reprendre au clavier | La reprise fonctionne — le correctif de l'écart 8 touche ce même chemin. |

## Ce que cette grille ne couvre pas

- Les six scénarios encore jamais joués de la recette d'origine : **VM-14, 15, 19, 20, 22, 23**, dont le champ de mot de passe et les retours partiels de `SendInput`.
- **ARM64** : le bundle en contient une tranche, aucune exécution ARM64 n'a jamais eu lieu. Une compilation croisée n'est pas une preuve.
- La **veille et la reprise** de session.
- L'**écart 5** (mode « forcer la compatibilité jeu », caractères perdus, mesures contradictoires), non corrigé.

## Côté machine, ce qui est déjà prouvé

- Build Release vert, 2 avertissements préexistants (`TrayApplication.cs` 1399 et 1412), aucun introduit.
- 495 tests verts : 18 Core, 157 Windows, 323 application.
- Trois témoins neufs sur `IsSnapshotStale`, **vérifiés par mutation** : la garde cassée fait rougir 2 tests sur 3, restaurée ils repassent au vert.
- Le binaire du bundle a la même empreinte SHA-256 que le binaire publié à 17:26 — aucun binaire périmé empaqueté.

Rien de tout cela ne prouve le comportement Windows réel des trois écarts : c'est ce que cette grille mesure.
