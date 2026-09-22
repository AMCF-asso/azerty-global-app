from pathlib import Path
import subprocess
import json

HERE = Path(__file__).resolve().parent
OUT = HERE/'evidence'
REPO = 'AMCF-asso/azerty-global-app'
RUN = '35715039350'
commands = [
    ('ci-run.json',['gh','run','view',RUN,'--repo',REPO,'--json','conclusion,headSha,jobs,url']),
    ('ci-run.log',['gh','run','view',RUN,'--repo',REPO,'--log']),
    ('ci-artifacts.json',['gh','api',f'repos/{REPO}/actions/runs/{RUN}/artifacts']),
    ('ci-download.log',['gh','run','download',RUN,'--repo',REPO,'--name','msixbundle','--dir',str(OUT/'ci-package')]),
]
for name,cmd in commands:
    p = subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180)
    (OUT/name).write_bytes(p.stdout)
    print(json.dumps({'name':name,'exit':p.returncode,'bytes':len(p.stdout)}),flush=True)
    if p.returncode: print(p.stdout.decode('utf-8','replace')[-1500:],flush=True)
