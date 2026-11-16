# Inventaire des ressources de src/ (généré)

Produit par `python scripts/audit-ressources-inventaire.py --md docs/audit-ressources-inventaire.md` ; ne pas modifier à la main. Verdicts lus : `docs/audit-ressources-verdicts.tsv`. Lecture : [audit-ressources.md](audit-ressources.md).

387 créations : abonnement 39, gdi 110, memoire 15, minuterie 32, windows 191.

| Catégorie | API | Nombre |
|---|---|---|
| abonnement | AppDomain | 1 |
| abonnement | DialogNavigation.Register | 7 |
| abonnement | event += | 31 |
| gdi | CreateBitmap | 3 |
| gdi | CreateCompatibleBitmap | 7 |
| gdi | CreateCompatibleDC | 8 |
| gdi | CreateFontW | 22 |
| gdi | CreateIconIndirect | 3 |
| gdi | CreatePen | 16 |
| gdi | CreateSolidBrush | 27 |
| gdi | GdipCreate | 20 |
| gdi | GdipCreateHBITMAPFromBitmap | 2 |
| gdi | GdipGetImageGraphicsContext | 2 |
| memoire | GCHandle.Alloc | 3 |
| memoire | Marshal.AllocHGlobal | 9 |
| memoire | Marshal.StringToHGlobalUni | 3 |
| minuterie | SetTimer | 25 |
| minuterie | System.Threading.Timer | 1 |
| minuterie | Thread | 1 |
| minuterie | ThreadPool | 5 |
| windows | AddFontMemResourceEx | 1 |
| windows | BeginPaint | 8 |
| windows | CoCreateInstance | 5 |
| windows | CoInitializeEx | 1 |
| windows | CoRegisterClassObject | 1 |
| windows | CreateCaret | 1 |
| windows | CreatePopupMenu | 8 |
| windows | CreateStreamOnHGlobal | 1 |
| windows | CreateWindowExW | 71 |
| windows | FileStream | 5 |
| windows | GdiplusStartup | 7 |
| windows | GetDC | 29 |
| windows | GlobalAlloc | 2 |
| windows | LoadCursorW | 7 |
| windows | Mutex | 1 |
| windows | OpenClipboard | 1 |
| windows | OpenProcess | 3 |
| windows | RegisterApplicationRestart | 1 |
| windows | RegisterClassExW | 1 |
| windows | SafeArrayCreateVector | 1 |
| windows | SaveDC | 12 |
| windows | SetCapture | 3 |
| windows | SetWinEventHook | 4 |
| windows | SetWindowSubclass | 11 |
| windows | SetWindowsHookEx | 2 |
| windows | Shell_NotifyIconW NIM_ADD | 2 |
| windows | UiaHostProviderFromHwnd | 1 |
| windows | WTSRegisterSessionNotification | 1 |

## abonnement (39)

| Site | API | Libération |
|---|---|---|
| `Program.cs:194` | AppDomain | jamais : gestionnaire d'exceptions du processus, posé une fois dans Main (lu) |
| `AboutWindow.cs:168` | DialogNavigation.Register | libéré : AboutWindow.cs:506 |
| `LayoutConflictWindow.cs:172` | DialogNavigation.Register | libéré : LayoutConflictWindow.cs:523 |
| `MaintainableLayersWindow.cs:113` | DialogNavigation.Register | libéré : MaintainableLayersWindow.cs:127, MaintainableLayersWindow.cs:575 |
| `OnboardingWindow.cs:981` | DialogNavigation.Register | libéré : OnboardingWindow.cs:2228 |
| `PauseDurationDialog.cs:159` | DialogNavigation.Register | libéré : PauseDurationDialog.cs:557 |
| `SettingsWindow.cs:431` | DialogNavigation.Register | libéré : SettingsWindow.cs:2530 |
| `UsageStatsWindow.cs:203` | DialogNavigation.Register | libéré : UsageStatsWindow.cs:771 |
| `AboutWindow.cs:104` | event += | désabonné : AboutWindow.cs:501 |
| `LessonsWindow.cs:259` | event += | désabonné : LessonsWindow.cs:3292 |
| `LessonsWindow.cs:263` | event += | désabonné : LessonsWindow.cs:3295 |
| `LessonsWindow.cs:264` | event += | désabonné : LessonsWindow.cs:3296 |
| `LessonsWindow.cs:265` | event += | désabonné : LessonsWindow.cs:3297 |
| `LessonsWindow.cs:270` | event += | désabonné : LessonsWindow.cs:3294 |
| `MaintainableLayersWindow.cs:81` | event += | désabonné : MaintainableLayersWindow.cs:571 |
| `OnboardingWindow.cs:341` | event += | désabonné : OnboardingWindow.cs:2221 |
| `PauseDurationDialog.cs:170` | event += | désabonné : PauseDurationDialog.cs:551 |
| `SettingsWindow.cs:322` | event += | désabonné : SettingsWindow.cs:2520 |
| `TrayApplication.cs:241` | event += | jamais : abonnement de l'instance unique du tray, une fois par processus (lu) |
| `TrayApplication.cs:253` | event += | jamais : abonnement de l'instance unique du tray, une fois par processus (lu) |
| `TrayApplication.cs:359` | event += | jamais : KeyMapper possédé par le tray, même durée de vie (une fois par processus) (lu) |
| `TrayApplication.cs:360` | event += | jamais : KeyMapper possédé par le tray, même durée de vie (une fois par processus) (lu) |
| `TrayApplication.cs:362` | event += | jamais : KeyboardHook possédé par le tray, même durée de vie (une fois par processus) (lu) |
| `TrayApplication.cs:363` | event += | jamais : KeyboardHook possédé par le tray, même durée de vie (une fois par processus) (lu) |
| `TrayApplication.cs:364` | event += | jamais : KeyboardHook possédé par le tray, même durée de vie (une fois par processus) (lu) |
| `TrayApplication.cs:365` | event += | jamais : KeyboardHook possédé par le tray, même durée de vie (une fois par processus) (lu) |
| `TrayApplication.cs:386` | event += | jamais : CharacterSearch créée une fois par le tray, même durée de vie (lu) |
| `TrayApplication.cs:388` | event += | jamais : CharacterSearch créée une fois par le tray, même durée de vie (lu) |
| `TrayApplication.cs:389` | event += | jamais : CharacterSearch créée une fois par le tray, même durée de vie (lu) |
| `TrayApplication.cs:391` | event += | jamais : CharacterSearch créée une fois par le tray, même durée de vie (lu) |
| `TrayApplication.cs:427` | event += | jamais : ForegroundMonitor créé une fois (TrayApplication.cs:426), disposé à l'arrêt (2364) (lu) |
| `TrayApplication.cs:430` | event += | désabonné : TrayApplication.cs:2363 |
| `TrayApplication.cs:881` | event += | jamais : fenêtre des couches maintenues créée une fois par le tray (garde null), même durée de vie (lu) |
| `TypingEngine.Windows/KeyMapper.cs:124` | event += | jamais : _maintainableLayers possédé par le KeyMapper, même durée de vie (lu) |
| `TypingEngine.Windows/SecureInputDetector.cs:63` | event += | désabonné : TypingEngine.Windows/SecureInputDetector.cs:64 |
| `UsageStatsWindow.cs:138` | event += | désabonné : UsageStatsWindow.cs:765 |
| `UsageStatsWindow.cs:139` | event += | désabonné : UsageStatsWindow.cs:766 |
| `VirtualKeyboard.cs:352` | event += | désabonné : VirtualKeyboard.cs:1341 |
| `VirtualKeyboard.cs:353` | event += | désabonné : VirtualKeyboard.cs:1342 |

