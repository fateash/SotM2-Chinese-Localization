"""Full source inventory and the opt-in 0.1.6 display compiler."""
import argparse, collections, re
from pathlib import Path
from catalog import Invalid, load, write, sha, encode, validate_text, POLICIES, STAGES, DISPLAY_VARIANTS

ROUTES={'inventory','ui','body','card-exact','card-style','keyword','phase','power-heading'}
OBJECTS={'Title Label','Card Name Label','Flavor Text Label','Flavor Reference Label','Keywords Label','Phase Label','Body Label','Setup Label','Advanced Label','Advanced Heading Label'}
def safe_path(name):
    if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*\.json',name):
        raise Invalid('Unsafe inventory file path')
    return name

def registry(data):
    if (data.get('schemaVersion'),data.get('runtimeVersion'),data.get('buildId'))!=(2,'0.1.6','26880829'):
        raise Invalid('Unsupported inventory schema')
    entries=data.get('entries');keys=set()
    if not isinstance(entries,list) or not entries:raise Invalid('Empty inventory')
    for e in entries:
        k=e.get('key');s=e.get('original');route=e.get('route')
        if not isinstance(k,str) or not k or len(k)>512 or any(ord(c)<32 for c in k) or k in keys:raise Invalid('Duplicate/invalid inventory key')
        keys.add(k);safe_path(e.get('file'))
        if not isinstance(s,str) or not s or e.get('sourceSha256')!=sha(s):raise Invalid('Invalid inventory original/hash: '+k)
        if route not in ROUTES:raise Invalid('Unknown runtime route: '+k)
        if route in {'body','card-exact','card-style'} and not e.get('card'):raise Invalid('Missing card identity')
        if route=='card-exact' and e.get('objectName') not in OBJECTS:raise Invalid('Invalid card object scope')
        if route=='card-style' and e.get('style')!='PowerTitle':raise Invalid('Unsupported card style')
        if route=='ui' and not e.get('objectName'):raise Invalid('Missing UI object scope')
        if route=='phase' and s not in {'Start','Play','Power','Draw','End','Incapacitated'}:raise Invalid('Unknown phase')
        if route=='power-heading' and s!='Power':raise Invalid('Unknown power heading')
    return entries

def snapshot(directory,data):
    expected={e['file'] for e in registry(data)};root=Path(directory)
    found={p.relative_to(root).as_posix() for p in root.rglob('*.json')}
    if found!=expected:raise Invalid('Snapshot file set mismatch')
    rows={}
    for file in sorted(expected):
        items=load(root/file)
        if not isinstance(items,list):raise Invalid('Expected array')
        wanted={e['key'] for e in data['entries'] if e['file']==file}
        if len({x.get('key') for x in items})!=len(items) or {x.get('key') for x in items}!=wanted:raise Invalid('Wrong/missing keys in '+file)
        for x in items:
            if x['key'] in rows:raise Invalid('Duplicate snapshot key')
            rows[x['key']]=x
    return rows

