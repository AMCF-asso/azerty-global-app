"""L'étape des cinq améliorations se coupe en deux écrans.

Arbitrage d'Antoine du 2026-08-30, après la planche de mesure. Le contexte : Onboarding mesure
désormais son contenu et les deux leviers globaux existent, mais à 175 % sur un portable 1080p il
manque encore ~130 px. Les cinq cartes de l'étape 1 pèsent **420 px sur 719**, soit 58 % de la
fenêtre, et aucun réglage ne comprime du contenu.

Trois options lui ont été posées : cartes sur une ligne, trois cartes au lieu de cinq, ou couper
l'étape en deux. Il a choisi la coupe — la seule qui ne perde rien : trois améliorations puis
deux, le wizard passe de trois à quatre étapes, et le prix est un clic de plus à la première
ouverture.

Les indices d'étape cessent au passage d'être des littéraux. Il y avait onze `_currentStep == 2`
ou `< 2` dispersés dans le fichier, chacun voulant dire « la dernière étape » sans le dire, et
les décaler tous d'un cran à la main est exactement le genre de changement où l'un est oublié.
Ils lisent maintenant `StepResources`, `StepCount` et leurs voisins.

La ligne sur la vie privée suit les deux dernières cartes : elle clôt la présentation, et la
présentation se termine désormais au second écran. Aucune chaîne de `Localization/` n'est
touchée — le titre des améliorations coiffe les deux écrans, qui sont deux moitiés d'une même
liste.
"""

import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "src" / "OnboardingWindow.cs"

data = TARGET.read_bytes()
if data[:3] != b"\xef\xbb\xbf":
    sys.exit("OnboardingWindow.cs a perdu son BOM")

LF_BEFORE = data.count(b"\n") - data.count(b"\r\n")
text = data.decode("utf-8")


def replace(name, old, new, expected=1):
    global text
    lines = old.replace("\r\n", "\n").split("\n")
    rx = re.compile(r"\r?\n".join(re.escape(l) for l in lines))
    matches = list(rx.finditer(text))
    if len(matches) != expected:
        sys.exit(f"{name} : {len(matches)} occurrence(s), {expected} attendue(s)\n---\n{old}\n---")
    out, cursor = [], 0
    for m in matches:
        eol = "\r\n" if "\r\n" in m.group(0) else "\n"
        out.append(text[cursor:m.start()])
        out.append(new.replace("\r\n", "\n").replace("\n", eol))
        cursor = m.end()
    out.append(text[cursor:])
    text = "".join(out)
    print(f"  {name:46s} {expected}×")


# ── 1. Les etapes prennent un nom ───────────────────────────────────────────
replace(
    "indices d'etape nommes",
    """    /// <summary>Nombre d'étapes, pour le banc.</summary>
    internal const int StepCountForCapture = 3;""",
    """    // ── Les quatre étapes ────────────────────────────────────────────
    // Coupées en quatre le 2026-08-30 : les cinq améliorations tenaient sur un seul écran de
    // 719 px dont elles occupaient 420, et la fenêtre ne pouvait pas tenir sur un 1080p à 175 %.
    // Trois puis deux. Les indices étaient onze littéraux dispersés — « == 2 » voulant dire
    // « la dernière » sans le dire — et les décaler à la main aurait oublié l'un d'eux.
    private const int StepFeaturesA = 0;
    private const int StepFeaturesB = 1;
    private const int StepUsage = 2;
    private const int StepResources = 3;
    private const int StepCount = 4;

    /// <summary>Nombre d'étapes, pour le banc.</summary>
    internal const int StepCountForCapture = StepCount;""",
)

# ── 2. Les onze litteraux ───────────────────────────────────────────────────
replace(
    "ShowStepForCapture",
    """        if (_currentStep == 2)
            _step3Reached = true;""",
    """        if (_currentStep == StepResources)
            _step3Reached = true;""",
)

replace(
    "visibilite de l'etape des ressources",
    """        int step3Vis = _currentStep == 2 ? 1 : 0;""",
    """        int step3Vis = _currentStep == StepResources ? 1 : 0;""",
)

