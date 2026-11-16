"""Inventaire des créations de ressources de src/ (audit des ressources, 16/11/2026).

Recense, hors projets de tests, chaque appel qui crée une minuterie, un abonnement ou un
objet Windows, et cherche sa libération : même variable libérée plus bas dans la même
méthode (objets graphiques, DC, poignées), ou partenaire nommé (KillTimer, -=, Unhook…).
Les sites sans partenaire trouvé sont marqués « à lire » : docs/audit-ressources.md donne le
verdict lu à la main pour chacun.

Usage : python scripts/audit-ressources-inventaire.py [--tsv sortie.tsv]
Règle de recherche : motifs CREATIONS ci-dessous, ligne par ligne, commentaires et
déclarations P/Invoke exclus ; appels coupés sur deux lignes recollés (ligne suivante jointe
quand la parenthèse ouverte n'est pas fermée).
"""
from __future__ import annotations

import argparse
import os
import re
import sys
from collections import Counter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")

# (catégorie, api, motif de création, motif de libération avec {v} = variable assignée)
CREATIONS = [
    # Minuteries et travail en arrière-plan
    ("minuterie", "SetTimer", r"\bSetTimer\s*\(", None),
    ("minuterie", "System.Threading.Timer", r"new\s+(System\.Threading\.)?Timer\s*\(", r"{v}\s*\??\.\s*(Dispose|Change)\b"),
    ("minuterie", "Thread", r"new\s+Thread\s*\(", None),
    ("minuterie", "ThreadPool", r"ThreadPool\.(QueueUserWorkItem|RegisterWaitForSingleObject)\s*\(", None),
    ("minuterie", "Task.Run", r"\bTask\.(Run|Delay|Factory\.StartNew)\s*\(", None),
    ("minuterie", "CancellationTokenSource", r"new\s+CancellationTokenSource\b", r"{v}\s*\??\.\s*Dispose\b"),
    # Abonnements
    ("abonnement", "event +=", r"\b(?P<evt>[A-Z]\w*)\s*\+=\s*(?!\d)", None),
    ("abonnement", "DialogNavigation.Register", r"DialogNavigation\.Register\s*\(", None),
    ("abonnement", "AppDomain", r"AppDomain\.CurrentDomain\.\w+\s*\+=", None),
    ("abonnement", "SystemEvents", r"SystemEvents\.\w+\s*\+=", None),
    # Objets Windows (USER, noyau, COM)
    ("windows", "SetWindowsHookEx", r"\bSetWindowsHookEx\w*\s*\(", None),
    ("windows", "SetWinEventHook", r"\bSetWinEventHook\s*\(", None),
    ("windows", "WTSRegisterSessionNotification", r"\bWTSRegisterSessionNotification\s*\(", None),
    ("windows", "SetWindowSubclass", r"\bSetWindowSubclass\s*\(", None),
    ("windows", "CoRegisterClassObject", r"\bCoRegisterClassObject\s*\(", None),
    ("windows", "AddFontMemResourceEx", r"\bAddFontMemResourceEx\s*\(", None),
    ("windows", "RegisterClassExW", r"\bRegisterClassExW\s*\(", None),
    ("windows", "GdiplusStartup", r"\bGdiplusStartup\s*\(", None),
    ("windows", "CreateWindowExW", r"\bCreateWindowExW\s*\(", None),
    ("windows", "Shell_NotifyIconW NIM_ADD", r"\bShell_NotifyIconW\s*\(\s*NIM_ADD", None),
    ("windows", "OpenProcess", r"\bOpenProcess\s*\(", r"CloseHandle\s*\(\s*{v}\b"),
    ("windows", "GlobalAlloc", r"\bGlobalAlloc\s*\(", r"GlobalFree\s*\(\s*{v}\b|SetClipboardData\s*\([^)]*{v}|CreateStreamOnHGlobal\s*\(\s*{v}"),
    ("windows", "CreateStreamOnHGlobal", r"\bCreateStreamOnHGlobal\s*\(", None),
    ("windows", "CoCreateInstance", r"\bCoCreateInstance\s*\(", None),
    ("windows", "CoInitializeEx", r"\bCoInitializeEx\s*\(", None),
    ("windows", "SafeArrayCreateVector", r"\bSafeArrayCreateVector\s*\(", r"SafeArrayDestroy\s*\(\s*{v}\b"),
    ("windows", "OpenClipboard", r"\bOpenClipboard\s*\(", None),
    ("windows", "SetCapture", r"\bSetCapture\s*\(", None),
    ("windows", "CreatePopupMenu", r"\bCreatePopupMenu\s*\(", r"DestroyMenu\s*\(\s*{v}\b|AppendMenuW\s*\([^;]*{v}"),
    ("windows", "CreateCaret", r"\bCreateCaret\s*\(", None),
    ("windows", "RegisterApplicationRestart", r"\bRegisterApplicationRestart\s*\(", None),
    ("windows", "UiaHostProviderFromHwnd", r"\bUiaHostProviderFromHwnd\s*\(", None),
    ("windows", "Mutex", r"new\s+Mutex\s*\(", None),
    ("windows", "FileStream", r"new\s+(FileStream|StreamWriter|StreamReader)\s*\(", None),
    ("windows", "GetDC", r"\bGetDC\s*\(", r"ReleaseDC\s*\([^;]*{v}\b"),
    ("windows", "BeginPaint", r"\bBeginPaint\s*\(", r"EndPaint\s*\("),
    ("windows", "SaveDC", r"\bSaveDC\s*\(", r"RestoreDC\s*\([^;]*{v}\b"),
    # Objets graphiques (GDI et GDI+)
    ("gdi", "CreateFontW", r"\bCreateFontW\s*\(", r"DeleteObject\s*\(\s*{v}\b"),
    ("gdi", "CreatePen", r"\bCreatePen\s*\(", r"DeleteObject\s*\(\s*{v}\b"),
    ("gdi", "CreateSolidBrush", r"\bCreateSolidBrush\s*\(", r"DeleteObject\s*\(\s*{v}\b"),
    ("gdi", "CreateBitmap", r"\bCreateBitmap\s*\(", r"DeleteObject\s*\(\s*{v}\b"),
    ("gdi", "CreateCompatibleBitmap", r"\bCreateCompatibleBitmap\s*\(", r"DeleteObject\s*\(\s*{v}\b"),
    ("gdi", "CreateCompatibleDC", r"\bCreateCompatibleDC\s*\(", r"DeleteDC\s*\(\s*{v}\b"),
    ("gdi", "CreateIconIndirect", r"\bCreateIconIndirect\s*\(", r"DestroyIcon\s*\(\s*{v}\b"),
    ("gdi", "GdipCreate", r"\bGdipCreate(?!HBITMAP)\w*\s*\(", r"Gdip(Delete\w+|DisposeImage)\s*\(\s*{v}\b"),
    ("gdi", "GdipCreateHBITMAPFromBitmap", r"\bGdipCreateHBITMAPFromBitmap\s*\(", r"DeleteObject\s*\(\s*{v}\b"),
    ("gdi", "GdipGetImageGraphicsContext", r"\bGdipGetImageGraphicsContext\s*\(", r"GdipDeleteGraphics\s*\(\s*{v}\b"),
]

