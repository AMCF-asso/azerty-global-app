"""Ajoute la suite donnée (corrections 1.3.0 du 25/09 au soir) dans verdicts.json."""
import json, pathlib
f = pathlib.Path(__file__).resolve().parent.parent / "verdicts.json"
v = json.loads(f.read_text(encoding="utf-8"))
SUITES = {
    "F-10": "Corrigé en 1.3.0 (4dbfa9d) : Échap sur un lien de l'étape 3 ferme l'accueil par la sortie de la croix. Recette : ligne 14. La mutualisation des sous-classes de liens reste pour la 1.4.0.",
    "V-07": "Corrigé en 1.3.0 (7b5d71c) : le clic passe par CharacterSearch.RowIndexAt, sur la géométrie d'OnPaint. Témoin SearchResultListTests. Le dimensionnement et le rognage restent pour la 1.4.0. Recette : ligne 15.",
    "V-08": "Corrigé en 1.3.0 (7b5d71c) : le pied dit « 20 sur 98 résultats ». Recette : ligne 16.",
    "V-06": "Corrigé en 1.3.0 (7b5d71c) pour « Shift » et « then ». « Verr. Maj. » sans couleur propre : choix d'affichage laissé à la 1.4.0. Recette : ligne 17.",
    "A-11": "Garde corrigée en 1.3.0 (62c7d4e) : un fichier illisible n'est plus écrasé, un fichier corrompu repart de zéro comme avant. Témoin UsageStatsReadFailureTests. Les clés inconnues restent pour la 1.4.0. Recette : ligne 18.",
    "L-14": "Reporté : la règle de mise en quarantaine et les textes du message sont à décider.",
}
for k, s in SUITES.items():
    v.setdefault(k, {})["suite"] = s
f.write_text(json.dumps(v, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
print("suites ajoutées :", ", ".join(SUITES))
