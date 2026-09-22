"""Non-destructive audit runner; retains full output and exit status per command."""
from pathlib import Path
import datetime as dt
import json
import os
import shutil
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
LOGS = HERE / 'evidence'
LOGS.mkdir(exist_ok=True)
PY = sys.executable
PS = shutil.which('pwsh') or 'powershell'
env = os.environ.copy()
env['PYTHONUTF8'] = '1'
env['PYTHONDONTWRITEBYTECODE'] = '1'
env['PYTHONPATH'] = str(HERE/'dependencies')
checks = [
    ('core-tests', ['dotnet','test','src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj','-c','Release','--no-restore','--logger','trx;LogFileName=core.trx','--results-directory',str(LOGS)]),
    ('windows-tests', ['dotnet','test','src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj','-c','Release','--no-restore','--logger','trx;LogFileName=windows.trx','--results-directory',str(LOGS)]),
    ('app-tests', ['dotnet','test','src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj','-c','Release','--no-restore','--logger','trx;LogFileName=app.trx','--results-directory',str(LOGS)]),
    ('python-tests', [PY,'-B','-m','unittest','discover','-s','scripts/tests','-v']),
    ('identity', [PY,'-B','scripts/list-identity-literals.py']),
    ('layout-schema', [PY,'-B','scripts/validate-layout.py']),
    ('layout-provenance', [PY,'-B','scripts/check-layout-provenance.py']),
    ('doc-versions', [PY,'-B','scripts/check-doc-versions.py']),
    ('doc-versions-release', [PY,'-B','scripts/check-doc-versions.py','--release']),
    ('verify-release', [PS,'-NoProfile','-File','scripts/Verify-Release.ps1']),
    ('freshness', [PS,'-NoProfile','-File',str(HERE/'check_freshness.ps1')]),
    ('nuget-vulnerabilities', ['dotnet','list','src/AZERTYGlobal.csproj','package','--vulnerable','--include-transitive','--format','json']),
]
selected = set(sys.argv[1:])
results_path = LOGS/'checks.json'
results = json.loads(results_path.read_text(encoding='utf-8')) if results_path.exists() else []
for name, cmd in checks:
    if selected and name not in selected:
        continue
    start = time.monotonic()
    print('RUN '+name, flush=True)
    try:
        p = subprocess.run(cmd, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=480)
        raw, code = p.stdout, p.returncode
    except subprocess.TimeoutExpired as e:
        raw, code = (e.stdout or b'') + b'\nAUDIT TIMEOUT\n', 124
    except OSError as e:
        raw, code = str(e).encode(), 127
    (LOGS/(name+'.log')).write_bytes(raw)
    decoded = raw.decode('utf-8','replace')
    blocked = '0x800711C7' in decoded or 'Could not load file or assembly' in decoded
    item = {'name':name,'command':cmd,'exit':code,'seconds':round(time.monotonic()-start,2),'application_control_block':blocked,'at':dt.datetime.now(dt.timezone.utc).isoformat()}
    results.append(item)
    (LOGS/'checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(item,ensure_ascii=False),flush=True)
    print('\n'.join(decoded.splitlines()[-5:]), flush=True)
