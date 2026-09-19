# Recette VM 1.2.0 — feuille de résultats

Compagnon de `recette-vm.md`, qui est le protocole. Ce fichier est le **compte
rendu** : une ligne par scénario, remplie au moment de l'exécution.

⛔ Un scénario non renseigné vaut **NON TESTÉ**, jamais « probablement bon ».

## Paquets de la campagne

| | |
|---|---|
| Candidat | `msix/AZERTYGlobal-1.2.0.0.msixbundle` — 6 798 518 o, 2026-09-19 09:46:20 |
| SHA-256 exe x64 | `4D8B82F579692F86D4F4FA682642F0957BB2B339B5AAAB56CE7531AFDE375E6D` |
| SHA-256 exe arm64 | `4D64569A07375568E6855F534871A80D006CB073260AB25144EB4E8F203290AC` |
| Commit | `2f5bb68` (branche `release/1.2.0-notation-store`) |
| Sideload 1.2.0 | `Archives/local-signing/1.2.0.0/AZERTYGlobal-1.2.0.0-local-signed-20260919.msixbundle` |
| Sideload 1.1.0 | `Archives/local-signing/1.1.0.0/AZERTYGlobal-1.1.0.0-resigned-20260919.msixbundle` |
| Certificat | `8086B18C82671DB12B366A60CD55D8EA3DB67DF0`, expire le 2027-08-18 |
| VM | `AZERTY-Test`, génération 2, 12 Go, Hyper-V sur `C:` |

⛔ Le candidat soumis au Store n'est **pas** signé ; les deux paquets sideload
ne servent qu'à la recette. Les binaires y sont identiques — ce que la
signature ne change pas.

## ⚠️ Deux limites à connaître avant de commencer

1. **La mise à jour Store→Store ne peut pas être testée ici.** La 1.1.0 servie
   par le Store est signée Microsoft ; poser par-dessus un paquet signé
   localement échoue sur la signature. VM-02 se joue donc sur la paire
   sideload, qui porte le **même** PFN `AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg`
   (cf. `manifestes-bundle-2026-09-19.md`) : la continuité du `TaskId`, des
   réglages et de la progression est bien vérifiée, le mécanisme Store ne
   l'est pas.
2. ⛔ **Hyper-V sur ce poste x64 ne peut pas exécuter d'invité ARM64.** Les
   lignes ARM64 de la recette (VM-21 et l'exécution réelle exigée au § 1)
   demandent une machine ARM physique. Aucune VM ne lèvera ce point.

## § 2 — Tests indispensables à la décision de soumission

| ID | Résultat | Date | Preuve / note |
|---|---|---|---|
| VM-01 | NON TESTÉ | | |
| VM-02 | NON TESTÉ | | voir limite 1 |
| VM-03 | NON TESTÉ | | |
| VM-04 | NON TESTÉ | | |
| VM-05 | NON TESTÉ | | |
| VM-06 | NON TESTÉ | | |
| VM-07 | NON TESTÉ | | |
| VM-08 | NON TESTÉ | | |
| VM-09 | NON TESTÉ | | |
| VM-10 | NON TESTÉ | | |
| VM-11 | NON TESTÉ | | |
| VM-12 | NON TESTÉ | | |
| VM-13 | NON TESTÉ | | |
| VM-14 | NON TESTÉ | | |
| VM-15 | NON TESTÉ | | |
| VM-16 | NON TESTÉ | | |
| VM-17 | NON TESTÉ | | |
| VM-18 | NON TESTÉ | | |
| VM-19 | NON TESTÉ | | |
| VM-20 | NON TESTÉ | | priorité : couche sécurisée sur champ de mot de passe |
| VM-21 | **PARTIEL** | 2026-09-19 | identité, version, architectures, contenu et absence de signature vérifiés hors VM : `manifestes-bundle-2026-09-19.md` ; WACK PASS : `bundle-msix-2026-09-19.md`. ⛔ Reste dû : le volet ARM64 exécuté, cf. limite 2 |
| VM-22 | NON TESTÉ | | |
| VM-23 | NON TESTÉ | | **priorité** : retours partiels de `SendInput` |

## § 3 — Parcours de retour utilisateur

| ID | Résultat | Date | Preuve / note |
|---|---|---|---|
| RET-01 | NON TESTÉ | | |
| RET-02 | NON TESTÉ | | |
| RET-03 | NON TESTÉ | | |
| RET-04 | **NON TESTÉ — décidé** | 2026-09-19 | ⛔ Hors périmètre de cette campagne : la VM est installée avec un **compte local**, sans compte Microsoft (décision d'Antoine du 2026-09-19). L'avis intégré Store ne peut pas être exercé sans session Store. À rouvrir après publication, sur poste réel |
| RET-05 | NON TESTÉ | | active le CLSID de toast vérifié au verrou 4 |
| RET-06 | NON TESTÉ | | dépend d'un état 1.1 réel, donc de VM-02 |
| RET-07 | NON TESTÉ | | |
| RET-08 | NON TESTÉ | | Narrateur, clavier seul, contraste |
| RET-09 | NON TESTÉ | | session longue |

## Préparation de la VM

⛔ **État au 2026-09-19 : `AZERTY-Test` est une coquille vide.** Le VHDX pèse
4 194 304 octets (4 Mo) pour 127 Go alloués — disque dynamique créé, jamais
démarré. Aucun système, aucun instantané. Rien de la recette ne peut commencer
avant l'installation de Windows 11.

Choix arrêtés le 2026-09-19 : **ISO officiel Microsoft** (pas l'image dev
Quick Create, qui embarque de l'outillage et expire à 90 jours) et **compte
local** (d'où RET-04 clos en NON TESTÉ ci-dessus).

0. Installer Windows 11 depuis l'ISO officiel, compte local.
1. Démarrer `AZERTY-Test`, vérifier l'édition et la version de Windows, la
   noter ici.
2. **Instantané « propre »** avant toute installation.
3. Importer le certificat dans l'invité, en **Personnes de confiance** de la
   machine locale — `Archives/local-signing/1.2.0.0/AZERTYGlobal-local-test.cer`.
4. Installer le sideload 1.1.0, exercer l'application, **second instantané**
   « état 1.1.0 » : c'est la base de VM-02 et de RET-06.
5. Installer le candidat 1.2.0 par-dessus.

⚠️ Sans l'instantané de l'étape 2, VM-01 et VM-22 ne sont pas rejouables : une
VM déjà utilisée n'est plus une VM propre.

## Porte de décision

Reprendre le § 5 de `recette-vm.md`. ⛔ Ni ce fichier ni un bundle vert ne
valent décision : la soumission se décide sur des lignes renseignées.

⚠️ L'échéance « avant le 17 septembre » du protocole est dépassée depuis le
2026-09-17. Elle n'a pas été retenue ici : aucune date de soumission n'est
posée tant que cette grille est vide.
