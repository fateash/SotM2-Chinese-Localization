"""Package the opt-in surface probe; never bundle game files or rewrite a catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.1.7-probe.1'
def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def build(dll, out, commit):
    package = out / ('SotM2-Surface-Probe-' + VERSION)
    if package.exists():
        raise ValueError('Output directory already exists: ' + str(package))
    (package / 'payload').mkdir(parents=True)
    shutil.copy2(dll, package / 'payload/SotM2ChinesePrototype.dll')
    shutil.copy2(ROOT / 'probe/surface-probe.json', package / 'payload/surface-probe.json')
    shutil.copy2(ROOT / 'probe/Manage.ps1', package / 'Manage.ps1')
    shutil.copy2(ROOT / 'docs/SURFACE-PROBE-0.1.7.md', package / '使用说明.md')
    for filename, action in [('01-Install-Probe.cmd', 'Install'), ('02-Collect-Evidence.cmd', 'Collect'), ('03-Restore-0.1.6.cmd', 'Restore')]:
        (package / filename).write_bytes(('@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage.ps1" -Action '+action+'\r\nset "rc=%errorlevel%"\r\npause\r\nexit /b %rc%\r\n').encode('ascii'))
    manifest = {
        'version': VERSION, 'buildId': '26880829', 'liveStatus': 'pending-user-test',
        'sourceCommit': commit,
        'dllSha256': digest(package / 'payload/SotM2ChinesePrototype.dll'),
        'probeSha256': digest(package / 'payload/surface-probe.json'),
        'acceptedDllSha256': '5d14bbe28756f3774249ff019c040666c55db9343c7ed3927014bf3996fb3933',
        'acceptedCatalogs': ['e6bbe9cb72239ffa866589d02afd9d5ad7347cf8c38b5375386d8dfe6f307524', '6549cf8ad931b11188fc8b8adedca39f3e283aaf8878fbc44cb557fc256037c5'],
        'gameFiles': {
            'GameAssembly.dll': 'f7d7ca3479310c4e09c90849a6d65c78fdc2cf3a648fbd6bf51c48601e6ed7ee',
            'Sentinels2_Data/il2cpp_data/Metadata/global-metadata.dat': 'b252acf9da519ce15729a43eb6b2cf6d643fda3200b2fdc7a146476819c05969'
        }
    }
    (package / 'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    archive = out / (package.name + '.zip')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(package.rglob('*')):
            if p.is_file():
                z.write(p, p.relative_to(out))
    return archive

if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--dll', type=Path, required=True)
    p.add_argument('--out', type=Path, default=ROOT / 'build-surface')
    p.add_argument('--source-commit', required=True)
    a = p.parse_args()
    print(build(a.dll, a.out, a.source_commit))
