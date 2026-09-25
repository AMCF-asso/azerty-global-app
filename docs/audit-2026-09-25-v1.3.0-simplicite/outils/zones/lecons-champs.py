"""Compte les champs d'instance (non static, non const) de LearningModule et LessonsWindow."""
import re
from pathlib import Path

SRC = Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")
pat = re.compile(r"^\s{4}(?:private|internal|public)\s+(?!static|const)(?:readonly\s+)?[\w<>\[\]?,(). ]+?\s+(_\w+)\s*(?:=[^;]*)?;")
for name in ("LearningModule.cs", "LessonsWindow.cs"):
    fields = []
    for i, line in enumerate((SRC / name).read_text(encoding="utf-8").splitlines(), 1):
        m = pat.match(line)
        if m:
            fields.append((i, m.group(1), "bool" in line.split(m.group(1))[0]))
    bools = [f for f in fields if f[2]]
    print(f"{name}: {len(fields)} champs d'instance, dont {len(bools)} booléens : {', '.join(b[1] for b in bools)}")
