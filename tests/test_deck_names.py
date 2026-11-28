import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]

class DeckNameTests(unittest.TestCase):
    def test_runtime_names_exactly_match_all_reviewed_decks(self):
        expected = {}
        for group in ('heroes', 'villians', 'environments'):
            for path in (ROOT / 'inventory-translations' / group).glob('*.json'):
                rows = json.loads(path.read_text())
                row = next(r for r in rows if r['key'].endswith('/decks/@' + path.stem + '/title'))
                self.assertTrue(row['translation'])
                expected[row['original']] = row['translation']
        actual = json.loads((ROOT / 'probe/dynamic-pilot.json').read_text())['DeckNames']
        self.assertEqual(actual, expected)

    def test_each_name_has_positive_and_negative_runtime_contracts(self):
        contracts = json.loads((ROOT / 'coverage/contracts.json').read_text())
        family = next(f for f in contracts['families'] if f['id'] == 'deck-name')
        names = json.loads((ROOT / 'probe/dynamic-pilot.json').read_text())['DeckNames']
        for source, target in names.items():
            cases = [c['case'] for c in family['contexts'] if c.get('case', {}).get('source') == source]
            self.assertTrue(any(c['expected'] == target and not c['inCardView'] for c in cases))
            self.assertTrue(any(c['expected'] == target and c.get('deckNameTitle') for c in cases))
            self.assertTrue(any(c['expected'] is None and c['inCardView'] for c in cases))
            self.assertTrue(any(c['expected'] is None and not c['inCardView'] for c in cases))
        self.assertFalse(any(c['status'] == 'live-accepted' for c in family['contexts']))
