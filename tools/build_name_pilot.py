"""Complete project payload with reversible clean install; no game or loader binaries."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
from build_dynamic_pilot import build as build_update

ROOT = Path(__file__).resolve().parents[1]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def build(dll, catalog, font, license_file, base_payload, out, commit, title_font=None, logos=None):
    # Reuse the existing pinned game/build and previous-version manifest provenance.
    temporary = out / 'manifest-source'
    build_update(dll, temporary, commit)
    old = json.loads(next(temporary.glob('*/manifest.json')).read_text())
    shutil.rmtree(temporary)
    package = out / 'SotM2-Names-Clean-Test-0.1.43-rc.1'
    (package / 'payload').mkdir(parents=True, exist_ok=False)
    sources = {'SotM2ChinesePrototype.dll': dll, 'surface-probe.json': ROOT / 'probe/dynamic-pilot.json',
               'sample-texts.json': catalog, 'NotoSansCJKsc-Regular.otf': font, 'OFL.txt': license_file}
    if title_font is not None:
        sources['NotoSansCJKsc-Bold.otf'] = title_font
    if logos is not None:
        sources['villain-logos.bytes'] = logos
    for name, source in sources.items():
        shutil.copy2(source, package / 'payload' / name)
    config = package / 'payload/sotm2.zh.prototype.cfg'
    config.write_bytes(b'[Prototype]\r\nEnableChinese = true\r\n')
    shutil.copy2(ROOT / 'probe/Manage-Names.ps1', package / 'Manage.ps1')
    shutil.copy2(ROOT / 'docs/NAME-CLEAN-TEST-0.1.43.md', package / '使用说明.md')
    runtime_files = ['BepInEx/core/BepInEx.Unity.IL2CPP.dll', 'BepInEx/core/Il2CppInterop.Runtime.dll',
                     'BepInEx/interop/Sentinels2.dll', 'BepInEx/interop/Sentinels2Engine.dll',
                     'BepInEx/interop/Unity.TextMeshPro.dll']
    manifest = {k: old[k] for k in ('version', 'buildId', 'liveStatus', 'sourceCommit', 'dllSha256', 'probeSha256', 'gameFiles')}
    manifest.update(pluginFiles={n: digest(package / 'payload' / n) for n in sources},
                    configSha256=digest(config), runtimeFiles={p: digest(base_payload / p) for p in runtime_files},
                    installationMode='clean-project-payload', catalogPolicy='reviewed-main-snapshot',
                    collectionStatus='held-until-game-available', imageReplacement=logos is not None,
                    imageReplacementScope='six-base-villain-LogoImage-only' if logos is not None else 'none')
    (package / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    actions = [('01-Clean-And-Install.cmd', 'CleanInstall'), ('02-Verify-Clean-State.cmd', 'Verify'),
               ('03-Collect-Evidence.cmd', 'Collect'), ('04-Restore-Previous.cmd', 'Restore')]
    for filename, action in actions:
        (package / filename).write_bytes(('@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage.ps1" -Action '
                                        + action + '\r\nset "rc=%errorlevel%"\r\npause\r\nexit /b %rc%\r\n').encode('ascii'))
    archive = out / (package.name + '.zip')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(package.rglob('*')):
            if p.is_file():
                z.write(p, p.relative_to(out))
    return archive

if __name__ == '__main__':
    p = argparse.ArgumentParser()
    for arg in ('dll', 'catalog', 'font', 'license-file', 'base-payload', 'out'):
        p.add_argument('--' + arg, type=Path, required=True)
    p.add_argument('--source-commit', required=True)
    p.add_argument('--title-font', type=Path, required=True)
    p.add_argument('--logos', type=Path, required=True)
    a = p.parse_args()
    print(build(a.dll, a.catalog, a.font, a.license_file, a.base_payload, a.out, a.source_commit, a.title_font, a.logos))
