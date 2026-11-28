"""Exercise the delivered player installer with disposable, independently hashed fixtures."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import importlib.util
ROOT=Path(__file__).resolve().parents[1]
PWSH=os.environ.get('SOTM2_PWSH') or shutil.which('pwsh')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

@unittest.skipUnless(PWSH,'PowerShell required')
class PlayerInstallerTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        root=Path(self.tmp.name);self.game=root/'game';self.kit=root/'kit';self.game.mkdir();self.kit.mkdir()
        self.plugin='BepInEx/plugins/SotM2ChinesePrototype/'
        self.put(self.game,'Sentinels2.exe',b'game executable')
        self.put(self.game,'GameAssembly.dll',b'game assembly')
        self.put(self.game,'saves/slot.dat',b'save sentinel')
        self.put(self.game,'BepInEx/plugins/OtherMod/Other.dll',b'other mod')
        shutil.copy2(ROOT/'release/Manage.ps1',self.kit/'Manage.ps1')
        self.files={self.plugin+'SotM2ChinesePrototype.dll':b'new plugin',self.plugin+'display-rules.json':b'new rules',
            self.plugin+'sample-texts.json':b'new text','BepInEx/config/sotm2.zh.prototype.cfg':b'[Prototype]\nEnableChinese = true\n',
            'BepInEx/core/base.dll':b'loader','dotnet/coreclr.dll':b'coreclr','winhttp.dll':b'doorstop',
            'BepInEx/config/BepInEx.cfg':b'[IL2CPP]\nUpdateInteropAssemblies = false\nPreloadIL2CPPInteropAssemblies = true\n'}
        for n,b in self.files.items():self.put(self.kit/'payload',n,b)
        self.manifest=dict(product='SotM2ChinesePlayer',version='0.1.45-rc.1',flavor='player-no-diagnostics',
            dependencies=['BepInEx/core/base.dll','dotnet/coreclr.dll','winhttp.dll'],
            files={n:sha(self.kit/'payload'/n) for n in self.files},gameFiles={'GameAssembly.dll':sha(self.game/'GameAssembly.dll')})
        self.save_manifest()
    def put(self,root,n,b):
        p=root/n;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b);return p
    def save_manifest(self):(self.kit/'manifest.json').write_text(json.dumps(self.manifest))
    def run_action(self,action,ok=True):
        r=subprocess.run([PWSH,'-NoProfile','-File',str(self.kit/'Manage.ps1'),'-Action',action,'-GamePath',str(self.game)],capture_output=True,text=True)
        self.assertEqual(r.returncode==0,ok,r.stdout+r.stderr);return r
    def assert_preserved(self):
        self.assertEqual((self.game/'saves/slot.dat').read_bytes(),b'save sentinel')
        self.assertEqual((self.game/'BepInEx/plugins/OtherMod/Other.dll').read_bytes(),b'other mod')
        self.assertEqual((self.game/'GameAssembly.dll').read_bytes(),b'game assembly')
    def test_fresh_offline_install_uninstall_shared_loader_retained(self):
        self.run_action('Install')
        for n,b in self.files.items():self.assertEqual((self.game/n).read_bytes(),b)
        self.run_action('Uninstall');self.assertFalse((self.game/(self.plugin+'SotM2ChinesePrototype.dll')).exists())
        self.assertEqual((self.game/'BepInEx/core/base.dll').read_bytes(),b'loader');self.assert_preserved()
    def test_migrate_internal_duplicate_and_rollback(self):
        old={self.plugin+'SotM2ChinesePrototype.dll':b'old plugin',self.plugin+'surface-probe.json':b'old probe',
             'BepInEx/plugins/old/SotM2ChinesePrototype-copy.dll':b'duplicate',
             'BepInEx/cache/chainloader_typeloader.dat':b'old scan cache'}
        for n,b in old.items():self.put(self.game,n,b)
        self.run_action('Install')
        self.assertFalse((self.game/(self.plugin+'surface-probe.json')).exists())
        self.assertFalse((self.game/'BepInEx/plugins/old/SotM2ChinesePrototype-copy.dll').exists())
        self.run_action('Rollback')
        for n,b in old.items():self.assertEqual((self.game/n).read_bytes(),b)
        self.assertFalse((self.game/(self.plugin+'display-rules.json')).exists());self.assert_preserved()
    def test_upgrade_and_rollback_state_then_uninstall(self):
        self.run_action('Install');old=(self.game/'SotM2ChinesePlayerState.json').read_bytes()
        n=self.plugin+'sample-texts.json';self.put(self.kit/'payload',n,b'updated text');self.manifest['files'][n]=sha(self.kit/'payload'/n);self.save_manifest()
        self.run_action('Install');self.run_action('Rollback')
        self.assertEqual((self.game/n).read_bytes(),b'new text')
        self.assertEqual((self.game/'SotM2ChinesePlayerState.json').read_bytes(),old)
        self.run_action('Uninstall');self.assert_preserved()
    def test_corrupt_payload_rejected_before_change(self):
        self.put(self.kit/'payload',self.plugin+'sample-texts.json',b'corrupt');self.run_action('Install',False)
        self.assertFalse((self.game/'SotM2ChinesePlayerBackups').exists());self.assert_preserved()
    def test_wrong_build_rejected_before_change(self):
        self.manifest['gameFiles']['GameAssembly.dll']='0'*64;self.save_manifest();self.run_action('Install',False)
        self.assertFalse((self.game/'SotM2ChinesePlayerBackups').exists());self.assert_preserved()
    def test_different_shared_runtime_rejected_untouched(self):
        self.put(self.game,'BepInEx/core/base.dll',b'other loader');self.run_action('Install',False)
        self.assertEqual((self.game/'BepInEx/core/base.dll').read_bytes(),b'other loader');self.assert_preserved()
    def test_existing_loader_settings_retained(self):
        cfg=b'[IL2CPP]\nUpdateInteropAssemblies = false\nPreloadIL2CPPInteropAssemblies = true\n# custom setting retained\n'
        self.put(self.game,'BepInEx/config/BepInEx.cfg',cfg);self.run_action('Install')
        self.assertEqual((self.game/'BepInEx/config/BepInEx.cfg').read_bytes(),cfg)
    def test_modified_owned_file_blocks_uninstall_and_rollback(self):
        self.run_action('Install');p=self.put(self.game,self.plugin+'sample-texts.json',b'user edits')
        self.run_action('Uninstall',False);self.run_action('Rollback',False);self.assertEqual(p.read_bytes(),b'user edits');self.assert_preserved()
    def test_unknown_added_file_survives_uninstall(self):
        self.run_action('Install');p=self.put(self.game,self.plugin+'user.txt',b'user addition');self.run_action('Uninstall')
        self.assertEqual(p.read_bytes(),b'user addition');self.assert_preserved()
    def test_corrupt_backup_blocks_rollback(self):
        self.put(self.game,self.plugin+'sample-texts.json',b'old');self.run_action('Install')
        state=json.loads((self.game/'SotM2ChinesePlayerState.json').read_text())
        p=self.game/'SotM2ChinesePlayerBackups'/state['backup']/'original'/self.plugin/'sample-texts.json';p.write_bytes(b'damaged')
        self.run_action('Rollback',False);self.assertEqual((self.game/(self.plugin+'sample-texts.json')).read_bytes(),b'new text')
    def test_symlink_rejected(self):
        (self.game/'BepInEx/plugins/link').symlink_to(self.kit,target_is_directory=True)
        self.run_action('Install',False);self.assert_preserved()
    def test_copy_failure_rolls_back_old_files_and_new_dependencies(self):
        self.put(self.game,self.plugin+'sample-texts.json',b'old text')
        # Existing directory at a payload file target forces a copy/verification failure.
        (self.game/(self.plugin+'display-rules.json')).mkdir()
        self.run_action('Install',False)
        self.assertEqual((self.game/(self.plugin+'sample-texts.json')).read_bytes(),b'old text')
        self.assertFalse((self.game/'winhttp.dll').exists());self.assert_preserved()

    def test_loader_generated_config_comments_allow_uninstall(self):
        self.run_action('Install')
        self.put(self.game,'BepInEx/config/sotm2.zh.prototype.cfg',b'# Settings file was created by BepInEx\n[Prototype]\n\n## Enable Chinese\nEnableChinese = true\n')
        self.run_action('Uninstall');self.assert_preserved()
    def test_manifest_cannot_write_to_game_binary(self):
        n='GameAssembly.dll';self.put(self.kit/'payload',n,b'bad')
        self.manifest['files'][n]=sha(self.kit/'payload'/n);self.save_manifest()
        self.run_action('Install',False);self.assert_preserved()

    def test_rollback_after_launch_archives_regenerated_loader_cache(self):
        cache='BepInEx/cache/chainloader_typeloader.dat'
        self.put(self.game,cache,b'old loader cache')
        self.put(self.game,self.plugin+'sample-texts.json',b'old text')
        self.run_action('Install')
        state=json.loads((self.game/'SotM2ChinesePlayerState.json').read_text())
        self.put(self.game,cache,b'new loader cache generated on launch')
        self.put(self.game,'BepInEx/cache/other-plugin.dat',b'other cache')
        self.put(self.game,'BepInEx/config/sotm2.zh.prototype.cfg',b'# Generated comments\n[Prototype]\nEnableChinese = true\n')
        self.run_action('Rollback')
        self.assertEqual((self.game/cache).read_bytes(),b'old loader cache')
        self.assertEqual((self.game/(self.plugin+'sample-texts.json')).read_bytes(),b'old text')
        archived=list((self.game/'SotM2ChinesePlayerBackups'/state['backup']).glob('post-install-cache-*'))
        self.assertEqual(len(archived),1)
        self.assertEqual(archived[0].read_bytes(),b'new loader cache generated on launch')
        self.assertEqual((self.game/'BepInEx/cache/other-plugin.dat').read_bytes(),b'other cache')
        self.assert_preserved()
    def test_rollback_without_original_cache_archives_new_cache(self):
        cache='BepInEx/cache/chainloader_typeloader.dat'
        self.run_action('Install')
        self.put(self.game,cache,b'new cache')
        self.run_action('Rollback')
        self.assertFalse((self.game/cache).exists())
        archived=list((self.game/'SotM2ChinesePlayerBackups').glob('*/post-install-cache-*'))
        self.assertEqual(len(archived),1);self.assertEqual(archived[0].read_bytes(),b'new cache')
        self.assert_preserved()
    def test_cache_exception_does_not_allow_occupied_old_plugin_path(self):
        old=self.plugin+'old-user-file.txt'
        self.put(self.game,old,b'old file');self.run_action('Install')
        self.put(self.game,old,b'new user file')
        cache=self.put(self.game,'BepInEx/cache/chainloader_typeloader.dat',b'new cache')
        self.run_action('Rollback',False)
        self.assertEqual((self.game/old).read_bytes(),b'new user file')
        self.assertEqual(cache.read_bytes(),b'new cache')
        self.assertTrue((self.game/'SotM2ChinesePlayerState.json').exists());self.assert_preserved()
    def test_directory_at_cache_path_blocks_before_removing_plugin(self):
        self.run_action('Install')
        cache=self.game/'BepInEx/cache/chainloader_typeloader.dat';cache.mkdir(parents=True)
        (cache/'user.txt').write_bytes(b'user data')
        self.run_action('Rollback',False)
        self.assertEqual((cache/'user.txt').read_bytes(),b'user data')
        self.assertEqual((self.game/(self.plugin+'SotM2ChinesePrototype.dll')).read_bytes(),b'new plugin')
        self.assert_preserved()
    def test_corrupt_original_cache_still_blocks_before_changes(self):
        cache='BepInEx/cache/chainloader_typeloader.dat'
        self.put(self.game,cache,b'old cache');self.run_action('Install')
        state=json.loads((self.game/'SotM2ChinesePlayerState.json').read_text())
        (self.game/'SotM2ChinesePlayerBackups'/state['backup']/'original'/cache).write_bytes(b'corrupt')
        self.put(self.game,cache,b'new cache');self.run_action('Rollback',False)
        self.assertEqual((self.game/cache).read_bytes(),b'new cache')
        self.assertTrue((self.game/'SotM2ChinesePlayerState.json').exists());self.assert_preserved()