replace(
    "libelle du bouton principal",
    """        string nextText = _currentStep == 2 ? L.Onboarding_LetsGo : L.Onboarding_Next;""",
    """        string nextText = _currentStep == StepResources ? L.Onboarding_LetsGo : L.Onboarding_Next;""",
)

replace(
    "repositionnement a la derniere etape",
    """        if (_currentStep == 2)
            RepositionControls();""",
    """        if (_currentStep == StepResources)
            RepositionControls();""",
)

replace(
    "avance par le bouton Suivant",
    """                        if (_currentStep < 2) { _currentStep++; if (_currentStep == 2) _step3Reached = true; UpdateStepVisibility(); }""",
    """                        if (_currentStep < StepResources) { _currentStep++; if (_currentStep == StepResources) _step3Reached = true; UpdateStepVisibility(); }""",
)

replace(
    "avance au clavier",
    """                    else if (_currentStep < 2)
                    {
                        _currentStep++;
                        if (_currentStep == 2) _step3Reached = true;""",
    """                    else if (_currentStep < StepResources)
                    {
                        _currentStep++;
                        if (_currentStep == StepResources) _step3Reached = true;""",
)

# ── 3. La barre de progression compte quatre segments ──────────────────────
replace(
    "barre de progression",
    """        int segW = barW / 3;""",
    """        int segW = barW / StepCount;""",
)

replace(
    "segments de la barre",
    """        for (int i = 0; i < 3; i++)
        {
            if (i > _currentStep) continue;
            int left = margin + i * segW + (i > 0 ? 1 : 0);
            int right = (i == 2) ? margin + barW : margin + (i + 1) * segW;""",
    """        for (int i = 0; i < StepCount; i++)
        {
            if (i > _currentStep) continue;
            int left = margin + i * segW + (i > 0 ? 1 : 0);
            int right = (i == StepCount - 1) ? margin + barW : margin + (i + 1) * segW;""",
)

