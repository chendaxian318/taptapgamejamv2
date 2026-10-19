from pathlib import Path
import json,re,shutil
root=Path(__file__).resolve().parents[1]
source=root/'Docs/SourceEvidence/UI_Final_20261018'
target=root/'Assets/HabitatIsland/Resources/UI/Final20261018'
target.mkdir(parents=True,exist_ok=True)
records=json.loads((root/'Verification/ui-final-20261018/inventory.json').read_text(encoding='utf-8'))
manifest=[]
for r in records:
    path=r['path']
    if not path.endswith('.png'):continue
    match=re.search(r'资源\s*(\d+)@300x',path)
    key=None
    if match:
        prefix='Hover' if '鼠标' in path else 'Clear' if '通关' in path else 'R'
        key=prefix+match[1]
    elif '背景贴图' in path and '补充' not in path:
        group=path[:2]
        if group in ('03','05','07','12') and '背景贴图_画板' in path:key='Background'+group
    if not key:continue
    dst=target/(key+'.png')
    if dst.exists() and dst.read_bytes()!=(source/path).read_bytes():raise ValueError('Duplicate '+key)
    shutil.copy2(source/path,dst)
    manifest.append(dict(key=key,source=path,sha256=r['sha256']))
(root/'Verification/ui-final-20261018/imported.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Imported',len(manifest),'original transparent parts/backgrounds')


