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

    def test_dynamic_negative_also_requires_integrated_assertion(self):
        case = next(c['case'] for f in self.contracts['families'] for c in f['contexts']
                    if c.get('case', {}).get('matchLayer') == 'dynamic')
        del case['integratedExpected']
        with self.assertRaisesRegex(ValueError, 'integrated output assertion'):
            audit.validate_contracts(self.contracts, self.templates)

    def test_unknown_contract_layer_rejected(self):
        self.contracts['families'][0]['contexts'][0]['case']['matchLayer'] = 'skip'
        with self.assertRaisesRegex(ValueError, 'Unknown contract match layer'):
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
        audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)
        self.inventory['entries'][0]['live'] = 'accepted'
        with self.assertRaises(ValueError):
            audit.validate_inventory(self.inventory, self.drafts, self.templates)

    def test_inventory_broken_or_negative_case_reference_rejected(self):
        row = self.inventory['entries'][0]
        original = copy.deepcopy(row['caseRefs'])
        for family, case_id in [('hint', 'missing'), ('hint', 'reaction-first-wrong-role')]:
            row['caseRefs'] = [{'family': family, 'id': case_id, 'relation': 'fragment'}]
            with self.assertRaisesRegex(ValueError, 'positive executable'):
                audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)
        row['caseRefs'] = original

    def test_inventory_cannot_close_all_fragment_combinations(self):
        self.inventory['entries'][0].update(display='covered')
        self.inventory['entries'][0]['review']['conclusion'] = 'mapped'
        with self.assertRaisesRegex(ValueError, 'fragment relationship'):
            audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)

    def test_inventory_relationship_must_match_complete_case(self):
        row = self.inventory['entries'][6]
        row['caseRefs'][0]['id'] = 'inventory-10'
        with self.assertRaisesRegex(ValueError, 'relationship'):
            audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)

    def test_conditional_catalog_cannot_masquerade_as_default_coverage(self):
        row = self.inventory['entries'][99]
        row['catalogIds'] = []
        with self.assertRaisesRegex(ValueError, 'needs rule references'):
            audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)

    def test_conditional_catalog_needs_scope_and_real_reference(self):
        row = self.inventory['entries'][99]
        del row['review']['scopeCondition']
        with self.assertRaisesRegex(ValueError, 'explicit condition'):
            audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)
        row['review']['scopeCondition'] = 'test'
        row['catalogIds'] = ['unknown']
        with self.assertRaisesRegex(ValueError, 'Unknown conditional'):
            audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)

    def test_inventory_review_cannot_disappear(self):
        del self.inventory['entries'][-1]['review']
        with self.assertRaisesRegex(ValueError, 'explicit review'):
            audit.validate_inventory(self.inventory, self.drafts, self.templates, self.contracts)

    def test_inventory_report_preserves_every_key_and_literal_source(self):
        report = audit.inventory_markdown(self.inventory, self.drafts, len(self.templates))
        for d in self.drafts:
            self.assertIn(d['key'], report)
        self.assertEqual(report, (ROOT / 'docs/DYNAMIC-INVENTORY-REVIEW.md').read_text())
        self.assertIn('仅部分组合接入', report)
        self.assertIn('will play or draw', report)
        self.assertIn('&lt;style=PowerHeading&gt;', report)

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

    def test_bound_title_evidence_retains_semantic_identity(self):
        row = copy.deepcopy(self.snapshot['samples'][0])
        row.update(objectName='Title Label', source='Legacy', inCardView=True, deckNameTitle=True)
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'log.txt'
            p.write_text('SURFACE_PROBE ' + json.dumps(row))
            captured = audit.intake([p])['samples'][0]
            self.assertTrue(captured['deckNameTitle'])
            self.assertNotEqual(audit.identity(captured), audit.identity(captured | {'deckNameTitle': False}))
            for invalid in ({'deckNameTitle': 'true'}, {'inCardView': False}, {'objectName': 'HP Label'}):
                p.write_text('SURFACE_PROBE ' + json.dumps(row | invalid))
                with self.assertRaises(ValueError):
                    audit.intake([p])

    def test_duplicates_or_malformed_records_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'log.txt'
            p.write_text('SURFACE_PROBE {broken')
            with self.assertRaises(ValueError):
                audit.intake([p])

