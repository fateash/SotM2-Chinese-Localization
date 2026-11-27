"""Package the opt-in surface probe; never bundle game files or rewrite a catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.1.16-rc.1'
def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def build(dll, out, commit):
    package = out / ('SotM2-Dynamic-Pilot-' + VERSION)
    if package.exists():
        raise ValueError('Output directory already exists: ' + str(package))
    (package / 'payload').mkdir(parents=True)
    shutil.copy2(dll, package / 'payload/SotM2ChinesePrototype.dll')
    shutil.copy2(ROOT / 'probe/dynamic-pilot.json', package / 'payload/surface-probe.json')
    shutil.copy2(ROOT / 'probe/Manage-Dynamic.ps1', package / 'Manage.ps1')
    shutil.copy2(ROOT / 'docs/DYNAMIC-PILOT-0.1.16.md', package / '使用说明.md')
    for filename, action in [('01-Install-Dynamic-Pilot.cmd', 'Install'), ('02-Collect-Evidence.cmd', 'Collect'), ('03-Restore-Previous.cmd', 'Restore')]:
        (package / filename).write_bytes(('@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage.ps1" -Action '+action+'\r\nset "rc=%errorlevel%"\r\npause\r\nexit /b %rc%\r\n').encode('ascii'))
    manifest = {
        'version': VERSION, 'buildId': '26880829', 'liveStatus': 'pending-user-test',
        'sourceCommit': commit,
        'dllSha256': digest(package / 'payload/SotM2ChinesePrototype.dll'),
        'probeSha256': digest(package / 'payload/surface-probe.json'),
        'acceptedInstallations': [
            {'dll': '63b352f240e17c07f651e87af6dedfc6c2d4edb231b185a6e02851402065fbc9', 'probe': '30dcae9ac3569ae1a3c70d32f002518844965374fbcc6b07bd670b787ece6d65'},
            {'dll': 'd85216911b3a79b9d7f63e437f8020b1ae0ecbc018251d3952409e3cf6495df5', 'probe': '439d9c4f2a49535b50201c35eb676b145876ea4aee115c4878c0ad8ecba38542'},
            {'dll': '01c955d6058b8c8a3705ea3fc50f63eb325033e583d85974d3a18f4e81708a89', 'probe': '43ff2c101071f17a2cf4817b56b4a082d286877a1341b1d1d9fe0ed5385069b8'},
            {'dll': 'f0d3152c6fe8e7f230f0eac38ed173f4b6c5c0251fc32c67bff62f20da0cb680', 'probe': '8431c200bead26aacd4d1fcb2c188e12c18fbd0def76591e174fbbdf9f97101d'},
            {'dll': '96779366850d77ccdbb9349edff15cef668588df789f6be19c59d39fcf382f1c', 'probe': '8431c200bead26aacd4d1fcb2c188e12c18fbd0def76591e174fbbdf9f97101d'},
            {'dll': '57c8b6c114ff66cd1645b205166fe242fa583d9480fab960c50f1a7646fa8c81', 'probe': 'b2e37e175fdb4f51762aee39eb012bf9ecedf055af9d696430a22fd92c4e9cda'},
            {'dll': 'd1ec12b8980245cd1464436389e4c87e0fb553b5eaa456427fcacf00b5b3797e', 'probe': '06a1fee08545fbd59ba72513c491a80bfc7348bc1d7b19af80d72f1b6d1dc0e9'},
            {'dll': '9e7d148bf4767abe926198759c28349be1ab2783da49144ffc8fb44841d6d4b1', 'probe': 'c9130a82241d79d831425a13cbd0c7721a64758502579e5fa1be892c34e87294'},
            {'dll': '5d14bbe28756f3774249ff019c040666c55db9343c7ed3927014bf3996fb3933', 'probe': 'ABSENT'},
            {'dll': 'b9f4cb1c5091cf16d236ae58b082e53bfff90e9d11f51195dc164a121bdbdf4f', 'probe': 'd4143aab0e9a76ddba65bcef4fd667ae252916430670191c9bef364dfb4fd3f9'}
        ],
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
    p.add_argument('--out', type=Path, default=ROOT / 'build-dynamic')
    p.add_argument('--source-commit', required=True)
    a = p.parse_args()
    print(build(a.dll, a.out, a.source_commit))
