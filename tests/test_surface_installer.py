"""Portable installer simulation; requires pwsh on PATH or SOTM2_PWSH."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
PWSH = os.environ.get('SOTM2_PWSH') or shutil.which('pwsh')
def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

@unittest.skipUnless(PWSH, 'PowerShell is required for installer simulation')
class SurfaceInstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.game, self.kit = root / 'game', root / 'kit'
        self.plugin = self.game / 'BepInEx/plugins/SotM2ChinesePrototype'
        self.plugin.mkdir(parents=True)
        (self.kit / 'payload').mkdir(parents=True)
        self.backup = self.game / 'SotM2ChineseBackups/surface-probe-0.1.7'
        shutil.copy(ROOT / 'probe/Manage.ps1', self.kit / 'Manage.ps1')
        self.dll = self.plugin / 'SotM2ChinesePrototype.dll'
        self.catalog = self.plugin / 'sample-texts.json'
        self.probe = self.plugin / 'surface-probe.json'
        self.dll.write_bytes(b'accepted dll'); self.catalog.write_bytes(b'accepted catalog')
        (self.plugin / 'font.otf').write_bytes(b'font sentinel')
        (self.game / 'config.cfg').write_bytes(b'config sentinel')
        (self.game / 'GameAssembly.dll').write_bytes(b'game build')
        (self.kit / 'payload/SotM2ChinesePrototype.dll').write_bytes(b'new dll')
        (self.kit / 'payload/surface-probe.json').write_bytes(b'new probe')
        manifest = dict(dllSha256=sha(self.kit / 'payload/SotM2ChinesePrototype.dll'), probeSha256=sha(self.kit / 'payload/surface-probe.json'), acceptedDllSha256=sha(self.dll), acceptedCatalogs=[sha(self.catalog)], gameFiles={'GameAssembly.dll': sha(self.game / 'GameAssembly.dll')})
        (self.kit / 'manifest.json').write_text(json.dumps(manifest))

    def run_script(self, action, ok=True):
        result = subprocess.run([PWSH, '-NoLogo', '-NoProfile', '-File', str(self.kit / 'Manage.ps1'), '-Action', action, '-GamePath', str(self.game)], capture_output=True, text=True)
        self.assertEqual(result.returncode == 0, ok, result.stdout + result.stderr)
        return result

    def assert_untouched(self):
        self.assertEqual(self.catalog.read_bytes(), b'accepted catalog')
        self.assertEqual((self.plugin / 'font.otf').read_bytes(), b'font sentinel')
        self.assertEqual((self.game / 'config.cfg').read_bytes(), b'config sentinel')

    def test_install_preserves_catalog_font_config_and_backup_not_scanned(self):
        self.run_script('Install'); self.assert_untouched()
        self.assertEqual(self.dll.read_bytes(), b'new dll')
        self.assertFalse(list(self.backup.rglob('*.dll')))
        self.assertTrue((self.backup / 'SotM2ChinesePrototype.dll.backup').exists())
        self.assertFalse((self.plugin / 'surface-probe-0.1.7').exists())

    def test_repeat_install_then_exact_restore(self):
        self.run_script('Install'); self.run_script('Install'); self.run_script('Restore')
        self.assertEqual(self.dll.read_bytes(), b'accepted dll')
        self.assertFalse(self.probe.exists()); self.assertFalse(self.backup.exists()); self.assert_untouched()

    def test_modified_dll_refuses_restore(self):
        self.run_script('Install'); self.dll.write_bytes(b'user changed')
        self.run_script('Restore', False)
        self.assertEqual(self.dll.read_bytes(), b'user changed'); self.assertTrue(self.backup.exists())

    def test_bad_build_refused_before_mutation(self):
        (self.game / 'GameAssembly.dll').write_bytes(b'new game build')
        self.run_script('Install', False)
        self.assertFalse(self.backup.exists()); self.assertEqual(self.dll.read_bytes(), b'accepted dll')

    def test_unknown_catalog_refused(self):
        self.catalog.write_bytes(b'changed translations'); self.run_script('Install', False)
        self.assertEqual(self.catalog.read_bytes(), b'changed translations'); self.assertFalse(self.backup.exists())

    def test_tampered_payload_refused_before_backup(self):
        (self.kit / 'payload/surface-probe.json').write_bytes(b'tampered'); self.run_script('Install', False)
        self.assertFalse(self.backup.exists()); self.assertFalse(self.probe.exists())

    def test_tampered_backup_refuses_restore(self):
        self.run_script('Install'); (self.backup / 'SotM2ChinesePrototype.dll.backup').write_bytes(b'tampered')
        self.run_script('Restore', False); self.assertEqual(self.dll.read_bytes(), b'new dll')

    def test_foreign_probe_refused(self):
        self.probe.write_bytes(b'foreign probe'); self.run_script('Install', False)
        self.assertEqual(self.probe.read_bytes(), b'foreign probe'); self.assertFalse(self.backup.exists())

    def test_copy_failure_rolls_back_and_remains_restorable(self):
        # Fail the second payload write after the DLL has already been replaced.
        quote = lambda s: "'"+str(s).replace("'", "''")+"'"
        script = '''function Copy-Item {
param([string]$LiteralPath,[string]$Destination,[switch]$Force)
if($LiteralPath.EndsWith('payload/surface-probe.json')){throw 'Injected copy failure'}
Microsoft.PowerShell.Management\\Copy-Item @PSBoundParameters
}
& '''+quote(self.kit / 'Manage.ps1')+' -Action Install -GamePath '+quote(self.game)
        r = subprocess.run([PWSH, '-NoProfile', '-Command', script], capture_output=True, text=True)
        self.assertNotEqual(r.returncode, 0)
        self.assertEqual(self.dll.read_bytes(), b'accepted dll'); self.assertFalse(self.probe.exists()); self.assert_untouched()
        self.assertTrue(self.backup.exists()); self.run_script('Restore')

class DynamicInstallerTests(SurfaceInstallerTests):
    def setUp(self):
        super().setUp()
        shutil.copy(ROOT / 'probe/Manage-Dynamic.ps1', self.kit / 'Manage.ps1')
        self.backup = self.game / 'SotM2ChineseBackups/dynamic-pilot-0.1.19'
        p = self.kit / 'manifest.json'
        m = json.loads(p.read_text())
        m['acceptedInstallations'] = [{'dll': m.pop('acceptedDllSha256'), 'probe': 'ABSENT'}]
        p.write_text(json.dumps(m))

    def test_upgrade_existing_probe_and_restore_exact_pair(self):
        self.dll.write_bytes(b'accepted previous probe dll')
        self.probe.write_bytes(b'accepted previous probe config')
        p = self.kit / 'manifest.json'; m = json.loads(p.read_text())
        m['acceptedInstallations'].append({'dll': sha(self.dll), 'probe': sha(self.probe)})
        p.write_text(json.dumps(m))
        self.run_script('Install'); self.run_script('Restore')
        self.assertEqual(self.dll.read_bytes(), b'accepted previous probe dll')
        self.assertEqual(self.probe.read_bytes(), b'accepted previous probe config')
        self.assert_untouched()

    def test_mismatched_pair_refused(self):
        self.probe.write_bytes(b'accepted previous probe config')
        p = self.kit / 'manifest.json'; m = json.loads(p.read_text())
        m['acceptedInstallations'].append({'dll': 'other-dll-hash', 'probe': sha(self.probe)})
        p.write_text(json.dumps(m)); self.run_script('Install', False)
        self.assertEqual(self.dll.read_bytes(), b'accepted dll'); self.assertFalse(self.backup.exists())

if __name__ == '__main__':
    unittest.main()
