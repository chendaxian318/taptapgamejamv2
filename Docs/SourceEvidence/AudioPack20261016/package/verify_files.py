"""Read-only verification of the delivery files (Python 3 standard library)."""
import hashlib,json
from pathlib import Path
root=Path(__file__).resolve().parent
manifest=json.loads((root/'文件校验清单.json').read_text(encoding='utf-8'))
problems=[]
for item in manifest['files']:
    p=(root/item['path']).resolve()
    if root not in p.parents: raise ValueError('Path outside delivery')
    if not p.is_file(): problems.append('MISSING: '+item['path']);continue
    b=p.read_bytes()
    if len(b)!=item['bytes'] or hashlib.sha256(b).hexdigest()!=item['sha256']:problems.append('CHANGED: '+item['path'])
print(json.dumps({'ok':not problems,'checked':len(manifest['files']),'problems':problems},ensure_ascii=False,indent=2))
raise SystemExit(1 if problems else 0)
