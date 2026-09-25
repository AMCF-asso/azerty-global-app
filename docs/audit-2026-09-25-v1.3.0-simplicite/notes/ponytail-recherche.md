# Ponytail : recherche sourcée (2026-09-25)

Légende : [S] page ouverte et lue ; [E] non ouverte (seulement un extrait de recherche ou échec de lecture).
Mise en garde : WebFetch passe les pages par un modèle résumeur. Les citations viennent de sa sortie : elles sont en principe littérales, mais une légère reformulation reste possible. Les chiffres clés sont à revérifier sur la page avant de les reprendre ailleurs.

## 1. Mesures indépendantes

### JetBrains AI Blog, partie 3 (Denis Shiryaev) [S]
URL : https://blog.jetbrains.com/ai/2026/07/ponytail-skill-claude-tested/ (aucune date affichée ; l'URL indique 2026/07)
- Protocole : "Harbor 0.18 — Docker sandboxes, task verifiers, paired runs".
- Benchmark : "SkillsBench, 80 paired tasks, auto-graded 0–1 with partial credit".
- Modèle : `claude-sonnet-5` avec un effort de raisonnement **medium**. Claude Code 2.1.201 en mode headless, `bypassPermissions`. Ponytail v4.8.4.
- Bras traité : "skill installed _and_ its ruleset injected, byte-identical to ... SessionStart hook".
- Volume : "251 billed agent trials", "USD 246.09". Trois étapes : un smoke test sur 10 tâches, les mêmes 10 tâches à k=3, puis les 80.
- Résumé : "Measured: −15% code, −10.3% cost and -11% time." Aucune p-value n'est donnée pour le temps.
- Coût : "typical task cost 10.3% less", "p=0.004 across 80 pairs". La tâche est moins chère dans 46 cas sur 80 et plus chère dans 34.
- Code : "shed 15.4% of the code", "10,205 lines became 8,756". Significativité : "p=0.088".
- Stratification : −31 % sur les grosses constructions, environ zéro sur les constructions déjà minimales.
- Jetons : relecture (cache) "fell 8.4%" (p=0.138), jetons neufs 3.9 % (p=0.085). Aucune des deux baisses n'est significative.
- Dispersion : "a bootstrap interval on the median just touches zero".
- Qualité : "Nine tasks scored slightly worse, six slightly better, 65 identical". Ce résultat est "a null result, not a clean bill of health". Prouver l'équivalence demanderait "several hundred paired tasks per arm rather than 80".
- Auto-activation : installé comme simple skill, "it self-activated zero times" sur 10 sessions. Les chiffres viennent donc du bras où le hook injecte les règles.
- Marqueurs : les commentaires `ponytail:` exigés par les règles sont apparus "once" sur 80 essais.
- Contrôle de contamination : "100% of treatment trials, 0% of baselines".
- Biais de tâches : SkillsBench "contains few of the front-end over-build traps". Le test est "a conservative test of the code claim".
- Biais de comptage : dans 7 tâches, la référence passe le code par heredoc sans l'écrire sur disque, alors que ponytail l'écrit.
- Conclusion de l'article : "Ponytail works", "you should be modestly better off".

### Autres évaluations
- **KuldeepB19**, issue #236 (22 juin 2026) [S] : https://github.com/DietrichGebert/ponytail/issues/236 et le dépôt https://github.com/KuldeepB19/ponytail-benchmark [S]. Le tableau de bord https://kuldeepb19.github.io/ponytail-benchmark/ renvoie une 404.
  - Protocole : "480 builds: 24 jobs, 4 levels", "5 reps each, on Opus 4.8". Notation par exécution du code.
  - Code : "about 44% less code at the default Full level (40% at Lite, 46% at Ultra)".
  - Correction : "99% pass both ways" ; mêmes attaques bloquées (path traversal, SQL, jetons signés).
  - Robustesse : "hit 5 of the 24 jobs", "fell from near-perfect to near-zero". Aucun coût sur les 19 autres jobs.
  - Effet du niveau : "the strongest setting is the most fragile".
  - Les langages des jobs ne sont pas indiqués.
