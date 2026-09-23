# Prompt unique — visuels de la fiche Microsoft Store v1.3.0

Rédigé le 2026-09-23. Remplace `prompts-chatgpt.md` : le prompt A1 testé dans Gemini a été rejeté par Antoine, qui préfère laisser toute la direction artistique à l’IA et ne fixer que le contenu.

Textes décidés par Antoine le 2026-09-23 : image 2 « Un raccourci oublié ? Il s’affiche. », image 3 « Cinq changements. 99 % des frappes identiques. ». Les autres textes sont proposés par l’IA, puis affinés.

Faits vérifiés : raccourcis de É, Ç et « dans `src/character-index.json` (É = Digit2 couche Caps, Ç = Digit9 couche Caps, « = KeyZ couche AltGr, soit W en AZERTY) ; le reste vient de `msix/Fiche Store.md`.

À coller tel quel dans une conversation neuve (Gemini ou ChatGPT) :

```
Tu es directeur artistique et graphiste. Tu vas créer les visuels de la fiche Microsoft Store d’un logiciel Windows : AZERTY Global. Tu as carte blanche sur la direction artistique : style, matière, couleurs, lumière, composition, typographie. Je fixe seulement les faits, quelques textes et les contraintes ci-dessous.

CE QUE JE TE DEMANDE

Génère au moins 12 visuels. Une image séparée par visuel : jamais plusieurs visuels dans une même image, jamais de grille ni de planche contact. Chaque image en format paysage 16:9, dans la meilleure qualité possible.

Organise-les en deux séries de six. Chaque série a sa propre direction artistique, cohérente d’une image à l’autre. Les deux séries doivent être franchement différentes l’une de l’autre.

Chaque série suit les 6 places de la fiche, dans l’ordre donné plus bas.

Avant chaque image, écris dans ta réponse, jamais dans l’image : la série, la place, et l’idée visuelle en une phrase.

Si tu ne peux pas tout générer d’un coup, avance le plus loin possible. J’écrirai « continue ».

LE PRODUIT

AZERTY Global est une disposition de clavier française améliorée pour Windows. Gratuite, open source (licence EUPL 1.2), sans publicité ni achat intégré, publiée par l’AMCF, une association loi 1901. Site : azerty.global.

Le principe : c’est l’AZERTY corrigé, pas réinventé. Les lettres A à Z et les touches é, è, à, ç ne bougent pas. Une seule touche est déplacée : ù. 99 % des frappes d’un texte français restent identiques. Formulation obligatoire : « 99 % des frappes », jamais « 99 % des habitudes ».

Les 5 changements, et il n’y en a que 5 :
1. Verrouillage Majuscule intelligent : il n’affecte plus que les lettres, donc É, È, Ç, À s’obtiennent en un appui.
2. Point en accès direct : le point et le point-virgule échangent leurs places.
3. @ et # sur la touche ², en haut à gauche, sans AltGr.
4. Symboles de programmation sur la rangée de repos : { } [ ] | \ avec AltGr + D F G H J K.
5. Accents internationaux sur la touche ù : accent aigu ´ et accent grave `.

Comparaison exacte :
- É : Alt + 0201 sur l’AZERTY traditionnel (5 touches), Verr. Maj + é avec AZERTY Global (2 touches).
- Ç : Alt + 0199 (5 touches), Verr. Maj + ç (2 touches).
- « : Alt + 0171 (5 touches), AltGr + W (2 touches).

Autres faits utilisables :
- Typographie française complète : ligatures œ et æ, guillemets « », tirets — et –, points de suspension …, apostrophe typographique ’, espaces insécables.
- Toutes les langues d’Europe et d’Afrique francophone (wolof, bambara, yoruba, haoussa…), alphabets grec et cyrillique, signes mathématiques.
- Installation sans droits administrateur.
- Clavier virtuel intégré : il affiche les 4 couches de la disposition, on retrouve un raccourci sans rien mémoriser.
- Recherche de caractères : on tape le nom d’un symbole, l’app montre sa touche et l’insère dans le document. Plus de 1 000 caractères indexés.
- Leçons guidées et Défi du jour : le même extrait pour tout le monde chaque jour, avec vitesse, précision et record personnel.
- Interface en français et en anglais.
- Vie privée : aucune télémétrie, aucun compte, aucun envoi réseau automatique, aucune frappe journalisée. Tout reste sur l’appareil.

N’utilise aucun autre fait, chiffre ou promesse que ceux de cette liste.

LE PUBLIC ET L’OBJECTIF

Des francophones qui tapent beaucoup (rédaction, études, développement) et qui pestent contre l’AZERTY sans savoir qu’une alternative existe. Ils découvrent le produit sur le Microsoft Store, sans le connaître.

L’objectif de la fiche : donner envie d’installer et de garder l’app. Le principal frein à lever, c’est la peur de devoir réapprendre à taper.

LES 6 PLACES

1. La vignette. C’est la seule image vue sans clic, elle porte presque tout. Message : on garde son AZERTY, on gagne les majuscules accentuées et la typographie. Appuie-toi sur la comparaison É. Titre à proposer par toi.
2. Le clavier virtuel. Texte imposé : « Un raccourci oublié ? Il s’affiche. »
3. Les 5 changements. Texte imposé : « Cinq changements. 99 % des frappes identiques. » Tu peux ajouter la liste des 5 changements en lignes très courtes, sans en changer le sens.
4. La recherche de caractères. Légende à proposer par toi, différente dans chaque série.
5. Le Défi du jour. Légende à proposer par toi.
6. La vie privée, pour clore la série. Titre à proposer par toi.

VRAIES CAPTURES : PLACES 2, 4 ET 5

Aux places 2, 4 et 5, une vraie capture d’écran de l’app sera posée après coup. Laisse à cet endroit un emplacement rectangulaire vide et uni, d’environ 60 % de la largeur de l’image, au format proche de 16:10, avec de la place pour une ombre portée. Ne dessine jamais la fenêtre ni l’interface de l’app : le Microsoft Store exige que ce qui représente l’app soit une vraie capture.

LES TEXTES DANS L’IMAGE

- Les textes imposés apparaissent exactement, lettre pour lettre, accents et ponctuation compris.
- Typographie française : espace avant : ; ? ! et à l’intérieur des guillemets « ».
- Titres de 8 mots au plus, une ligne secondaire au plus. Chaque image doit se lire en deux secondes, même en petite vignette.
- Aucun autre texte. N’invente pas de logo. Le nom AZERTY Global peut apparaître, en simple texte.
- En français pour l’instant. La version anglaise viendra ensuite.

CLAVIERS ET TOUCHES

Tu peux montrer des touches isolées si leur légende est exactement un des caractères cités (É, Ç, «, @, #, {…). Ne dessine jamais un clavier complet avec des légendes : elles seraient fausses, et le produit parle justement de la place de chaque touche. Un clavier sans aucune légende, flou ou partiel, est permis.

À ÉVITER

- Déjà essayé et rejeté, parce que ça fait modèle de présentation : cartes blanches à coins arrondis, accent bleu Windows, gros chiffre en couleur, séparateurs gris, pied de page répété avec un slogan, trop d’informations par image.
- Ce qui trahit l’image générée : halos, particules, dégradés néon, formes flottantes, rendu 3D brillant, surfaces trop lisses. Évite aussi les personnes et les mains.
- Les éléments importants à moins de 5 % du bord : l’image sera recadrée en 1920 × 1080.
```
