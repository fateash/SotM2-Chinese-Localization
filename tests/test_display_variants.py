import copy,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
import catalog as c

class DisplayVariantTests(unittest.TestCase):
    def setUp(self):
        self.reg=c.load(ROOT/'baseline/registry.json')
        self.rows=c.snapshot((ROOT/'translations').glob('*.json'))
        self.evidence=c.load(ROOT/'tests/fixtures/legacy-incapacitated.json')
    def test_observed_backside_all_three_lines_resolve(self):
        out,report=c.compile_catalog(self.reg,self.rows,'translated')
        lookup={(r['Card'],r['Source']):r['Target'] for r in out['Cards']}
        lines=self.evidence['source'].split('\r\n')
        self.assertEqual(len(lines),3)
        for source,key in zip(lines,self.evidence['keys']):
            prefix,suffix=c.DISPLAY_VARIANTS['incapacitated-bullet-v1']
            self.assertEqual(lookup[('LegacyCharacter',source)],prefix+self.rows[key]['translation']+suffix)
        self.assertEqual(report['registered'],89);self.assertEqual(report['included'],89)
        self.assertEqual(report['displayAliases'],3);self.assertEqual(report['runtimeEntries'],92)
    def test_edited_translation_updates_alias(self):
        key=self.evidence['keys'][0];self.rows[key]['translation']='由1位英雄进行其{Power}。'
        out,_=c.compile_catalog(self.reg,self.rows,'translated')
        entry=next(r for r in out['Cards'] if r['Source']==self.evidence['source'].split('\r\n')[0])
        self.assertIn('由1位英雄',entry['Target'])
    def test_ineligible_translation_omits_alias_together(self):
        self.rows[self.evidence['keys'][0]]['stage']=2
        out,r=c.compile_catalog(self.reg,self.rows,'translated')
        self.assertEqual(r['displayAliases'],2);self.assertEqual(r['included'],88)
        self.assertFalse(any(x['Source']==self.evidence['source'].split('\r\n')[0] for x in out['Cards']))
    def test_export_has_only_authored_entries(self):
        exports=c.export_files(self.reg,self.rows)
        self.assertEqual(len(exports['Legacy.json']),27)
        self.assertFalse(any('BulletPoint' in r['original'] for r in exports['Legacy.json']))
    def test_unknown_variant_rejected(self):
        self.reg['entries'][-1]['displayVariants']=['unknown']
        with self.assertRaises(c.Invalid):c.compile_catalog(self.reg,self.rows,'translated')
    def test_ui_variant_rejected(self):
        self.reg['entries'][0]['displayVariants']=['incapacitated-bullet-v1']
        with self.assertRaises(c.Invalid):c.compile_catalog(self.reg,self.rows,'translated')
    def test_runtime_identity_collision_rejected(self):
        key=self.evidence['keys'][0];e=copy.deepcopy(next(e for e in self.reg['entries'] if e['key']==key))
        e['key']='collision';e.pop('displayVariants');e['original']=self.evidence['source'].split('\r\n')[0];e['sourceSha256']=c.sha(e['original'])
        self.reg['entries'].append(e);self.rows['collision']={'key':'collision','original':e['original'],'translation':e['original'],'stage':1}
        with self.assertRaises(c.Invalid):c.compile_catalog(self.reg,self.rows,'translated')

if __name__=='__main__':unittest.main()
