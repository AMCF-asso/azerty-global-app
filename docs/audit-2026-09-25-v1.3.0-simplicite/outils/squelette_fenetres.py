"""Squelette Win32 recopié d'une fenêtre à l'autre : présence de chaque élément par fichier.
python squelette_fenetres.py <racine_depot>   (aussi importé par metriques.py)"""
import os
import re
import subprocess
import sys

ELEMENTS = {
    'RegisterClassExW': r'RegisterClassExW\(ref ',
    'CreateWindowExW': r'CreateWindowExW\(',
    'UnregisterClassW (Dispose)': r'UnregisterClassW\(',
    'centrage moniteur du curseur': r'MonitorFromPoint\(cursorPt',
    'AdjustWindowRectEx': r'AdjustWindowRectEx\(',
    'DPI GetDeviceCaps(88)': r'GetDeviceCaps\(\w+, 88\)',
    'DPI GetDpiForWindow nu': r'GetDpiForWindow\(_hWnd\)',
    'DPI GetDpiForWindowOrDefault': r'GetDpiForWindowOrDefault\(',
    'catch « Windows 8.1- »': r'Windows 8\.1',
    'WM_DPICHANGED': r'case Win32\.WM_DPICHANGED',
    'WM_ERASEBKGND': r'case Win32\.WM_ERASEBKGND',
    'WM_CTLCOLOR*': r'case Win32\.WM_CTLCOLOR',
    'WM_PAINT': r'case Win32\.WM_PAINT',
    'WM_GETDLGCODE (sous-classe)': r'WM_GETDLGCODE',
    'LinkSubclassProc (liens survolés)': r'LinkSubclassProc',
    'SetWindowSubclass': r'SetWindowSubclass\(',
    'EnableDarkTitleBar': r'EnableDarkTitleBar\(',
    'DialogNavigation.Register': r'DialogNavigation\.Register\(',
    'CreateFonts/DestroyFonts/RecreateFonts': r'void (Create|Destroy|Recreate)Fonts\(',
    'GdiplusStartup': r'GdiplusStartup\(',
    "S(int) mise à l'échelle": r'int S\(int ',
    'hbrBackground = pinceau de l\'instance': r'hbrBackground = _h\w*B\w*rush',
    'hbrBackground = IntPtr.Zero': r'hbrBackground = IntPtr\.Zero',
    'commentaire recopié « 2e instance »': r'2e instance',
    'commentaire recopié « Windows recycle les HWND »': r'Windows recycle les HWND',
}


def mesurer(root):
    files = [f for f in subprocess.run(['git', '-C', root, 'ls-files', 'src'], capture_output=True, text=True,
                                       encoding='utf-8').stdout.splitlines()
             if f.endswith('.cs') and '.Tests/' not in f and 'TestSupport' not in f]
    table = {}
    for rel in files:
        with open(os.path.join(root, rel), encoding='utf-8-sig') as f:
            t = f.read()
        if rel.endswith('Win32.cs') or not re.search(ELEMENTS['RegisterClassExW'], t):
            continue
        table[rel] = {k: len(re.findall(p, t)) for k, p in ELEMENTS.items()}
    totals = {k: sum(1 for v in table.values() if v[k]) for k in ELEMENTS}
    return {'fichiers_enregistrant_une_classe': len(table), 'totaux_fichiers': totals, 'par_fichier': table}


if __name__ == '__main__':
    res = mesurer(sys.argv[1])
    print('fichiers qui enregistrent une classe de fenêtre :', res['fichiers_enregistrant_une_classe'])
    for k, n in res['totaux_fichiers'].items():
        print(f'  {n:2d} fichiers : {k}')
