# Consignes communes — lots d'implémentation 1.3.0 (audit du 25/09)

Décision d'Antoine (QCM du 25/09 au soir) : tous les chantiers sûrs et structurels de l'audit entrent **dans la 1.3.0**, sur la branche `release/1.2.0-notation-store`. Chaque lot passe par les vérifications avant son commit. Les lots s'enchaînent un par un.

- Dépôt : `D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store` (checkout principal, branche `release/1.2.0-notation-store`).
- Entrée de session héritée : `D:/My files/IA/sessions/active/2026-09-25-claude-code-42f632b84c7a.md`. Écriture autorisée dans `src/`, `docs/audit-v1.2.0/witness-lot-c.py` et le scratchpad `C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/`. **Ne pas toucher `Changelog.md`** : proposer ses lignes dans le compte rendu.
- Sources de l'audit :
  - constats avec les verdicts de la contre-expertise : `docs/audit-2026-09-25-v1.3.0-simplicite/donnees/constats.json` ;
  - rapport : `.../rapport.md` ;
  - notes de zone : `.../notes/` ;
  - contre-expertises : `.../contre-avis/` ;
  - mesures : `.../donnees/metriques.json` et `.../outils/`.

## Règles du dépôt (dures)

1. **Jamais d'`Edit` ni de `Write` sur un `.cs` existant.** Patcher par un script Python du scratchpad, sur le modèle de `scratchpad/correctifs/patcher.py` : ancre unique, fin de ligne de l'ancre, BOM conservé, tout ou rien, comptes CRLF/LF imprimés. Les fichiers sont mixtes (ex. `OnboardingWindow.cs` : CRLF et LF). Mesurer avant d'écrire, avec `scratchpad/correctifs/mesurer.py`.
2. **Fichier neuf** : LF pur, UTF-8 sans BOM. `src/AZERTYGlobal.Tests/` : commentaires en français accentué. `src/TypingEngine.Core*` : anglais et ASCII.
3. **Textes affichés** : passer par `src/Localization/L.*.cs` en FR et EN. La typographie française est vérifiée par `FrenchTypographyTests` (espace insécable avant `: ; ? ! »` et après `«`, apostrophe `’`). Les bulles et toasts suivent le gabarit des notifications (titre = état, corps = action, nom du produit une seule fois, 120 caractères par ligne).
4. **Garde-fous à ne jamais affaiblir** : réentrance du hook, VK_PACKET/B1, LLKHF_INJECTED, saisie sécurisée, jeux et anti-triche, accord d'activation, pause avant l'accord, mutex D19, compatibilité de config (clés inconnues conservées, refus d'écraser), accessibilité (noms accessibles, navigation clavier, DialogNavigation). Pour dire qu'un code est mort : grep des appelants, y compris les tests (réflexion sur des noms privés), `nameof`, les JSON et les scripts de `docs/` (témoins de mutation).
5. **Aucun changement visuel non annoncé.** Si un lot change un pixel à l'écran, s'arrêter avant le commit et rendre des images avant/après. Antoine valide l'affichage sur images, jamais par QCM.
6. **Ne pas lancer l'application**, ne pas installer de hook global, ne pas toucher au registre.

## Vérification avant chaque commit

- `dotnet build src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj -c Release` : 0 erreur, et aucun avertissement neuf (2 CS8604 préexistants dans `TrayApplication.cs`).
- Les trois suites, une par une :
  - `dotnet test src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj -c Release`
  - `dotnet test src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj -c Release`
  - `dotnet test src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj -c Release`
  - Toujours rapporter les trois compteurs. Le 25/09, la base était à 18 / 257 / 641.
  - Si la sortie contient `Could not load file or assembly` ou `0x800711C7`, c'est Application Control : le dire, ne pas conclure, laisser la CI trancher.
- `python -m unittest discover -s scripts/tests`, `python scripts/list-identity-literals.py` et `python scripts/check-doc-versions.py`.
- **Logique neuve ou déplacée** : un témoin, et une mutation qui le fait rougir. Modèle : `scratchpad/correctifs/mutations.py`, restauration à l'octet près. **Suppression pure** : le build et les suites suffisent.

## Commits

- `git add -- <fichiers neufs>`, puis `git commit -F <fichier message> -- <chemins explicites>`. Jamais `-A`, `-a`, `--amend`, `reset`, `clean` ni `stash`.
- Ne pas toucher aux changements des autres sessions : `docs/audit-2026-09-22-v1.3.0/feu-vert/evidence/recette-ba587ccee059/installation.txt` modifié, `Archives/` et d'autres fichiers non suivis sous `docs/`.
- Style des messages : sujet `1.3.0 : <ce qui change>`, un corps court qui cite les constats (ex. « Audit du 25/09, X-03 : … »), puis la ligne vide et `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Un commit par groupe cohérent.
- **Ne pas pousser** : la session principale pousse après relecture.

## Compte rendu (réponse finale)

- Commits (sha et sujet), fichiers et lignes (`git diff --stat`) ;
- les trois compteurs et les autres contrôles ;
- les mutations (rouges attendus et obtenus) ;
- ce qui a été écarté, et pourquoi (garde-fou, doute, dépendance à un autre lot) ;
- les lignes de Changelog proposées et les lignes de recette à ajouter ;
- les risques restants. En français.
