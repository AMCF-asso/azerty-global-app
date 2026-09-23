# Recherche — déclaration accessibilité Partner Center / MSIX Win32

## Q1. Partner Center — page "Product declarations" (MSIX)
URL: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/product-declarations
ms.date affiché: 2022-10-30 (updated_at: 2026-04-09)

Citation exacte de la case à cocher (titre de section) :
"This product has been tested to meet accessibility guidelines"

"Checking this box makes your app discoverable to customers who are specifically looking for accessible apps in the Store."

Liste des critères à remplir avant de cocher (citation quasi littérale, puces originales) :
- "Set all the relevant accessibility info for UI elements, such as accessible names."
- "Implemented keyboard navigation and operations, taking into account tab order, keyboard activation, arrow keys navigation, shortcuts."
- "Ensured an accessible visual experience by including such things as a 4.5:1 text contrast ratio, and don't rely on color alone to convey info to the user."
- "Used accessibility testing tools, such as Inspect or AccChecker, to verify your app, and resolve all high-priority errors detected by those tools."
- "Verified the app's key scenarios from end to end using such facilities and tools as Narrator, Magnifier, On Screen Keyboard, High Contrast, and High DPI."

"When you declare your app as accessible, you agree that your app is accessible to all customers, including those with disabilities."

Avertissement explicite :
"Don't list your app as accessible unless you have specifically engineered and tested it for that purpose. If your app is declared as accessible, but it doesn't actually support accessibility, you'll probably receive negative feedback from the community."

Liens donnés par la page (checklist visée) :
- Accessibility overview
- Accessibility testing (../../../design/accessibility/accessibility-testing)
- Accessibility in the Store (../../../design/accessibility/accessibility-in-the-store)

Note: page parente "Enter app properties for MSIX app" (même famille) ms.date 2022-10-30, updated_at 2026-03-06 :
https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/enter-app-properties
"You can check boxes in this section to indicate if any of the declarations apply to your app. This may affect the way your app is displayed, whether it is offered to certain customers, or how customers can use it."

## Effet sur la fiche Store — page dédiée
URL: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-in-the-store
ms.date: 2026-03-17 (updated_at 2026-03-26)

"Users discover accessible apps by using the Accessible filter while searching the Microsoft Store. Declaring your app as accessible also adds the Accessible tag to your app's description."

Critères listés (repris, un peu plus détaillés que la page Partner Center) :
- "Set all the relevant accessibility information for UI elements, including name, role, value, and so on."
- "Implemented full keyboard accessibility, enabling the user to: Accomplish primary app scenarios by using only the keyboard. Tab among UI elements in a logical order. Navigate among UI elements within a control by using the arrow keys. Use keyboard shortcuts to reach primary app functionality. Use Narrator touch gestures for Tab and arrow equivalency for devices with no keyboard."
- "Ensured that your app UI is visually accessible: has a minimum text contrast ratio of 4.5:1, does not rely on color alone to convey information, and so on."
- "Used accessibility testing tools such as Inspect and UIAVerify to verify your accessibility implementation, and resolved all priority 1 errors reported by such tools."
- "Verified your app's primary scenarios from end to end by using Narrator, Magnifier, On-Screen Keyboard, a high contrast theme, and adjusted dpi settings."

Renvoi explicite : "See the Accessibility checklist for more detail on these procedures and links to resources."

Certitude: haute (page officielle, primaire, datée récemment).

## Q2. Checklist / procédure d'accessibilité Windows apps

### Accessibility checklist (page dédiée)
URL: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist
ms.date: 2026-07-22 (updated_at 2026-07-17)

8 points listés textuellement :
1. "Set accessible names and descriptions" — nom accessible obligatoire (required), description optionnelle.
2. "Implement keyboard accessibility" — tab order, arrow-key navigation, keyboard activation, access/accelerator keys, AutomationPeer pour contrôles custom.
3. "Ensure text is a readable size" — Magnifier, mise à l'échelle système, "Make text bigger".
4. "Verify color and contrast" — "Use a color analyzer tool to verify that the visual text contrast ratio is at least 4.5:1." + "Switch to a high contrast theme and verify that the UI for your app is readable and usable." + "Ensure that your UI doesn't use color as the only way to convey information."
5. "Run accessibility tools and verify screen reading" — Inspect, AccChecker, Narrator.
6. "Add automated accessibility regression checks to your CI pipeline."
7. "Verify app manifest settings" — renvoi vers Security Considerations for Assistive Technologies.
8. "Declare your app as accessible in the Microsoft Store."

