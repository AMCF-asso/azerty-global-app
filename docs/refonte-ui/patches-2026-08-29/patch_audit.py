# -*- coding: utf-8 -*-
"""Ajoute la section 12 a l'audit de refonte UI et redate le fichier."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\operations\refonte-app"
            r"\2026-08-28-audit-refonte-ui.md")

FENCE = "```"

SECTION = """## 12. Exécution — 2026-08-29, le fond de classe

Deux commits, `f9e7a8e` et `c27b9a9`. **Rien n'est poussé.** Trois projets de test : **18 / 108
/ 354**, zéro échec.

### Le fond blanc de Durée de pause n'était pas un défaut de la fenêtre

Les six captures du 28/08 portaient un client blanc. Mesure pixel par pixel : le jeton `papier`
n'y couvrait que **7,6 à 10,1 %** de l'image — la seule barre de titre — quand À propos rendait
le sien sur 84 à 86 % de la sienne. Rendue **en première fenêtre d'un processus**, la même
fenêtre se peint pourtant correctement par les quatre voies de capture essayées : `PrintWindow`
avec et sans `PW_RENDERFULLCONTENT`, `BitBlt` du DC client, et les deux mêmes après
`SetForegroundWindow`. Ni la fenêtre ni le banc n'étaient en cause : la variable était le **rang
de création**.

`ThemeWindow.ApplyClassBackground` confiait à la classe une brosse tirée du **cache partagé** de
`Theme`. Une brosse de fond de classe appartient au système dès qu'elle est posée, et il la
détruit au désenregistrement de la classe — ce que fait chaque `Dispose` :

{fence}
brosse avant   : 0x…8D100E4C  GetObjectType=2   ← OBJ_BRUSH, vivante
about.Dispose() → UnregisterClassW
brosse apres   : 0x…8D100E4C  GetObjectType=0   ← détruite par le système
meme handle rendu par le cache : True
{fence}

Le cache servait ensuite ce handle mort à toutes les fenêtres suivantes : `FillRect` ne peignait
plus rien (fond blanc) et `WM_CTLCOLORSTATIC` retombait sur `COLOR_BTNFACE` (étiquettes grises).
Les champs et les boutons, qui lisent d'autres entrées du cache, gardaient leurs couleurs — d'où
une fenêtre mi-charte, mi-système, dont l'apparence n'orientait vers aucune cause unique. Le
commentaire livré avec le helper affirmait l'inverse (« il ne faut jamais la détruire ») ;
`LearningModule.cs` porte une note sur ce même piège depuis le 2026-05-01.

### La seconde fenêtre à handle nul avait la même cause

`AboutWindow` inscrivait aussi la brosse partagée à l'enregistrement de sa classe. Au second
passage, `RegisterClassExW` échouait sur ce handle mort et `CreateWindowExW` rendait nul. C'est
le « une seule cellule par processus » que le banc contournait : il rend désormais les six
cellules d'un seul processus, et sa note a été retirée. ⚠️ **La fiche mémoire
`aboutwindow-seconde-instance-handle-nul` est périmée** — le défaut n'était pas propre à
`AboutWindow`, et il ne tenait pas à sa seconde instance mais au rang de la fenêtre.

**Rien de publié n'est touché** : `8775489`, le HEAD du bundle installé, est antérieur au socle
CH0, vérifié par `git merge-base --is-ancestor`. Le défaut est né dans CH0 et n'a jamais quitté
le poste.

### Correctif et témoin

Chaque fenêtre reçoit sa propre brosse, et le helper ne détruit que celles qu'il a créées — d'où
`ForgetClassBackground`, appelée au `Dispose` avant `DestroyWindow` plutôt qu'une libération qui
ferait double emploi avec celle du système.
`ThemeWindowTests.FondDeClasse_NeConsommePasLaBrosseDuCacheDeTheme` crée une vraie fenêtre, la
détruit avec sa classe, et vérifie que le cache rend toujours un `OBJ_BRUSH`. Témoin prouvé :
mutation posée (`Theme.Brush` remis en place), rouge en `Expected 2, Actual 0`, mutation retirée.

Mesures après correctif, sur les six captures de Durée de pause :

| Couleur | 28 août | 29 août |
|---|---|---|
| jeton `papier` / `fond` | 7,6 – 10,1 % | **73,7 – 76,6 %** |
| `#FFFFFF` non peint | 51,3 – 62,4 % | 0 – 7,8 % (surface des champs) |
| `#F0F0F0` système | 11,2 – 17,5 % | **0 %** |

### La planche d'états

`StatesBoard`, fermée par `AZERTY_STATES` comme le banc l'est par `AZERTY_CAPTURE`, rend les
primitives de `ThemeControls` état par état sur un bitmap mémoire, dans les deux thèmes. Motif :
le banc rend des fenêtres, donc il ne montre qu'un contrôle **au repos** — aucune souris ne
survole un bouton dans un processus de test — or le premier point de l'arrêt visuel porte
précisément sur le survol d'un bouton primaire. Mesuré sur la planche plutôt que jugé à l'œil :
la bordure au repos du bouton secondaire est `#B3A996`, soit `texte-2`, conforme à la charte.

La page d'arrêt visuel remise à Antoine le 2026-08-29 porte les 12 captures régénérées, les deux
planches et les trois points annotés.

---

*Dernière mise à jour : 2026-08-29*
""".replace("{fence}", FENCE)

OLD_TAIL = """---

*Dernière mise à jour : 2026-08-28*
"""


def main():
    text = PATH.read_text(encoding="utf-8")
    if text.count(OLD_TAIL) != 1:
        print(f"REFUS : pied de page trouve {text.count(OLD_TAIL)} fois")
        return 1
    if "## 12." in text:
        print("REFUS : la section 12 existe deja")
        return 1
    PATH.write_text(text.replace(OLD_TAIL, SECTION, 1), encoding="utf-8")
    print(f"ecrit : {PATH.stat().st_size} octets")
    return 0


if __name__ == "__main__":
    sys.exit(main())
