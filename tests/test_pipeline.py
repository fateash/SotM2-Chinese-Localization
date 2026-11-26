import copy,json,sys,unittest,tempfile,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
import catalog as c
from pull_paratranz import fetch
FIXTURE=c.load(ROOT/'tests/fixtures/pilot.json')

class PipelineTests(unittest.TestCase):
    def setUp(self):
        self.reg=copy.deepcopy(FIXTURE['registry']);self.rows=copy.deepcopy(FIXTURE['rows'])
        self.key=next(k for k in self.rows if k.startswith('card.LegacyCharacter'))
    def reject(self):
        with self.assertRaises(c.Invalid):c.compile_catalog(self.reg,self.rows,'translated')
    def test_exact_round_trip(self):
        files=c.export_files(self.reg,self.rows)
        rows={r['key']:r for f in files.values() for r in f}
        out,report=c.compile_catalog(self.reg,rows,'pilot')
        self.assertEqual(out,FIXTURE['catalog']);self.assertEqual(report['included'],44)
    def test_edit_returns_only_to_its_context(self):
        key=next(k for k,v in self.rows.items() if v['original']=='Options')
        self.rows[key]['translation']='设置'
        out,_=c.compile_catalog(self.reg,self.rows,'translated')
        changes=[(a,b) for a,b in zip(FIXTURE['catalog']['Ui'],out['Ui']) if a!=b]
        self.assertEqual(len(changes),1);self.assertEqual(changes[0][1]['Target'],'设置')
    def test_missing_key(self):del self.rows[self.key];self.reject()
    def test_unknown_key(self):self.rows['foreign']=copy.deepcopy(self.rows[self.key]);self.reject()
    def test_stale_original(self):self.rows[self.key]['original']+=' ';self.reject()
    def test_placeholder_loss(self):self.rows[self.key]['translation']=self.rows[self.key]['translation'].replace('{Start}','开始阶段');self.reject()
    def test_number_change(self):self.rows[self.key]['translation']=self.rows[self.key]['translation'].replace('+1','+2');self.reject()
    def test_sign_change(self):self.rows[self.key]['translation']=self.rows[self.key]['translation'].replace('+1','-1');self.reject()
    def test_line_break_change(self):self.rows[self.key]['translation']+='\n';self.reject()
    def test_rich_text_nesting(self):
        with self.assertRaises(c.Invalid):c.validate_text('<b><i>3</i></b>','<b><i>3</b></i>','card.body')
    def test_sprite_and_paired_tags(self):c.validate_text('<b>1</b><sprite name=HP>','<b>1</b><sprite name=HP>','card.body')
    def test_empty_translation_omits_for_english_fallback(self):
        self.rows[self.key]['translation']='';out,r=c.compile_catalog(self.reg,self.rows,'translated')
        self.assertEqual(r['included'],43);self.assertEqual(len(out['Cards']),6)
    def test_review_stage_is_allowlist(self):
        for stage in [-1,0,1,2,3,5,9]:
            with self.subTest(stage=stage):
                for row in self.rows.values():row['stage']=stage
                if stage in (5,9):self.assertEqual(c.compile_catalog(self.reg,self.rows,'reviewed')[1]['included'],44)
                else:
                    with self.assertRaises(c.Invalid):c.compile_catalog(self.reg,self.rows,'reviewed')
    def test_disputed_not_allowed_in_translated(self):
        self.rows[self.key]['stage']=2
        self.assertEqual(c.compile_catalog(self.reg,self.rows,'translated')[1]['included'],43)
    def test_missing_stage_only_pilot(self):
        for row in self.rows.values():row.pop('stage')
        self.assertEqual(c.compile_catalog(self.reg,self.rows,'pilot')[1]['included'],44)
        self.reject()
    def test_invalid_stage_not_silently_exported(self):self.rows[self.key]['stage']=True;self.reject()
    def test_source_hash_drift(self):self.reg['entries'][0]['original']+='X';self.reject()
    def test_duplicate_registry_key(self):self.reg['entries'].append(self.reg['entries'][0]);self.reject()
    def test_duplicate_runtime_locator(self):
        e=copy.deepcopy(self.reg['entries'][0]);e['key']='new.key';self.reg['entries'].append(e);self.reject()
    def test_future_field_requires_runtime_support(self):self.reg['entries'][0]['kind']='card.title';self.reject()
    def test_order_independent_snapshot(self):
        a=c.compile_catalog(self.reg,self.rows,'translated')[0]
        b=c.compile_catalog(self.reg,dict(reversed(list(self.rows.items()))),'translated')[0];self.assertEqual(a,b)
    def test_diff_preserves_id_across_source_changes(self):
        old=copy.deepcopy(self.reg);e=self.reg['entries'][0];e['original']='Changed wording';e['sourceSha256']=c.sha(e['original'])
        d=c.diff_registries(old,self.reg);self.assertEqual(d['added'],[]);self.assertEqual(d['sourceChanged'],[e['key']])
    def test_duplicate_json_property(self):
        with tempfile.TemporaryDirectory() as t:
            p=Path(t)/'bad.json';p.write_text('{"key":"a","key":"b"}')
            with self.assertRaises(c.Invalid):c.load(p)
    def test_duplicate_snapshot_key_across_files(self):
        with tempfile.TemporaryDirectory() as t:
            a=Path(t)/'a.json';b=Path(t)/'b.json';row=self.rows[self.key];c.write(a,[row]);c.write(b,[row])
            with self.assertRaises(c.Invalid):c.snapshot([a,b])
    def test_failed_build_keeps_previous_output(self):
        with tempfile.TemporaryDirectory() as t:
            p=Path(t);c.write(p/'registry.json',self.reg);c.write(p/'rows.json',list(self.rows.values())[:-1]);out=p/'output';out.mkdir();(out/'sample-texts.json').write_text('previous')
            r=subprocess.run([sys.executable,str(ROOT/'tools/catalog.py'),'build','--registry',str(p/'registry.json'),'--input',str(p/'rows.json'),'--out',str(out),'--mode','translated'],capture_output=True)
            self.assertNotEqual(r.returncode,0);self.assertEqual((out/'sample-texts.json').read_text(),'previous')
    def fake_api(self,change=False,missing=False):
        calls=[]
        listing=[{'id':1,'name':'ui.json','modifiedAt':'v1','updatedAt':'v1','total':37},{'id':2,'name':'cards-body.json','modifiedAt':'v1','updatedAt':'v1','total':7}]
        def api(path):
            calls.append(path)
            if path.endswith('/files'):
                r=copy.deepcopy(listing)
                if change and len(calls)>1:r[0]['modifiedAt']='v2'
                return r
            prefix='ui.' if path.endswith('/files/1/translation') else 'card.'
            rows=[v for k,v in self.rows.items() if k.startswith(prefix)]
            return rows[:-1] if missing else rows
        return api,calls
    def test_pull_complete(self):
        api,calls=self.fake_api();out=fetch({'projectId':1,'files':{'ui.json':1,'cards-body.json':2}},self.reg,api)
        self.assertEqual(sum(map(len,out.values())),44);self.assertEqual(len(calls),4)
    def test_pull_changes_during_fetch_rejected(self):
        api,_=self.fake_api(change=True)
        with self.assertRaises(c.Invalid):fetch({'projectId':1,'files':{'ui.json':1,'cards-body.json':2}},self.reg,api)
    def test_pull_incomplete_rejected(self):
        api,_=self.fake_api(missing=True)
        with self.assertRaises(c.Invalid):fetch({'projectId':1,'files':{'ui.json':1,'cards-body.json':2}},self.reg,api)
    def test_blank_project_rejected(self):
        with self.assertRaises(c.Invalid):fetch({'projectId':None},self.reg,lambda x:[])

if __name__=='__main__':unittest.main()