### Accessibility testing (page dédiée, outils + procédures)
URL: https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing
ms.date: 2026-07-22 (updated_at 2026-07-17)

Outils cités :
- Accessibility Insights (Live Inspect, FastPass, Troubleshooting) — "FastPass - a lightweight, two-step process that helps developers identify common, high-impact accessibility issues in less than five minutes."
- Outils "legacy" SDK Windows : AccScope, Inspect, UI Accessibility Checker (AccChecker), UI Automation Verify (UIA Verify), Accessible Event Watcher (AccEvent). Note: "we strongly recommend transitioning to Accessibility Insights."

Procédures listées (titres exacts) :
- "Test keyboard accessibility" — "Validate keyboard behavior without pointer input. Confirm a complete and logical Tab sequence across all interactive elements..."
- "Verify the contrast ratio of visible text" — renvoie à "Accessible text requirements" et aux techniques WCAG 2.2 G18.
- "Verify your app in high contrast" — "Use your app while a high-contrast theme is active to verify that all the UI elements display correctly."
- "Verify your app with display settings" — validation du scaling DPI.
- "Verify main app scenarios by using Narrator" — procédure détaillée (Win+Ctrl+Enter, Tab/flèches/Caps Lock+flèches, Caps Lock+Enter pour activer, Caps Lock+Shift+Enter recherche, mode développeur Ctrl+Caps Lock+F12, touch mode).
- "Examine the UI Automation representation for your app" — AccScope, AutomationProperties.AccessibilityView.

FastPass détaillé (page dédiée Accessibility Insights, non-Microsoft.com mais site officiel du projet Microsoft) :
URL: https://accessibilityinsights.io/docs/windows/getstarted/fastpass/
"FastPass consists of two main features – Automated Checks and Tab Stops." (résultat de recherche, page non revérifiée en fetch direct — citation issue du snippet)
Certitude: moyenne (contenu confirmé par le snippet de recherche seulement, pas de fetch direct de cette page précise).

Certitude générale Q2: haute pour accessibility-checklist et accessibility-testing (fetch direct, dates récentes juillet 2026).

## Q3. UI Automation pour surfaces GDI owner-draw (Win32)

### WM_GETOBJECT
URL: https://learn.microsoft.com/en-us/windows/win32/winauto/handling-the-wm-getobject-message
ms.date: 2025-07-14

"If the receiving application is a UI Automation provider and the object identifier is UiaRootObjectId, the provider should return the IRawElementProviderSimple interface of the object. The provider obtains the interface by calling the UiaReturnRawElementProvider function."

"Because most standard Windows controls and common controls implemented by the common control library (ComCtl32.dll) do not implement either Microsoft Active Accessibility or UI Automation, these controls typically do not handle the WM_GETOBJECT message."
→ implication directe pour une app avec surfaces GDI owner-draw : elle doit implémenter elle-même le handling WM_GETOBJECT, rien n'est fourni gratuitement par les contrôles communs.

### How-to expose a server-side provider (exemple de code officiel)
URL: https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-howto-expose-serverside-uiautomation-provider
ms.date: 2025-07-14

"When a provider receives a WM_GETOBJECT message, it should check whether the lParam parameter is equal to UiaRootObjectId. If it is, the provider should return the IRawElementProviderSimple interface of the object."
Exemple de code C++ officiel montrant le pattern case WM_GETOBJECT → UiaReturnRawElementProvider(hwnd, wParam, lParam, pRootProvider).

### UiaReturnRawElementProvider (référence API)
URL: https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiareturnrawelementprovider
ms.date: 2018-12-05 (updated_at 2024-02-22)

