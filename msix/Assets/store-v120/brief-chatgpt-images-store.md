# Brief à coller dans ChatGPT — images de la fiche Microsoft Store

À copier intégralement dans une conversation ChatGPT neuve. Rédigé le 2026-09-18.

---

Tu es directeur artistique. Tu dois concevoir les **6 visuels** de la fiche Microsoft Store d'un logiciel Windows. Je ne te demande pas de les produire tout de suite : je te demande de **décider** lesquels, dans quel ordre, et ce que chacun contient.

## Le produit

**AZERTY Global** — une disposition de clavier française améliorée pour Windows. Gratuite, open source (licence EUPL 1.2), publiée par une association loi 1901. Site : azerty.global.

Le principe : c'est l'AZERTY *corrigé*, pas réinventé. Les lettres A à Z et les touches é, è, à, ç ne bougent pas. Une seule touche est déplacée : ù. **99 % des frappes d'un texte français restent identiques.** (Formulation imposée : toujours « 99 % des frappes », jamais « 99 % des habitudes ».)

Les 5 changements, et il n'y en a que 5 :

1. **Verrouillage Majuscule intelligent** — il n'affecte plus que les lettres, donc É, È, Ç, À s'obtiennent en un appui.
2. **Point en accès direct** — le point et le point-virgule échangent leurs places.
3. **@ et #** passent sur la touche ², en haut à gauche, sans AltGr.
4. **Symboles de programmation sur la rangée de repos** — `{ } [ ] | \` avec AltGr + D F G H J K.
5. **Accents internationaux sur la touche ù** — accent aigu ´ et accent grave `, ce qui ouvre plus de 300 langues.

Comparaison concrète, vérifiée sur la source du logiciel :

| Caractère | AZERTY traditionnel | AZERTY Global |
|---|---|---|
| É | Alt + 0201 (5 touches) | Verr. Maj + é (2 touches) |
| Ç | Alt + 0199 (5 touches) | Verr. Maj + ç (2 touches) |
| « | Alt + 0171 (5 touches) | AltGr + W (2 touches) |

Autres arguments disponibles : typographie française complète (ligatures œ æ, guillemets « », cadratins — –, points de suspension …, espaces insécables) ; toutes les langues d'Europe et d'Afrique francophone ; grec et cyrillique ; installation sans droits administrateur.

**Confidentialité**, argument fort : aucune télémétrie, aucun compte, aucun envoi réseau automatique, aucune frappe journalisée. Tout reste sur la machine.

## Ce que le logiciel sait montrer à l'écran

Trois fenêtres réelles sont capturables, ce sont de vraies captures d'écran, pas des maquettes :

- **Clavier virtuel** — un clavier visuel interactif affichant les 4 couches de la disposition.
- **Recherche de caractères** — on tape le nom d'un symbole, l'app montre où il est sur le clavier et l'insère dans le document. 1034 entrées d'index.
- **Défi du jour / leçons** — un module d'entraînement, le même extrait pour tout le monde chaque jour, avec vitesse, précision et record personnel.

## Le public et l'objectif

Trafic froid sur le Microsoft Store : des francophones qui tapent beaucoup (rédaction, développement, études) et qui pestent contre l'AZERTY sans savoir qu'une alternative existe. Ils n'ont jamais entendu parler du produit.

La version publiée est la **1.2.0**, une mise à jour de transition avant une 2.0.0. **L'objectif de cette fiche est de récolter des avis** : elle doit convaincre d'installer et de rester, pas vendre une nouveauté. Les nouveautés de la 1.2.0 ne méritent donc **aucun visuel dédié**.

Le premier visuel porte presque tout : c'est la vignette visible sans clic. Le frein principal à lever, c'est la peur du réapprentissage.

## Contraintes dures

- **6 visuels**, tous en **1920 × 1080** exactement.
- Deux jeux, **français et anglais**, même conception, seuls les textes changent.
- **Le rendu ne doit surtout pas « faire IA », ni faire template.** C'est le critère numéro un.
- **Partage de fabrication imposé :** une IA de génération d'images produit uniquement le **décor** (matière, fond, lumière, environnement). Tout le reste — titres, légendes, glyphes, captures du logiciel — est composé par-dessus en HTML puis rendu en PNG. Le décor ne doit donc contenir **aucun texte, aucune lettre, aucun clavier, aucune interface**. Conçois en conséquence : ce que tu mets dans une image doit être soit du décor pur, soit du contenu composable.
- Règle du Microsoft Store, clause 10.1 : les visuels doivent refléter fidèlement le logiciel. Une capture montrée doit être une vraie capture. Un habillage autour est autorisé.
- Les légendes font **4 à 6 mots**, posées hors de la capture, pas par-dessus.

## Ce qui a déjà été essayé et rejeté

Une première série a été refusée. Motifs, à ne pas reproduire :

- **Registre graphique** : cartes blanches à coins arrondis, accent bleu Windows, gros chiffre « 99 % » en bleu, séparateurs gris, pied de page répété avec une tagline sur chaque image. Ça faisait template SaaS générique.
- **Densité** : trop d'informations, illisible en vignette.
- **Textes** : accroches plates.

## Ce que je te demande de rendre

Pour chacun des 6 visuels, dans l'ordre de la fiche :

1. Son **rôle** dans la séquence — ce qu'il fait comprendre, et pourquoi à cette place.
2. Sa **nature** : capture réelle habillée, ou composition sans capture.
3. Son **contenu exact** : le texte français mot pour mot (titre, légende, tout élément textuel), et ce qui est montré.
4. Sa **composition** : où se place quoi dans le cadre 1920 × 1080.
5. Le **décor** attendu, décrit pour être généré ensuite.

Ajoute à la fin : ce qui assure la **cohérence** des 6 images entre elles, et la liste de ce que tu t'interdis pour éviter le rendu « IA » et le rendu « template ».

Commence par me poser les questions qui te manquent avant de proposer quoi que ce soit.
