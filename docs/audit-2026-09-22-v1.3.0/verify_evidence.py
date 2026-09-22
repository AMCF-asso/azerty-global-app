from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
OUT = HERE/'evidence'
summary = {'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()}
tests = {}
for name in ['core','windows','app']:
    tree = ET.parse(OUT/(name+'.trx'))
    counters = next(x for x in tree.getroot().iter() if x.tag.endswith('Counters'))
    tests[name] = counters.attrib
    assert int(counters.attrib['failed']) == 0
    assert int(counters.attrib['passed']) == int(counters.attrib['total'])
summary['tests'] = tests
summary['total_csharp'] = sum(int(x['passed']) for x in tests.values())
assert summary['total_csharp'] == 695
py = (OUT/'python-tests.log').read_text(encoding='utf-8')
assert re.search(r'Ran 125 tests',py) and py.rstrip().endswith('OK')
summary['python_tests'] = 125
race = ET.parse(HERE/'race-witness/evidence/foreground-aba-witness.trx').getroot()
race_counts = next(x for x in race.iter() if x.tag.endswith('Counters')).attrib
assert race_counts['total'] == '4' and race_counts['passed'] == '3' and race_counts['failed'] == '1'
failed_race = [x for x in race.iter() if x.tag.endswith('UnitTestResult') and x.attrib.get('outcome') == 'Failed']
assert len(failed_race) == 1 and 'Security_property_anti_cheat_A_must_remain_suspended_after_ABA' in failed_race[0].attrib['testName']
summary['new_race_witness'] = {'counters':race_counts,'expected_failure':failed_race[0].attrib['testName']}
for label,filename in [('local','package.json'),('ci','ci-package.json')]:
    d = json.loads((OUT/filename).read_text(encoding='utf-8'))
    for b in d['bundles']:
        path = (ROOT/'msix'/b['name']) if label == 'local' else OUT/'ci-package'/b['name']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == b['sha256']
        assert all(x['ok'] for x in b['blockmap'])
        for p in b['packages']:
            assert all(x['ok'] for x in p['blockmap'])
            assert not p['suspicious_entries']
    summary[label+'_bundle_sha256'] = d['bundles'][0]['sha256']
summary['tracked_product_diff_exit'] = subprocess.run(['git','diff','--exit-code','--','src','scripts','.github','msix'],cwd=ROOT,capture_output=True).returncode
assert summary['tracked_product_diff_exit'] == 0
summary['ci_code_diff_exit'] = subprocess.run(['git','diff','--exit-code','49187578ac918be8d66f1585964a09ee55190b84','HEAD','--','src','scripts','.github','msix/AppxManifest.xml','msix/Assets'],cwd=ROOT,capture_output=True).returncode
assert summary['ci_code_diff_exit'] == 0
scan = json.loads((OUT/'binskim-summary.json').read_text(encoding='utf-8'))
summary['binskim'] = [{'arch':r['arch'],'exit':r['exit'],'kinds':dict(Counter(x['kind'] for x in r['results'])),'executionSuccessful':[v.get('executionSuccessful') for inv in r['invocations'] for v in inv]} for r in scan['runs']]
links = []
for doc in [HERE/'rapport.md',HERE/'recette-finale.md']:
    for target in re.findall(r'\]\(([^)]+)\)',doc.read_text(encoding='utf-8')):
        if target.startswith('https://'): continue
        p = (doc.parent/target).resolve()
        links.append({'target':target,'exists':p.exists()})
        assert p.exists(),str(p)
summary['local_report_links'] = links
(OUT/'verification-final.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
