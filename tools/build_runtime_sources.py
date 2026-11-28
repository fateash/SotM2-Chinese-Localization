"""Extract corresponding dependency source, excluding old localization/diagnostic tools."""
import argparse
import hashlib
from pathlib import Path
import zipfile

PREFIXES=('work/tools/BepInEx-b782da4/','work/tools/Il2CppInterop-81a6f78/',
          'work/loader-research/','work/DummyGenerator/','work/InteropGenerator/')
PATCH='work/crash-research/interop-pr291.patch'

def build(source, output):
    output.parent.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(source) as old, zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as new:
        selected=[n for n in old.namelist() if n.startswith(PREFIXES) or n==PATCH]
        if not any(n.startswith(PREFIXES[0]) for n in selected) or not any(n.startswith(PREFIXES[1]) for n in selected):
            raise ValueError('Complete modified dependency sources required')
        for n in sorted(selected):
            if n.startswith('/') or '..' in Path(n).parts:raise ValueError('Unsafe source path')
            new.writestr(n,old.read(n))
        rebuild=old.read('Rebuild.ps1').decode('utf-8-sig')
        rebuild=rebuild.replace("RunDotnet @('build','work/ChinesePrototype','-c','Release','-m:1','-nr:false')\n",'')
        new.writestr('Rebuild-Runtime.ps1',rebuild.encode('utf-8-sig'))
        new.writestr('README.md',
            '# Corresponding runtime source for SotM2 Build 26880829\n\n'
            'Includes the supplied complete modified BepInEx and Il2CppInterop source trees, original adaptation patches, '
            'the follow-up interop patch, and the wrapper generation tools. Original copyright/license notices are retained.\n\n'
            'On Windows, install .NET SDK 8, extract this archive, and run `Rebuild-Runtime.ps1 -GameRoot "<your game directory>"`. '
            'NuGet restore requires internet access. The script checks the exact game binary and metadata hashes before using your own files. '
            'It builds loaders and generates interop wrappers; it does not include the game or install a plugin. '
            'This source-only package is separate from the player installer and contains no old localization plugin or gameplay evidence.\n\n'
            'The matching runtime binary SHA256 list is frozen in the player manifest. '
            'The historical complete source snapshot used here has SHA256 '+hashlib.sha256(source.read_bytes()).hexdigest()+'.\n')
    with zipfile.ZipFile(output) as z:
        if z.testzip():raise ValueError('Source archive verification failed')
    return output

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--out',type=Path,required=True)
    a=p.parse_args();print(build(a.source,a.out))