## gdi (110)

| Site | API | Libération |
|---|---|---|
| `GdiHelpers.cs:100` | CreateBitmap | libéré : GdiHelpers.cs:103 |
| `TrayApplication.cs:3244` | CreateBitmap | libéré : TrayApplication.cs:3255 |
| `TrayIcon.cs:172` | CreateBitmap | libéré : TrayIcon.cs:175 |
| `DevRects.cs:337` | CreateCompatibleBitmap | libéré : DevRects.cs:357 |
| `LessonsWindow.Home.cs:805` | CreateCompatibleBitmap | libéré : LessonsWindow.Home.cs:828 |
| `LessonsWindow.cs:1385` | CreateCompatibleBitmap | libéré : LessonsWindow.cs:1420 |
| `LessonsWindow.cs:999` | CreateCompatibleBitmap | libéré : LessonsWindow.cs:1023 |
| `PaintBuffer.cs:52` | CreateCompatibleBitmap | libéré : PaintBuffer.cs:70 |
| `TrayApplication.cs:3197` | CreateCompatibleBitmap | libéré : TrayApplication.cs:3256 |
| `VirtualKeyboard.cs:960` | CreateCompatibleBitmap | libéré : VirtualKeyboard.cs:979 |
| `DevRects.cs:336` | CreateCompatibleDC | libéré : DevRects.cs:358 |
| `LessonsWindow.Home.cs:804` | CreateCompatibleDC | libéré : LessonsWindow.Home.cs:829 |
| `LessonsWindow.cs:1035` | CreateCompatibleDC | libéré : LessonsWindow.cs:1049 |
| `LessonsWindow.cs:1384` | CreateCompatibleDC | libéré : LessonsWindow.cs:1422 |
| `LessonsWindow.cs:998` | CreateCompatibleDC | libéré : LessonsWindow.cs:1016 |
| `PaintBuffer.cs:51` | CreateCompatibleDC | libéré : PaintBuffer.cs:58 |
| `TrayApplication.cs:3196` | CreateCompatibleDC | libéré : TrayApplication.cs:3257 |
| `VirtualKeyboard.cs:959` | CreateCompatibleDC | libéré : VirtualKeyboard.cs:980 |
| `AppFonts.cs:183` | CreateFontW | rendu à l'appelant (AppFonts.Create) : AppFonts.Get le garde pour la vie du processus (tailles qui ne suivent que le DPI, une par écran) ; WindowFonts le détruit quand l'échelle de sa fenêtre change et à la fermeture (correctif C1 et C2) (lu) |
| `LessonsWindow.cs:434` | CreateFontW | libéré : LessonsWindow.cs:464 |
| `LessonsWindow.cs:435` | CreateFontW | libéré : LessonsWindow.cs:465 |
| `LessonsWindow.cs:436` | CreateFontW | libéré : LessonsWindow.cs:466 |
| `LessonsWindow.cs:437` | CreateFontW | libéré : LessonsWindow.cs:467 |
| `LessonsWindow.cs:438` | CreateFontW | libéré : LessonsWindow.cs:468 |
| `LessonsWindow.cs:439` | CreateFontW | libéré : LessonsWindow.cs:469 |
| `LessonsWindow.cs:440` | CreateFontW | libéré : LessonsWindow.cs:470 |
| `LessonsWindow.cs:441` | CreateFontW | libéré : LessonsWindow.cs:471 |
| `LessonsWindow.cs:442` | CreateFontW | libéré : LessonsWindow.cs:472 |
| `LessonsWindow.cs:443` | CreateFontW | libéré : LessonsWindow.cs:473 |
| `LessonsWindow.cs:444` | CreateFontW | libéré : LessonsWindow.cs:474 |
| `LessonsWindow.cs:445` | CreateFontW | libéré : LessonsWindow.cs:475 |
| `LessonsWindow.cs:446` | CreateFontW | libéré : LessonsWindow.cs:476 |
| `LessonsWindow.cs:447` | CreateFontW | libéré : LessonsWindow.cs:477 |
| `MaintainableLayersWindow.cs:243` | CreateFontW | libéré : MaintainableLayersWindow.cs:579 |
| `MaintainableLayersWindow.cs:246` | CreateFontW | libéré : MaintainableLayersWindow.cs:580 |
| `OnboardingWindow.cs:393` | CreateFontW | libéré : OnboardingWindow.cs:399 |
| `PauseDurationDialog.cs:195` | CreateFontW | libéré : PauseDurationDialog.cs:207 |
| `TrayApplication.cs:3232` | CreateFontW | libéré : TrayApplication.cs:3239 |
| `TypeRamp.cs:29` | CreateFontW | aucun appelant dans src/ (TypeRamp.Create inutilisé) ; la HFONT appartiendrait à l'appelant (lu) |
| `TypeRamp.cs:40` | CreateFontW | fabrique : la HFONT appartient à l'appelant ; 6 appelants, tous en champ détruit au Dispose ou au changement d'échelle (AboutWindow.cs:130, LayoutConflictWindow.cs:130-133, UsageStatsWindow.cs:163) (lu) |
| `GdiHelpers.cs:102` | CreateIconIndirect | fabrique : l'icône appartient à l'appelant ; NativeWindow.LogoIcon la garde en cache par taille (jamais détruite, bornée : 16 et 32 px par DPI) ; TrayApplication.CreateLogoIcon la remplace et détruit l'ancienne (TrayApplication.cs:2952) (lu) |
| `TrayApplication.cs:3252` | CreateIconIndirect | fabrique CreateTextIcon : l'icône appartient à l'appelant, ancienne détruite au remplacement (TrayApplication.cs:2553, 2952) ; bitmaps et police de travail détruits sur place (3239, 3255-3256) (lu) |
| `TrayIcon.cs:174` | CreateIconIndirect | fabrique FromPixels : l'icône appartient à l'appelant (TrayApplication._hIcon, ancienne détruite à TrayApplication.cs:2553) (lu) |
| `CharacterSearch.cs:1381` | CreatePen | libéré : CharacterSearch.cs:1390 |
| `GdiHelpers.cs:148` | CreatePen | libéré : GdiHelpers.cs:155 |
| `GdiHelpers.cs:209` | CreatePen | libéré : GdiHelpers.cs:216 |
| `KeyboardRenderer.cs:440` | CreatePen | libéré : KeyboardRenderer.cs:451 |
| `LessonsWindow.Home.cs:843` | CreatePen | libéré : LessonsWindow.Home.cs:849 |
| `LessonsWindow.Paper.cs:138` | CreatePen | libéré : LessonsWindow.Paper.cs:144 |
| `LessonsWindow.cs:2227` | CreatePen | libéré : LessonsWindow.cs:2233 |
| `LightButton.cs:116` | CreatePen | libéré : LightButton.cs:122 |
| `MiniKeyboardRenderer.cs:252` | CreatePen | libéré : MiniKeyboardRenderer.cs:258 |
| `MiniKeyboardRenderer.cs:266` | CreatePen | libéré : MiniKeyboardRenderer.cs:272 |
| `OnboardingWindow.cs:1339` | CreatePen | libéré : OnboardingWindow.cs:1345 |
| `PaperDraw.cs:107` | CreatePen | libéré : PaperDraw.cs:113 |
| `PaperDraw.cs:28` | CreatePen | libéré : PaperDraw.cs:42 |
| `PaperKeyboard.cs:324` | CreatePen | libéré : PaperKeyboard.cs:330 |
| `PaperKeyboard.cs:568` | CreatePen | libéré : PaperKeyboard.cs:578 |
| `PaperKeyboard.cs:590` | CreatePen | libéré : PaperKeyboard.cs:596 |
| `CharacterSearch.cs:683` | CreateSolidBrush | libéré : CharacterSearch.cs:1614 |
| `GdiHelpers.cs:123` | CreateSolidBrush | libéré : GdiHelpers.cs:125 |
| `GdiHelpers.cs:147` | CreateSolidBrush | libéré : GdiHelpers.cs:156 |
| `GdiHelpers.cs:208` | CreateSolidBrush | libéré : GdiHelpers.cs:217 |
| `KeyboardRenderer.cs:439` | CreateSolidBrush | libéré : KeyboardRenderer.cs:452 |
| `KeyboardRenderer.cs:459` | CreateSolidBrush | libéré : KeyboardRenderer.cs:461 |
| `KeyboardRenderer.cs:485` | CreateSolidBrush | libéré : KeyboardRenderer.cs:487 |
| `LayoutConflictWindow.cs:89` | CreateSolidBrush | libéré : LayoutConflictWindow.cs:528 |
| `LessonsWindow.Home.cs:815` | CreateSolidBrush | libéré : LessonsWindow.Home.cs:817 |
| `LessonsWindow.Home.cs:842` | CreateSolidBrush | libéré : LessonsWindow.Home.cs:850 |
| `LessonsWindow.cs:2226` | CreateSolidBrush | libéré : LessonsWindow.cs:2234 |
| `MaintainableLayersWindow.cs:150` | CreateSolidBrush | libéré : MaintainableLayersWindow.cs:581 |
| `MiniKeyboardRenderer.cs:251` | CreateSolidBrush | libéré : MiniKeyboardRenderer.cs:259 |
| `MiniKeyboardRenderer.cs:265` | CreateSolidBrush | libéré : MiniKeyboardRenderer.cs:273 |
| `NativeWindow.cs:108` | CreateSolidBrush | pinceau de classe : l'ancien détruit au remplacement (NativeWindow.cs:110-111), le dernier libéré par Windows à l'UnregisterClass de chaque fenêtre (12 classes de fenêtres, toutes désinscrites au Dispose) (lu) |
| `OnboardingWindow.cs:1311` | CreateSolidBrush | libéré : OnboardingWindow.cs:1310, OnboardingWindow.cs:2236 |
| `OnboardingWindow.cs:318` | CreateSolidBrush | libéré : OnboardingWindow.cs:1310, OnboardingWindow.cs:2236 |
| `PaperDraw.cs:27` | CreateSolidBrush | libéré : PaperDraw.cs:43 |
| `PaperKeyboard.cs:323` | CreateSolidBrush | libéré : PaperKeyboard.cs:331 |
| `PaperKeyboard.cs:589` | CreateSolidBrush | libéré : PaperKeyboard.cs:597 |
| `PauseDurationDialog.cs:150` | CreateSolidBrush | libéré : PauseDurationDialog.cs:573 |
| `SettingsWindow.Paper.cs:88` | CreateSolidBrush | libéré : l'ancien pinceau détruit au remplacement (SettingsWindow.Paper.cs:89), le dernier au Dispose (SettingsWindow.cs:2535) (lu) |
| `SettingsWindow.cs:300` | CreateSolidBrush | libéré : SettingsWindow.cs:2535 |
| `ToggleNotification.cs:34` | CreateSolidBrush | libéré : ToggleNotification.cs:181 |
| `TrayApplication.cs:3202` | CreateSolidBrush | libéré : TrayApplication.cs:3205 |
| `TrayApplication.cs:3211` | CreateSolidBrush | libéré : TrayApplication.cs:3213 |
| `TrayApplication.cs:3220` | CreateSolidBrush | libéré : TrayApplication.cs:3222 |
| `AboutWindow.cs:385` | GdipCreate | libéré : AboutWindow.cs:479 |
| `GdiHelpers.cs:91` | GdipCreate | libéré : GdiHelpers.cs:109 |
| `GdiImageLoader.cs:45` | GdipCreate | fabrique : l'image GDI+ appartient à l'appelant ; 7 appelants, tous disposés (AboutWindow.cs:520, NativeWindow.cs:182, OnboardingWindow.cs:2231-2233, TrayApplication.cs:2972, TrayIcon.cs:72) (lu) |
| `OnboardingWindow.cs:1415` | GdipCreate | libéré : OnboardingWindow.cs:1421 |
| `OnboardingWindow.cs:1629` | GdipCreate | libéré : OnboardingWindow.cs:1646 |
| `OnboardingWindow.cs:2042` | GdipCreate | libéré : OnboardingWindow.cs:2044 |
| `OnboardingWindow.cs:2114` | GdipCreate | libéré : OnboardingWindow.cs:2121 |
| `PaperChoice.cs:165` | GdipCreate | libéré : PaperChoice.cs:212 |
| `PaperChoice.cs:230` | GdipCreate | libéré : PaperChoice.cs:251 |
| `PaperChoice.cs:240` | GdipCreate | libéré : PaperChoice.cs:243 |
| `PaperChoice.cs:245` | GdipCreate | libéré : PaperChoice.cs:248 |
| `PaperChoice.cs:261` | GdipCreate | libéré : PaperChoice.cs:263 |
| `PaperChoice.cs:269` | GdipCreate | libéré : PaperChoice.cs:271 |
| `PaperChoice.cs:278` | GdipCreate | libéré : PaperChoice.cs:285 |
| `PaperDraw.cs:55` | GdipCreate | libéré : PaperDraw.cs:88 |
| `PaperDraw.cs:62` | GdipCreate | libéré : PaperDraw.cs:87 |
| `PaperDraw.cs:78` | GdipCreate | libéré : PaperDraw.cs:86 |
| `PaperDraw.cs:79` | GdipCreate | libéré : PaperDraw.cs:85 |
| `TrayIcon.cs:168` | GdipCreate | libéré : TrayIcon.cs:181 |
| `TrayIcon.cs:60` | GdipCreate | libéré : TrayIcon.cs:71 |
| `GdiHelpers.cs:97` | GdipCreateHBITMAPFromBitmap | libéré : GdiHelpers.cs:103 |
| `TrayIcon.cs:170` | GdipCreateHBITMAPFromBitmap | libéré : TrayIcon.cs:175 |
| `GdiHelpers.cs:92` | GdipGetImageGraphicsContext | libéré : GdiHelpers.cs:96 |
| `TrayIcon.cs:62` | GdipGetImageGraphicsContext | libéré : TrayIcon.cs:66 |

