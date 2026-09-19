# Scan de vulnérabilités NuGet — 2026-09-19

Lève le point 2 de `corrections.md` § 4 (« Rétablir la vérification des
vulnérabilités des dépendances »), resté ouvert le 2026-09-15 parce que
l'index NuGet était inaccessible (NU1900).

## Ce qui a été exécuté

Depuis `components/microsoft-store/src`, sur les six projets :

```powershell
dotnet restore <projet> --force
dotnet list <projet> package --vulnerable --include-transitive
```

Index joignable cette fois : chaque exécution imprime
`The following sources were used: https://api.nuget.org/v3/index.json`.
Aucun NU1900. Le scan est donc réel, pas un silence d'index — la distinction
compte, les deux rendent une liste vide à l'œil.

## Résultat

| Projet | Livré au Store | Verdict |
|---|---|---|
| `AZERTYGlobal` | oui | aucune vulnérabilité |
| `TypingEngine.Core` | oui | aucune vulnérabilité |
| `TypingEngine.Windows` | oui | aucune vulnérabilité |
| `AZERTYGlobal.Tests` | non | 2 × High, transitives |
| `TypingEngine.Core.Tests` | non | 2 × High, transitives |
| `TypingEngine.Windows.Tests` | non | 2 × High, transitives |

**Les trois projets qui composent le paquet Store sont propres.**

## Les deux avis, et d'où ils viennent

| Paquet | Version résolue | Sévérité | Avis |
|---|---|---|---|
| `System.Net.Http` | 4.3.0 | High | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) |
| `System.Text.RegularExpressions` | 4.3.0 | High | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) |

Chaîne identique dans les trois projets de test (`dotnet nuget why`) :

```
xunit (2.6.0)
└─ xunit.core (2.6.0)
   └─ xunit.extensibility.core (2.6.0)
      └─ NETStandard.Library (1.6.1)
         ├─ System.Net.Http (4.3.0)
         └─ System.Text.RegularExpressions (4.3.0)
```

`NETStandard.Library 1.6.1` est un méta-paquet de 2016 que xunit 2.6.0 traîne
encore ; sur `net8.0` ses assemblages ne sont pas utilisés à l'exécution, les
implémentations viennent du runtime.

## Décision — reportée après la v1.2.0

Pas de montée de xunit avant la soumission. Raison : les 609 tests sont verts
sur `release/1.2.0-notation-store` ; changer le harnais de test juste avant la
recette VM met en jeu la seule mesure de non-régression disponible, pour
corriger des avis qui ne touchent aucun binaire livré.

À faire sur la branche v2.0.0 : passer xunit en 2.9.x, qui a coupé la
dépendance à `NETStandard.Library`, puis rejouer les trois suites.

⚠️ À rejouer si la soumission glisse de plus de quelques semaines : un scan
n'est valable qu'à sa date.
