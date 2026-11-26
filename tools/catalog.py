"""SotM2 0.1.4 catalog bridge. Python 3.10+, standard library only."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys
import tempfile
import os

class Invalid(ValueError): pass

def sha(text): return hashlib.sha256(text.encode('utf-8')).hexdigest()
def file_for(entry):
    name=entry.get('file', 'ui.json' if entry['kind']=='ui.label' else 'cards-body.json')
    if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z0-9_-]+\.json',name):
        raise Invalid('Invalid translation file name')
    return name
def no_duplicates(pairs):
    obj = {}
    for k, v in pairs:
        if k in obj: raise Invalid('Duplicate JSON property: '+k)
        obj[k] = v
    return obj

def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'), object_pairs_hook=no_duplicates)

def encode(obj): return json.dumps(obj, ensure_ascii=False, indent=2)+'\n'
def write(path, obj):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(prefix=path.name+'.', dir=path.parent)
    try:
        with os.fdopen(fd,'w',encoding='utf-8',newline='\n') as f: f.write(encode(obj))
        os.replace(tmp,path)
    finally:
        if os.path.exists(tmp): os.unlink(tmp)

TOKEN = re.compile(r'\{[^{}]+\}|<[^<>]+>')
PAIR = {'b','i','u','s','color','size','font','material','align','indent','line-indent','cspace','mspace','voffset','width','link','style','mark','nobr','noparse','sub','sup','rotate','uppercase','lowercase','smallcaps'}
def validate_tags(text):
    stack=[]
    for tag in re.findall(r'<[^<>]+>', text):
        m=re.match(r'<(/?)([A-Za-z-]+)(?:[\s=>])',tag)
        if not m: continue
        close,name=m.groups(); name=name.lower()
        if name not in PAIR: continue
        if close:
            if not stack or stack.pop()!=name: raise Invalid('Unbalanced rich text tags')
        else: stack.append(name)
    if stack: raise Invalid('Unclosed rich text tags')

def validate_text(original, target, kind):
    if Counter(TOKEN.findall(original)) != Counter(TOKEN.findall(target)):
        raise Invalid('Placeholder or tag mismatch')
    validate_tags(original); validate_tags(target)
    if re.findall(r'\r\n|\r|\n',original)!=re.findall(r'\r\n|\r|\n',target):
        raise Invalid('Line-break structure changed')
    if kind=='card.body':
        def numbers(s): return Counter(re.findall(r'[+\-−]?\d+(?:\.\d+)?',TOKEN.sub('',s)))
        if numbers(original)!=numbers(target): raise Invalid('Rule numbers/signs changed')
        def variables(s): return re.findall(r'(?<![A-Za-z])X(?![A-Za-z])',TOKEN.sub('',s))
        if len(variables(original))!=len(variables(target)): raise Invalid('Rule variable X changed')
    if any(ord(c)<32 and c not in '\t\r\n' for c in target): raise Invalid('Control character in translation')

def registry(data):
    if data.get('schemaVersion')!=1 or data.get('runtimeVersion')!='0.1.4' or data.get('buildId')!='26880829':
        raise Invalid('Unsupported registry/runtime schema')
    entries=data.get('entries'); keys=set(); locators=set()
    if not isinstance(entries,list) or not entries: raise Invalid('Empty registry')
    for e in entries:
        if not isinstance(e,dict): raise Invalid('Invalid registry row')
        k=e.get('key'); kind=e.get('kind'); source=e.get('original'); locator=e.get('locator')
        if not isinstance(k,str) or not re.fullmatch(r'[A-Za-z0-9_.-]+',k) or k in keys: raise Invalid('Duplicate/invalid stable key')
        if kind not in ('ui.label','card.body'): raise Invalid('Runtime does not support this field kind')
        file_for(e)
        if not isinstance(source,str) or not source: raise Invalid('Empty original')
        if not isinstance(locator,str) or not locator: raise Invalid('Empty runtime locator')
        if e.get('sourceSha256')!=sha(source): raise Invalid('Registry source hash mismatch: '+k)
        identity=(kind,locator,source)
        if identity in locators: raise Invalid('Ambiguous runtime locator: '+k)
        keys.add(k);locators.add(identity)
    return entries

def snapshot(paths):
    rows={}
    for p in sorted(map(Path,paths)):
        data=load(p)
        if not isinstance(data,list): raise Invalid('Expected ParaTranz JSON array: '+str(p))
        for row in data:
            if not isinstance(row,dict): raise Invalid('Invalid translation row')
            key=row.get('key')
            if not isinstance(key,str) or key in rows: raise Invalid('Duplicate/invalid snapshot key: '+str(key))
            rows[key]=row
    return rows

STAGES={-1,0,1,2,3,5,9}
POLICIES={'pilot':{None,1,3,5,9},'translated':{1,3,5,9},'reviewed':{5,9}}
def compile_catalog(data, rows, mode='reviewed', allow_empty=False):
    entries=registry(data)
    if mode not in POLICIES: raise Invalid('Unknown mode')
    expected={e['key'] for e in entries}
    if set(rows)!=expected:
        raise Invalid('Key set mismatch; missing='+str(sorted(expected-set(rows)))+' unknown='+str(sorted(set(rows)-expected)))
    out={'Ui':[],'Cards':[]}; skipped=[]
    for e in entries:
        k=e['key']; row=rows[k]
        if row.get('original')!=e['original']: raise Invalid('Stale/changed original: '+k)
        stage=row.get('stage')
        if 'stage' in row and (type(stage) is not int or stage not in STAGES): raise Invalid('Invalid stage: '+k)
        target=row.get('translation')
        if not isinstance(target,str): raise Invalid('Translation must be a string: '+k)
        if not target.strip() or stage not in POLICIES[mode]:
            skipped.append({'key':k,'stage':stage,'reason':'empty' if not target.strip() else 'stage-not-eligible'})
            continue
        try: validate_text(e['original'],target,e['kind'])
        except Invalid as ex: raise Invalid(k+': '+str(ex)) from ex
        if e['kind']=='ui.label':
            out['Ui'].append({'ObjectName':e['locator'],'Source':e['original'],'Target':target})
        else:
            out['Cards'].append({'Card':e['locator'],'Source':e['original'],'Target':target})
    count=len(out['Ui'])+len(out['Cards'])
    if not count and not allow_empty: raise Invalid('No eligible translations; no runtime catalog produced')
    report={'schemaVersion':1,'toolVersion':'0.1.1','runtimeVersion':'0.1.4','buildId':data['buildId'],'mode':mode,
            'registered':len(entries),'included':count,'ui':len(out['Ui']),'cardBody':len(out['Cards']),
            'skipped':skipped,'registrySha256':sha(encode(data)),
            'snapshotSha256':sha(encode({k:rows[k] for k in sorted(rows)})),
            'catalogSha256':sha(encode(out)), 'platformRoundTrip':'not-yet-verified'}
    return out,report

def export_files(data,rows):
    compile_catalog(data,rows,'pilot',allow_empty=True)
    files={}
    for e in data['entries']:
        row=rows[e['key']]
        context=f"{e['kind']} | {e['locator']} | runtime=0.1.4 | build={data['buildId']}\n{e['context']}\nsourceSha256={e['sourceSha256']}"
        item={'key':e['key'],'original':e['original'],'translation':row['translation'],'context':context}
        files.setdefault(file_for(e),[]).append(item)
    return files

def diff_registries(old,new):
    a={e['key']:e for e in registry(old)}; b={e['key']:e for e in registry(new)}
    return {'added':sorted(b.keys()-a.keys()),'removed':sorted(a.keys()-b.keys()),
            'sourceChanged':[k for k in sorted(a.keys()&b.keys()) if a[k]['original']!=b[k]['original']],
            'locatorChanged':[k for k in sorted(a.keys()&b.keys()) if (a[k]['kind'],a[k]['locator'])!=(b[k]['kind'],b[k]['locator'])]}

def main():
    ap=argparse.ArgumentParser(); sub=ap.add_subparsers(dest='cmd',required=True)
    for name in ('build','export'):
        p=sub.add_parser(name);p.add_argument('--registry',default='baseline/registry.json');p.add_argument('--input',default='translations');p.add_argument('--out',required=True)
        if name=='build':p.add_argument('--mode',choices=POLICIES,default='reviewed')
    p=sub.add_parser('diff');p.add_argument('old');p.add_argument('new')
    args=ap.parse_args()
    if args.cmd=='diff': print(encode(diff_registries(load(args.old),load(args.new))),end='');return
    data=load(args.registry); input_path=Path(args.input)
    paths=list(input_path.glob('*.json')) if input_path.is_dir() else [input_path]
    rows=snapshot(paths); dest=Path(args.out)
    if args.cmd=='export':
        files=export_files(data,rows)
        for name,items in files.items():write(dest/name,items)
        print('EXPORT_OK '+str(sum(map(len,files.values()))));return
    out,report=compile_catalog(data,rows,args.mode)
    # Validation completed before any output writes. A report hashes the matching catalog.
    write(dest/'sample-texts.json',out);write(dest/'build-report.json',report)
    print('BUILD_OK '+str(report['included'])+' mode='+args.mode)

if __name__=='__main__':
    try: main()
    except (Invalid,OSError,ValueError,KeyError,TypeError) as e:
        print('FAILED: '+str(e),file=sys.stderr);sys.exit(1)
