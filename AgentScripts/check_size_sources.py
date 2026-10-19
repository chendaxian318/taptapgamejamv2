from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image
root = Path(__file__).resolve().parents[1]
e = root/'Verification/package-size-20261018'
entries = json.loads((e/'new-textures.json').read_text(encoding='utf-8'))['entries']
hashes = {}
for row in entries:
    p = root/row['source']
    sha = hashlib.file_digest(p.open('rb'), 'sha256').hexdigest()
    assert sha == row['sourceSha256'], row['source']
    hashes[row['source']] = sha
changed=[]
for backup in (root/'ChangeBackups/package-size-20261018').rglob('*.mat'):
    p=root/backup.relative_to(root/'ChangeBackups/package-size-20261018')
    if 'Models/MeshyShrimp' in p.as_posix():
        p=root/'Assets/HabitatIsland/Art/Legacy/MeshyShrimp'/p.name
    assert p.exists(), p
    if backup.read_bytes()!=p.read_bytes(): changed.append(str(p.relative_to(root)))
(e/'changed-materials.json').write_text(json.dumps(changed,indent=2),encoding='utf-8')
comparisons=[]
for before in sorted((e/'textures').glob('*-before.png')):
    after = before.with_name(before.name.replace('-before','-after'))
    a=np.array(Image.open(before).convert('RGBA')).astype(np.float32)
    b=np.array(Image.open(after).convert('RGBA')).astype(np.float32)
    assert a.shape==b.shape
    diff=np.abs(a-b)
    comparisons.append(dict(name=before.stem.removesuffix('-before'),meanAbsoluteError=float(diff.mean()),p99=float(np.percentile(diff,99)),max=float(diff.max()),scope='1024px GPU preview, not original-resolution source comparison; UI mip removal also changes filtering'))
(e/'source-and-preview-checks.json').write_text(json.dumps(dict(originalModelHashes=hashes,changedMaterials=changed,previews=comparisons),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(originalModelsUnchanged=len(hashes),changedMaterials=len(changed),previews=comparisons),indent=2))
