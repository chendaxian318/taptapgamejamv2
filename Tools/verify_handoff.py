"""Verify the extracted handoff before development (Python standard library only)."""
import argparse
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    root = args.root.resolve()
    manifest = json.loads((root / 'MIGRATION_MANIFEST.json').read_text(encoding='utf-8'))
    missing, changed = [], []
    for entry in manifest['files']:
        path = (root / entry['path']).resolve()
        if not path.is_relative_to(root):
            raise ValueError('Manifest path escapes project root')
        if not path.is_file():
            missing.append(entry['path'])
            continue
        digest = hashlib.sha256()
        with path.open('rb') as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                digest.update(chunk)
        if path.stat().st_size != entry['bytes'] or digest.hexdigest() != entry['sha256']:
            changed.append(entry['path'])
    result = {'verified': not missing and not changed, 'files': len(manifest['files']),
              'missing': missing, 'changed': changed,
              'note': 'New files created by Unity are ignored. This verifies files, not gameplay.'}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['verified'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
