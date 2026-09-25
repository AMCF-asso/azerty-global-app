import subprocess, re
repo = "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store"
def git(*a):
    return subprocess.run(["git", *a], cwd=repo, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
files = [l.split(":", 1)[1] for l in git("grep", "-l", "RegisterClassExW", "f0a98ba", "--", "src").splitlines()]
for f in files:
    if "Tests" in f or f.endswith("Win32.cs"):
        continue
    text = git("show", f"f0a98ba:{f}").splitlines()
    out = []
    for i, l in enumerate(text, 1):
        s = l.strip()
        if s.startswith("//"):
            continue
        if re.search(r"hbrBackground\s*=", l) or re.search(r"\bRegisterClassExW\(", l) or re.search(r"\bUnregisterClassW\(", l) \
           or re.search(r"\bDestroyWindow\(", l) or re.search(r"DeleteObject\(_hBgBrush|DeleteObject\(_h\w*Bg\w*Brush|DeleteObject\(_hBrushBg", l) \
           or re.search(r"void Dispose\(", l) or re.search(r"=\s*Win32\.CreateSolidBrush\(", l):
            out.append(f"{i}: {s[:130]}")
    print("=====", f)
    print("\n".join(out))
