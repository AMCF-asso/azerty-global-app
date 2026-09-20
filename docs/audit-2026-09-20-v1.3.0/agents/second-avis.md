# Second avis — revue adversariale du verdict et du classement (audit v1.3.0, `4d5da24`)

Lecture seule. J'attaque le **§1** et le **§2**, pas l'audit.
**CONFIRMÉ** = exécuté, ou je cite la ligne qui le prouve. **PLAUSIBLE** = lu, non exécuté.

## 1. Les quatre bloquants

### B1 — bundle antérieur au code
**Tient.** Reproduit — mais la preuve citée n'est pas la bonne.

CONFIRMÉ. Publish x64 `17:26:17`, arm64 `17:26:21`, bundle `17:27:22`. Sept fichiers de `src/` sont plus
récents que l'exe publié (`find -newer`) : `ConfigManager.cs` 19:02:17, `TrayApplication.cs` et
`TrainingReminders.cs` 18:59:45, `ProductIdentity.cs` 18:59:44, `lessons.json` 18:59:45
(`EmbeddedResource`, `AZERTYGlobal.csproj:75`), plus 3 fichiers de test.

- **La preuve est le `mtime`, pas le `git log`.** Un horodatage de commit est postérieur à l'écriture :
  `404d68a` est commité à **17:30:30**, après le publish, et son contenu **est** pourtant dans le binaire
  (`SettingsWindow.cs` mtime 17:20:47, `KeyboardHook.cs` 17:05:43). Le rapport a raison de ne nommer que
  trois commits, mais son raisonnement tel qu'écrit en désignerait quatre.
- **Le maillon publish ↔ bundle est sain, et mesurable.** Les deux `.msix` extraits du bundle en mémoire
  donnent `df69897528d0171e…` (x64) et `a36f74c2eef995f3…` (arm64) — SHA-256 **identiques** aux exes de
  `publish/`. Le seul maillon rompu est source → publish, là où le rapport le dit.

Réserve : un bundle périmé **n'empêche pas** la soumission, il la rend fausse. Il est bloquant parce que `d356841` (rattrapage `reviewPromptDone`) vise le parc 1.1.0 installé, raison d'être de cette release.

### B2 — CI rouge sur `release/**`
**Tient avec nuance : bloquant de processus, pas de soumission.**

CONFIRMÉ. `check-doc-versions.py` → **exit 1**, « 9 erreur(s), 32 attente(s) ». `ci.yml` déclenche bien
sur `release/**`, et `unittest discover -s scripts/tests` passe **avant** `Setup .NET 8 SDK`, les publish,
les trois `dotnet test`, `Pack-MSIX` et `Verify-Release`.

Ce que ça ne prouve pas : le bundle soumis n'est **pas** produit par la CI — il l'est en local par
`Pack-MSIX.ps1`, et il existe sur le disque. La CI rouge bloque l'attestation et la porte verte
qu'Antoine s'est donnée, pas l'upload Partner Center. À garder Bloquant (une porte est une décision), en
corrigeant le libellé. Et le correctif chiffré « une heure » ne solde que les **9 erreurs** : les 32
attentes restent, dont `EMPREINTE-EN-ATTENTE : … document non publiable en l'état` (§4.2).

### B3 — recette VM incomplète
**Tient.** Non revérifié par moi (pas de VM). Seule étiquette littérale des quatre : la recette du 19/09
déclare l'écart 5 bloquant, et AG130-06 — que je **confirme** au §2 — rouvre l'écart 8 que `404d68a`
prétend fermer. B3 ne se lève donc pas par un simple rejeu : AG130-06 se décide d'abord.

### B4 — politique de confidentialité datée 2026-07-11
**Ne tient pas comme bloquant de soumission ; tient comme obligation de conformité.**

Pour : 10.5.1 exige une politique « kept up-to-date as you add new features », et l'URL déclarée est bien
celle auditée (`msix/Fiche Store.md:277` → `/mentions-legales`, CONFIRMÉ). La 1.3.0 écrit des compteurs et
un journal de compatibilité que le § 4.2 ne décrit pas. Hors Store, c'est une information RGPD inexacte
publiée par une association française.