- **Colin Eberhardt (Scott Logic)**, issue #126 (18 juin 2026) [S] : https://github.com/DietrichGebert/ponytail/issues/126
  - Il dénonce une référence bavarde : 108 lignes en moyenne pour la référence, "16" avec un prompt système, "8.25" pour ponytail.
  - Conclusion : "the central claims of this project should be a bit more modest".
- **InfoQ** (Steef-Jan Wiggers, 5 août 2026) [S] : https://www.infoq.com/news/2026/08/ponytail-agent-skill-benchmark/
  - Le README a été révisé : l'ancien chiffre était "a per-task ceiling misreported as an average".
- **Hacker News** [S] : https://news.ycombinator.com/item?id=49094075
  - 4 points et un seul commentaire, sans mesure : "reads so closely to AI slop".
- **Issue #65** [S] : https://github.com/DietrichGebert/ponytail/issues/65
  - Demande de résultats SWE-Bench Pro et Terminal Bench 2.1 : "none ever measure performance". Fermée sans données.
- **Tweet de Shiryaev** [E] (HTTP 402) : https://x.com/literallydenis/status/2082820864706248912
  - Extrait de recherche : "saves around 10% on output tokens", "Ponytail doesn't suit me". La raison n'a pas pu être lue.

## 2. Méthodologie du dépôt

- **benchmarks/README.md** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/benchmarks/README.md
  - Single-shot : "10 executions per cell", médiane.
  - Tâches : email validator, debounce, somme CSV, React countdown, FastAPI rate-limit.
  - Validation : juge d'exécution d'un côté, contrôle par regex structurelle de l'autre (React, FastAPI).
  - Réserves : l'écart initial "overstates the win" ; ponytail "can also raise tool calls and cost on completion-forced tasks".
- **benchmarks/agentic/README.md** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/benchmarks/agentic/README.md
  - Palier LOC : 12 fonctionnalités mesurées par `git diff`. Palier Safety : "7 surgical tasks".
  - Safety est une "Safety gate" : le code survit à des entrées adverses "deterministic, stdlib-only".
  - Juge de sur-ingénierie : Claude Sonnet 4.6, température 0, échelle 0–3. Les juges passent `--selftest`.
  - Taille : "n=4 runs per configuration".
  - Limite : "a deterministic safety check is a floor, not a proof of security".
- **2026-06-18-agentic.md** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/benchmarks/results/2026-06-18-agentic.md
  - Haiku 4.5, Claude Code 2.1.177, "n=4 runs per (task, arm)".
  - Dépôt de test : `tiangolo/full-stack-fastapi-template` (FastAPI + React).
  - LOC : 191 → ~88 (−54 %). Date picker : 404 → 23. Color picker : 287 → 23.
  - Backend en lignes (référence → caveman → ponytail) : Search 44→44→44, CSV export 36→36→33, Count 21→20→17.
  - Safety : "5 security tasks × 4 runs = 20 runs per arm". Référence 20/20, ponytail 20/20, yagni-oneliner 19/20.
  - Contamination : "that hook was firing on every arm, including the baseline".
  - Autres réserves : un seul modèle ; 4 cellules en timeout ; LOC frontend variable (300–570) ; le yagni-oneliner est une paraphrase.
- **2026-06-17-agentic-safety.md** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/benchmarks/results/2026-06-17-agentic-safety.md
  - "6 tasks × 5 arms × 3 models × 5 runs = 450". Langage : Python. Modèles : Haiku 4.5, Sonnet 4.6, Opus 4.8.
  - Résultats : ponytail 100 %, référence 100 %, yagni-oneliner 94.4 %.
  - Sur-ingénierie : "zero of 450 cells flagged", tous bras confondus. Le juge ne distingue donc rien sur ces tâches.
  - Le document est marqué "superseded".
- **2026-06-16-robustness-audit.md** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/benchmarks/results/2026-06-16-robustness-audit.md
  - Portée : "12 classic edge-case traps", n=20 par cellule.
  - Résultat : "baseline 20/20 == ponytail 20/20". Le langage n'est pas indiqué.
