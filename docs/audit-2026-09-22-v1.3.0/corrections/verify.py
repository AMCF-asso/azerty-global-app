"""Vérifications des corrections, séparées des preuves du candidat audité."""
from pathlib import Path
import datetime as dt
import json
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = HERE / 'evidence'
OUT.mkdir(exist_ok=True)
PS = r'C:/Users/antoi/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
env = os.environ.copy()
env['PYTHONPATH'] = str(HERE.parent / 'dependencies')
env['PYTHONDONTWRITEBYTECODE'] = '1'
env['PATH'] += r';C:\Program Files (x86)\Microsoft Visual Studio\Installer'
checks = {}
for short, project in [('core', 'TypingEngine.Core.Tests'), ('windows', 'TypingEngine.Windows.Tests'), ('app', 'AZERTYGlobal.Tests')]:
    checks[short] = ['dotnet', 'test', f'src/{project}/{project}.csproj', '-c', 'Release', '--no-restore', '--nologo', '--logger', f'trx;LogFileName={short}-verified.trx', '--results-directory', str(OUT)]
checks.update({
    'python': [sys.executable, '-B', '-m', 'unittest', 'discover', '-s', 'scripts/tests', '-v'],
    'identity': [sys.executable, '-B', 'scripts/list-identity-literals.py'],
    'schema': [sys.executable, '-B', 'scripts/validate-layout.py'],
    'docs': [sys.executable, '-B', 'scripts/check-doc-versions.py'],
    'publish-x64': ['dotnet', 'publish', 'src/AZERTYGlobal.csproj', '-c', 'Release', '-r', 'win-x64', '--no-restore'],
    'publish-arm64': ['dotnet', 'publish', 'src/AZERTYGlobal.csproj', '-c', 'Release', '-r', 'win-arm64', '--no-restore'],
    'binskim': [sys.executable, '-B', 'scripts/check-binary-hardening.py', '--package', str(HERE.parent/'dependencies/binskim/binskim-4.4.9.11.nupkg'), '--output', str(OUT/'binskim')],
    'pack': [PS, '-NoProfile', '-File', 'scripts/Pack-MSIX.ps1'],
    'release': [PS, '-NoProfile', '-File', 'scripts/Verify-Release.ps1'],
    'freshness': [PS, '-NoProfile', '-File', str(HERE.parent/'check_freshness.ps1')],
})
path = OUT/'checks.json'
results = json.loads(path.read_text(encoding='utf-8')) if path.exists() else []
failed = False
for name in sys.argv[1:]:
    command = checks[name]
    print('Vérification :', name, flush=True)
    start = time.monotonic()
    run = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=900)
    log = OUT/(name + '-' + dt.datetime.now().strftime('%H%M%S') + '.log')
    log.write_bytes(run.stdout)
    row = {'name': name, 'exit': run.returncode, 'seconds': round(time.monotonic()-start, 2), 'log': str(log.relative_to(HERE)), 'at': dt.datetime.now(dt.timezone.utc).isoformat()}
    if name in ('core', 'windows', 'app'):
        trx = ET.parse(OUT/f'{name}-verified.trx')
        counters = trx.find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
        row['tests'] = counters.attrib if counters is not None else {}
        row['validated'] = run.returncode == 0 and int(row['tests'].get('passed', 0)) > 0 and row['tests'].get('passed') == row['tests'].get('total')
    else:
        row['validated'] = run.returncode == 0
    results.append(row)
    path.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(row, ensure_ascii=False), flush=True)
    if not row['validated']:
        failed = True
        print(run.stdout.decode('utf-8', 'replace')[-3500:], flush=True)
        if name.startswith('publish') or name in ('binskim','pack'):
            break
sys.exit(1 if failed else 0)