Contre : la certification vérifie que le champ est renseigné et que l'URL répond, jamais le texte contre
le jeu de fonctions. Aucune donnée ne sort de la machine (le rapport le dit lui-même) : rien ne déclenche
la revue manuelle qui verrait l'écart. Un paquet soumis demain passerait.

Tranché : **Majeur hors dépôt, à faire avant publication, pas avant soumission.** Conséquence
opérationnelle nulle (30 min, hors chemin critique) — mais une étiquette fausse affaiblit les trois autres.

## 2. Un Majeur qui devrait être Bloquant

**AG130-06.** Confirmé par lecture directe, dans les **deux** sens — le rapport n'en décrit qu'un.

CONFIRMÉ, `src/TrayApplication.cs`. `OnForegroundChanged` n'a que deux branches :
`mode == DisabledAntiCheat && !_suspendedForCompatibility` (l.2239) et
`mode != DisabledAntiCheat && _suspendedForCompatibility` (l.2275). Une transition directe entre deux
applications qui suspendent toutes les deux (`mode` constant, seul `reason` change) ne tire **aucune**
branche → `ApplyHookState()` (l.490-515) n'est pas rappelé → `ShortcutsWhilePassThrough` garde sa valeur
précédente. L'événement est pourtant bien émis : `ForegroundMonitor.cs:238-245` notifie dès que
`processName != null`, même à mode constant. Aucun rattrapage ailleurs (`grep ApplyHookState` : 1538,
1591, 1602, 2249, 2281, 2289 — rien sur ce chemin).

- Sens 1 (celui du rapport) : app en `UserOverride` → app anti-cheat. Raccourcis **armés** sous
  anti-cheat : exactement ce que l'« inertie totale » de `KeyboardHook.cs:74-78` promet d'empêcher, et ce
  que `ApplyCompatibilityOverride` (l.2319-2323) refuse explicitement à l'utilisateur.
- Sens 2, **absent du rapport** : app anti-cheat → app en `UserOverride`. Raccourcis restés **désarmés**,
  ce qui rend l'écart 8 d'origine — dont le commentaire de `ApplyHookState` (l.501-502) dit lui-même que
  le geste réel « détruisait le travail en cours » (Ctrl+Maj+W lu Ctrl+W par la cible).

Population identifiable, perte de données dans un sens, risque de bannissement dans l'autre, **zéro test**. Sous la grille « empêche la soumission », aucun des AG130-05→14 et 40→42 n'est bloquant : c'est la grille qu'il faut corriger, pas le classement.

## 3. Un Bloquant ou Majeur surclassé

**AG130-07 — tient avec nuance : le fait est exact, un tiers de sa justification est faux.**
CONFIRMÉ pour le fait : `KeyMapper.cs:26` ne déclare que `LLKHF_EXTENDED = 0x01`, aucune occurrence de
`LLKHF_INJECTED` dans `src/` ; l'auto-reconnaissance passe par `dwExtraInfo == INJECTED_FLAG`
(`KeyboardHook.cs:161`), valeur **aléatoire à chaque démarrage** (l.23-30).
CONFIRMÉ pour l'erreur : `GameRegistry.RemoteAccessProcesses` contient `mstsc.exe`, `msrdc.exe`,
`msrdcw.exe`, `parsecd.exe`, `AnyDesk.exe`, `TeamViewer.exe`, `rustdesk.exe`, et déclenche une suspension
complète. **Le Bureau à distance est déjà couvert par un autre mécanisme** : le citer ici est un doublon.
Restent AutoHotkey et le clavier visuel : le Majeur tient, sa justification est à réécrire.