# ── 4. L'etape 1 devient deux peintres ─────────────────────────────────────
replace(
    "PaintStep1 coupee en deux",
    """    private int PaintStep1(IntPtr hdc, int cw, int ch, int y)
    {
        int margin = S(BASE_MARGIN);
        y += S(18);

        // ── Titre ──
        Win32.SelectObject(hdc, _hFontStepSummary);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var stepTitleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(28) };
        Win32.DrawTextW(hdc, L.Onboarding_Step1Title, -1, ref stepTitleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += S(40);

        // ── Les 5 améliorations ──
        DrawFeatureWithHighlight(hdc, margin, cw, ref y, "1",
            L.Onboarding_Feature1Title,
            "");
        DrawFeature(hdc, margin, cw, ref y, "2",
            L.Onboarding_Feature2Title,
            L.Onboarding_Feature2Desc);
        DrawFeature(hdc, margin, cw, ref y, "3",
            L.Onboarding_Feature3Title,
            L.Onboarding_Feature3Desc);
        DrawFeatureWithHighlight(hdc, margin, cw, ref y, "4",
            L.Onboarding_Feature4Title,
            "");
        DrawFeatureWithHighlight(hdc, margin, cw, ref y, "5",
            L.Onboarding_Feature5Title,
            "");

        // ── Mention rassurante (vie privée) ──
        y += S(8);
        Win32.SelectObject(hdc, _hFontReassure);
        Win32.SetTextColor(hdc, CLR_REASSURE);
        string reassure = L.Onboarding_PrivacyReassurance;
        var reassureRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(18) };
        Win32.DrawTextW(hdc, reassure, -1, ref reassureRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        return reassureRect.bottom;
    }""",
    """    /// <summary>
    /// Premier écran des améliorations : les trois premières. Le titre coiffe les deux écrans —
    /// ils sont deux moitiés d'une même liste, et aucune chaîne n'a été ajoutée pour l'occasion.
    /// </summary>
    private int PaintStep1A(IntPtr hdc, int cw, int ch, int y)
    {
        int margin = S(BASE_MARGIN);
        y = DrawFeaturesTitle(hdc, margin, cw, y);

        DrawFeatureWithHighlight(hdc, margin, cw, ref y, "1",
            L.Onboarding_Feature1Title,
            "");
        DrawFeature(hdc, margin, cw, ref y, "2",
            L.Onboarding_Feature2Title,
            L.Onboarding_Feature2Desc);
        DrawFeature(hdc, margin, cw, ref y, "3",
            L.Onboarding_Feature3Title,
            L.Onboarding_Feature3Desc);
        return y;
    }

    /// <summary>
    /// Second écran des améliorations : les deux dernières, et la ligne sur la vie privée, qui
    /// clôt la présentation — donc qui suit la dernière carte, pas la troisième.
    /// </summary>
    private int PaintStep1B(IntPtr hdc, int cw, int ch, int y)
    {
        int margin = S(BASE_MARGIN);
        y = DrawFeaturesTitle(hdc, margin, cw, y);

        DrawFeatureWithHighlight(hdc, margin, cw, ref y, "4",
            L.Onboarding_Feature4Title,
            "");
        DrawFeatureWithHighlight(hdc, margin, cw, ref y, "5",
            L.Onboarding_Feature5Title,
            "");

        y += S(8);
        Win32.SelectObject(hdc, _hFontReassure);
        Win32.SetTextColor(hdc, CLR_REASSURE);
        var reassureRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(18) };
        Win32.DrawTextW(hdc, L.Onboarding_PrivacyReassurance, -1, ref reassureRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        return reassureRect.bottom;
    }

    /// <summary>Titre commun aux deux écrans d'améliorations, et le <c>y</c> d'après.</summary>
    private int DrawFeaturesTitle(IntPtr hdc, int margin, int cw, int y)
    {
        y += S(18);
        Win32.SelectObject(hdc, _hFontStepSummary);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var rect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(28) };
        Win32.DrawTextW(hdc, L.Onboarding_Step1Title, -1, ref rect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        return y + S(40);
    }""",
)

# ── 5. Les deux aiguillages ────────────────────────────────────────────────
replace(
    "aiguillage de peinture",
    """            case 0: PaintStep1(hdc, cw, ch, y); break;
            case 1: PaintStep2(hdc, gfx, cw, ch, y); break;
            case 2: PaintStep3(hdc, gfx, cw, ch, y); break;""",
    """            case StepFeaturesA: PaintStep1A(hdc, cw, ch, y); break;
            case StepFeaturesB: PaintStep1B(hdc, cw, ch, y); break;
            case StepUsage: PaintStep2(hdc, gfx, cw, ch, y); break;
            case StepResources: PaintStep3(hdc, gfx, cw, ch, y); break;""",
)

replace(
    "aiguillage de mesure",
    """                int bottom = step switch
                {
                    0 => PaintStep1(hdc, cw, 0, y),
                    1 => PaintStep2(hdc, gfx, cw, 0, y),
                    _ => PaintStep3(hdc, gfx, cw, 0, y),
                };""",
    """                int bottom = step switch
                {
                    StepFeaturesA => PaintStep1A(hdc, cw, 0, y),
                    StepFeaturesB => PaintStep1B(hdc, cw, 0, y),
                    StepUsage => PaintStep2(hdc, gfx, cw, 0, y),
                    _ => PaintStep3(hdc, gfx, cw, 0, y),
                };""",
)

# ── Verification ────────────────────────────────────────────────────────────
for line in text.splitlines():
    stripped = line.lstrip()
    if stripped.startswith("//") or stripped.startswith("///"):
        continue
    if "_currentStep == 2" in line or "_currentStep < 2" in line:
        sys.exit(f"un indice d'etape est reste litteral :\n{line}")

data = text.encode("utf-8")
lf_after = data.count(b"\n") - data.count(b"\r\n")
TARGET.write_bytes(data)
print(f"\n{TARGET.name} écrit — LF isolés {LF_BEFORE} → {lf_after}")