DECL = re.compile(r"static\s+extern|static\s+partial|\[(DllImport|LibraryImport)|^\s*(internal|public|private)?\s*(static\s+)?(IntPtr|nint|bool|int|uint)\s+\w+\s*\([^)]*\)\s*(=>|;)")
ASSIGN = re.compile(r"(?:\b(?:var|IntPtr|nint)\s+)?(?P<v>[_A-Za-z][\w\.]*)\s*(?:\?\?)?=\s*[^=]")
OUTVAR = re.compile(r"out\s+(?:IntPtr|nint|var)?\s*(?P<v>[_A-Za-z]\w*)\s*\)")
EVENTS_DECL = re.compile(r"\bevent\s+[\w<>?,\. ]+\s+(?P<n>\w+)\s*[;{=]")
WINDOW_LINES = 120


def source_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = sorted(d for d in dirnames if not d.endswith("Tests") and d not in ("bin", "obj", "TestSupport"))
        for fn in sorted(filenames):
            if fn.endswith(".cs"):
                path = os.path.join(dirpath, fn)
                yield os.path.relpath(path, ROOT).replace("\\", "/"), path


def logical_lines(lines):
    """Rend (numéro, texte) en recollant un appel ouvert sur la ligne suivante."""
    i = 0
    while i < len(lines):
        text = lines[i].rstrip("\n")
        start = i
        while text.count("(") > text.count(")") and i + 1 < len(lines) and i - start < 4:
            i += 1
            text += " " + lines[i].strip()
        yield start + 1, text
        i += 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tsv")
    args = ap.parse_args()

    declared_events = set()
    files = list(source_files())
    for rel, path in files:
        with open(path, encoding="utf-8-sig") as f:
            for line in f:
                m = EVENTS_DECL.search(line)
                if m:
                    declared_events.add(m.group("n"))

    rows = []
    for rel, path in files:
        with open(path, encoding="utf-8-sig") as f:
            lines = f.readlines()
        for no, text in logical_lines(lines):
            s = text.strip()
            if s.startswith("//") or s.startswith("*") or DECL.search(s):
                continue
            for cat, api, pat, release in CREATIONS:
                m = re.search(pat, s)
                if not m:
                    continue
                if api == "event +=" and m.group("evt") not in declared_events:
                    continue
                var = None
                if release:
                    mo = OUTVAR.search(s[m.start():])
                    ma = ASSIGN.search(s[: m.start()]) if not mo else None
                    var = (mo or ma).group("v") if (mo or ma) else None
                partner = "à lire"
                if release and var:
                    rx = re.compile(release.format(v=re.escape(var)))
                    for k in range(no - 1 + 1, min(len(lines), no - 1 + WINDOW_LINES)):
                        if rx.search(lines[k]):
                            partner = f"{rel}:{k + 1}"
                            break
                elif release and not var:
                    rx = re.compile(release.replace("{v}", r"\w+"))
                    for k in range(no, min(len(lines), no + WINDOW_LINES)):
                        if rx.search(lines[k]):
                            partner = f"{rel}:{k + 1}"
                            break
                rows.append((cat, api, f"{rel}:{no}", var or "", partner, s[:150]))
                break

    counts = Counter((r[0], r[1]) for r in rows)
    per_cat = Counter(r[0] for r in rows)
    unread = sum(1 for r in rows if r[4] == "à lire")
    if args.tsv:
        with open(args.tsv, "w", encoding="utf-8") as o:
            o.write("categorie\tapi\tsite\tvariable\tliberation\ttexte\n")
            for r in rows:
                o.write("\t".join(r) + "\n")
    out = sys.stdout
    out.reconfigure(encoding="utf-8")
    for (cat, api), n in sorted(counts.items()):
        out.write(f"{cat:11} {api:32} {n}\n")
    out.write(f"Total {len(rows)} : " + ", ".join(f"{k} {v}" for k, v in sorted(per_cat.items())) + f" ; partenaire à lire : {unread}\n")


if __name__ == "__main__":
    main()
