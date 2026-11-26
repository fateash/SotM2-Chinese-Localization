import copy,sys,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
import apply_baron_fix as f
from catalog import Invalid

class ApprovedBaronTests(unittest.TestCase):
 def rows(self):return {k:dict(translation=v,stage=5) for k,v in f.BEFORE.items()}
 def test_only_two_keys_and_preserved_stage(self):
  rows=self.rows();rows['unrelated']=dict(translation='不动',stage=1)
  items=f.corrections(rows)
  self.assertEqual({r['key'] for r in items},set(f.AFTER));self.assertTrue(all(r['stage']==5 for r in items))
 def test_unexpected_edit_is_not_overwritten(self):
  rows=self.rows();rows[next(iter(f.BEFORE))]['translation']='用户新修订'
  with self.assertRaises(Invalid):f.corrections(rows)
 def test_repeat_has_no_writes(self):
  rows={k:dict(translation=v,stage=1) for k,v in f.AFTER.items()}
  self.assertEqual(f.corrections(rows),[])