- **2026-06-22-issue-245-217-comprehension.md** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/benchmarks/results/2026-06-22-issue-245-217-comprehension.md
  - Tâche : `bank.py` (Python), n=6. Résultat : "Sonnet 4.6: baseline 1/6 (0.17) → ponytail 6/6".
  - Issue #217 : l'échelon « réutiliser l'existant » a été livré, mais "benefit unproven".
- **PR #903** [S], fermée sans fusion le 19 septembre 2026 : https://github.com/DietrichGebert/ponytail/pull/903
  - Le bras ponytail "loaded the plugin from ~/.claude/plugins/cache".
  - Les résultats "don't record which ponytail was measured".
- **Langages couverts** : Python, JS/TS/React. Aucune mention de C# ni de Rust n'a été trouvée dans les pages lues.

## 3. Effets de bord connus

### Hooks
- **claude-codex-hooks.json** [S] (966 octets) : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/hooks/claude-codex-hooks.json
  - SessionStart sur "startup|resume|clear|compact", plus SubagentStart et UserPromptSubmit. Timeout de 5 s.
- **ponytail-activate.js** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/hooks/ponytail-activate.js
  - Lit `settings.json`.
  - Écrit `$CLAUDE_CONFIG_DIR/.ponytail-active` et `.ponytail-statusline-nudged`.
  - Injecte les règles et affiche une seule fois une suggestion de statusline.
  - Aucun appel réseau ni télémétrie détecté.
- **ponytail-instructions.js** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/hooks/ponytail-instructions.js
  - Lit `skills/ponytail/SKILL.md`, retire le frontmatter et filtre selon le mode.
  - En cas d'échec, bascule sur un texte de repli qui contient "ACTIVE EVERY RESPONSE. No drift back to over-building."
- **ponytail-mode-tracker.js** [S] : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/hooks/ponytail-mode-tracker.js
  - Ne réagit qu'à `/^[/@$]ponytail/` et aux commandes de désactivation.
  - Sur un prompt ordinaire sous Claude ou Codex, il ne produit aucune sortie. Sous Qoder, il réinjecte les règles à chaque prompt.
- **ponytail-subagent.js** [S] :
  - Injecte `getPonytailInstructions(mode)` dans chaque sous-agent.
  - Un filtrage est possible avec `PONYTAIL_SUBAGENT_MATCHER`. En cas d'erreur ou de timeout, l'injection a lieu quand même.
- **ponytail-config.js** [S] :
  - Lit les variables `PONYTAIL_DEFAULT_MODE`, `PONYTAIL_QUIET_STARTUP`, `PONYTAIL_HIDE_STATUS`, `APPDATA`, `CLAUDE_CONFIG_DIR`.
  - Écrit `%APPDATA%\ponytail\config.json`. Mode par défaut : `'full'`. Aucun réseau.
- `ponytail-runtime.js` (5682 octets) n'a pas été ouvert [E].

### SKILL.md [S]
URL : https://raw.githubusercontent.com/DietrichGebert/ponytail/main/skills/ponytail/SKILL.md
- Persistance : "ACTIVE EVERY RESPONSE."
- Garde-fou : "Never simplify away: input validation at trust boundaries, error handling that prevents data loss".

