"""Read-only ZIP/MSIX inventory, block integrity, PE flags and source freshness."""
from pathlib import Path
from io import BytesIO
import base64
import datetime as dt
import hashlib
import json
import struct
import subprocess
import sys
from urllib.parse import unquote
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
OUT = HERE/'evidence'
OUT.mkdir(exist_ok=True)
def sha(b): return hashlib.sha256(b).hexdigest()
def stamp(p): return dt.datetime.fromtimestamp(p.stat().st_mtime,dt.timezone.utc).isoformat()
def blockmap(z):
    bm = ET.fromstring(z.read('AppxBlockMap.xml'))
    checks = []
    for f in bm:
        name = f.attrib['Name'].replace('\\','/')
        actual = next(n for n in z.namelist() if unquote(n) == name)
        data = z.read(actual)
        blocks = [b for b in f if b.tag.rsplit('}',1)[-1] == 'Block']
        good = len(data) == int(f.attrib['Size']) and len(blocks) == max(1,(len(data)+65535)//65536)
        for i,b in enumerate(blocks):
            good = good and base64.b64encode(hashlib.sha256(data[i*65536:(i+1)*65536]).digest()).decode() == b.attrib['Hash']
        for b in f:
            if b.tag.rsplit('}',1)[-1] == 'FileHash':
                good = good and base64.b64encode(hashlib.sha256(data).digest()).decode() == b.attrib['Hash']
        checks.append({'file':name,'ok':good,'size':len(data)})
    return checks
def pe(data):
    off = struct.unpack_from('<I',data,0x3c)[0]
    machine = struct.unpack_from('<H',data,off+4)[0]
    flags = struct.unpack_from('<H',data,off+24+70)[0]
    return {'machine':hex(machine),'dll_characteristics':hex(flags),'high_entropy_va':bool(flags&0x20),'dynamic_base':bool(flags&0x40),'nx_compat':bool(flags&0x100),'guard_cf':bool(flags&0x4000)}

ci = '--ci' in sys.argv
result = {'at':dt.datetime.now(dt.timezone.utc).isoformat(),'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'source':'ci' if ci else 'local','bundles':[]}
paths = list((OUT/'ci-package').glob('*.msixbundle')) if ci else [ROOT/'msix'/n for n in ['AZERTYGlobal-1.3.0.0.msixbundle','AZERTYGlobal.msixbundle']]
for path in paths:
    fname = path.name
    b = path.read_bytes()
    row = {'name':fname,'sha256':sha(b),'size':len(b),'mtime_utc':stamp(path),'packages':[]}
    with zipfile.ZipFile(BytesIO(b)) as z:
        row['entries'] = z.namelist()
        row['blockmap'] = blockmap(z)
        for name in z.namelist():
            if not name.endswith('.msix'): continue
            with zipfile.ZipFile(BytesIO(z.read(name))) as p:
                manifest_raw = p.read('AppxManifest.xml')
                manifest = ET.fromstring(manifest_raw)
                ns = {'f':'http://schemas.microsoft.com/appx/manifest/foundation/windows10'}
                identity = manifest.find('f:Identity',ns).attrib
                arch = identity['ProcessorArchitecture']
                exe_name = next(n for n in p.namelist() if unquote(n)=='AZERTY Global.exe')
                exe = p.read(exe_name)
                pub = ROOT/f'src/bin/Release/net8.0-windows10.0.17763.0/win-{arch}/publish/AZERTY Global.exe'
                files = []
                for q in (ROOT/'src').rglob('*'):
                    if not q.is_file(): continue
                    rel = q.relative_to(ROOT/'src')
                    if any(x in ('bin','obj','TestSupport') or x.endswith('.Tests') for x in rel.parts): continue
                    if q.stat().st_mtime > pub.stat().st_mtime:
                        files.append({'path':rel.as_posix(),'mtime_utc':stamp(q)})
                (OUT/f'{"ci" if ci else "candidate"}-manifest-{arch}.xml').write_bytes(manifest_raw)
                if ci:
                    (OUT/'ci-exes').mkdir(exist_ok=True)
                    (OUT/'ci-exes'/f'AZERTYGlobal-{arch}.exe').write_bytes(exe)
                row['packages'].append({'name':name,'identity':identity,'entries':p.namelist(),'blockmap':blockmap(p),'exe_sha256':sha(exe),'exe_size':len(exe),'pe':pe(exe),'publish_sha256':sha(pub.read_bytes()),'publish_mtime_utc':stamp(pub),'sources_newer_than_publish':files,'suspicious_entries':[n for n in p.namelist() if n.lower().endswith(('.pfx','.cer','.pdb','.cs','.msixbundle'))]})
                if ci:
                    for field in ['publish_sha256','publish_mtime_utc','sources_newer_than_publish']:
                        row['packages'][-1].pop(field)
    result['bundles'].append(row)
wack = ET.parse(ROOT/'wack-report-v1.3.0.0.xml').getroot()
result['wack'] = {'attributes':wack.attrib,'tests':[{'name':t.attrib.get('NAME'),'optional':t.attrib.get('OPTIONAL'),'result':t.findtext('RESULT')} for t in wack.findall('.//TEST')]}
(OUT/('ci-package.json' if ci else 'package.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for b in result['bundles']:
    print(b['name'],b['sha256'],b['size'])
    for p in b['packages']:
        print(p['identity']['ProcessorArchitecture'], p['pe'],'stale:',len(p.get('sources_newer_than_publish',[])) if not ci else 'N/A','blockmap_ok:',all(c['ok'] for c in p['blockmap']),'suspicious:',p['suspicious_entries'])
print('WACK',result['wack']['attributes'])
