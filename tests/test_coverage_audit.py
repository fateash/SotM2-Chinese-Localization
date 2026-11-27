import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('coverage_audit', ROOT / 'tools/coverage_audit.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

class CoverageAuditTests(unittest.TestCase):
    def setUp(self):
        self.snapshot = audit.read(ROOT / 'coverage/evidence/latest.json')
        self.contracts = audit.read(ROOT / 'coverage/contracts.json')
        self.templates = audit.read(ROOT / 'probe/dynamic-pilot.json')['Templates']
        self.drafts = audit.read(ROOT / 'probe/dynamic-text-review.json')['entries']
        self.inventory = audit.read(ROOT / 'coverage/inventory-status.json')

    def test_nonhero_matrix_covers_current_inventory(self):
        result = audit.validate_nonhero_matrix(audit.read(ROOT / 'coverage/nonhero-route-matrix.json'), self.contracts)
        self.assertEqual(result, {'owners': 12, 'contracts': 180, 'liveAccepted': False})

    def test_nonhero_owner_cannot_be_dropped_or_duplicated(self):
        matrix = audit.read(ROOT / 'coverage/nonhero-route-matrix.json')
        for owners in [matrix['owners'][1:], matrix['owners'] + [matrix['owners'][0]]]:
            with self.assertRaises(ValueError):
                audit.validate_nonhero_matrix(matrix | {'owners': owners}, self.contracts)

    def test_new_inventory_owner_requires_matrix_review(self):
        matrix = audit.read(ROOT / 'coverage/nonhero-route-matrix.json')
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            for owner in matrix['owners']:
                path = root / owner['inventoryFile']
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text((ROOT / owner['inventoryFile']).read_text())
            added = root / 'inventory-translations/environments/NewEnvironment.json'
            added.write_text('[]')
            with self.assertRaisesRegex(ValueError, 'roster'):
                audit.validate_nonhero_matrix(matrix, self.contracts, root)

    def test_nonhero_missing_role_or_contract_rejected(self):
        for mode in ['role', 'contract', 'required']:
            matrix = audit.read(ROOT / 'coverage/nonhero-route-matrix.json')
            if mode == 'role':
                del matrix['owners'][0]['contracts']['trash-discard']
            elif mode == 'contract':
                matrix['owners'][0]['contracts']['trash-discard']['id'] = 'missing'
            else:
                matrix['requiredChecks'].remove('trash-discard')
            with self.assertRaises(ValueError):
                audit.validate_nonhero_matrix(matrix, self.contracts)

    def test_nonhero_source_name_is_grounded_in_inventory(self):
        matrix = audit.read(ROOT / 'coverage/nonhero-route-matrix.json')
        matrix['owners'][0]['sourceName'] = 'Unverified Name'
        with self.assertRaisesRegex(ValueError, 'source name'):
            audit.validate_nonhero_matrix(matrix, self.contracts)

    def test_nonhero_synthetic_case_cannot_be_live_accepted(self):
        matrix = audit.read(ROOT / 'coverage/nonhero-route-matrix.json')
        link = matrix['owners'][0]['contracts']['deck-reveal']
        context = next(c for f in self.contracts['families'] if f['id'] == link['family']
                       for c in f['contexts'] if c['id'] == link['id'])
        context['status'] = 'live-accepted'
        with self.assertRaisesRegex(ValueError, 'live acceptance'):
            audit.validate_nonhero_matrix(matrix, self.contracts)

    def test_contracts_cover_all_templates_and_reviewed_runtime(self):
        audit.validate_contracts(self.contracts, self.templates)

    def test_added_template_requires_family_registration(self):
        with self.assertRaises(ValueError):
            audit.validate_contracts(self.contracts, self.templates | {'new.action': '新增'})

    def test_changed_runtime_requires_coverage_review(self):
        self.contracts['reviewedFiles']['runtime/DynamicDisplayRules.cs'] = '0' * 64
        with self.assertRaises(ValueError):
            audit.validate_contracts(self.contracts, self.templates)

    def test_review_cannot_drop_runtime_files(self):
        self.contracts['reviewedFiles'] = {}
        with self.assertRaises(ValueError):
            audit.validate_contracts(self.contracts, self.templates)

    def test_log_match_cannot_auto_promote_context_to_live(self):
        c = self.contracts['families'][0]['contexts'][0]
        c['status'] = 'live-accepted'
        with self.assertRaises(ValueError):
            audit.validate_contracts(self.contracts, self.templates)

    def test_missing_negative_context_rejected(self):
        f = self.contracts['families'][0]
        f['contexts'] = [c for c in f['contexts'] if c['status'] != 'excluded']
        with self.assertRaises(ValueError):
            audit.validate_contracts(self.contracts, self.templates)

    def test_inventory_covers_source_and_requires_real_evidence(self):
        audit.validate_inventory(self.inventory, self.drafts, self.templates)
        self.inventory['entries'][0]['live'] = 'accepted'
        with self.assertRaises(ValueError):
            audit.validate_inventory(self.inventory, self.drafts, self.templates)

    def test_changed_source_reopens_review(self):
        self.inventory['entries'][0]['sourceSha256'] = 'invalid'
        with self.assertRaises(ValueError):
            audit.validate_inventory(self.inventory, self.drafts, self.templates)

    def test_fragments_need_complete_sentence_links(self):
        row = self.inventory['entries'][0]
        row.update(display='composed', live='not-applicable')
        with self.assertRaises(ValueError):
            audit.validate_inventory(self.inventory, self.drafts, self.templates)

    def test_new_unknown_blocks_until_explicit_triage(self):
        row = self.snapshot['samples'][0]
        replay = {'rows': [{'id': row['id'], 'reason': 'unresolved-full-string'}]}
        errors = audit.validate_triage([row], replay, {'entries': []})
        self.assertEqual(errors, ['untriaged:' + row['id']])
        decision = dict(id=row['id'], reviewedReason='unresolved-full-string', disposition='fix-stage-1',
                        reason='完整提示待适配', owner='维护者', nextAction='核对参数后补规则')
        self.assertEqual(audit.validate_triage([row], replay, {'entries': [decision]}), [])
        replay['rows'][0]['reason'] = 'known-source-route-gap'
        self.assertIn('changed-classification:' + row['id'], audit.validate_triage([row], replay, {'entries': [decision]}))

    def test_deleting_old_observation_cannot_hide_pending_item(self):
        with self.assertRaises(ValueError):
            audit.validate_triage([], {'rows': []}, {'entries': [{'id': self.snapshot['samples'][0]['id']}]})

    def test_source_identity_is_bound_to_path_and_card(self):
        row = copy.deepcopy(self.snapshot['samples'][0])
        row['path'] += '/Changed'
        with self.assertRaises(ValueError):
            audit.validate_triage([row], {'rows': []}, {'entries': []})

    def test_known_match_cannot_regress_into_old_triage(self):
        baseline = {'entries': [{'id': 'sample', 'reason': 'current-rule-match', 'target': '在此打出'}]}
        good = {'rows': [{'id': 'sample', 'reason': 'current-rule-match', 'target': '在此打出'}]}
        self.assertEqual(audit.validate_regression(baseline, good), [])
        for rows in ([], [{'id': 'sample', 'reason': 'unresolved-full-string', 'target': None}],
                     [{'id': 'sample', 'reason': 'current-rule-match', 'target': '错误'}]):
            self.assertEqual(audit.validate_regression(baseline, {'rows': rows}), ['known-observation-regressed:sample'])

    def test_intake_appends_and_is_idempotent(self):
        row = self.snapshot['samples'][0]
        captured = {k: row[k] for k in ('objectName', 'path', 'cardId', 'source', 'inCardView', 'inputSource', 'truncated', 'markerCandidate')}
        log = 'PROTOTYPE_LOADED test build=26880829 mode=Chinese\nSURFACE_PROBE ' + json.dumps(captured) + '\n'
        with tempfile.TemporaryDirectory() as tmp:
            archive = Path(tmp) / 'evidence.zip'
            with zipfile.ZipFile(archive, 'w') as z:
                z.writestr('Evidence\\LogOutput.log', log)
                z.writestr('Evidence\\manifest.json', json.dumps(dict(version='test', buildId='26880829', sourceCommit='test', dllSha256='dll', probeSha256='probe')))
                z.writestr('Evidence\\installed-hashes.json', json.dumps({'SotM2ChinesePrototype.dll': 'dll', 'surface-probe.json': 'probe'}))
            first = audit.intake([archive], copy.deepcopy(self.snapshot))
            self.assertEqual(len(first['samples']), len(self.snapshot['samples']))
            self.assertEqual(len(first['sessions']), len(self.snapshot['sessions']) + 1)
            self.assertEqual(audit.intake([archive], copy.deepcopy(first)), first)
            self.assertFalse(first['sessions'][-1]['exhaustive'])

    def test_unverifiable_raw_log_is_not_clean_evidence(self):
        row = self.snapshot['samples'][0]
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'log.txt'
            p.write_text('SURFACE_PROBE ' + json.dumps(row))
            result = audit.intake([p])
            self.assertIn('missing-installed-hashes', result['sessions'][0]['issues'])

    def test_duplicates_or_malformed_records_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'log.txt'
            p.write_text('SURFACE_PROBE {broken')
            with self.assertRaises(ValueError):
                audit.intake([p])

if __name__ == '__main__':
    unittest.main()