def build(data,rows,mode='translated'):
    entries=registry(data)
    if mode not in POLICIES or set(rows)!={e['key'] for e in entries}:raise Invalid('Mode/key set mismatch')
    out={'SchemaVersion':2,'RuntimeVersion':'0.1.6','BuildId':'26880829','Ui':[],'Cards':[],'Display':[]};skipped=[];included=0
    def display(e,obj,s,t,kind='exact',style='',card=None):
        out['Display'].append(dict(ObjectName=obj,Card=e.get('card','') if card is None else card,Mode=kind,Style=style,Source=s,Target=t))
    for e in entries:
        row=rows[e['key']];s=e['original'];t=row.get('translation');stage=row.get('stage');route=e['route']
        if row.get('original')!=s:raise Invalid('Stale original: '+e['key'])
        if 'stage' in row and (type(stage) is not int or stage not in STAGES):raise Invalid('Invalid stage')
        if not isinstance(t,str):raise Invalid('Translation is not a string')
        if not t.strip() or stage not in POLICIES[mode]:
            skipped.append(dict(key=e['key'],reason='empty' if not t.strip() else 'stage-not-eligible'));continue
        # Holding inventory is intentional: malformed source markup or unproven paths
        # must not prevent storage/sync, and must never reach the runtime catalog.
        if route=='inventory':
            skipped.append(dict(key=e['key'],reason=e.get('holdReason','runtime-not-enabled')));continue
        validate_text(s,t,'card.body' if route=='body' else 'display')
        if any(mark in t for mark in ('名·','题·','能·','类·','阶·','引·','刊·','难·')):raise Invalid('Probe marker in production translation')
        included+=1
        if route=='ui':out['Ui'].append(dict(ObjectName=e['objectName'],Source=s,Target=t))
        elif route=='body':
            out['Cards'].append(dict(Card=e['card'],Source=s,Target=t))
            if e.get('incapacitatedBullet'):
                a,b=DISPLAY_VARIANTS['incapacitated-bullet-v1'];out['Cards'].append(dict(Card=e['card'],Source=a+s+b,Target=a+t+b))
        elif route=='card-exact':
            obj=e['objectName']
            if obj=='Flavor Text Label':
                # Preserve original authored line breaks; only expand the game's BR token.
                for br in ('\r\n','\n'):
                    a,b=s.replace('{BR}',br),t.replace('{BR}',br)
                    for left,right in [('',''),('“','”'),('"','"')]:display(e,obj,left+a+right,left+b+right)
            elif obj=='Flavor Reference Label':display(e,obj,'--'+s,'--'+t)
            else:
                for objname in ('Title Label','Card Name Label'):
                    for source in dict.fromkeys((s,s.upper())):display(e,objname,source,t)
        elif route=='card-style':display(e,'Body Label',s,t,'style','PowerTitle')
        elif route=='keyword':display(e,'Keywords Label',s,t,'keyword',card='')
        elif route=='phase':
            for obj in ('Body Label','Setup Label','Advanced Label','Advanced Heading Label'):
                for source in (s+' Phase',(s+' Phase').upper()):
                    display(e,obj,source,t,'style',s+'Phase',card='')
                    display(e,obj,source,t,'phase-inline',card='')
            for source in (s+' Phase',(s+' Phase').upper()):display(e,'Phase Label',source,t,card='')
        elif route=='power-heading':
            for obj in ('Body Label','Setup Label','Advanced Label'):
                display(e,obj,'POWER: ',t+': ','style','PowerHeading',card='')
    # Shared strings may be emitted repeatedly; equal translations coalesce,
    # conflicting translations in the same runtime scope fail closed.
    for bucket,fields in [('Ui',('ObjectName','Source')),('Cards',('Card','Source')),('Display',('ObjectName','Card','Mode','Style','Source'))]:
        unique={}
        for row in out[bucket]:
            k=tuple(row[x] for x in fields)
            if k in unique and unique[k]['Target']!=row['Target']:raise Invalid('Conflicting runtime translations: '+repr(k))
            unique[k]=row
        out[bucket]=list(unique.values())
    if not included:raise Invalid('No eligible runtime translations; no catalog produced')
    report=dict(toolVersion='0.2.0',runtimeVersion='0.1.6',buildId=data['buildId'],registered=len(entries),included=included,
      ui=len(out['Ui']),cardBody=len(out['Cards']),display=len(out['Display']),skipped=skipped,mode=mode,
      catalogSha256=sha(encode(out)),registrySha256=sha(encode(data)),snapshotSha256=sha(encode(rows)),liveStatus='new-runtime-pending')
    return out,report

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--export-originals',action='store_true');ap.add_argument('--registry',default='baseline/inventory.json');ap.add_argument('--input',default='inventory-translations');ap.add_argument('--out',default='build');ap.add_argument('--mode',choices=POLICIES,default='translated');a=ap.parse_args()
    data=load(a.registry)
    if a.export_originals:
        files={}
        for e in registry(data):files.setdefault(e['file'],[]).append(dict(key=e['key'],original=e['original'],translation='',context=e['context']))
        for name,items in files.items():write(Path(a.out)/name,items)
        print('ORIGINAL_EXPORT_OK '+str(sum(map(len,files.values()))));return
    out,report=build(data,snapshot(a.input,data),a.mode)
    write(Path(a.out)/'sample-texts.json',out);write(Path(a.out)/'build-report.json',report)
    print(f'INVENTORY_BUILD_OK registered={report["registered"]} included={report["included"]} display={report["display"]}')
if __name__=='__main__':main()
