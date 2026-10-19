import json,struct,re,shutil,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
paths=set()
mapping_path=root/'Assets/HabitatIsland/Editor/TextureOptimizationMap.json'
# Keep textures added by later asset passes when refreshing the original scene audit.
# Otherwise that audit (which predates rod/net/magnet/cannon usage) drops their mappings.
if mapping_path.exists():
    paths.update(e['source'] for e in json.loads(mapping_path.read_text(encoding='utf-8-sig'))['entries'])
for f in (root/'Verification/audit-optimization-20261018').glob('inventory-level-*.json'):
    paths.update(t['path'] for t in json.loads(f.read_text(encoding='utf-8'))['sceneMaterialTextures'] if t['path'].endswith('.glb'))
records=[]
for path in sorted(paths):
    data=(root/path).read_bytes();pos=12;doc=None;binary=None
    while pos<len(data):
        length,kind=struct.unpack_from('<II',data,pos);chunk=data[pos+8:pos+8+length];pos+=8+length
        if kind==0x4e4f534a:doc=json.loads(chunk.rstrip(b'\0 '))
        if kind==0x004e4942:binary=chunk
    if not doc or binary is None:continue
    linear_images=set()
    color_images=set()
    for mat in doc.get('materials',[]):
        pbr=mat.get('pbrMetallicRoughness',{})
        for ref,linear in [(pbr.get('baseColorTexture'),False),(pbr.get('metallicRoughnessTexture'),True),(mat.get('normalTexture'),True),(mat.get('occlusionTexture'),True),(mat.get('emissiveTexture'),False)]:
            if ref is not None:
                source=doc['textures'][ref['index']]['source']
                (linear_images if linear else color_images).add(source)
    if linear_images & color_images:raise RuntimeError('Image shared by color and data channels; needs separate imports: '+path)
    for index,img in enumerate(doc.get('images',[])):
        if 'bufferView' not in img:continue
        view=doc['bufferViews'][img['bufferView']];off=view.get('byteOffset',0);raw=binary[off:off+view['byteLength']]
        name=img.get('name',f'image_{index}')
        ext='.jpg' if img.get('mimeType')=='image/jpeg' else '.png'
        out=Path('Assets/HabitatIsland/Art/OptimizedTextures')/Path(path).stem/(re.sub(r'[^\w.-]','_',name)+ext)
        (root/out).parent.mkdir(parents=True,exist_ok=True)
        if (root/out).exists() and (root/out).read_bytes()!=raw:raise RuntimeError('Conflicting extracted texture '+str(out))
        (root/out).write_bytes(raw)
        records.append(dict(source=path,sourceSha256=hashlib.sha256(data).hexdigest(),name=name,index=index,target=out.as_posix(),linear=index in linear_images))
    print(Path(path).name,[i.get('name') for i in doc.get('images',[])])
mapping_path.write_text(json.dumps({'entries':records},ensure_ascii=False,indent=2),encoding='utf-8')
backup=root/'ChangeBackups/20261018_Optimization/Materials'
for f in (root/'Assets/HabitatIsland').rglob('*.mat'):
    out=backup/f.relative_to(root);out.parent.mkdir(parents=True,exist_ok=True)
    if not out.exists():shutil.copy2(f,out)
print('Extracted',len(records),'original embedded image streams; backed up standalone materials.')
