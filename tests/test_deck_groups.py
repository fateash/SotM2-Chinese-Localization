import copy,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
import catalog as c
from pull_paratranz import fetch

class DeckGroupTests(unittest.TestCase):
    def setUp(self):
        self.reg=c.load(ROOT/'baseline/registry.json')
        self.rows=c.snapshot((ROOT/'translations').glob('*.json'))
        self.config=c.load(ROOT/'paratranz-project.json')
        # Exercise exact-name discovery independently of production IDs.
        self.config.pop('remoteNames',None)
        self.config['files']={'ui.json':3595636,'Legacy.json':None,'BaronBlade.json':None}
        self.files={p.name:c.load(p) for p in (ROOT/'translations').glob('*.json')}
        self.listing=[{'id':i+1,'name':name,'modifiedAt':'v1','total':len(rows)} for i,(name,rows) in enumerate(self.files.items())]
        for f in self.listing:
            if f['name']=='ui.json':f['id']=3595636
    def fake(self, swapped=False, replaced=False):
        listing_calls=0
        def request(path):
            nonlocal listing_calls
            if path.endswith('/files'):
                listing_calls+=1;result=copy.deepcopy(self.listing)
                if replaced and listing_calls>1:
                    next(f for f in result if f['name']=='Legacy.json')['id']=9999
                return result
            fid=int(path.split('/')[-2]);name=next(f['name'] for f in self.listing if f['id']==fid)
            if swapped and name=='Legacy.json':name='BaronBlade.json'
            return copy.deepcopy(self.files[name])
        return request
    def test_split_preserves_existing_44(self):
        old=c.load(ROOT/'tests/fixtures/pilot.json')
        out,report=c.compile_catalog(self.reg,self.rows,'translated')
        for kind in ('Ui','Cards'):
            for row in old['catalog'][kind]:self.assertIn(row,out[kind])
        self.assertEqual(report['included'],89)
        current={e['key']:e for e in self.reg['entries']}
        for e in old['registry']['entries']:
            for field in ('kind','locator','original','sourceSha256'):
                self.assertEqual(current[e['key']][field],e[field])
            for field in ('translation','stage'):
                self.assertEqual(self.rows[e['key']][field],old['rows'][e['key']][field])
    def test_group_export_and_remote_fetch(self):
        exports=c.export_files(self.reg,self.rows)
        self.assertEqual({n:len(r) for n,r in exports.items()},{'ui.json':37,'Legacy.json':27,'BaronBlade.json':25})
        self.assertEqual(fetch(self.config,self.reg,self.fake()),self.files)
    def test_wrong_deck_file_rejected(self):
        with self.assertRaises(c.Invalid):fetch(self.config,self.reg,self.fake(swapped=True))
    def test_dynamic_x_variable_preserved(self):
        key=next(k for k in self.rows if k.startswith('card.BladeBattalion.'))
        self.rows[key]['translation']=self.rows[key]['translation'].replace('X','若干')
        with self.assertRaises(c.Invalid):c.compile_catalog(self.reg,self.rows,'translated')
    def test_duplicate_remote_name_rejected(self):
        f=copy.deepcopy(next(f for f in self.listing if f['name']=='Legacy.json'));f['id']=9999;self.listing.append(f)
        with self.assertRaises(c.Invalid):fetch(self.config,self.reg,self.fake())
    def test_missing_remote_deck_rejected(self):
        self.listing=[f for f in self.listing if f['name']!='Legacy.json']
        with self.assertRaises(c.Invalid):fetch(self.config,self.reg,self.fake())
    def test_remote_file_recreated_during_fetch_rejected(self):
        with self.assertRaises(c.Invalid):fetch(self.config,self.reg,self.fake(replaced=True))
    def test_unconfigured_group_rejected(self):
        self.config['files'].pop('Legacy.json')
        with self.assertRaises(c.Invalid):fetch(self.config,self.reg,self.fake())
    def test_file_path_traversal_rejected(self):
        self.reg['entries'][0]['file']='../ui.json'
        with self.assertRaises(c.Invalid):c.export_files(self.reg,self.rows)
    def test_source_index_matches_every_card_line(self):
        index=c.load(ROOT/'baseline/batch01-source-index.json')['entries']
        expected={e['key']:e for e in self.reg['entries'] if e['kind']=='card.body'}
        self.assertEqual(len(index),len(expected));self.assertEqual({e['key'] for e in index},set(expected))
        for e in index:
            r=expected[e['key']]
            self.assertEqual(e['original'],r['original']);self.assertEqual(e['card'],r['locator'])
            self.assertEqual(r['file'],e['deck']+'.json');self.assertNotIn('/variants/',e['pointer'])
    def test_legacy_archive_not_read(self):
        self.listing.append({'id':3595635,'name':'cards-body.json','total':7})
        self.assertEqual(fetch(self.config,self.reg,self.fake()),self.files)
    def test_remote_folders_map_to_local_files(self):
        names={'Legacy.json':'heroes/Legacy.json','BaronBlade.json':'villians/BaronBlade.json'}
        self.config['remoteNames']=names
        contents={f['id']:self.files[f['name']] for f in self.listing}
        for f in self.listing:
            self.config['files'][f['name']]=f['id']
            f['name']=names.get(f['name'],f['name'])
        def request(path):
            if path.endswith('/files'):return copy.deepcopy(self.listing)
            return copy.deepcopy(contents[int(path.split('/')[-2])])
        self.assertEqual(fetch(self.config,self.reg,request),self.files)
        self.config['remoteNames']['Legacy.json']='other/Legacy.json'
        with self.assertRaises(c.Invalid):fetch(self.config,self.reg,request)

if __name__=='__main__':unittest.main()