class CaptureWaiverTests(unittest.TestCase):
    def setUp(self):
        self.waivers = audit.read(ROOT / 'coverage/gate-waivers.json')
        snapshot = audit.read(ROOT / 'coverage/evidence/latest.json')
        hashes = {r['sha256'] for r in self.waivers['entries']}
        self.sessions = [s for s in snapshot['sessions'] if s['sha256'] in hashes]
        self.sample = snapshot['samples'][0]

    def test_explicit_batch_waiver_preserves_original_evidence(self):
        before = copy.deepcopy(self.sessions)
        errors, accepted = audit.session_gate_issues(self.sessions, self.waivers)
        self.assertEqual(errors, [])
        self.assertEqual(len(accepted), 4)
        self.assertEqual(self.sessions, before)
        self.assertTrue(all(s['captureLimitReached'] and not s['exhaustive'] for s in self.sessions))
        self.assertEqual(len(audit.capture_closure_errors(accepted)), 4)

    def test_no_waiver_still_blocks_capture_limit(self):
        errors, accepted = audit.session_gate_issues(self.sessions)
        self.assertEqual(len(errors), 4)
        self.assertEqual(accepted, [])

    def test_new_renamed_or_changed_session_does_not_inherit_waiver(self):
        for delta in [{'sha256': 'f' * 64}, {'file': 'renamed.zip'}, {'version': 'future'}]:
            with self.subTest(delta=delta):
                added = self.sessions[0] | delta
                errors, accepted = audit.session_gate_issues(self.sessions + [added], self.waivers)
                self.assertEqual(errors, [added['file'] + ':bounded-capture-limit-reached'])
                self.assertEqual(len(accepted), 4)

    def test_other_issue_in_waived_session_still_blocks(self):
        self.sessions[0]['issues'].append('missing-installed-hashes')
        errors, _ = audit.session_gate_issues(self.sessions, self.waivers)
        self.assertEqual(errors, [self.sessions[0]['file'] + ':missing-installed-hashes'])

    def test_mismatched_identity_or_missing_authorization_is_rejected(self):
        for key, value in [('sha256', 'f' * 64), ('file', 'missing.zip'), ('version', 'future'),
                           ('buildId', 'future'), ('authorization', ''), ('approvedBy', ''),
                           ('reason', ''), ('scope', 'all-future-logs')]:
            with self.subTest(key=key):
                changed = copy.deepcopy(self.waivers)
                changed['entries'][0][key] = value
                with self.assertRaises(ValueError):
                    audit.session_gate_issues(self.sessions, changed)

    def test_duplicate_or_non_capture_waiver_is_rejected(self):
        changed = copy.deepcopy(self.waivers)
        changed['entries'].append(changed['entries'][0])
        with self.assertRaises(ValueError):
            audit.session_gate_issues(self.sessions, changed)
        changed = copy.deepcopy(self.waivers)
        changed['entries'][0]['issue'] = 'missing-installed-hashes'
        with self.assertRaises(ValueError):
            audit.session_gate_issues(self.sessions, changed)
        with self.assertRaises(ValueError):
            audit.session_gate_issues(self.sessions, {'schemaVersion': 2, 'entries': []})

    def test_report_retains_untriaged_error_and_no_live_acceptance(self):
        snapshot = {'sessions': self.sessions, 'samples': [self.sample]}
        replay = {'rows': [{'id': self.sample['id'], 'reason': 'unresolved-full-string', 'target': None}]}
        report = audit.make_report(snapshot, replay, {'entries': []}, 'test-hash', [], self.waivers)
        self.assertEqual(report['gateErrors'], ['untriaged:' + self.sample['id']])
        self.assertEqual(len(report['acceptedGateWaivers']), 4)
        self.assertFalse(report['entries'][0]['stages']['liveAccepted'])
        self.assertIn('本批合并放行', audit.markdown(report))
        self.assertEqual(report['sessions'], self.sessions)

    def test_missing_cap_or_missing_issue_cannot_use_waiver(self):
        for delta in [{'captureLimitReached': False}, {'issues': []}]:
            sessions = copy.deepcopy(self.sessions)
            sessions[0].update(delta)
            with self.assertRaises(ValueError):
                audit.session_gate_issues(sessions, self.waivers)

if __name__ == '__main__':
    unittest.main()


