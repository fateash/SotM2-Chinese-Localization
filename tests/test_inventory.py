import copy,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
import catalog as c
import inventory as i
import sync_inventory as s

class InventoryTests(unittest.TestCase):
 def setUp(self):
  self.data=c.load(ROOT/'baseline/inventory.json');self.rows=i.snapshot(ROOT/'inventory-translations',self.data)
  self.config=c.load(ROOT/'paratranz-inventory.json')
  # Stable test fixture; production snapshot may gain translations at any time.
  legacy=c.snapshot((ROOT/'translations').glob('*.json'))
  for key,row in self.rows.items():
   row.update(translation=legacy.get(key,{}).get('translation',''),stage=legacy.get(key,{}).get('stage',0))
 def test_all_sources_and_legacy_translations_preserved(self):
  out,r=i.build(self.data,self.rows)
  old,_=c.compile_catalog(c.load(ROOT/'baseline/registry.json'),c.snapshot((ROOT/'translations').glob('*.json')),'translated')
  self.assertEqual(len(self.rows),2444);self.assertEqual(len(self.config['files']),48)
  self.assertEqual(out['Ui'],old['Ui']);self.assertEqual(set(map(str,out['Cards'])),set(map(str,old['Cards'])))
  self.assertEqual(out['Display'],[]);self.assertEqual(r['included'],89)
 def test_new_translation_routes_and_crlf(self):
  e=next(e for e in self.data['entries'] if e['route']=='card-exact' and e['objectName']=='Flavor Text Label' and '{BR}' in e['original'])
  self.rows[e['key']].update(translation='译文'+e['original'],stage=1)
  out,_=i.build(self.data,self.rows)
  self.assertTrue(any('\r\n' in x['Source'] and x['Source'].startswith('“') and x['Card']==e['card'] for x in out['Display']))
 def test_changed_original_rejected(self):
  next(iter(self.rows.values()))['original']='changed'
  with self.assertRaises(c.Invalid):i.build(self.data,self.rows)
 def test_probe_markers_rejected(self):
  row=self.rows['ui.label.0001'];row['translation']='名·取消'
  with self.assertRaises(c.Invalid):i.build(self.data,self.rows)
 def test_held_inventory_never_emitted(self):
  e=next(e for e in self.data['entries'] if e['route']=='inventory')
  self.rows[e['key']].update(translation='暂存译文',stage=5)
  out,r=i.build(self.data,self.rows);self.assertTrue(any(x['key']==e['key'] and x['reason']!='empty' for x in r['skipped']))
  self.assertNotIn('暂存译文',str(out))
 def test_malformed_source_held(self):
  rows=[e for e in self.data['entries'] if e.get('holdReason')=='source-markup-needs-review']
  self.assertEqual(len(rows),4);self.assertTrue(all(e['route']=='inventory' for e in rows))
 def test_path_escape_rejected(self):
  self.data['entries'][0]['file']='../escape.json'
  with self.assertRaises(c.Invalid):i.registry(self.data)
 def test_title_and_phase_compilation(self):
  for e in self.data['entries']:
   if (e['route']=='card-style' and e['original']=='Galvanize') or (e['route']=='phase' and e['original']=='Start'):
    self.rows[e['key']].update(translation='测试标题' if e['route']=='card-style' else '开始阶段',stage=5)
  out,_=i.build(self.data,self.rows)
  self.assertTrue(any(x['Mode']=='style' and x['Style']=='PowerTitle' and x['Card']=='LegacyCharacter' for x in out['Display']))
  self.assertTrue(any(x['Mode']=='phase-inline' and x['Source']=='START PHASE' for x in out['Display']))

class MigrationTests(unittest.TestCase):
 setUp=InventoryTests.setUp
 def fake(self,unknown=False):
  groups=s.grouped(self.data);files={};self.writes=[];next_id=4000000
  for name,fid in self.config['files'].items():
   if fid is None:continue
   rows=[copy.deepcopy(self.rows[e['key']]) for e in groups[name] if self.rows[e['key']]['translation']]
   if unknown and name=='heroes/Legacy.json':rows.append(dict(key='user.extra',original='Unknown',translation='保留',stage=5))
   files[name]=dict(meta=dict(name=name,id=fid,modifiedAt='v1',total=len(rows)),rows=rows)
  def request(path,method='GET',payload=None):
   nonlocal next_id
   if method=='POST':
    self.writes.append(copy.deepcopy(payload));name=payload['filename']
    self.assertTrue(all('translation' not in r and 'stage' not in r for r in payload['items']))
    if name not in files:
     files[name]=dict(meta=dict(name=name,id=next_id,modifiedAt='v1',total=0),rows=[]);next_id+=1
    else:self.assertTrue(payload['incremental'])
    prior={r['key']:r for r in files[name]['rows']}
    files[name]['rows']=[dict(r,translation=prior.get(r['key'],{}).get('translation',''),stage=prior.get(r['key'],{}).get('stage',0)) for r in payload['items']]
    files[name]['meta'].update(modifiedAt='v2',total=len(files[name]['rows']));return files[name]['meta']
   if path.endswith('/files'):return [copy.deepcopy(f['meta']) for f in files.values()]
   fid=int(path.split('/')[-2]);return copy.deepcopy(next(f['rows'] for f in files.values() if f['meta']['id']==fid))
  return request
 def test_full_additive_migration_preserves_89(self):
  result,resolved=s.migrate(self.config,self.data,self.fake(),True)
  self.assertEqual(len(result),48);self.assertEqual(sum(map(len,result.values())),2444)
  self.assertEqual(resolved['files']['heroes/Legacy.json'],3595650)
  self.assertEqual(len(self.writes),48)
  new={r['key']:r for rows in result.values() for r in rows}
  for k,r in self.rows.items():self.assertEqual((new[k]['translation'],new[k]['stage']),(r['translation'],r['stage']))
 def test_preflight_failure_performs_no_writes(self):
  with self.assertRaises(c.Invalid):s.migrate(self.config,self.data,self.fake(True),True)
  self.assertEqual(self.writes,[])
 def test_plan_is_read_only(self):
  plan=s.migrate(self.config,self.data,self.fake(),False)
  self.assertEqual(len(plan),48);self.assertEqual(self.writes,[])
 def test_incomplete_remote_pull_rejected(self):
  with self.assertRaises(c.Invalid):s.pull(self.config,self.data,self.fake())

class UploadEncodingTests(unittest.TestCase):
 def test_nested_new_file_uses_separate_folder_and_basename(self):
  body,ct=s.encode_payload('/projects/21585/files','POST',{'filename':'shared/phases.json','items':[{'key':'a','original':'Start','context':'阶段'}]})
  text=body.decode('utf-8');self.assertTrue(ct.startswith('multipart/form-data; boundary='))
  self.assertIn('name="path"\r\n\r\nshared/\r\n',text)
  self.assertIn('filename="phases.json"',text);self.assertNotIn('filename="shared/',text)
  self.assertNotIn('"translation"',text);self.assertNotIn('"stage"',text)
 def test_existing_file_update_keeps_incremental_json(self):
  body,ct=s.encode_payload('/projects/21585/files/3595650','POST',{'filename':'heroes/Legacy.json','items':[],'incremental':True})
  self.assertEqual(ct,'application/json');self.assertTrue(__import__('json').loads(body)['incremental'])
