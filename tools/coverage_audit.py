"""Evidence intake, explicit triage and CI gate. Never writes runtime translations."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LIMIT = 32 * 1024 * 1024

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def digest(value):
    return hashlib.sha256(value).hexdigest()

def identity(row):
    return digest(json.dumps([row[k] for k in ('buildId', 'path', 'cardId', 'source', 'inputSource')],
                             ensure_ascii=False, separators=(',', ':')).encode())[:24]

def intake(paths, existing=None):
    samples = {r['id']: r for r in (existing or {}).get('samples', [])}
    sessions = list((existing or {}).get('sessions', []))
    for path in paths:
        path = Path(path)
        data = path.read_bytes()
        if len(data) > LIMIT:
            raise ValueError('Evidence file too large')
        session = {'file': path.name, 'sha256': digest(data), 'issues': []}
        if any(s['sha256'] == session['sha256'] for s in sessions):
            continue
        if path.suffix.lower() == '.zip':
            with zipfile.ZipFile(path) as z:
                names = {}
                for entry in z.infolist():
                    name = entry.filename.replace('\\', '/').rsplit('/', 1)[-1]
                    if name in ('LogOutput.log', 'manifest.json', 'installed-hashes.json'):
                        if name in names or entry.file_size > LIMIT:
                            raise ValueError('Duplicate/oversized evidence member: ' + name)
                        names[name] = entry
                if set(names) != {'LogOutput.log', 'manifest.json', 'installed-hashes.json'}:
                    raise ValueError('Incomplete evidence archive')
                log = z.read(names['LogOutput.log']).decode('utf-8-sig')
                manifest = json.loads(z.read(names['manifest.json']).decode('utf-8-sig'))
                installed = json.loads(z.read(names['installed-hashes.json']).decode('utf-8-sig'))
            session.update(version=manifest['version'], buildId=manifest['buildId'], installed=installed,
                           manifestSourceCommit=manifest['sourceCommit'])
            for name, key in [('SotM2ChinesePrototype.dll', 'dllSha256'), ('surface-probe.json', 'probeSha256')]:
                if installed.get(name) != manifest[key]:
                    session['issues'].append('installed-manifest-mismatch:' + name)
        else:
            log = data.decode('utf-8-sig')
            session.update(version='unknown', buildId='26880829', installed={})
            session['issues'].append('missing-installed-hashes')
        if f"PROTOTYPE_LOADED {session['version']} build={session['buildId']} mode=Chinese" not in log:
            session['issues'].append('loaded-version-or-mode-unverified')
        errors = ['SURFACE_PROBE_STOPPED', 'UI_RENDER_FALLBACK', 'FONT_GLYPHS_MISSING', 'UPDATE_ERROR']
        session['runtimeWarnings'] = [token for token in errors if token in log]
        session['issues'].extend(session['runtimeWarnings'])
        # Existing application logs are deduplicated by object/source, not full path.
        # Retain as supporting signals only, never as per-path live acceptance.
        session['applicationSignals'] = []
        for line in log.splitlines():
            match = re.search(r'UI_TRANSLATED_BEFORE_RENDER object=(.*?) source=(.*)$', line)
            if match:
                value = json.loads(match[2])
                if isinstance(value, str) and not value.endswith('…'):
                    session['applicationSignals'].append({'objectName': match[1], 'source': value})
        counts = collections.Counter()
        n = 0
        for line_number, line in enumerate(log.splitlines(), 1):
            if 'SURFACE_PROBE {' not in line:
                continue
            raw = json.loads(line.split('SURFACE_PROBE ', 1)[1])
            fields = ('objectName', 'path', 'cardId', 'source', 'inCardView', 'inputSource', 'truncated', 'markerCandidate')
            row = {k: raw[k] for k in fields}
            if any(not isinstance(row[k], str) for k in ('objectName', 'path', 'cardId', 'source', 'inputSource')):
                raise ValueError('Invalid evidence string field')
            if any(type(row[k]) is not bool for k in ('inCardView', 'truncated', 'markerCandidate')):
                raise ValueError('Invalid evidence boolean field')
            row['buildId'] = session['buildId']
            row['id'] = identity(row)
            row['evidence'] = [{'session': session['sha256'], 'line': line_number,
                                'version': session['version'], 'markerCandidate': raw['markerCandidate']}]
            if row['id'] in samples:
                samples[row['id']]['evidence'].extend(row['evidence'])
                # Latest supplied session is the observation used in the summary.
                samples[row['id']]['markerCandidate'] = row['markerCandidate']
            else:
                samples[row['id']] = row
            counts[row['objectName']] += 1
            n += 1
        if not n:
            raise ValueError('No surface observations')
        session['observations'] = n
        session['captureLimitReached'] = n >= 1200 or any(v >= 200 for v in counts.values())
        if session['captureLimitReached']:
            session['issues'].append('bounded-capture-limit-reached')
        # Even below the limit, excluded controls and unvisited scenes are unknown.
        session['exhaustive'] = False
        sessions.append(session)
    return {'schemaVersion': 1, 'sessions': sessions, 'samples': sorted(samples.values(), key=lambda x: x['id'])}

NONHERO_CHECKS = {'deck-reveal', 'deck-replace', 'deck-draw', 'trash-discard',
                 'deck-discard-excluded', 'trash-replace-excluded', 'trash-draw-excluded',
                 'wrong-category-excluded', 'hero-layout-excluded', 'unknown-owner-excluded',
                 'hp-excluded', 'small-effect', 'full-effect', 'hint-phase', 'result-reveal'}

def validate_nonhero_matrix(matrix, contracts, root=ROOT):
    """Require every inventoried nonhero owner and its executable role checks.

    This validates offline scope only. It does not infer UI visibility or live acceptance.
    The shipped inventory folder is intentionally spelled 'villians'.
    """
    if matrix.get('schemaVersion') != 1 or set(matrix.get('requiredChecks', [])) != NONHERO_CHECKS:
        raise ValueError('Nonhero matrix must retain every required role check')
    folders = {'villain': 'villians', 'environment': 'environments'}
    expected = {(kind, p.stem) for kind, folder in folders.items()
                for p in (root / 'inventory-translations' / folder).glob('*.json')}
    owners = matrix['owners']
    actual = [(x['kind'], x['id']) for x in owners]
    if not expected or len(actual) != len(set(actual)) or set(actual) != expected:
        raise ValueError('Nonhero matrix roster differs from current text inventory')
    contexts = {(f['id'], c['id']): c for f in contracts['families'] for c in f['contexts']}
    used = set()
    for owner in owners:
        inventory_file = 'inventory-translations/' + folders[owner['kind']] + '/' + owner['id'] + '.json'
        if owner['inventoryFile'] != inventory_file or not owner.get('provenance'):
            raise ValueError('Nonhero owner needs exact inventory provenance')
        titles = [x['original'] for x in read(root / inventory_file)
                  if x['key'].endswith('/decks/@' + owner['id'] + '/title')]
        if titles != [owner['sourceName']]:
            raise ValueError('Nonhero source name differs from inventory')
        if set(owner['contracts']) != NONHERO_CHECKS:
            raise ValueError('Nonhero owner missing a required role')
        for role, link in owner['contracts'].items():
            key = (link['family'], link['id'])
            if key not in contexts or key in used:
                raise ValueError('Nonhero role requires a unique executable contract')
            used.add(key)
            context = contexts[key]
            if context['status'] != ('excluded' if role.endswith('-excluded') else 'offline-covered'):
                raise ValueError('Synthetic nonhero contract cannot claim live acceptance')
            if 'case' not in context or (context['case'].get('expected') is None) != role.endswith('-excluded'):
                raise ValueError('Nonhero role must execute its positive or negative case')
    return {'owners': len(owners), 'contracts': len(used), 'liveAccepted': False}

def validate_contracts(contract, templates, root=ROOT):
    required_files = {'runtime/DynamicDisplayRules.cs', 'runtime/SurfaceProbeRules.cs',
                      'runtime/SurfaceProbe.cs', 'runtime/Plugin.cs', 'runtime/DisplayRules.cs',
                      'probe/dynamic-pilot.json'}
    if set(contract['reviewedFiles']) != required_files:
        raise ValueError('Coverage review must include every runtime routing file')
    claimed, ids = [], set()
    for family in contract['families']:
        if family['id'] in ids:
            raise ValueError('Duplicate family')
        ids.add(family['id'])
        claimed.extend(family['templates'])
        contexts = family['contexts']
        if len({c['id'] for c in contexts}) != len(contexts):
            raise ValueError('Duplicate context')
        if not any(c['status'] == 'offline-covered' for c in contexts):
            raise ValueError('Family has no positive contract')
        if not any(c['status'] == 'excluded' for c in contexts):
            raise ValueError('Family has no negative contract')
        for c in contexts:
            if c['status'] not in ('offline-covered', 'unobserved', 'deferred', 'excluded', 'live-accepted'):
                raise ValueError('Unknown context status')
            if not c.get('reason') or not all(c.get(k) for k in ('view', 'heroState', 'layout')):
                raise ValueError('Context needs reason and dimensions')
            if c['status'] in ('offline-covered', 'excluded', 'live-accepted'):
                if 'case' not in c or ('expected' not in c['case']):
                    raise ValueError('Covered/excluded context requires executable case')
                if (c['case']['expected'] is None) != (c['status'] == 'excluded'):
                    raise ValueError('Context status and expected output disagree')
            if c['status'] == 'live-accepted' and not c.get('acceptanceEvidence'):
                raise ValueError('Live acceptance needs explicit human evidence')
            if c['status'] == 'deferred' and not c.get('revisitWhen'):
                raise ValueError('Deferred context needs a reopening condition')
    if len(claimed) != len(set(claimed)) or set(claimed) != set(templates):
        raise ValueError('Every runtime template must belong to exactly one family')
    for path, expected in contract['reviewedFiles'].items():
        if digest((root / path).read_bytes()) != expected:
            raise ValueError('Coverage review required after changing ' + path)

def validate_triage(rows, replay, triage):
    if len({r['id'] for r in rows}) != len(rows):
        raise ValueError('Duplicate snapshot identity')
    for r in rows:
        if r['id'] != identity(r):
            raise ValueError('Snapshot source/identity mismatch')
    results = {r['id']: r for r in replay['rows']}
    if len(results) != len(replay['rows']) or set(results) != {r['id'] for r in rows}:
        raise ValueError('Replay does not cover exact evidence set')
    decisions = {r['id']: r for r in triage['entries']}
    if len(decisions) != len(triage['entries']):
        raise ValueError('Duplicate triage identity')
    if set(decisions) - set(results):
        raise ValueError('Previously triaged observations disappeared; append evidence instead of replacing it')
    errors = []
    allowed = {'fix-stage-1', 'fixed-text-stage-2', 'deck-review-stage-3', 'deferred-ea', 'nonlinguistic', 'pending-diagnosis', 'catalog-difference-review'}
    for row in rows:
        result = results[row['id']]
        if result['reason'] not in ('unresolved-full-string', 'known-source-route-gap', 'truncated-evidence'):
            continue
        decision = decisions.get(row['id'])
        if not decision:
            errors.append('untriaged:' + row['id'])
            continue
        if decision.get('disposition') not in allowed or not decision.get('reason') or not decision.get('owner') or not decision.get('nextAction'):
            errors.append('incomplete-triage:' + row['id'])
        if decision.get('reviewedReason') != result['reason']:
            errors.append('changed-classification:' + row['id'])
        if decision.get('disposition') == 'deferred-ea' and not decision.get('revisitWhen'):
            errors.append('missing-deferral-condition:' + row['id'])
    return errors

def validate_inventory(status, drafts, templates):
    sources = {r['key']: r for r in drafts}
    entries = {r['key']: r for r in status['entries']}
    if len(entries) != len(status['entries']) or set(entries) != set(sources):
        raise ValueError('Inventory status must cover every source key exactly once')
    for key, row in entries.items():
        if row['sourceSha256'] != digest(sources[key]['original'].encode()):
            raise ValueError('Source changed; reopen inventory review: ' + key)
        if row['stage'] not in (1, 2, 3) or row['translation'] not in ('draft', 'reviewed') or row['display'] not in ('pending', 'covered', 'composed', 'deferred') or row['live'] not in ('pending', 'accepted', 'deferred', 'not-applicable'):
            raise ValueError('Invalid inventory state')
        if not set(row['templateIds']) <= set(templates):
            raise ValueError('Unknown runtime template reference')
        if row['display'] in ('covered', 'composed') and (not row['templateIds'] or not row['evidence']):
            raise ValueError('Display coverage needs rule references and evidence')
        if row['live'] == 'accepted' and (row['translation'] != 'reviewed' or row['display'] not in ('covered', 'composed') or not row.get('acceptanceEvidence')):
            raise ValueError('Live acceptance requires reviewed translation, display coverage and human evidence')
        if row['live'] == 'not-applicable' and (row['display'] != 'composed' or not row['evidence']):
            raise ValueError('Fragments must reference complete-sentence evidence before closure')
        if (row['live'] == 'deferred' or row['display'] == 'deferred') and not row.get('revisitWhen'):
            raise ValueError('Deferred inventory needs reopening condition')
    return entries

def validate_regression(baseline, replay):
    current = {r['id']: r for r in replay['rows']}
    if len({r['id'] for r in baseline['entries']}) != len(baseline['entries']):
        raise ValueError('Duplicate regression identity')
    errors = []
    for row in baseline['entries']:
        actual = current.get(row['id'])
        if actual is None or (actual['reason'], actual.get('target')) != (row['reason'], row.get('target')):
            errors.append('known-observation-regressed:' + row['id'])
    return errors

def make_report(snapshot, replay, triage, catalog_hash, drafts):
    results = {r['id']: r for r in replay['rows']}
    decisions = {r['id']: r for r in triage['entries']}
    errors = validate_triage(snapshot['samples'], replay, triage)
    for s in snapshot['sessions']:
        errors.extend(s['file'] + ':' + x for x in s['issues'])
    counts = collections.Counter(r['reason'] for r in replay['rows'])
    entries = []
    for row in snapshot['samples']:
        result = results[row['id']]
        signals = [s['sha256'] for s in snapshot['sessions'] if
                   {'objectName': row['objectName'], 'source': row['source']} in s.get('applicationSignals', [])]
        entries.append({**row, 'replay': result, 'triage': decisions.get(row['id']),
                        'applicationSignalSessions': signals, 'applicationSignalHasFullPath': False,
                        'stages': {'extracted': True, 'draftAvailable': any(d['original'] == row['source'] for d in drafts),
                                   'currentDisplayRuleMatched': result['reason'] == 'current-rule-match',
                                   'liveAccepted': False}})
    return {'schemaVersion': 1, 'summary': dict(sorted(counts.items())), 'gateErrors': errors,
            'sessions': snapshot['sessions'], 'replayCatalogSha256': catalog_hash,
            'catalogMatchesCaptured': all(s['installed'].get('sample-texts.json') == catalog_hash for s in snapshot['sessions']),
            'limits': ['Offline replay is not screenshot/click acceptance.',
                       'markerCandidate only reports the dynamic layer; base catalog may handle a nonmatch.',
                       'Numeric/formatted buffers, input fields, unvisited views and capture limits prevent exhaustive coverage.',
                       'Unknown text is unclassified, not automatically a missing translation or a blocked guard.'],
            'entries': entries,
            'inventoryLedger': [{'key': d['key'], 'file': d['file'], 'original': d['original'],
                                 'extracted': True, 'translationStatus': 'draft', 'reviewStatus': d['status'],
                                 'runtimeEmissionFromDraft': False, 'liveAccepted': False,
                                 'displayStatus': 'matched-in-current-replay' if any(r['source'] == d['original'] and results[r['id']]['reason'] == 'current-rule-match' for r in snapshot['samples']) else 'not-established',
                                 'observations': [r['id'] for r in snapshot['samples'] if r['source'] == d['original']]}
                                for d in drafts]}

def markdown(report):
    out = ['# 显示覆盖审计', '', '本报告区分离线规则匹配、采集时动态匹配与人工实机验收。', '',
           '检查结果：' + ('通过（所有未知项已登记去向；不表示全部完成）' if not report['gateErrors'] else '未通过'), '',
           '| 分类 | 数量 |', '| --- | ---: |']
    out += [f'| {k} | {v} |' for k, v in report['summary'].items()]
    out += ['', '运行词典与采集时词典完全一致：' + str(report['catalogMatchesCaptured']),
            '第一阶段完成：' + str(report.get('stage1Complete', False)),
            '动态库存译稿：' + str(len(report['inventoryLedger'])) + '条；译稿不等于已接入或已验收。', '', '## 待办', '']
    for e in report['entries']:
        if e['replay']['reason'] in ('unresolved-full-string', 'known-source-route-gap', 'truncated-evidence'):
            t = e['triage'] or {}
            out += [f"- `{e['id']}` [{t.get('disposition', 'untriaged')}] {e['source'].replace(chr(10), ' ')}",
                    '  - 路径：`' + e['path'] + '`', '  - 下一步：' + t.get('nextAction', '登记处理方式')]
            if e['applicationSignalSessions']:
                out.append('  - 已有同对象名／原文的应用日志信号；日志不含完整路径，不自动认定本位置验收。')
    out += ['', '## 边界', '', '日志采用限量采集，未出现的界面仍为未知。数字和格式化缓冲不作为翻译目标。',
            '相同原文可能由基础词典处理，不能仅凭 markerCandidate=false 判为漏译。',
            '当前候选词典与采集时词典不一致时，仅能报告当前代码的离线结果。',
            '日志中的动态匹配不能自动提升为画面或点击验收；保护条件是否实际拦截需独立证据。', '']
    if report['gateErrors']:
        out += ['## 检查失败', ''] + ['- ' + x for x in report['gateErrors']]
    return '\n'.join(out)

def main():
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest='command', required=True)
    e = sub.add_parser('intake'); e.add_argument('evidence', nargs='+'); e.add_argument('--out', required=True)
    c = sub.add_parser('check')
    c.add_argument('--snapshot', default='coverage/evidence/latest.json')
    c.add_argument('--contracts', default='coverage/contracts.json')
    c.add_argument('--nonhero-matrix', default='coverage/nonhero-route-matrix.json')
    c.add_argument('--triage', default='coverage/triage.json')
    c.add_argument('--inventory-status', default='coverage/inventory-status.json')
    c.add_argument('--regression', default='coverage/regression-baseline.json')
    c.add_argument('--catalog', default='build/sample-texts.json')
    c.add_argument('--probe', default='probe/dynamic-pilot.json')
    c.add_argument('--dotnet', default='dotnet'); c.add_argument('--out', default='build/coverage')
    c.add_argument('--require-stage-1-complete', action='store_true')
    a = p.parse_args()
    if a.command == 'intake':
        write(a.out, intake(a.evidence, read(a.out) if Path(a.out).exists() else None)); return
    contracts = read(a.contracts)
    templates = read(a.probe)['Templates']
    validate_contracts(contracts, templates)
    nonhero_summary = validate_nonhero_matrix(read(a.nonhero_matrix), contracts)
    drafts = read(ROOT / 'probe/dynamic-text-review.json')['entries']
    inventory_status = validate_inventory(read(a.inventory_status), drafts, templates)
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    replay_path = out / 'replay.json'
    subprocess.run([a.dotnet, 'run', '--project', str(ROOT / 'tools/coverage-replay/CoverageReplay.csproj'), '--',
                    a.catalog, a.probe, a.snapshot, a.contracts, str(replay_path)], check=True)
    report = make_report(read(a.snapshot), read(replay_path), read(a.triage), digest(Path(a.catalog).read_bytes()),
                         drafts)
    report['gateErrors'].extend(validate_regression(read(a.regression), read(replay_path)))
    report['nonheroMatrix'] = nonhero_summary
    for row in report['inventoryLedger']:
        status = inventory_status[row['key']]
        row['reviewedStatus'] = status
        row['translationStatus'] = status['translation']
        row['liveAccepted'] = status['live'] == 'accepted'
    closure = []
    for e in report['entries']:
        if e['replay']['reason'] in ('unresolved-full-string', 'known-source-route-gap', 'truncated-evidence') and (e['triage'] or {}).get('disposition') in ('fix-stage-1', 'pending-diagnosis', 'catalog-difference-review'):
            closure.append('unresolved-stage-1:' + e['id'])
    for family in contracts['families']:
        for context in family['contexts']:
            if context['status'] not in ('live-accepted', 'excluded', 'deferred'):
                closure.append('context-not-accepted:' + family['id'] + ':' + context['id'])
    # Draft fragments/parameter semantics cannot silently be declared covered by tests.
    for key, d in inventory_status.items():
        if d['stage'] == 1 and d['live'] not in ('accepted', 'deferred', 'not-applicable'):
            closure.append('inventory-not-closed:' + key)
    report['stage1ClosureErrors'] = closure
    report['stage1Complete'] = not closure and not report['gateErrors']
    write(out / 'report.json', report)
    (out / 'report.md').write_text(markdown(report), encoding='utf-8')
    print(json.dumps({'summary': report['summary'], 'gateErrors': report['gateErrors']}, ensure_ascii=False))
    if report['gateErrors'] or (a.require_stage_1_complete and closure):
        raise SystemExit(1)

if __name__ == '__main__':
    main()
