"""Relier les sources locales au commit CI et vérifier le contenu des deux MSIX."""
from pathlib import Path
import hashlib
import io
import json
import subprocess
import sys
import tarfile

sys.stdout.reconfigure(encoding='utf-8')
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
commit='120b5075ec38e43ee601a7d2498d29424245e0de'
archive=subprocess.check_output(['git','archive',commit,'src','scripts','.github/workflows/ci.yml'],cwd=ROOT)
checked=[]
with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
    for member in tar:
        if not member.isfile(): continue
        expected=tar.extractfile(member).read()
        actual=(ROOT/member.name).read_bytes()
        assert expected==actual, member.name
        checked.append({'path':member.name,'sha256':hashlib.sha256(actual).hexdigest()})
(HERE/'evidence/source-snapshot.json').write_text(json.dumps({'commit':commit,'files':checked},indent=2),encoding='utf-8')
print('Sources identiques au commit CI :', len(checked), 'fichiers')

source=(HERE.parent/'inspect_package.py').read_text(encoding='utf-8')
source=source.replace('ROOT = HERE.parent.parent','ROOT = HERE.parents[2]')
local=HERE/'inspect_corrected_package.py'
local.write_text(source,encoding='utf-8')
result=subprocess.run([sys.executable,'-B',str(local)],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
(HERE/'evidence/package-inspection.log').write_bytes(result.stdout)
assert result.returncode==0
data=json.loads((HERE/'evidence/package.json').read_text(encoding='utf-8'))
data['source_commit_verified'] = commit
data['old_wack_applies_to_this_candidate'] = False
for bundle in data['bundles']:
    assert all(item['ok'] for item in bundle['blockmap'])
    assert {p['identity']['ProcessorArchitecture'] for p in bundle['packages']} == {'x64', 'arm64'}
    for package in bundle['packages']:
        assert all(item['ok'] for item in package['blockmap'])
        assert not package['suspicious_entries']
        assert not package['sources_newer_than_publish']
(HERE/'evidence/package.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print('Paquets inspectés :',len(data['bundles']))