class SemanticAcceptanceTests(unittest.TestCase):
    def setUp(self):
        self.plan = audit.read(ROOT / 'coverage/acceptance-plan.json')
        self.contracts = audit.read(ROOT / 'coverage/contracts.json')
        self.inventory = audit.read(ROOT / 'coverage/inventory-status.json')

    def test_all_families_and_inventory_have_functional_owners(self):
        pending = audit.validate_acceptance(self.plan, self.contracts, self.inventory)
        self.assertEqual(len(pending), sum(len(g['scenarios']) for g in self.plan['groups']))
        self.assertLess(len(pending), 50)

    def test_missing_family_or_inventory_cannot_make_acceptance_green(self):
        for field in ('families', 'inventoryKeys'):
            plan = copy.deepcopy(self.plan)
            group = next(g for g in plan['groups'] if g[field])
            group[field].pop()
            with self.assertRaises(ValueError):
                audit.validate_acceptance(plan, self.contracts, self.inventory)

    def test_duplicate_and_nonexistent_representative_rejected(self):
        for mode in ('duplicate', 'bad-case'):
            plan = copy.deepcopy(self.plan)
            if mode == 'duplicate': plan['groups'].append(plan['groups'][0])
            else: plan['groups'][0]['representativeCases'][0]['id'] = 'absent'
            with self.assertRaises(ValueError):
                audit.validate_acceptance(plan, self.contracts, self.inventory)

    def test_offline_pass_cannot_become_live_acceptance(self):
        scene = self.plan['groups'][0]['scenarios'][0]
        scene['status'] = 'accepted'
        scene['evidence'] = {'source': 'CI passed', 'version': '0.1.36'}
        with self.assertRaisesRegex(ValueError, 'human visual'):
            audit.validate_acceptance(self.plan, self.contracts, self.inventory)

    def completeness(self, text, reason='current-rule-match', **row):
        return audit.translation_completeness({'source': text, **row}, {'reason': reason, 'target': text})

    def test_partial_transformation_stays_incomplete(self):
        for text, token in [('准备: H minus 1张牌', 'minus'), ('开始阶段：Omnitron发动FABRICATE', 'FABRICATE')]:
            value = self.completeness(text)
            self.assertEqual(value['status'], 'partial-residual')
            self.assertIn(token, value['residualTokens'])
        self.assertEqual(self.completeness('准备: minus 1张牌', 'translated-output-observed')['status'], 'partial-residual')

    def test_whole_reviewed_names_and_defined_variable_only(self):
        value = self.completeness('S-83突击机兵对TCF“坚毅”号造成X点伤害，X为英雄数。', objectName='Body Label', cardId='Example')
        self.assertEqual(value['status'], 'no-unapproved-latin')
        self.assertEqual(value['residualTokens'], [])
        for text in ('S未知', 'TCF未知', 'X未知', 'EXTERMINATE'):
            self.assertTrue(self.completeness(text)['residualTokens'])
        self.assertIn('X', self.completeness('X为英雄数。', objectName='Title Label', cardId='Example')['residualTokens'])

    def test_markup_and_escaped_newlines_are_not_visible_english(self):
        self.assertEqual(self.completeness('<sprite name="H" color=#FFFFFF>减1张牌。\\r\\n')['status'], 'no-unapproved-latin')
        self.assertEqual(self.completeness('3', 'protected-number')['status'], 'not-assessed')

    def test_button_proof_changes_identity_without_rewriting_old_evidence(self):
        row = audit.read(ROOT / 'coverage/evidence/latest.json')['samples'][0]
        old = audit.identity(row)
        self.assertEqual(audit.identity({**row, 'buttonLabel': False}), old)
        self.assertNotEqual(audit.identity({**row, 'buttonLabel': True}), old)

    def test_inventory_reconciliation_does_not_approve_fragment_or_live_state(self):
        result = audit.reconcile_inventory([{'key':'a','original':'Play Here'},{'key':'b','original':'Play'}], self.contracts)
        self.assertTrue(result['a']['currentWholeSourceCases'])
        self.assertEqual(result['b']['currentWholeSourceCases'], [])
        self.assertNotIn('liveAccepted', result['a'])

    def test_layout_cannot_drop_one_hero_count(self):
        group = next(g for g in self.plan['groups'] if g['id'] == 'hero-layouts')
        group['scenarios'] = [x for x in group['scenarios'] if x['id'] != 'four']
        with self.assertRaisesRegex(ValueError, 'scenario missing'):
            audit.validate_acceptance(self.plan, self.contracts, self.inventory)

    def test_historical_partial_requires_clean_current_source_proof(self):
        row = {'source': 'Legacy抽到的牌：', 'objectName': 'Description Label', 'cardId': ''}
        result = {'reason': 'translated-output-observed', 'target': None,
                  'sourceReplayOutputs': [{'sourceId': 'original', 'target': '传承侠抽到的牌：'}]}
        self.assertEqual(audit.translation_completeness(row, result)['status'], 'historical-partial-current-source-clean')
        result['sourceReplayOutputs'][0]['target'] = 'Legacy抽到的牌：'
        self.assertEqual(audit.translation_completeness(row, result)['status'], 'partial-residual')
