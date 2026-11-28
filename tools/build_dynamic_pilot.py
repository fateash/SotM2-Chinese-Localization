"""Package the opt-in surface probe; never bundle game files or rewrite a catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.1.27-rc.1'
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
    shutil.copy2(ROOT / 'docs/DYNAMIC-PILOT-0.1.27.md', package / '使用说明.md')
    for filename, action in [('01-Install-Dynamic-Pilot.cmd', 'Install'), ('02-Collect-Evidence.cmd', 'Collect'), ('03-Restore-Previous.cmd', 'Restore')]:
        (package / filename).write_bytes(('@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage.ps1" -Action '+action+'\r\nset "rc=%errorlevel%"\r\npause\r\nexit /b %rc%\r\n').encode('ascii'))
    manifest = {
        'version': VERSION, 'buildId': '26880829', 'liveStatus': 'pending-user-test',
        'sourceCommit': commit,
        'dllSha256': digest(package / 'payload/SotM2ChinesePrototype.dll'),
        'probeSha256': digest(package / 'payload/surface-probe.json'),
        'acceptedInstallations': [
            {'dll': 'f70d4ab161aac01552bc2c7b2fd49ea718fffa2118521e086896d62106e0092d', 'probe': '304647aa69bd1b5a916c1f8d89cf2f789f5ce76a5e8cba1d9a1777d7bdd9237f'},
            {'dll': '70fee0c0d00df83c6e18040a8e99b5e2aa33c378eeccaee131539df76ab7a358', 'probe': 'd700a73f552d4ffd5c3427f097db8ad164f02ff7b7432ed2bad4e007f845d194'},
            {'dll': '9d07b0fefad312f5aec1c7a2e1d38f3455108ba1799284421b1551960662204a', 'probe': '24f488ec75feccf3488ec9dc4e00a9ce86ec85b2fcf33509216569f5454b867f'},
            {'dll': 'b750ae1f22687b98887a8a13724323493716bf77a692f67e527cd360edadc277', 'probe': 'ace70e032530818441cfe962016ec23e81992c56bfc471a2a5a2102a7f81d898'},
            {'dll': '75f0c399a5e52679fb04d15abd4bc3453ce9581b5096a4ac0cd421b82eef4f88', 'probe': '43655455491a7a9bf86fc3854d3ee00a04d0210994001abecc068f29b975c0e3'},
            {'dll': '33819e1b20e1f910ef822562cc5941d95a39181074aa2d360761d96648ca1a1e', 'probe': '264dd2f10f2e2a473c19f22e7aac6ba15dbcd08b8010772ff359f65d55678735'},
            {'dll': '79306d1ce713d826e3493aa282381fdc9a42b55be66827a190a28db26aecd38b', 'probe': 'ac4d19974cd5adbcca9ee5652611b94a8f946a986e8bbee8c0d7256c6de8ed29'},
            {'dll': 'bc59c5e78793376d0c7821e6d79fd93415fb45280602f6277c2a430dc9bcecbd', 'probe': '3b65ec6dba3771f1c3595d05069417db54cb622e2b7bc278ef18cd9733377258'},
            {'dll': '10cc2b255de9d1be0347ca1d231bf09c272081fa7067b1b14128920487062385', 'probe': '3b65ec6dba3771f1c3595d05069417db54cb622e2b7bc278ef18cd9733377258'},
            {'dll': '4dc5ee7cfe48336fb4a8f58888ecc0e12d6e9b9857e06a433f8856e8c19c9d05', 'probe': '9e6ca30ff232850be29a75facc387e219498f448eca229e12cc061b0cdb700e7'},
            {'dll': '9d16dbf14f22eb77e7cfdb8be7b6cb410d51ec29ca2e8791cb4b815a9f0c83c5', 'probe': '3f80c6c6a0c2fdb40b734d017e86fa194a0f7fbe620a1ce0d583fd2181c8d543'},
            {'dll': '8b4432b636c4f1cf60c8eec71706c49a5383bca06600db87841accf7add0375e', 'probe': '48ad9b5c12b2d6eccbfa6c6c1397998873d12bcfa6c77509db18a1785ce26bff'},
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
