import collections
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

class DynamicReviewTests(unittest.TestCase):
    def test_drafts_cover_exact_source_keys_and_preserve_parameters(self):
        sources = {r['key']: (str(p.relative_to(ROOT / 'inventory-import')), r['original'])
                   for p in (ROOT / 'inventory-import/dynamic').glob('*.json')
                   for r in json.loads(p.read_text())}
        rows = json.loads((ROOT / 'probe/dynamic-text-review.json').read_text())['entries']
        self.assertEqual(len(rows), len(sources))
        self.assertEqual(len(rows), len({r['key'] for r in rows}))
        self.assertEqual({r['key'] for r in rows}, set(sources))
        for row in rows:
            with self.subTest(key=row['key']):
                self.assertEqual((row['file'], row['original']), sources[row['key']])
                self.assertTrue(row['translationDraft'].strip())
                self.assertFalse(row['runtimeEmission'])
                self.assertEqual(collections.Counter(re.findall(r'\{\d+\}', row['original'])),
                                 collections.Counter(re.findall(r'\{\d+\}', row['translationDraft'])))
                self.assertEqual(re.findall(r'<[^<>]+>', row['original']),
                                 re.findall(r'<[^<>]+>', row['translationDraft']))
                if row['file'].endswith('/sentence-fragments.json'):
                    self.assertEqual(row['status'], 'assembly-only')
                self.assertIn(row['status'], ['assembly-only', 'parameter-semantics-review',
                                             'display-route-review', 'stage-2-fixed-text'])

if __name__ == '__main__':
    unittest.main()