"This function is called by a control when it receives the WM_GETOBJECT message, to provide UI Automation with the UI Automation provider for the control. The control should pass the wParam and lParam parameters to the UiaReturnRawElementProvider function without filtering them first, because filtering can cause problems with Microsoft Active Accessibility clients."

"Microsoft recommends making this call from the WM_DESTROY message handler of the window that returns the UI Automation providers" (pour UiaReturnRawElementProvider(hwnd,0,0,NULL) au nettoyage).

### Interfaces requises (résultat de recherche, non re-fetché en page dédiée séparée)
D'après le snippet de recherche sur "How to Expose a Server-Side UI Automation Provider" :
"Every provider must implement at least IRawElementProviderSimple. Providers for elements within a fragment (a complex control usually hosted in a single HWND) must implement IRawElementProviderFragment, and the provider for the top-level element in the fragment must implement IRawElementProviderFragmentRoot."
Certitude: moyenne (formulation vue dans les résultats de recherche agrégés, à revérifier sur la page exacte "Implementing Server-side UI Automation Providers" — https://learn.microsoft.com/en-us/previous-versions/aa359583(v=vs.85) — page "previous-versions", donc contenu figé/ancien).

Certitude générale Q3: haute pour WM_GETOBJECT + UiaReturnRawElementProvider (fetch direct). Le point sur les interfaces obligatoires (Simple/Fragment/FragmentRoot) est correct par expérience connue mais la citation exacte vient d'un extrait secondaire, pas d'un fetch direct confirmé sur la bonne page.

## Q4. High contrast (Win32)

### High contrast parameter (référence)
URL: https://learn.microsoft.com/en-us/windows/win32/winauto/high-contrast-parameter
ms.date: 2025-07-14

"Applications use the SPI_GETHIGHCONTRAST and SPI_SETHIGHCONTRAST flags with the SystemParametersInfo function to get and set the high contrast parameter."

"During initialization and when processing WM_SYSCOLORCHANGE messages, applications should determine the state of the high contrast parameter. To make this determination, applications should call SystemParametersInfo with the SPI_GETHIGHCONTRAST flag to obtain a HIGHCONTRAST structure. If the dwFlags member of the HIGHCONTRAST structure has the HCF_HIGHCONTRASTON bit set, then the high contrast feature is enabled, and applications should do the following: Map all colors to a single pair of foreground and background colors. Use the GetSysColor function..."
→ Note: c'est WM_SYSCOLORCHANGE (pas WM_SETTINGCHANGE) que la doc associe explicitement à SPI_GETHIGHCONTRAST dans ce passage.

### Supporting High Contrast Themes
URL: https://learn.microsoft.com/en-us/windows/win32/controls/supporting-high-contrast-themes
ms.date: 2018-05-31 (updated_at 2025-03-11)

"Your application can determine whether a high contrast theme is active by calling the SystemParametersInfo function with the SPI_GETHIGHCONTRAST flag."

"Colors. Avoid using hard-coded colors. Instead, use the system colors because they are based on the current theme. Using custom colors can interfere with and override the colors in the high contrast themes."

"Font and control sizes. To ensure that your UI is accessible to users with disabilities, set font sizes according to the current theme settings. Set the size of controls to be at least the default size."

Manifeste de compatibilité (GUIDs Windows 8/7/Vista) requis pour bénéficier du nouveau modèle de theming en high contrast — sinon rendu "classic" simulé.

### WM_THEMECHANGED (issu des résultats de recherche, non re-fetché séparément)
"If your app caches the colors retrieved from the theme or applies colors in a nonstandard fashion, add a message handler for WM_THEMECHANGE that recalculates the stored color values and repaints the UI." — snippet Learn (page probable: "Testing for accessibility" ou doc thèmes contrôles communs). Certitude: moyenne (vu uniquement dans le résumé de recherche, pas confirmé par fetch direct de la page source).

Certitude générale Q4: haute pour SPI_GETHIGHCONTRAST/HIGHCONTRAST/GetSysColor (2 pages fetchées directement). Moyenne pour le lien exact avec WM_SETTINGCHANGE/WM_THEMECHANGED (la doc fetchée mentionne WM_SYSCOLORCHANGE, pas WM_SETTINGCHANGE).

## Q5. Microsoft Store Policies — clause accessibilité ?

URL 7.20 (en vigueur au moment de la recherche, 2026-09-22) : https://learn.microsoft.com/en-us/windows/apps/publish/store-policies
"Document version: 7.20" / "Publish date: September 15, 2026" / "Effective date: October 22, 2026"
ms.date (metadata): 2026-09-14, updated_at: 2026-09-15

URL 7.19 (archive) : https://learn.microsoft.com/en-us/windows/apps/publish/store-policy-archive/store-policy-7-19
"Document version: 7.19" / "Publish date: September 10, 2025" / "Effective date: October 14, 2025"
→ confirme les dates données par l'utilisateur.

Recherche du mot "accessibility" dans le corps des deux versions (7.19 et 7.20, texte intégral lu) :
- Aucune clause dédiée "accessibilité de l'app" / "exactitude des déclarations produit accessibilité" trouvée.
- Seules occurrences du champ lexical :
  - 10.2.8 (identique dans 7.19 et 7.20) : "Unsupported methods include but are not limited to use of accessibility APIs or undocumented or unsupported APIs in unsupported ways." — porte sur l'usage des API d'accessibilité pour modifier les réglages Windows sans consentement, PAS sur la conformité accessibilité de l'app elle-même.
  - 11.5 Offensive Content : liste "disability" parmi les caractéristiques protégées contre le contenu discriminatoire — sans rapport avec la case à cocher.
- Clause générale applicable par défaut à toute déclaration inexacte : 10.1 "Distinct Function & Value; Accurate Representation" — "Your product and its associated metadata, including but not limited to your app title, description, screenshots, trailers, content rating, search terms, and product category, must accurately and clearly reflect the source, functionality, and features of your product." et 10.1.1 : "All aspects of your product, including metadata, should accurately describe the functions, features, user experience and any important limitations of your product..." — la case "accessibility guidelines" fait partie des déclarations/métadonnées du produit, donc entre dans le champ de cette clause générale, mais le texte de la politique ne nomme jamais explicitement "accessibility declaration".

Certitude: haute sur le fait qu'aucune clause n'utilise littéralement "accessibility" au sens de la case à cocher (lecture intégrale des deux versions). Moyenne sur le rattachement à 10.1 (déduction logique, pas une citation qui nomme la case).

## Q6. Accessibility Insights for Windows — CLI (AxeWindows)

Dépôt: https://github.com/microsoft/axe-windows
Licence: MIT (confirmée en pied de page du dépôt GitHub)

README CLI: https://github.com/microsoft/axe-windows/blob/main/src/CLI/README.MD
"Axe.Windows also has a command line interface (CLI) to simplify automated testing in build pipelines."
Scan par process id : paramètre "--processid" décrit comme "Process Id", "identifies the process ID of the application to be scanned. Must be specified as an integer value."
Autres paramètres cités : --processname, --outputdirectory, --scanid, --scanrootwindowhandle, --verbosity.
Rapport produit : fichier ".a11ytest" — "an .a11ytest file will be generated if 1 or more errors were detected or if the alwayssavetestfile option was specified" — ouvrable avec Accessibility Insights for Windows.
Prérequis: ".NET Core runtime" version 3.0 ou supérieure.
Installation: "By default, this tool gets installed to c:\Program Files (x86)\AxeWindowsCLI\<version>\AxeWindowsCLI.exe" ; sorties = AxeWindowsCLI.msi et AxeWindowsCLI.zip ; commande d'install "msiexec.exe /i AxeWindowsCLI-X.Y.Z.msi /quiet".
Distribution NuGet du moteur (pas juste la CLI) : "To get the latest version of the Axe.Windows NuGet package, visit Axe.Windows on NuGet.org."
Releases GitHub (téléchargement des binaires CLI): https://github.com/microsoft/axe-windows/releases (page listée dans les résultats de recherche, non ouverte en détail — vérifier la version exacte disponible au moment de l'usage).

Certitude: haute (README fetché directement + confirmation de licence sur la page dépôt).
