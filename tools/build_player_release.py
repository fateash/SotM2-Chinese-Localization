"""Build a pinned player payload independently of the internal evidence/test kits."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.1.45-rc.1'
INSTALLER_REVISION = 2
PLUGIN = 'BepInEx/plugins/SotM2ChinesePrototype/'
PROJECT = ('SotM2ChinesePrototype.dll', 'sample-texts.json', 'display-rules.json',
           'NotoSansCJKsc-Regular.otf', 'NotoSansCJKsc-Bold.otf', 'OFL.txt',
           'villain-logos.bytes', 'villain-deckbacks.bytes')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def player_dll(path):
    data = path.read_bytes()
    if b'player-no-diagnostics' not in data or b'internal-diagnostics' in data:
        raise ValueError('PlayerRelease=true build required')
    for marker in ('SURFACE_PROBE ', 'ENV_TITLE_ARGUMENT_DIAGNOSTIC ', 'ENV_TITLE_SNAPSHOT_DIAGNOSTIC ',
                   'ENV_BACK_SNAPSHOT_DIAGNOSTIC ', 'CARD_TRANSLATED ', 'TITLE_INPUT_PROBE '):
        if marker.encode('utf-16le') in data:
            raise ValueError('Diagnostic marker in player assembly: ' + marker)

def build(dll, accepted, runtime, licenses, out, commit):
    player_dll(dll)
    content = json.loads((ROOT/'release/content-lock.json').read_text())
    for name, expected in content['files'].items():
        if sha(accepted/name) != expected:
            raise ValueError('Content differs from accepted baseline: '+name)
    if sha(ROOT/'probe/dynamic-pilot.json') != content['displayRulesSha256']:
        raise ValueError('Display configuration differs from accepted baseline')
    lock = json.loads((ROOT/'release/runtime-lock.json').read_text())
    package = out / ('SotM2-Chinese-Player-' + VERSION)
    package.mkdir(parents=True, exist_ok=False)
    payload = package/'payload'
    sources = {PLUGIN+n: accepted/n for n in PROJECT if n not in ('SotM2ChinesePrototype.dll','display-rules.json')}
    sources[PLUGIN+'SotM2ChinesePrototype.dll'] = dll
    sources[PLUGIN+'display-rules.json'] = ROOT/'probe/dynamic-pilot.json'
    for rel, expected in lock['files'].items():
        source = runtime/rel
        if sha(source) != expected:
            raise ValueError('Dependency differs from pinned baseline: '+rel)
        sources[rel] = source
    for rel, source in sources.items():
        target = payload/rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    cfg=payload/'BepInEx/config'
    cfg.mkdir(parents=True, exist_ok=True)
    (cfg/'sotm2.zh.prototype.cfg').write_bytes(b'[Prototype]\r\nEnableChinese = true\r\n')
    (cfg/'BepInEx.cfg').write_bytes(b'[IL2CPP]\r\nUpdateInteropAssemblies = false\r\nPreloadIL2CPPInteropAssemblies = true\r\n[Logging]\r\nUnityLogListening = false\r\n[Logging.Console]\r\nEnabled = false\r\n[Logging.Disk]\r\nEnabled = true\r\nAppendLog = false\r\nLogLevels = Fatal, Error, Warning, Message\r\n')
    shutil.copy2(ROOT/'release/Manage.ps1',package/'Manage.ps1')
    shutil.copy2(ROOT/'release/使用说明.md',package/'使用说明.md')
    for name, expected in lock['licenses'].items():
        src=licenses/name
        if sha(src)!=expected: raise ValueError('License changed: '+name)
        dst=package/'licenses'/name;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
    (package/'licenses/THIRD-PARTY-NOTICES.md').write_text(
        'Third-party runtime dependencies are pinned by SHA256 in manifest.json.\n'
        'BepInEx b782da4 and Il2CppInterop 81a6f78 use the Unity 6000.6 adaptation and follow-up fix supplied with the accepted runtime.\n'
        'Complete supplied modified source, original patches and interop generators accompany this package in SotM2-Runtime-Sources-26880829.zip.\n'
        'Upstream projects: https://github.com/BepInEx/BepInEx ; https://github.com/BepInEx/Il2CppInterop ; '
        'https://github.com/itsRevela/Moon-Sweeper-Mahjong ; https://github.com/NeighTools/UnityDoorstop ; '
        'https://github.com/BepInEx/Dobby ; https://github.com/dotnet/runtime ; https://github.com/notofonts/noto-cjk .\n'
        'Original license texts and dependency notices are preserved beside this notice.\n',encoding='utf-8')
    for filename, action in [('01-Install-or-Update.cmd','Install'),('02-Uninstall.cmd','Uninstall'),('03-Rollback.cmd','Rollback')]:
        (package/filename).write_bytes(('@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage.ps1" -Action '+action+'\r\nset "rc=%errorlevel%"\r\npause\r\nexit /b %rc%\r\n').encode('ascii'))
    files={p.relative_to(payload).as_posix():sha(p) for p in sorted(payload.rglob('*')) if p.is_file()}
    expected=set(lock['files'])|{PLUGIN+n for n in PROJECT}|{'BepInEx/config/BepInEx.cfg','BepInEx/config/sotm2.zh.prototype.cfg'}
    if set(files)!=expected: raise ValueError('Player payload is not the exact allowlist')
    manifest=dict(product='SotM2ChinesePlayer',version=VERSION,flavor='player-no-diagnostics',buildId='26880829',
                  sourceCommit=commit,installerRevision=INSTALLER_REVISION,installerSha256=sha(package/'Manage.ps1'),acceptedBaseline='07f51d2251d1f147d57dfa686a95c648a5a88bc7',
                  gameFiles=lock['gameFiles'],dependencies=list(lock['files']),files=files,correspondingSource=lock['correspondingSource'])
    (package/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    archive=out/(package.name+'.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(package.rglob('*')):
            if p.is_file(): z.write(p,p.relative_to(out))
    # Read the actual ZIP, not just the directory used as its input.
    with zipfile.ZipFile(archive) as z:
        if z.testzip(): raise ValueError('Corrupt archive')
        for rel, expected_hash in files.items():
            if hashlib.sha256(z.read(package.name+'/payload/'+rel)).hexdigest()!=expected_hash:
                raise ValueError('Archived payload mismatch: '+rel)
        actual=set(z.namelist())
        expected_entries={package.name+'/'+p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file()}
        if actual!=expected_entries: raise ValueError('Unexpected ZIP entries')
    return archive

if __name__=='__main__':
    p=argparse.ArgumentParser()
    for arg in ('dll','accepted-payload','runtime','licenses','out'):p.add_argument('--'+arg,type=Path,required=True)
    p.add_argument('--source-commit',required=True)
    a=p.parse_args();print(build(a.dll,a.accepted_payload,a.runtime,a.licenses,a.out,a.source_commit))
