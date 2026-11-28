import collections
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

class DynamicReviewTests(unittest.TestCase):
    def test_drafts_cover_exact_source_keys_and_preserve_parameters(self):
        registry = json.loads((ROOT / 'baseline/inventory.json').read_text())['entries']
        candidate_keys = {e['key'] for e in registry if e['kind'] == 'dynamic.candidate'}
        all_sources = {r['key']: (str(p.relative_to(ROOT / 'inventory-import')), r['original'])
                   for p in (ROOT / 'inventory-import/dynamic').glob('*.json')
                   for r in json.loads(p.read_text())}
        self.assertEqual(set(all_sources), {e['key'] for e in registry if e['file'].startswith('dynamic/')})
        sources = {k: v for k, v in all_sources.items() if k in candidate_keys}
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

    def test_editorial_additions_remain_distinct_from_literal_review(self):
        registry = json.loads((ROOT / 'baseline/inventory.json').read_text())['entries']
        additions = [e for e in registry if e['kind'] in {'dynamic.editorial-template', 'ui.editorial-candidate'}]
        self.assertEqual(len(additions), 119)
        self.assertTrue(all(e['route'] == 'inventory' and e['holdReason'] == ('runtime-template-bound-not-global-dictionary' if e['key'] in {'editorial.ui.button.put.here', 'editorial.ui.hint.reduce.single', 'editorial.ui.hint.choose.number', 'editorial.ui.hint.dealer', 'editorial.ui.hint.hero.draw.count', 'editorial.ui.hint.put.hand', 'editorial.ui.hint.attack.order.dual', 'editorial.ui.hint.attack.unspecified.nobr', 'editorial.ui.hint.attack.order.triple', 'editorial.ui.button.skip.draw.phase', 'editorial.ui.result.voss.conquered', 'editorial.ui.result.baron.moonfall', 'editorial.ui.hint.hero.take.damage', 'editorial.ui.hint.trash.bury.upto', 'editorial.ui.finish.bury'} else 'editorial-template-runtime-binding-pending') for e in additions))
        drafts = json.loads((ROOT / 'probe/dynamic-text-review.json').read_text())['entries']
        self.assertFalse({e['key'] for e in additions} & {e['key'] for e in drafts})

if __name__ == '__main__':
    unittest.main()
