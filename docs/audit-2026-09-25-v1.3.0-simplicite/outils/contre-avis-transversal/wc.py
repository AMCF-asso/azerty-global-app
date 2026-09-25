import subprocess
repo = "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store"
def git(*a):
    return subprocess.run(["git", *a], cwd=repo, capture_output=True).stdout
files = git("ls-tree", "-r", "--name-only", "f0a98ba", "--", "src").decode("utf-8").splitlines()
cs = [f for f in files if f.endswith(".cs")]
prod = [f for f in cs if "Tests" not in f and "TestSupport" not in f]
tests = [f for f in cs if f not in prod]
def wc(f):
    return git("show", f"f0a98ba:{f}").count(b"\n")
tp = {f: wc(f) for f in prod}
tot = sum(tp.values())
print("cs", len(cs), "prod", len(prod), "tests", len(tests))
print("wc -l prod total", tot, "tests total", sum(wc(f) for f in tests))
top = sorted(tp.items(), key=lambda kv: -kv[1])[:8]
for f, n in top:
    print(n, f)
five = ["src/LearningModule.cs", "src/TrayApplication.cs", "src/LessonsWindow.cs", "src/SettingsWindow.cs", "src/OnboardingWindow.cs"]
s = sum(tp[f] for f in five)
print("5 fichiers", s, "part", round(100 * s / tot, 2))