### Issues
Aucune issue ne porte le label `bug` (recherche [S]).
- **#647** (Windows, 29 juillet) [S] : UserPromptSubmit "median 6.4s, p95 7.0s, 5 runs hit the 5s timeout".
- **#791** (ouverte, v4.9.0) [S] : une fenêtre console clignote à chaque appel de hook sous Windows.
- **#857** (fermée) [S] : `commandWindows` "silently discarded" ; exécution via Git Bash ; crash MSYS2 quand plusieurs plugins ont des hooks.
- **#597** (ouverte, 15 juillet) [S] : SubagentStart envoie le SKILL.md complet.
- **#626** (ouverte) [S] : sous Codex, les changements de mode "repeatedly inject full rulesets". Le corps du skill peut dupliquer les règles injectées.
- **#764** (27 août) [S] : sous Codex, "Invalid prompt ... flagged" par intermittence, disparu après désinstallation.
- **#735** [S] : clone piégé `0xwilliamortiz/ponytail-improved` (DLL side-load sous Windows). Le signalement précise que le vrai dépôt "does not ship any .exe/.dll".
- **#658** [E, titre seul] : `PONYTAIL_SUBAGENT_MATCHER` "compiled into RegExp without validation (crash ... + ReDoS)".
- **#419 / #406** [E, titres seuls] : "error handling silently omitted, not listed in Skipped section" (fermées, 27 juin).
- **#120** [S] : "Does it always have to add 'ponytail:' ... in each ... commit name?" Fermée sans réponse visible.

## 4. Adéquation (faits seulement)

- **(a) Audit d'un code mûr**
  - #679 (ouverte) [S] : l'audit a déclaré "zero src callers" alors qu'environ 35 assertions de test utilisaient la méthode. Aucune vérification sur tout l'arbre n'est exigée, et le commit de base n'est pas enregistré.
  - #866 (ouverte) [S] : "Replacement: nothing" masque ce que la suppression laisse derrière elle (commentaires de justification, tests à réorienter).
  - #682 (ouverte) [S] : l'audit n'a pas de passe pour "blanket try/except / catch wrappers" ; la proposition est "flatten, don't delete".
  - #683 [E, titre seul] : pas de passe sur la fragilité des tests.
  - #660 (ouverte) [S] : "Shortest working diff wins" contesté au profit de "Minimize concepts, contracts".
  - KuldeepB19 [S] : robustesse dégradée sur les cas limites non énoncés.
- **(b) Rust neuf**
  - Aucune donnée C#/Rust trouvée. Le README ne mentionne aucun langage hors JS/TS/Python (constat du lecteur : "absent").
  - #810 [S] : `ponytail-debt` utilise le motif `(#|//) ?ponytail:`. Il ignore les commentaires de bloc `/* */`.
- **(c) Site statique**
  - Les plus gros gains viennent de composants frontend remplacés par un input natif (date picker, color picker).
  - Question ouverte : les composants d'un design system existant sont-ils respectés ? Voir https://dev.to/yashddesai/ponytail-the-ai-coding-skill-taking-github-by-storm-and-the-one-question-nobodys-answered-yet-46mc [S] ("the benchmark tested ... without component libraries").
- **(d) Harnais qui a déjà ses règles**
  - JetBrains : le skill seul ne s'active jamais ("zero times"). L'effet mesuré passe par l'injection du hook.
  - La règle des marqueurs `ponytail:` n'a été suivie qu'"once" sur 80 essais.
  - Côté dépôt : 7 mots de YAGNI donnaient 94.4–95 % de safety contre 100 % pour ponytail, sur Python et FastAPI.
  - #126 [S] : une seule consigne de prompt système ramène la référence de 108 à 16 lignes.
- **Usage réel de ponytail-audit** : les seuls retours trouvés sont les issues #679, #682, #683, #866 et #868. Aucune mesure publiée.

## 5. Coût en contexte

- `skills/ponytail/SKILL.md` pèse "size: 6637" octets (API GitHub [S] : https://api.github.com/repos/DietrichGebert/ponytail/contents/skills/ponytail).
- Charge injectée en mode full, d'après #597 [S] (https://github.com/DietrichGebert/ponytail/issues/597) : "5,229 characters (~1,300 tokens)". Texte de repli condensé : "2,680 characters / ~670 tokens".
- Fréquence :
  - À chaque SessionStart : startup, resume, clear et compact, donc réinjection après chaque compaction.
  - À chaque sous-agent : "a single 50-spawn swarm alone pays ~65k tokens" (#597).
  - Aucune injection sur un prompt ordinaire sous Claude ou Codex (mode-tracker [S]). Sous Codex, #626 signale des réinjections complètes à chaque changement de mode.
- Latence Windows par prompt : "median 6.4s" (#647).
