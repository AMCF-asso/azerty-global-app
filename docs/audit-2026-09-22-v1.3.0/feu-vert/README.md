# Feu vert Microsoft Store 1.3.0 — 22 septembre 2026 (soir)

Suite des [corrections de l’audit](../corrections/README.md). Arbitrages d’Antoine du jour : corriger ce qui se corrige et déroger nominativement au reste pour BinSkim ; Antoine exécute la recette et le WACK à partir de ce kit ; ARM64 publié avec l’écart documenté ; soumission visée sous 48 h. **Rien n’est soumis, tagué ni fusionné.**

## Candidat

**Candidat actuel (2026-09-23) :** run [35851382703](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35851382703), vert (18 / 235 / 507 tests C#, 147 Python, BinSkim sans alerte bloquante), commit `585672f` sur `ci/verif`, contenu de `34feb04`, bundle attesté `ba587ccee0597a96d63b99052b0fd26dfabfb3830a81a07cb463cb3e1bb0a5c4`. Le tableau ci-dessous décrit le candidat précédent, sans le lot d’accessibilité.

| Élément | Valeur |
|---|---|
| Source | `bd33c9d` sur `release/1.2.0-notation-store` ; contenu identique poussé en `9350947` sur `ci/verif` (décision 29) |
| CI | [35779820283](https://github.com/AMCF-asso/azerty-global-app/actions/runs/35779820283), **succès** : 18 + 235 + 473 = 726 tests C#, 147 tests Python, publications x64/ARM64, `Pack-MSIX`, `Verify-Release`, BinSkim, SARIF, attestation |
| Bundle CI attesté | `AZERTYGlobal-1.3.0.0.msixbundle`, SHA-256 `768f13fbe9e79e98cc56009e34496916ca352f84dbf31c1350c4be6506c4250c` |
| Bundle local | `62a4960c…` : même source, chemins de build différents, donc empreinte différente. Sert uniquement à valider le kit ; **ne pas soumettre** |

## BinSkim : ce qui a changé

- **BA2025 corrigé** : `/CETCOMPAT` est passé au linker x64 (`src/AZERTYGlobal.csproj`). Preuve (`evidence/cet/sandbox-cet-probe.json`) : dans Windows Sandbox sur i5-13600K, la 1.3.0 tourne avec **shadow stack ON et CFG ON**, rattrape la `JsonException` d’un `config.json` invalide et reste vivante. Le paquet installé du kit affiche le même état (`evidence/recette-62a4960c65c8/installation.txt`). Pas de `/guard:ehcont`, puisque ILC 8 n’émet pas les cibles de continuation.
- **BA2024, BA2026, BA2027, BA6006 dérogés** dans `scripts/check-binary-hardening.py`, uniquement au niveau `warning` et uniquement si leur condition tient sur le binaire analysé. Pour Spectre et SDL, chaque objet C/C++ compté par l’éditeur de liens (VC_FEATURE : 135 en x64, 122 en ARM64) doit appartenir aux sept bibliothèques Microsoft citées par BA2024. Les verdicts SARIF restent inchangés ; les dérogations appliquées sont écrites dans `binskim-sarif/derogations-<arch>.json`. Onze témoins nouveaux. À réexaminer lors de la migration vers .NET 10 : [.NET 8 n’est plus supporté après le 2026-11-10](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
- Le niveau SARIF effectif suit maintenant le §3.27.10 (résultat, puis règle, `none` hors `fail`). Sans cela, les avertissements hérités n’étaient pas reconnus.

**Anomalie non expliquée, consignée :** un petit témoin console ou WinExe .NET 8 NativeAOT avec `ControlFlowGuard=Guard` s’arrête au premier `throw` sur ce poste (0xC0000409, sous-code 0x0A). Le phénomène est identique avec ILCompiler 8.0.29 ou 8.0.31, avec ou sans `/CETCOMPAT`, avec ou sans le manifeste de l’app ; sans CFG, il passe (`evidence/cet/temoin/`). **L’application réelle ne le reproduit pas** : `throw` et `catch` dans `ConfigManager.EnsureLoaded` fonctionnent avec CFG, hors paquet sur l’hôte comme packagée dans le Sandbox. La 1.1.0 en production porte le même CFG et n’a laissé aucun plantage dans le journal de ce poste depuis le 28/07. La ligne A11 de la recette exerce ce chemin sur le paquet final.

## Partner Center — relevé en lecture seule du 2026-09-22 (23 h)

Aucun brouillon n’est ouvert ; la soumission 15 (modifiée le 02/09) est en ligne avec `AZERTYGlobal-1.1.0.0.msixbundle` (Desktop, minimum 10.0.17763.0).

| Élément | État en ligne | À faire lors de la soumission 1.3.0 |
|---|---|---|
| Notes de certification | Désormais sur la page produit « Additional Testing Info » ; texte encore « Update 1.1.0 — 2026-07-23 » | Remplacer par la section « Notes pour l’équipe de certification » de `msix/Fiche Store.md` |
| Nouveautés FR | « Version 1.1.0 : … » | Coller « Nouveautés de cette version » (FR) et « What’s new » (EN) de la fiche |
| Description | Valeur masquée à la lecture automatique : mention des dons non vérifiée | Recoller la description longue FR/EN de la fiche, qui mentionne l’AMCF et HelloAsso |
| Propriétés | Utilitaires ; données personnelles « oui », politique `https://azerty.global/mentions-legales` ; support `https://azerty.global` | Inchangé |
| Déclaration d’accessibilité | Cochée | **À décocher** (décision finale d’Antoine du 2026-09-22, 23 h) : correctifs rapides en 1.3.0, conformité complète en 1.3.1, puis on recoche. Voir [accessibilite-1.3.0.md](accessibilite-1.3.0.md) |
| Classification | IARC 3+ / PEGI 3 / ESRB Everyone | Inchangé, la 1.3.0 n’ajoute aucun appel réseau |
| Publication | « Dès la certification » | **Conservé** (décision du 2026-09-22) |
| Paquets | 1.1.0.0 seul | Ajouter le bundle final ; le Store sert la version la plus haute |

**Conséquence de la décision d’accessibilité :** le lot de correctifs rapides change le code, donc le bundle `768f13fb…` n’est plus le candidat de soumission. La recette et le WACK se feront sur le bundle produit après ce chantier ; le kit reste valable tel quel, seule l’empreinte change.

## À faire par Antoine, dans l’ordre

1. Récupérer le bundle CI du run ci-dessus et vérifier son empreinte (`gh attestation verify <bundle> -R AMCF-asso/azerty-global-app`).
2. `sandbox\lancer-recette.ps1 -Bundle <bundle>`, puis dérouler [recette.md](recette.md), partie A au minimum.
3. `wack.ps1 -Bundle <bundle>` dans un PowerShell administrateur.
4. Partner Center (arrêt humain) : téléverser **ce** bundle, reprendre les notes de certification de `msix/Fiche Store.md`, vérifier captures, IARC, confidentialité et mention des dons.
5. Après certification : tag `v1.3.0` et fusion, qui restent ta main.