## memoire (15)

| Site | API | Libération |
|---|---|---|
| `CharacterSearch.cs:1131` | GCHandle.Alloc | libéré : CharacterSearch.cs:1139 |
| `TrayIcon.cs:164` | GCHandle.Alloc | libéré : TrayIcon.cs:182 |
| `TrayIcon.cs:55` | GCHandle.Alloc | libéré : TrayIcon.cs:73 |
| `AppFonts.cs:146` | Marshal.AllocHGlobal | libéré : AppFonts.cs:151 |
| `LayoutConflictWindow.cs:304` | Marshal.AllocHGlobal | libéré : LayoutConflictWindow.cs:313 |
| `PauseDurationDialog.cs:320` | Marshal.AllocHGlobal | libéré : PauseDurationDialog.cs:327 |
| `SettingsWindow.Paper.cs:165` | Marshal.AllocHGlobal | libéré : SettingsWindow.Paper.cs:172 |
| `SettingsWindow.cs:1041` | Marshal.AllocHGlobal | libéré : SettingsWindow.cs:1047 |
| `SettingsWindow.cs:2189` | Marshal.AllocHGlobal | libéré : SettingsWindow.cs:2216 |
| `VirtualKeyboard.cs:399` | Marshal.AllocHGlobal | libéré : VirtualKeyboard.cs:407 |
| `VirtualKeyboard.cs:423` | Marshal.AllocHGlobal | libéré : VirtualKeyboard.cs:431 |
| `VirtualKeyboard.cs:445` | Marshal.AllocHGlobal | libéré : VirtualKeyboard.cs:453 |
| `LayoutConflictWindow.cs:295` | Marshal.StringToHGlobalUni | libéré : LayoutConflictWindow.cs:297 |
| `SettingsWindow.cs:1037` | Marshal.StringToHGlobalUni | libéré : SettingsWindow.cs:1049 |
| `SettingsWindow.cs:2110` | Marshal.StringToHGlobalUni | libéré : SettingsWindow.cs:2112 |

