# Essais à la main après l'analyse des machines à états (application 1.3.0)

Branche `claude/ag-machines-etats-130`, 16 novembre 2026. Détail de chaque correctif : [machines-a-etats-tickets.md](machines-a-etats-tickets.md).

Les tests automatiques rejouent des messages et des touches simulés. Aucun de ces essais n'a été fait sur un vrai clavier ni à la souris : cette fiche liste ce que seule une main peut confirmer, du plus important au moins important. Compter 30 à 40 minutes pour les sections 1 à 6.

Dans la fiche, « la touche spéciale » est l'appui bref sur Maj + `*` : la lettre suivante sort en grec. Deux appuis de suite verrouillent le grec.

## 1. Accueil : double clic sur « Suivant » (ME-03)

- [ ] À l'étape 2, double clic rapide à la souris sur « Suivant ». Attendu : l'accueil s'arrête à l'étape 3, il ne se valide pas tout seul.
- [ ] Attendre une seconde, puis cliquer sur le bouton final. Attendu : l'accueil se valide.
- [ ] À l'étape 1, appuyer deux fois de suite sur Entrée, puis recommencer avec Espace. Attendu : chaque appui avance d'une étape, aucun n'est perdu.
- [ ] « Précédent » puis « Suivant » aussitôt. Attendu : les deux agissent.
- [ ] Cocher « Ne plus afficher », valider, rouvrir l'accueil depuis le menu. Attendu : la case est cochée ; valider sans y toucher garde le refus.

## 2. Lancement automatique refusé par Windows (ME-04)

- [ ] Gestionnaire des tâches, onglet des applications de démarrage : désactiver AZERTY Global. Dans les Paramètres de l'application, cocher « Lancer au démarrage ». Attendu : un message dit que Windows refuse et renvoie vers Paramètres Windows → Applications → Démarrage ; la case reste décochée.
- [ ] Réactiver dans Windows, cocher de nouveau. Attendu : la case reste cochée, sans message.

## 3. La touche spéciale et les autres touches (ME-12)

Après chaque ligne, taper une lettre : elle doit sortir en français.

- [ ] Touche spéciale, puis Entrée. Recommencer avec Tab.
- [ ] Touche spéciale, puis une flèche. Recommencer avec F5, Suppr, Pause, une touche de volume.
- [ ] Touche spéciale, puis la touche Windows (le menu Démarrer s'ouvre, le fermer).
- [ ] Touche spéciale, puis un chiffre du pavé numérique, Verr. num allumé. Attendu : le chiffre s'écrit, la lettre suivante est française.
- [ ] Touche spéciale, puis `^` ou `¨`, puis `e`. Attendu : ê ou ë, pas ε.

La lettre suivante doit au contraire rester en grec dans ces cas :

- [ ] Touche spéciale, puis Maj + a. Attendu : Α (alpha majuscule).
- [ ] Touche spéciale, puis Ctrl, Alt, AltGr, Verr. maj ou Verr. num seuls, puis une lettre. Attendu : lettre grecque.
- [ ] Grec verrouillé (deux appuis), puis Entrée, une flèche, un chiffre du pavé. Attendu : le verrou reste, les lettres sortent en grec.

À juger à l'usage : la touche Windows, Impr. écran, la touche Menu et les touches multimédia annulent aussi l'attente, même frappées sans lien avec le texte.

## 4. Maj tenu et touche tenue sous le grec (ME-05)

- [ ] Tenir Maj + `*`, taper a, garder Maj, taper a de nouveau. Attendu : α puis « a » minuscule, jamais « A ».
- [ ] Tenir Maj + `*`, relâcher Maj, garder `*` enfoncé jusqu'à la répétition. Attendu : aucune étoile ne s'écrit.
- [ ] Frappe ordinaire, Maj + lettre, AltGr + lettre, touche tenue dans un jeu. Attendu : rien de changé.

## 5. Leçons sous le grec (ME-06)

- [ ] Dans un exercice des Leçons : touche spéciale tenue, a, puis a en gardant Maj. Attendu : l'exercice attend α puis « a », sans frappe fantôme.
- [ ] Grec verrouillé puis une lettre. Attendu : la lettre grecque est comptée juste.
- [ ] Appui bref sur la touche spéciale, puis un accent, puis une lettre. Attendu : l'exercice attend la lettre accentuée.

## 6. Parcours de prise en main fermé au bilan (ME-12)

- [ ] « Activer et essayer » depuis l'accueil, finir un exercice jusqu'au bilan, fermer par la croix, rouvrir par « Reprendre ». Attendu : le même exercice du parcours, pas le suivant.
- [ ] Même essai en fermant par Échap, puis par « Passer pour l'instant ».
- [ ] Leçons ouvertes depuis le menu (hors parcours), fermer sur un bilan. Attendu : la reprise propose l'exercice suivant, comme avant.

## 7. Essais plus longs à monter

- [ ] Arrêt de Windows annulé (ME-09) : garder un Bloc-notes non enregistré, demander l'arrêt, cliquer sur « Annuler ». Attendu : l'application ne se fige pas. Changer un réglage, arrêter pour de bon. Attendu : le réglage est là au redémarrage.
- [ ] Lancement en administrateur puis lancement normal (ME-09). Attendu : le second sort en silence, sans boîte d'erreur.
- [ ] Horloge (ME-10) : avancer l'horloge de plusieurs mois, finir une séance du Défi, remettre l'horloge. Attendu : le lendemain, le rappel et le compte des séances repartent ; la série n'est pas remise à 1.
- [ ] Format régional thaï (ME-01) : régler le format régional de Windows sur le thaï, ouvrir Statistiques. Attendu : la série affichée est la vraie, le Défi du jour ne se recompte pas.
- [ ] Application figée (ME-12) : devant une fenêtre gelée, passer au Bloc-notes. Attendu : la recherche de caractère et le clavier virtuel reviennent en quelques secondes.
- [ ] Presse-papiers occupé (ME-11) : avec un outil qui tient le presse-papiers, « Copier mon résultat » au bilan du Défi. Attendu : le bouton devient « Copie impossible, réessayer » ; un nouveau clic réessaie.
- [ ] Statistiques ouvertes, décocher l'entraînement dans les Paramètres (ME-07). Attendu : la fenêtre se resserre, sans bandeau vide.
- [ ] Sur le Défi, « Réinitialiser la progression », fermer, rouvrir (ME-07). Attendu : la première leçon, pas le Défi.
- [ ] Recherche de caractère : copier, rouvrir en moins de 1,5 s (ME-07). Attendu : pas de « Copié » affiché.
- [ ] Couper puis rallumer les notifications dans les Paramètres (ME-08) ; rouvrir l'accueil depuis le menu pendant un silence de 15 s. Attendu : aucune demande d'avis par-dessus l'accueil.
- [ ] `lessons-progress.json` édité avec une entrée numérique dans `exercises` (ME-02). Attendu : la fenêtre des Leçons s'ouvre, les autres entrées sont gardées.

## Ce qui ne s'essaie pas à la main

- Un envoi de caractères coupé en cours de route (ME-06) et l'échec de création de la fenêtre de l'indicateur (ME-07) : couverts par les tests seulement.
- Le double clic sur « Noter sur le Microsoft Store » (ME-08, C7.5) : demande un vrai paquet du Store ; non corrigé, resté plausible.
- Les 11 constats plausibles (C2.1, C2.4, C3.1 à C3.7, C5.2, C7.5) n'ont ni test ni correctif.
