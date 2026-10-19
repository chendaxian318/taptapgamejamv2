import json, struct, re, shutil, hashlib
from pathlib import Path

root = Path(__file__).resolve().parents[1]
backup = root / 'ChangeBackups/package-size-20261018'
evidence = root / 'Verification/package-size-20261018'
evidence.mkdir(parents=True, exist_ok=True)
sources = [
    'FishingRod/fishing_rod_unirig_animated.glb', 'Shovel/Shovel_PlayerReady.glb',
    'Magnet/Magnet_GameReady.glb', 'LandingNet/LandingNet.glb',
    'ShellBellows/Shell_Bellows_Rigged.glb', 'Cannon/Cannon_GameReady.glb',
]
mapping = root / 'Assets/HabitatIsland/Editor/TextureOptimizationMap.json'
paths = [mapping]
paths += list((root / 'Assets/HabitatIsland/Resources').rglob('*.mat'))
paths += list((root / 'Assets/HabitatIsland/Resources/UI/Delivered').glob('*.png.meta'))
paths += list((root / 'Assets/HabitatIsland/Resources/Models/MeshyShrimp').rglob('*'))
paths += [root / p for p in ['Assets/HabitatIsland/Editor/HabitatShrimpSetup.cs', 'Assets/HabitatIsland/Editor/HabitatPlanVisualChecks.cs', 'Assets/HabitatIsland/Scripts/HabitatView.cs']]
for p in paths:
    if p.is_file():
        dest = backup / p.relative_to(root)
        dest.parent.mkdir(parents=True, exist_ok=True)
        if not dest.exists(): shutil.copy2(p, dest)

records = json.loads(mapping.read_text(encoding='utf-8-sig'))['entries']
added = []
for relative in sources:
    path = 'Assets/HabitatIsland/Art/' + relative
    data = (root / path).read_bytes()
    pos, doc, binary = 12, None, None
    while pos < len(data):
        size, kind = struct.unpack_from('<II', data, pos)
        chunk = data[pos + 8:pos + 8 + size]; pos += 8 + size
        if kind == 0x4E4F534A: doc = json.loads(chunk.rstrip(b'\0 '))
        if kind == 0x004E4942: binary = chunk
    assert doc and binary is not None
    linear, color = set(), set()
    for mat in doc.get('materials', []):
        pbr = mat.get('pbrMetallicRoughness', {})
        for ref, is_linear in [(pbr.get('baseColorTexture'),False), (pbr.get('metallicRoughnessTexture'),True), (mat.get('normalTexture'),True), (mat.get('occlusionTexture'),True), (mat.get('emissiveTexture'),False)]:
            if ref is not None:
                index = doc['textures'][ref['index']]['source']
                (linear if is_linear else color).add(index)
    assert not linear & color, path
    for index, img in enumerate(doc.get('images', [])):
        assert 'bufferView' in img, path
        view = doc['bufferViews'][img['bufferView']]
        start = view.get('byteOffset', 0)
        raw = binary[start:start + view['byteLength']]
        name = img.get('name', f'image_{index}')
        ext = '.jpg' if img.get('mimeType') == 'image/jpeg' else '.png'
        out = Path('Assets/HabitatIsland/Art/OptimizedTextures') / Path(path).stem / (re.sub(r'[^\w.-]', '_', name) + ext)
        (root / out).parent.mkdir(parents=True, exist_ok=True)
        if (root / out).exists(): assert (root / out).read_bytes() == raw
        else: (root / out).write_bytes(raw)
        entry = dict(source=path, name=name, index=index, target=out.as_posix(), linear=index in linear, sourceSha256=hashlib.sha256(data).hexdigest())
        existing = [e for e in records if e['source'] == path and e['name'] == name]
        if existing: assert existing == [entry]
        else: records.append(entry)
        added.append(entry)
mapping.write_text(json.dumps({'entries':records},ensure_ascii=False,indent=2),encoding='utf-8')
(evidence/'new-textures.json').write_text(json.dumps({'entries':added},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Backed up originals; extracted {len(added)} original image streams without resampling or changing GLB files.')
