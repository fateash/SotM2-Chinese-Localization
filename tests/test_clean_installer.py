"""Exercise complete project cleanup and exact rollback using disposable installations."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PWSH = os.environ.get('SOTM2_PWSH') or shutil.which('pwsh')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

@unittest.skipUnless(PWSH, 'PowerShell required')
class CleanInstallerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.game, self.kit = root / 'game', root / 'kit'
        self.plugin = self.game / 'BepInEx/plugins/SotM2ChinesePrototype'
        self.plugin.mkdir(parents=True)
        (self.kit / 'payload').mkdir(parents=True)
        self.config = self.game / 'BepInEx/config/sotm2.zh.prototype.cfg'
        self.state = self.game / 'SotM2ChineseCleanState.json'
        self.cache = self.game / 'BepInEx/cache/chainloader_typeloader.dat'
        self.original = {
            'BepInEx/plugins/SotM2ChinesePrototype/SotM2ChinesePrototype.dll': b'old plugin',
            'BepInEx/plugins/SotM2ChinesePrototype/sample-texts.json': b'old text',
            'BepInEx/plugins/SotM2ChinesePrototype/old-probe.json': b'old probe',
            'BepInEx/plugins/old-test/SotM2ChinesePrototype-old.dll': b'duplicate',
            'BepInEx/config/sotm2.zh.prototype.cfg': b'[Prototype]\nEnableChinese = false\n',
            'BepInEx/cache/chainloader_typeloader.dat': b'old cache',
            'SotM2ChineseBackups/dynamic-pilot-0.1.18/old.dll.backup': b'old backup',
            'SotM2-prototype-install.json': b'old installation state',
        }
        for name, data in self.original.items():
            p = self.game / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(data)
        for name in ('BepInEx/plugins/OtherMod/Other.dll', 'save.dat', 'GameAssembly.dll', 'BepInEx/core/base.dll'):
            p = self.game / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b'unrelated sentinel')
        shutil.copy(ROOT / 'probe/Manage-Names.ps1', self.kit / 'Manage.ps1')
        names = ('SotM2ChinesePrototype.dll', 'surface-probe.json', 'sample-texts.json', 'NotoSansCJKsc-Regular.otf', 'OFL.txt')
        for name in names:
            (self.kit / 'payload' / name).write_bytes(('new ' + name).encode())
        cfg = self.kit / 'payload/sotm2.zh.prototype.cfg'
        cfg.write_bytes(b'[Prototype]\r\nEnableChinese = true\r\n')
        manifest = dict(version='0.1.19-rc.1', pluginFiles={n: sha(self.kit / 'payload' / n) for n in names},
                        configSha256=sha(cfg), gameFiles={'GameAssembly.dll': sha(self.game / 'GameAssembly.dll')},
                        runtimeFiles={'BepInEx/core/base.dll': sha(self.game / 'BepInEx/core/base.dll')})
        (self.kit / 'manifest.json').write_text(json.dumps(manifest))

    def run_script(self, action, ok=True):
        r = subprocess.run([PWSH, '-NoProfile', '-File', str(self.kit / 'Manage.ps1'), '-Action', action,
                            '-GamePath', str(self.game)], capture_output=True, text=True)
        self.assertEqual(r.returncode == 0, ok, r.stdout + r.stderr)
        return r

    def assert_unrelated(self):
        for name in ('BepInEx/plugins/OtherMod/Other.dll', 'save.dat', 'GameAssembly.dll', 'BepInEx/core/base.dll'):
            self.assertEqual((self.game / name).read_bytes(), b'unrelated sentinel')

    def assert_original(self):
        for name, data in self.original.items():
            self.assertEqual((self.game / name).read_bytes(), data, name)
        self.assert_unrelated()

    def test_dedicated_title_font_is_verified_and_restored(self):
        font = self.kit / 'payload/NotoSansCJKsc-Bold.otf'
        font.write_bytes(b'dedicated title font fixture')
        p = self.kit / 'manifest.json'
        manifest = json.loads(p.read_text())
        manifest['pluginFiles'][font.name] = sha(font)
        p.write_text(json.dumps(manifest))
        self.run_script('CleanInstall'); self.run_script('Verify')
        self.assertEqual((self.plugin / font.name).read_bytes(), font.read_bytes())
        (self.plugin / font.name).write_bytes(b'changed title font')
        self.run_script('Verify', False)
        (self.plugin / font.name).write_bytes(font.read_bytes())
        self.run_script('Restore'); self.assert_original()
        self.assertFalse((self.plugin / font.name).exists())

    def test_villain_logo_payload_is_verified_and_restored(self):
        art = self.kit / 'payload/villain-logos.bytes'
        art.write_bytes(b'approved logo fixture')
        p = self.kit / 'manifest.json'
        manifest = json.loads(p.read_text())
        manifest['pluginFiles'][art.name] = sha(art)
        p.write_text(json.dumps(manifest))
        self.run_script('CleanInstall'); self.run_script('Verify')
        self.assertEqual((self.plugin / art.name).read_bytes(), art.read_bytes())
        (self.plugin / art.name).write_bytes(b'changed logo')
        self.run_script('Verify', False)
        self.run_script('Restore', False)
        (self.plugin / art.name).write_bytes(art.read_bytes())
        self.run_script('Restore'); self.assert_original()
        self.assertFalse((self.plugin / art.name).exists())

    def test_clean_install_is_complete_and_archive_is_not_loaded(self):
        self.run_script('CleanInstall'); self.run_script('Verify'); self.assert_unrelated()
        self.assertEqual(len(list(self.plugin.iterdir())), 5)
        self.assertFalse((self.plugin / 'old-probe.json').exists())
        self.assertFalse((self.game / 'BepInEx/plugins/old-test/SotM2ChinesePrototype-old.dll').exists())
        self.assertFalse((self.game / 'SotM2ChineseBackups').exists())
        self.assertFalse(self.cache.exists())
        state = json.loads(self.state.read_text())
        archive = self.game / 'SotM2ChineseArchives' / state['archive'] / 'original'
        for name, data in self.original.items():
            self.assertEqual((archive / name).read_bytes(), data)

    def test_exact_restore_and_repeated_clean_install(self):
        self.run_script('CleanInstall'); first_state = self.state.read_bytes()
        self.run_script('CleanInstall'); self.run_script('Verify'); self.run_script('Restore')
        self.assertEqual(self.state.read_bytes(), first_state)
        self.run_script('Verify'); self.run_script('Restore'); self.assert_original()
        self.assertFalse(self.state.exists())

    def test_restore_accepts_regenerated_cache_and_config_comments(self):
        self.run_script('CleanInstall')
        self.cache.write_bytes(b'regenerated cache')
        self.config.write_text('# BepInEx generated comments\n[Prototype]\nEnableChinese = true\n')
        self.run_script('Verify'); self.run_script('Restore'); self.assert_original()

    def test_bad_payload_refused_before_cleanup(self):
        (self.kit / 'payload/sample-texts.json').write_bytes(b'tampered')
        self.run_script('CleanInstall', False); self.assert_original()
        self.assertFalse(self.state.exists())

    def test_changed_game_and_runtime_refused_before_cleanup(self):
        for name in ('GameAssembly.dll', 'BepInEx/core/base.dll'):
            p = self.game / name
            p.write_bytes(b'changed'); self.run_script('CleanInstall', False)
            self.assertEqual((self.plugin / 'old-probe.json').read_bytes(), b'old probe')
            p.write_bytes(b'unrelated sentinel')
        self.assert_original()

    def test_restore_refuses_new_user_file_or_changed_translation(self):
        self.run_script('CleanInstall')
        p = self.plugin / 'user.txt'; p.write_bytes(b'user work')
        self.run_script('Restore', False); self.assertEqual(p.read_bytes(), b'user work')
        p.unlink(); p = self.plugin / 'sample-texts.json'; p.write_bytes(b'edited translation')
        self.run_script('Restore', False); self.assertEqual(p.read_bytes(), b'edited translation')

    def test_symlink_is_rejected_before_cleanup(self):
        p = self.game / 'BepInEx/plugins/link'
        try:
            p.symlink_to(self.game / 'BepInEx/core', target_is_directory=True)
        except OSError:
            self.skipTest('symlink creation unavailable')
        self.run_script('CleanInstall', False); self.assert_original()

    def test_damaged_archive_refused_before_restore_mutation(self):
        self.run_script('CleanInstall')
        state = json.loads(self.state.read_text())
        p = self.game / 'SotM2ChineseArchives' / state['archive'] / 'original' / self.config.relative_to(self.game)
        p.write_bytes(b'damaged archive')
        self.run_script('Restore', False)
        self.run_script('Verify')

    def test_verifier_detects_reintroduced_duplicate(self):
        self.run_script('CleanInstall')
        (self.game / 'BepInEx/plugins/SotM2ChinesePrototype-copy.dll').write_bytes(b'duplicate')
        self.run_script('Verify', False)

    def test_collection_records_clean_state_and_actual_payload_hashes(self):
        self.run_script('CleanInstall')
        (self.game / 'BepInEx/LogOutput.log').write_text('test session')
        self.run_script('Collect')
        archive = next(self.kit.glob('SotM2-Dynamic-Evidence-*.zip'))
        with zipfile.ZipFile(archive) as z:
            files = {Path(n).name: n for n in z.namelist()}
            state = json.loads(z.read(files['clean-state-check.json']).decode('utf-8-sig'))
            hashes = json.loads(z.read(files['installed-hashes.json']).decode('utf-8-sig'))
        self.assertTrue(state['pureStateVerified'])
        self.assertEqual(hashes['sample-texts.json'], sha(self.plugin / 'sample-texts.json'))
        self.assertEqual(state['runtimeFiles']['BepInEx/core/base.dll'], sha(self.game / 'BepInEx/core/base.dll'))

    def test_running_game_blocks_cleanup(self):
        quote = lambda s: "'" + str(s).replace("'", "''") + "'"
        script = "function Get-Process { 'running' }; & " + quote(self.kit / 'Manage.ps1') + ' -Action CleanInstall -GamePath ' + quote(self.game)
        r = subprocess.run([PWSH, '-NoProfile', '-Command', script], capture_output=True, text=True)
        self.assertNotEqual(r.returncode, 0)
        self.assert_original()

    def test_partial_copy_failure_restores_previous_files(self):
        quote = lambda s: "'" + str(s).replace("'", "''") + "'"
        script = '''function Copy-Item {
param([string]$LiteralPath,[string]$Destination,[switch]$Force,[switch]$Recurse)
if($LiteralPath.EndsWith('payload/sample-texts.json')){throw 'Injected payload copy failure'}
Microsoft.PowerShell.Management\\Copy-Item @PSBoundParameters
}
& ''' + quote(self.kit / 'Manage.ps1') + ' -Action CleanInstall -GamePath ' + quote(self.game)
        r = subprocess.run([PWSH, '-NoProfile', '-Command', script], capture_output=True, text=True)
        self.assertNotEqual(r.returncode, 0)
        self.assert_original(); self.assertFalse(self.state.exists())
