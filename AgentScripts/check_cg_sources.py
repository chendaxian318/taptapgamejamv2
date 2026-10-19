from pathlib import Path
import hashlib,json
root=Path('.');src=root/'Docs/SourceEvidence/CG20261018';dst=root/'Assets/StreamingAssets/StoryCG'
rows=[]
for name in ['00','01','03']:
 a=next(src.rglob('cg'+name+'*.mp4'));b=dst/('cg'+name+'.mp4');h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
 rows.append(dict(key='cg'+name,bytes=b.stat().st_size,source=str(a),runtime=str(b),sha256=h(b),identical=h(a)==h(b)))
assert all(v['identical'] for v in rows)
(root/'Verification/feedback-221708/cg-manifest.json').write_text(json.dumps(rows,indent=2,ensure_ascii=False),encoding='utf-8')
print('CG original bytes preserved:',sum(v['bytes'] for v in rows))