**AG130-40 — tient avec nuance : constat mécanique exact, conclusion trop absolue.**
CONFIRMÉ : `grep IsDialogMessage|TranslateAccelerator src/` → **0**, et la boucle de
`TrayApplication.cs:546-551` est bien `GetMessageW → TranslateMessage → DispatchMessageW` nus ; seule
`SettingsWindow` traite `VK_TAB` à la main (`:1610-1621`, `:1654`).
Ce qui ne tient pas : « Un utilisateur au clavier seul ne traverse pas les fenêtres ». `OnboardingWindow`
sous-classe ses contrôles et route Entrée (`:1028-1035`) et les flèches (`:1047-1062`), et son propre
commentaire (`:1044-1046`) constate que **Windows déplace déjà le focus entre boutons tabulables avec les
flèches**, sans dialog manager. Réellement prouvé : Tab est inerte hors Paramètres, et `BS_DEFPUSHBUTTON`
(7 sites) n'est pas activé par Entrée. Non affirmable sans runtime : qu'aucun contrôle ne soit atteignable.

**Surclassement le plus net : AG130-41.** Gradé Majeur, sa propre cellule de preuve concède que « les
contrôles Win32 natifs restent, eux, accessibles » — exact, le fournisseur UIA pour HWND les expose sans
code. « Zéro UIA » ne décrit que les zones dessinées. Mineur + chantier 1.4.

## 4. Ce que le rapport n'a pas regardé

1. **Les quatre captures du Store.** `msix/Fiche Store.md:359-370` déclare `Screenshot1..4.png` ; sur
   disque : 2026-04-01, 2026-06-07, 2026-04-06, **2026-03-21** — toutes antérieures à la 1.2.0 et à la
   1.3.0, quand la clause 10.1 exige qu'une capture reflète le produit. Le §4 « Conformité » traite 10.1.4 avec le
   Changelog et le README seulement. À ouvrir : `msix/Fiche Store.md`, `msix/Assets/`.
2. **Les 32 attentes de `check-doc-versions.py`.** Trois documents du kit DSI portent
   `empreintes-attendues: en-attente`, que le contrôle qualifie de « non publiable en l'état », et vingt
   lignes `NOM-BUNDLE-PERIME` nomment encore le bundle 1.1.0. Le correctif B2 les laisse intactes : CI
   verte et canal entreprise toujours bloqué. À ouvrir : `sources/legacy/…/SIGNATURE.md`, `…/LISEZMOI-DSI.md`.
3. **La montée de version depuis le parc 1.1.0 installé.** Seul `reviewPromptDone` est audité (AG130-16) ;
   la lecture d'un `config.json` et d'un `usage-stats.json` écrits par la 1.1.0 n'est éprouvée nulle part,
   alors que c'est ce que vivront 100 % des utilisateurs le jour de l'acceptation. À ouvrir :
   `src/ConfigManager.cs`.

## 5. Une phrase qui affirme plus que sa preuve

> « **Couverture : 21 fichiers de production sur 58 ne sont nommés par aucun test, 9 572 lignes sur
> 27 653 (34,6 %).** » (AG130-13)

L'agent 05 §2 écrit sa méthode et sa limite : on compte les fichiers de test qui **nomment** un type, et
« ⚠️ Ce que cette mesure ne dit pas : nommer un type n'est pas l'éprouver. Elle borne le **haut**, jamais
le bas. » Le rapport supprime la réserve et rebaptise le nombre « Couverture ». C'est un chiffre qui sera
recité, et il ne mesure aucune couverture.

Même travers au §1 : « **aucun appel réseau sortant** dans `src/` » — puis, dans la même phrase, « et
l'API d'avis du Store ». Un grep de quatre types .NET ne voit pas ce qui part par WinRT.

## Réserve de méthode

Je partage la famille de modèle de la session qui a rédigé ce rapport, donc ses angles morts. À passer à
un autre fournisseur avant clôture : **AG130-06** (machine à états, lue et non exécutée) et **B4**
(lecture d'une politique Store, où deux modèles de la même famille se trompent volontiers ensemble).

Non vérifié faute d'accès : la recette VM et le WACK, le rendu réel des fenêtres, `IsDialogMessage` au
runtime, la page `/mentions-legales`, et AG130-08, 09, 10, 11, 42, sur lecture d'agent seule.
