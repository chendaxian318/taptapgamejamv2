from pathlib import Path
import hashlib
import json
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
evidence = root / 'Verification' / 'release-playtest-20261018'
build = json.loads((evidence / 'build-report.json').read_text(encoding='utf-8-sig'))
assert build['result'] == 'Succeeded' and build['errors'] == 0, build
folder = Path(build['output']).resolve()
assert folder.parent == (root / 'Builds').resolve()
for name in ('HabitatIsland.exe', 'UnityPlayer.dll', 'HabitatIsland_Data'):
    assert (folder / name).exists(), name
shutil.copy2(evidence / '试玩说明.txt', folder / '试玩说明.txt')
shutil.copy2(evidence / '验收结果.txt', folder / '验收结果.txt')
files = sorted(p for p in folder.rglob('*') if p.is_file()
               and not any('DoNotShip' in part or 'DontShipItWithYourGame' in part for part in p.relative_to(folder).parts)
               and p.suffix.lower() != '.pdb')
archive = folder.with_suffix('.zip')
assert not archive.exists(), 'Do not overwrite an existing release archive'
with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as z:
    for p in files:
        z.write(p, str(Path(folder.name) / p.relative_to(folder)))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None, 'ZIP CRC verification failed'
    assert len(z.infolist()) == len(files)
digest = hashlib.file_digest(archive.open('rb'), 'sha256').hexdigest()
archive.with_suffix('.zip.sha256.txt').write_text(digest + '  ' + archive.name + '\n', encoding='utf-8')
result = dict(folder=str(folder), exe=str(folder / 'HabitatIsland.exe'), zip=str(archive),
              zipBytes=archive.stat().st_size, files=len(files), sha256=digest, crcVerified=True)
(evidence / 'delivery.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