## minuterie (32)

| Site | API | Libération |
|---|---|---|
| `CharacterSearch.cs:1219` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer CharacterSearch.cs:848, CharacterSearch.cs:990 ; morte avec la fenêtre (CharacterSearch.cs:1619) |
| `CharacterSearch.cs:1230` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer CharacterSearch.cs:848, CharacterSearch.cs:990 ; morte avec la fenêtre (CharacterSearch.cs:1619) |
| `DevRects.cs:158` | SetTimer | jamais : minuterie de thread sans fenêtre, une par processus ; build Debug seulement (#if DEBUG, TrayApplication.cs:713-715) (lu) |
| `LessonsWindow.cs:2670` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer LessonsWindow.cs:1103, LessonsWindow.cs:2666, LessonsWindow.cs:2851 ; morte avec la fenêtre (LessonsWindow.cs:3302) |
| `LessonsWindow.cs:2804` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer LessonsWindow.cs:1105, LessonsWindow.cs:2660, LessonsWindow.cs:2793 ; morte avec la fenêtre (LessonsWindow.cs:3302) |
| `LessonsWindow.cs:2812` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer LessonsWindow.cs:1104, LessonsWindow.cs:2792, LessonsWindow.cs:2806 ; morte avec la fenêtre (LessonsWindow.cs:3302) |
| `LessonsWindow.cs:2912` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer LessonsWindow.cs:1103, LessonsWindow.cs:2666, LessonsWindow.cs:2851 ; morte avec la fenêtre (LessonsWindow.cs:3302) |
| `LessonsWindow.cs:3217` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer LessonsWindow.cs:2648 ; morte avec la fenêtre (LessonsWindow.cs:3302) |
| `ToggleNotification.cs:105` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer ToggleNotification.cs:104, ToggleNotification.cs:121 ; morte avec la fenêtre (ToggleNotification.cs:176) |
| `TrayApplication.cs:1276` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:1003 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:2144` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:2043, TrayApplication.cs:2155, TrayApplication.cs:2357 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:2688` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:2689 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:331` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:1015 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:338` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:1023 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:665` | SetTimer | minuterie de fenêtre, même id remplacé : aucun KillTimer ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:666` | SetTimer | minuterie de fenêtre, même id remplacé : aucun KillTimer ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:667` | SetTimer | minuterie de fenêtre, même id remplacé : aucun KillTimer ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:669` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:2358 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:671` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:2359 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:673` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:2360 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:677` | SetTimer | minuterie de fenêtre, même id remplacé : aucun KillTimer ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:680` | SetTimer | minuterie de fenêtre, même id remplacé : aucun KillTimer ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `TrayApplication.cs:758` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer TrayApplication.cs:761, TrayApplication.cs:998 ; morte avec la fenêtre (TrayApplication.cs:2346) |
| `UsageStatsWindow.cs:502` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer UsageStatsWindow.cs:457 ; morte avec la fenêtre (UsageStatsWindow.cs:772) |
| `VirtualKeyboard.cs:760` | SetTimer | minuterie de fenêtre, même id remplacé : KillTimer VirtualKeyboard.cs:847 ; morte avec la fenêtre (VirtualKeyboard.cs:1350) |
| `ConfigManager.cs:1449` | System.Threading.Timer | libéré : ConfigManager.cs:1451 |
| `TypingEngine.Windows/SecureInputProbe.cs:85` | Thread | borné : un fil vivant, plus au plus 3 fils bloqués abandonnés (MaxAbandonedWorkers, SecureInputProbe.cs:38) (lu) |
| `ConfigManager.cs:1066` | ThreadPool | aucun objet gardé : tâche ponctuelle du pool, une seule en vol (garde _deferredLogDraining) (lu) |
| `ConfigManager.cs:1457` | ThreadPool | aucun objet gardé : tâche ponctuelle du pool (changement de session) (lu) |
| `ConfigManager.cs:987` | ThreadPool | aucun objet gardé : tâche ponctuelle du pool (une ligne de journal) (lu) |
| `TrayApplication.cs:1371` | ThreadPool | aucun objet gardé : sonde ponctuelle du pool, une seule en vol (garde _remoteHostProbeRunning) (lu) |
| `UsageStats.cs:668` | ThreadPool | aucun objet gardé : tâche ponctuelle du pool, une seule en vol (garde _backgroundFlushQueued) (lu) |

## windows (191)

| Site | API | Libération |
|---|---|---|
| `AppFonts.cs:148` | AddFontMemResourceEx | jamais : polices privées chargées une fois par processus (garde _loaded) ; mémoire gardée jusqu'à la sortie, bornée (lu) |
| `ChipButton.cs:48` | BeginPaint | libéré : ChipButton.cs:50 |
| `LayerIndicatorWindow.cs:134` | BeginPaint | libéré : LayerIndicatorWindow.cs:137 |
| `LessonsWindow.cs:1348` | BeginPaint | libéré : LessonsWindow.cs:1361 |
| `LightButton.cs:60` | BeginPaint | libéré : LightButton.cs:62 |
| `OnboardingWindow.cs:1597` | BeginPaint | libéré : OnboardingWindow.cs:1618 |
| `PaintBuffer.cs:28` | BeginPaint | libéré : PaintBuffer.cs:83 |
| `UsageStatsWindow.cs:581` | BeginPaint | libéré : UsageStatsWindow.cs:603 |
| `VirtualKeyboard.cs:950` | BeginPaint | libéré : VirtualKeyboard.cs:955 |
| `AccessibleName.cs:166` | CoCreateInstance | libéré : Release par la vtable dans le finally (AccessibleName.cs:185-188) (lu) |
| `AccessibleName.cs:208` | CoCreateInstance | libéré : Release par la vtable dans le finally (AccessibleName.cs:221-224) (lu) |
| `AccessibleName.cs:91` | CoCreateInstance | libéré : Release par la vtable dans le finally (AccessibleName.cs:103-106) (lu) |
| `AutoStart.cs:225` | CoCreateInstance | libéré : IShellLink et IPersistFile relâchés dans deux finally (AutoStart.cs:273-286) (lu) |
| `TypingEngine.Windows/SecureInputDetector.cs:79` | CoCreateInstance | jamais : objet UI Automation gardé en cache statique, relâché et recréé seulement après une erreur (ReleaseAutomation) (lu) |
| `TypingEngine.Windows/SecureInputDetector.cs:43` | CoInitializeEx | jamais : CoInitializeEx une fois par fil de la sonde ; COM libéré à la sortie du fil (lu) |
| `ToastActivation.cs:122` | CoRegisterClassObject | libéré : ToastActivation.cs:146 |
| `LessonsWindow.cs:1546` | CreateCaret | libéré : LessonsWindow.cs:1557 |
| `TrayApplication.cs:1754` | CreatePopupMenu | libéré : TrayApplication.cs:1767 |
| `TrayApplication.cs:1788` | CreatePopupMenu | libéré : TrayApplication.cs:1789 |
| `TrayApplication.cs:1814` | CreatePopupMenu | libéré : TrayApplication.cs:1815 |
| `TrayApplication.cs:1833` | CreatePopupMenu | libéré : TrayApplication.cs:1838 |
| `TrayApplication.cs:1860` | CreatePopupMenu | libéré : TrayApplication.cs:1863 |
| `TrayApplication.cs:1894` | CreatePopupMenu | libéré : TrayApplication.cs:1895 |
| `TrayApplication.cs:1898` | CreatePopupMenu | libéré : TrayApplication.cs:1899 |
| `TrayApplication.cs:1906` | CreatePopupMenu | libéré : TrayApplication.cs:1910 |
| `GdiImageLoader.cs:37` | CreateStreamOnHGlobal | libéré : flux relâché dans le finally (GdiImageLoader.cs:51), la mémoire HGLOBAL part avec lui (fDeleteOnRelease) ; GlobalFree si la création échoue (40) (lu) |
| `AboutWindow.cs:162` | CreateWindowExW | libéré : AboutWindow.cs:507 |
| `AboutWindow.cs:176` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (AboutWindow.cs:507) |
| `AboutWindow.cs:182` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (AboutWindow.cs:507) |
| `AboutWindow.cs:188` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (AboutWindow.cs:507) |
| `AboutWindow.cs:198` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (AboutWindow.cs:507) |
| `AboutWindow.cs:205` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (AboutWindow.cs:507) |
| `CharacterSearch.cs:641` | CreateWindowExW | libéré : CharacterSearch.cs:1619 |
| `CharacterSearch.cs:668` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (CharacterSearch.cs:1619) |
| `CharacterSearch.cs:673` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (CharacterSearch.cs:1619) |
| `LayerIndicatorWindow.cs:35` | CreateWindowExW | libéré : LayerIndicatorWindow.cs:174 |
| `LayoutConflictWindow.cs:165` | CreateWindowExW | libéré : LayoutConflictWindow.cs:524 |
| `LayoutConflictWindow.cs:193` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (LayoutConflictWindow.cs:524) |
| `LayoutConflictWindow.cs:198` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (LayoutConflictWindow.cs:524) |
| `LessonsWindow.cs:512` | CreateWindowExW | libéré : LessonsWindow.cs:3302 |
| `MaintainableLayersWindow.cs:143` | CreateWindowExW | libéré : MaintainableLayersWindow.cs:576 |
| `MaintainableLayersWindow.cs:173` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (MaintainableLayersWindow.cs:576) |
| `MaintainableLayersWindow.cs:185` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (MaintainableLayersWindow.cs:576) |
| `MaintainableLayersWindow.cs:202` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (MaintainableLayersWindow.cs:576) |
| `MaintainableLayersWindow.cs:214` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (MaintainableLayersWindow.cs:576) |
| `OnboardingWindow.cs:490` | CreateWindowExW | libéré : OnboardingWindow.cs:2229 |
| `OnboardingWindow.cs:511` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:517` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:527` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:552` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:558` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:565` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:571` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:577` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:603` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `OnboardingWindow.cs:628` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (OnboardingWindow.cs:2229) |
| `PauseDurationDialog.cs:144` | CreateWindowExW | libéré : PauseDurationDialog.cs:558 |
| `PauseDurationDialog.cs:263` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (PauseDurationDialog.cs:558) |
| `PauseDurationDialog.cs:275` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (PauseDurationDialog.cs:558) |
| `PauseDurationDialog.cs:288` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (PauseDurationDialog.cs:558) |
| `PauseDurationDialog.cs:304` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (PauseDurationDialog.cs:558) |
| `PauseDurationDialog.cs:333` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (PauseDurationDialog.cs:558) |
| `SettingsWindow.cs:1004` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:1966` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) (lu) |
| `SettingsWindow.cs:425` | CreateWindowExW | libéré : SettingsWindow.cs:2531 |
| `SettingsWindow.cs:444` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:456` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:465` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:476` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:481` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:486` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:492` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:499` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:509` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:528` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:533` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:546` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:551` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:561` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:570` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:575` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:582` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:587` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:592` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:603` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:607` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) |
| `SettingsWindow.cs:630` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (SettingsWindow.cs:2531) (lu) |
| `ToggleNotification.cs:55` | CreateWindowExW | libéré : ToggleNotification.cs:176 |
| `TrayApplication.cs:198` | CreateWindowExW | libéré : DestroyWindow à l'arrêt (TrayApplication.cs:2346) ; fenêtre unique du processus (lu) |
| `UsageStatsWindow.cs:194` | CreateWindowExW | libéré : UsageStatsWindow.cs:772 |
| `UsageStatsWindow.cs:212` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (UsageStatsWindow.cs:772) |
| `UsageStatsWindow.cs:225` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (UsageStatsWindow.cs:772) (lu) |
| `UsageStatsWindow.cs:231` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (UsageStatsWindow.cs:772) |
| `UsageStatsWindow.cs:238` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (UsageStatsWindow.cs:772) |
| `UsageStatsWindow.cs:243` | CreateWindowExW | fenêtre enfant de _hWnd : détruite avec elle (UsageStatsWindow.cs:772) |
| `VirtualKeyboard.cs:341` | CreateWindowExW | libéré : VirtualKeyboard.cs:1350 |
| `VirtualKeyboard.cs:373` | CreateWindowExW | libéré : VirtualKeyboard.cs:1345 |
| `ConfigManager.cs:1614` | FileStream | using : fermé en fin de bloc |
| `DailyChallenge.cs:99` | FileStream | using : fermé en fin de bloc |
| `LessonCatalog.cs:188` | FileStream | using : fermé en fin de bloc |
| `LessonProgressStore.cs:545` | FileStream | using : fermé en fin de bloc |
| `UsageStats.cs:726` | FileStream | using : fermé en fin de bloc |
| `AboutWindow.cs:97` | GdiplusStartup | arrêté : AboutWindow.cs:525 |
| `NativeWindow.cs:176` | GdiplusStartup | arrêté : NativeWindow.cs:184 |
| `OnboardingWindow.cs:329` | GdiplusStartup | arrêté : OnboardingWindow.cs:2234 |
| `SettingsWindow.cs:308` | GdiplusStartup | arrêté : SettingsWindow.cs:2539 |
| `TrayApplication.cs:2962` | GdiplusStartup | arrêté : TrayApplication.cs:2973 |
| `TrayIcon.cs:163` | GdiplusStartup | arrêté : TrayIcon.cs:74, TrayIcon.cs:183 |
| `TrayIcon.cs:52` | GdiplusStartup | arrêté : TrayIcon.cs:74, TrayIcon.cs:183 |
| `AboutWindow.cs:91` | GetDC | libéré : AboutWindow.cs:93 |
| `CharacterSearch.cs:1096` | GetDC | libéré : CharacterSearch.cs:1099 |
| `CharacterSearch.cs:714` | GetDC | libéré : CharacterSearch.cs:716 |
| `CharacterSearch.cs:769` | GetDC | libéré : CharacterSearch.cs:771 |
| `DevRects.cs:335` | GetDC | libéré : DevRects.cs:338 |
| `GdiHelpers.cs:39` | GetDC | libéré : GdiHelpers.cs:57 |
| `LayoutConflictWindow.cs:345` | GetDC | libéré : LayoutConflictWindow.cs:353 |
| `LayoutConflictWindow.cs:94` | GetDC | libéré : LayoutConflictWindow.cs:96 |
| `LessonsWindow.HomeText.cs:234` | GetDC | libéré : LessonsWindow.HomeText.cs:236 |
| `LessonsWindow.cs:1804` | GetDC | libéré : LessonsWindow.cs:1816 |
| `LessonsWindow.cs:1834` | GetDC | libéré : LessonsWindow.cs:1854 |
| `LessonsWindow.cs:1919` | GetDC | libéré : LessonsWindow.cs:1932 |
| `LessonsWindow.cs:252` | GetDC | libéré : LessonsWindow.cs:254 |
| `LessonsWindow.cs:991` | GetDC | libéré : LessonsWindow.cs:1017 |
| `MaintainableLayersWindow.cs:287` | GetDC | libéré : MaintainableLayersWindow.cs:313 |
| `OnboardingWindow.cs:1788` | GetDC | libéré : OnboardingWindow.cs:1849 |
| `OnboardingWindow.cs:1863` | GetDC | libéré : OnboardingWindow.cs:1873 |
| `OnboardingWindow.cs:321` | GetDC | libéré : OnboardingWindow.cs:323 |
| `OnboardingWindow.cs:786` | GetDC | libéré : OnboardingWindow.cs:789 |
| `OnboardingWindow.cs:801` | GetDC | libéré : OnboardingWindow.cs:804 |
| `PaintBuffer.cs:50` | GetDC | libéré : PaintBuffer.cs:53 |
| `SettingsWindow.cs:1876` | GetDC | libéré : SettingsWindow.cs:1884 |
| `SettingsWindow.cs:302` | GetDC | libéré : SettingsWindow.cs:304 |
| `SettingsWindow.cs:356` | GetDC | libéré : SettingsWindow.cs:383 |
| `ToggleNotification.cs:36` | GetDC | libéré : ToggleNotification.cs:38 |
| `TrayApplication.cs:3195` | GetDC | libéré : TrayApplication.cs:3258 |
| `UsageStatsWindow.cs:131` | GetDC | libéré : UsageStatsWindow.cs:133 |
| `UsageStatsWindow.cs:319` | GetDC | libéré : UsageStatsWindow.cs:329 |
| `VirtualKeyboard.cs:958` | GetDC | libéré : VirtualKeyboard.cs:962 |
| `CharacterSearch.cs:1244` | GlobalAlloc | libéré : CharacterSearch.cs:1251 |
| `GdiImageLoader.cs:18` | GlobalAlloc | libéré : GdiImageLoader.cs:25 |
| `LessonsWindow.cs:1269` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `LinkBehavior.cs:72` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `NativeWindow.cs:50` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `OnboardingWindow.cs:1224` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `OnboardingWindow.cs:1230` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `SettingsWindow.cs:1794` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `VirtualKeyboard.cs:825` | LoadCursorW | aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit |
| `Program.cs:49` | Mutex | libéré : using var jusqu'à la fin de Main (Program.cs:137, 149) (lu) |
| `TextEditBuffer.cs:30` | OpenClipboard | libéré : TextEditBuffer.cs:32 |
| `TypingEngine.Windows/Win32Api/RealWin32Api.cs:145` | OpenProcess | libéré : TypingEngine.Windows/Win32Api/RealWin32Api.cs:178 |
| `TypingEngine.Windows/Win32Api/RealWin32Api.cs:68` | OpenProcess | libéré : TypingEngine.Windows/Win32Api/RealWin32Api.cs:83 |
| `TypingEngine.Windows/Win32Api/RealWin32Api.cs:92` | OpenProcess | libéré : TypingEngine.Windows/Win32Api/RealWin32Api.cs:103 |
| `RestartRegistration.cs:41` | RegisterApplicationRestart | aucun objet : inscription du redémarrage auprès de Windows, une par processus (lu) |
| `NativeWindow.cs:56` | RegisterClassExW | jamais : une classe par type de fenêtre, inscrite une fois (RegisterOrReplace), désinscrite au Dispose de la fenêtre (12 UnregisterClass ; la classe du tray vit avec le processus) (lu) |
| `LessonsAutomation.cs:1083` | SafeArrayCreateVector | libéré : LessonsAutomation.cs:1090 |
| `LayoutConflictWindow.cs:399` | SaveDC | libéré : LayoutConflictWindow.cs:401 |
| `LessonsWindow.HomeText.cs:456` | SaveDC | libéré : LessonsWindow.HomeText.cs:505 |
| `LessonsWindow.Notation.cs:79` | SaveDC | libéré : LessonsWindow.Notation.cs:143 |
| `LessonsWindow.Paper.cs:358` | SaveDC | libéré : LessonsWindow.Paper.cs:393 |
| `LessonsWindow.Paper.cs:579` | SaveDC | libéré : LessonsWindow.Paper.cs:608 |
| `PaperChoice.cs:220` | SaveDC | libéré : PaperChoice.cs:223 |
| `SettingsWindow.Paper.cs:280` | SaveDC | libéré : SettingsWindow.Paper.cs:283 |
| `SettingsWindow.Paper.cs:348` | SaveDC | libéré : SettingsWindow.Paper.cs:357 |
| `SettingsWindow.Paper.cs:391` | SaveDC | libéré : SettingsWindow.Paper.cs:395 |
| `SettingsWindow.cs:1538` | SaveDC | libéré : SettingsWindow.cs:1540 |
| `TablatureRenderer.cs:237` | SaveDC | libéré : TablatureRenderer.cs:302 |
| `UsageStatsWindow.cs:700` | SaveDC | libéré : UsageStatsWindow.cs:703 |
| `LessonsWindow.cs:2073` | SetCapture | libéré : LessonsWindow.cs:2097, LessonsWindow.cs:2104, LessonsWindow.cs:2283 |
| `LessonsWindow.cs:2273` | SetCapture | libéré : LessonsWindow.cs:2097, LessonsWindow.cs:2104, LessonsWindow.cs:2283 |
| `SettingsWindow.CompatScroll.cs:152` | SetCapture | libéré : SettingsWindow.CompatScroll.cs:23, SettingsWindow.CompatScroll.cs:163 |
| `TypingEngine.Windows/ForegroundMonitor.cs:174` | SetWinEventHook | libéré : TypingEngine.Windows/ForegroundMonitor.cs:343, TypingEngine.Windows/ForegroundMonitor.cs:348, TypingEngine.Windows/ForegroundMonitor.cs:353 |
| `TypingEngine.Windows/ForegroundMonitor.cs:176` | SetWinEventHook | libéré : TypingEngine.Windows/ForegroundMonitor.cs:343, TypingEngine.Windows/ForegroundMonitor.cs:348, TypingEngine.Windows/ForegroundMonitor.cs:353 |
| `TypingEngine.Windows/ForegroundMonitor.cs:183` | SetWinEventHook | libéré : TypingEngine.Windows/ForegroundMonitor.cs:343, TypingEngine.Windows/ForegroundMonitor.cs:348, TypingEngine.Windows/ForegroundMonitor.cs:353 |
| `TypingEngine.Windows/Win32Api/RealWin32Api.cs:183` | SetWinEventHook | libéré : TypingEngine.Windows/Win32Api/RealWin32Api.cs:185 |
| `CharacterSearch.cs:686` | SetWindowSubclass | retirée : CharacterSearch.cs:1609 |
| `ChipButton.cs:37` | SetWindowSubclass | retirée : ChipButton.cs:60 |
| `LightButton.cs:49` | SetWindowSubclass | retirée : LightButton.cs:71 |
| `LinkBehavior.cs:81` | SetWindowSubclass | retirée : LinkBehavior.cs:89 |
| `OnboardingWindow.cs:535` | SetWindowSubclass | retirée : OnboardingWindow.cs:2224, OnboardingWindow.cs:2225, OnboardingWindow.cs:2226 |
| `OnboardingWindow.cs:536` | SetWindowSubclass | retirée : OnboardingWindow.cs:2224, OnboardingWindow.cs:2225, OnboardingWindow.cs:2226 |
| `OnboardingWindow.cs:537` | SetWindowSubclass | retirée : OnboardingWindow.cs:2224, OnboardingWindow.cs:2225, OnboardingWindow.cs:2226 |
| `SettingsWindow.CompatScroll.cs:43` | SetWindowSubclass | retirée : SettingsWindow.CompatScroll.cs:63 |
| `SettingsWindow.cs:1017` | SetWindowSubclass | retirée : SettingsWindow.cs:2526 |
| `SettingsWindow.cs:462` | SetWindowSubclass | retirée : SettingsWindow.cs:2522, SettingsWindow.cs:2524 |
| `SettingsWindow.cs:471` | SetWindowSubclass | retirée : SettingsWindow.cs:2522, SettingsWindow.cs:2524 |
| `TypingEngine.Windows/KeyboardHook.cs:151` | SetWindowsHookEx | libéré : TypingEngine.Windows/KeyboardHook.cs:185, TypingEngine.Windows/KeyboardHook.cs:451 |
| `TypingEngine.Windows/KeyboardHook.cs:171` | SetWindowsHookEx | libéré : TypingEngine.Windows/KeyboardHook.cs:185, TypingEngine.Windows/KeyboardHook.cs:451 |
| `TrayApplication.cs:1122` | Shell_NotifyIconW NIM_ADD | libéré : TrayApplication.cs:2383 |
| `TrayApplication.cs:237` | Shell_NotifyIconW NIM_ADD | libéré : TrayApplication.cs:2383 |
| `LessonsAutomation.cs:552` | UiaHostProviderFromHwnd | transfert : le fournisseur hôte est rendu à UI Automation, qui le relâche (convention COM des paramètres de sortie) (lu) |
| `TrayApplication.cs:213` | WTSRegisterSessionNotification | libéré : TrayApplication.cs:2361 |
