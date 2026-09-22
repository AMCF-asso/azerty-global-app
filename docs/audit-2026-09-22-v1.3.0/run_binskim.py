"""Run Microsoft's documented standalone NuGet distribution in audit-local storage."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import urllib.request
import zipfile

HERE = Path(__file__).resolve().parent
OUT = HERE/'evidence'
DEP = HERE/'dependencies'/'binskim'
DEP.mkdir(parents=True,exist_ok=True)
base = 'https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.binskim/'
if len(sys.argv) > 1:
    version = sys.argv[1]
else:
    index = json.load(urllib.request.urlopen(base+'index.json',timeout=30))
    version = [v for v in index['versions'] if '-' not in v][-1]
url = base+version+'/microsoft.codeanalysis.binskim.'+version+'.nupkg'
pkg = DEP/('binskim-'+version+'.nupkg')
if not pkg.exists():
    with urllib.request.urlopen(url,timeout=90) as r:
        pkg.write_bytes(r.read())
with zipfile.ZipFile(pkg) as z:
    entries = [n for n in z.namelist() if '/win-x64/' in n and not n.endswith('/')]
    for n in entries:
        target = (DEP/n).resolve()
        if not target.is_relative_to(DEP.resolve()): raise ValueError('Invalid ZIP path')
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(z.read(n))
exe = next(DEP.rglob('BinSkim.exe'),None) or next(DEP.rglob('binskim.exe'))
summary = {'version':version,'url':url,'package_sha256':hashlib.sha256(pkg.read_bytes()).hexdigest(),'executable':str(exe),'runs':[]}
print('BinSkim',version,flush=True)
for arch in ['x64','arm64']:
    binary = OUT/'ci-exes'/('AZERTYGlobal-'+arch+'.exe')
    sarif = OUT/('binskim-'+arch+'.sarif')
    cmd = [str(exe),'analyze',str(binary),'--output',str(sarif),'--disable-telemetry','--kind','Fail;Pass;Review;Open;NotApplicable']
    try:
        p = subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180,creationflags=subprocess.CREATE_NO_WINDOW)
        raw, code = p.stdout,p.returncode
    except Exception as e:
        raw,code = str(e).encode(),127
    (OUT/('binskim-'+arch+'.log')).write_bytes(raw)
    row = {'arch':arch,'exit':code,'command':cmd,'sarif_exists':sarif.exists()}
    if sarif.exists():
        d = json.loads(sarif.read_text(encoding='utf-8-sig'))
        row['results'] = [{'ruleId':r.get('ruleId'),'level':r.get('level'),'kind':r.get('kind'),'message':r.get('message')} for run in d['runs'] for r in run.get('results',[])]
        row['invocations'] = [run.get('invocations',[]) for run in d['runs']]
    summary['runs'].append(row)
    (OUT/'binskim-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(row,ensure_ascii=False),flush=True)
