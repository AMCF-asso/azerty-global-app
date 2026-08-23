# AZERTY Global — Application Windows

> 🇬🇧 Windows companion app for **AZERTY Global**, the free, open-source (EUPL 1.2) corrected French keyboard layout — available on the [Microsoft Store](https://apps.microsoft.com/detail/9N4BTS43SSSZ). C# / .NET 8.0, native AOT. Project home: [azerty.global](https://azerty.global). README in French below.

Application AZERTY Global pour Windows, disponible sur le [Microsoft Store](https://apps.microsoft.com/detail/9N4BTS43SSSZ).

## Qu'est-ce qu'AZERTY Global ?

AZERTY Global est une disposition clavier française améliorée, alternative à l'AZERTY traditionnel de Windows (1984) et à la norme AFNOR (2019). Elle corrige les problèmes quotidiens du clavier français tout en conservant 99 % des habitudes existantes.

**Site web :** [azerty.global](https://azerty.global)

## L'application

L'application Windows permet d'utiliser AZERTY Global **sans installation système et sans droits administrateur**. Elle fonctionne en arrière-plan et intercepte les frappes clavier pour appliquer la disposition.

**État du code :** version 1.2.0, manifeste MSIX local en 1.2.0.0, portes de version de `Verify-Release.ps1` franchies. Aucun package n'a encore été produit ni soumis.

### Fonctionnalités

- **Remapping clavier complet** — 48 touches, 8 couches par touche, 29 touches mortes
- **Verrouillage Majuscule Intelligent** — N'affecte que les lettres : `É`, `È`, `Ç`, `À` en un appui
- **Clavier virtuel** — Visualisation interactive de la disposition
- **Recherche de caractères** — Trouvez n'importe quel caractère parmi les 1 000+ disponibles, avec insertion directe dans l'application d'origine
- **Couches maintenables** *(désactivées par défaut)* — Grec, cyrillique et scientifique en maintien ou en verrouillage par application, sans changer les touches mortes existantes
- **Module d'apprentissage** — Leçons interactives pour s'entraîner aux 5 améliorations
- **Interface française et anglaise** — Changement de langue à chaud dans toute l'application
- **Statistiques locales** — Compteurs agrégés conservés sur l'appareil, sans télémétrie réseau
- **Défi du jour facultatif** — Rappels et séances courtes, désactivés par défaut
- **Suspension automatique pour les jeux** — Détection des applications fullscreen et désactivation transparente du remapping
- **Compatibilité jeux renforcée** — Mode d'émission natif par scancode et désactivation de sécurité pour les anti-cheats connus
- **Détection de l'application au premier plan** — Pour des comportements contextuels par application
- **Icône dans la zone de notification** — Activation / désactivation rapide
- **Aucune installation requise** — Fonctionne depuis le Microsoft Store

### Configuration requise

- Windows 10 (version 1809+) ou Windows 11
- Compatible avec tous les types de claviers physiques (ISO, ANSI, ergonomique)

## Compilation

Le projet utilise .NET 8.0 avec compilation AOT native :

```bash
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r win-arm64
pwsh ./scripts/Pack-MSIX.ps1
pwsh ./scripts/Verify-Release.ps1
```

> **Note :** si le linker AOT ne trouve pas `vswhere.exe`, ajoutez temporairement `C:\Program Files (x86)\Microsoft Visual Studio\Installer` au `PATH`.

Le binaire compilé se trouve dans `src/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/`.

### Tests

Le projet sépare les tests du produit, de l'adaptateur Windows et du moteur portable.

```bash
dotnet test src/AZERTYGlobal.Tests
dotnet test src/TypingEngine.Windows.Tests
dotnet test src/TypingEngine.Core.Tests
```

La suite comprend 287 tests applicatifs, 107 tests Windows et 18 tests portables, soit 412 tests. L'architecture cible et sa séquence d'extraction sont décrites dans [`docs/keyboard-platform.md`](docs/keyboard-platform.md).

## Structure du projet

```
src/                              Code source C#
├── TypingEngine.Core/            Modèle, JSON et composition portables
├── TypingEngine.Core.Tests/      Tests du moteur sans dépendance Windows
├── TypingEngine.Windows/         Hook, injection et compatibilité Windows
├── TypingEngine.Windows.Tests/   Tests isolés de l'adaptateur Windows
├── TestSupport/                  Doubles partagés par les suites de tests
├── Program.cs                    Point d'entrée
├── TrayApplication.cs            Application tray (icône, menu)
├── AzertyGlobalWindowsTypingHost.cs Adaptateur configuration/statistiques du produit
├── LayoutLoader.cs               Adaptateur de ressource vers le moteur commun
├── CharacterSearch.cs            Recherche de caractères
├── TextInsertionService.cs       Insertion directe depuis la recherche
├── MaintainableLayersWindow.cs   Réglages des couches maintenables
├── LayerIndicatorWindow.cs       Indicateur de couche près du curseur
├── Localization/                 Textes français et anglais
├── UsageStats.cs                 Statistiques agrégées 100 % locales
├── UsageStatsWindow.cs           Interface des statistiques locales
├── DailyChallenge.cs             Sélection du défi quotidien
├── TrainingReminders.cs          Politique locale de rappels facultatifs
├── ToastActivation.cs            Activation des notifications Store
├── VirtualKeyboard.cs            Clavier virtuel interactif
├── LearningModule.cs             Module d'apprentissage interactif
├── LessonsWindow.cs              Fenêtre Leçons avec catalogue et mode libre
├── LessonCatalog.cs              Chargement du catalogue de leçons
├── LessonTypingSession.cs        Moteur de saisie des exercices
├── LessonProgressStore.cs        Progression locale des leçons
├── LessonHintProvider.cs         Indices pédagogiques et touches mortes
├── KeyboardRenderer.cs           Rendu clavier partagé
├── OnboardingWindow.cs           Fenêtre de première utilisation
├── SettingsWindow.cs             Fenêtre des paramètres
├── ConfigManager.cs              Gestion de la configuration
├── AutoStart.cs                  Démarrage automatique
├── AutoStartNudge.cs             Règle de la relance unique du démarrage auto
├── GdiHelpers.cs                 Utilitaires GDI+ (rendu texte)
├── GdiImageLoader.cs             Chargement d'images GDI+
├── Win32.cs                      Interop Win32 propre à l'interface du produit
├── AssemblyAttributes.cs         Attributs d'assemblage
├── AZERTY Global 2026.json       Disposition clavier (ressource embarquée)
├── character-index.json          Index de recherche (ressource embarquée)
├── lessons.json                  Catalogue de leçons (ressource embarquée)
├── favicon-azerty-global.png     Icône (ressource embarquée)
├── discord-icon.png              Icône Discord (ressource embarquée)
├── Properties/                   Métadonnées du projet
└── AZERTYGlobal.Tests/           Tests du produit et d'intégration xUnit
msix/                             Packaging Microsoft Store
├── AppxManifest.xml              Manifeste MSIX
├── Fiche Store.md                Descriptions FR/EN pour le Store
├── README.md                     Documentation packaging
└── Assets/                       Logos, screenshots et templates
scripts/                          Scripts de build
├── Pack-MSIX.ps1                 Packaging MSIX
├── Sync-LayoutResources.ps1       Synchronisation des ressources de disposition
└── Verify-Release.ps1            Vérification pré-publication
```

## Licence

[EUPL 1.2](https://eupl.eu/1.2/fr/) — European Union Public Licence

© 2017–2026 Antoine Olivier
