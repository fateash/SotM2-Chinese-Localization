"""Read complete ParaTranz inventory; optional additive original-only migration."""
import argparse,json,os,tempfile,shutil,uuid
from pathlib import Path
from urllib.request import Request,build_opener
from urllib.error import HTTPError,URLError
from catalog import Invalid,load,write
from inventory import registry,build,safe_path
from pull_paratranz import NoRedirect

def encode_payload(path,method,payload):
    if payload is None:return None,'application/json'
    if method=='POST' and path.endswith('/files'):
        # Documented multipart path field avoids treating a folder as a filename.
        filename=safe_path(payload['filename']);folder,_,base=filename.rpartition('/')
        boundary='SotM2-'+uuid.uuid4().hex
        pieces=[]
        if folder:pieces.append('--'+boundary+'\r\nContent-Disposition: form-data; name="path"\r\n\r\n'+folder+'/\r\n')
        pieces.append('--'+boundary+'\r\nContent-Disposition: form-data; name="file"; filename="'+base+'"\r\nContent-Type: application/json\r\n\r\n'+json.dumps(payload['items'],ensure_ascii=False)+'\r\n--'+boundary+'--\r\n')
        return ''.join(pieces).encode('utf-8'),'multipart/form-data; boundary='+boundary
    return json.dumps(payload,ensure_ascii=False).encode('utf-8'),'application/json'

def api(path,token,method='GET',payload=None):
    data,content_type=encode_payload(path,method,payload)
    req=Request('https://paratranz.cn/api'+path,data=data,method=method,headers={'Authorization':'Bearer '+token,'Content-Type':content_type,'Accept':'application/json','User-Agent':'SotM2-Inventory/0.2.0'})
    try:
        with build_opener(NoRedirect).open(req,timeout=45) as res:
            body=res.read(16*1024*1024+1)
            if len(body)>16*1024*1024:raise Invalid('API response too large')
            return json.loads(body.decode('utf-8-sig'))
    except HTTPError as e:raise Invalid('ParaTranz HTTP '+str(e.code)) from None
    except URLError:raise Invalid('ParaTranz network error') from None

def grouped(data):
    result={}
    for e in registry(data):result.setdefault(e['file'],[]).append(e)
    return result
def identity(config,items,groups,allow_missing=False):
    if set(config['files'])!=set(groups) or type(config.get('projectId')) is not int or config['projectId']!=21585:raise Invalid('Config project/file scope mismatch')
    result={}
    for name,fid in config['files'].items():
        safe_path(name)
        if fid is not None and (type(fid) is not int or fid<=0):raise Invalid('Invalid remote ID')
        matches=[f for f in items if f.get('name')==name or (fid is not None and f.get('id')==fid)]
        if not matches and allow_missing and fid is None:continue
        if len(matches)!=1 or matches[0].get('name')!=name or (fid is not None and matches[0].get('id')!=fid):raise Invalid('Remote identity mismatch: '+name)
        if type(matches[0].get('id')) is not int:raise Invalid('Invalid resolved file ID')
        result[name]=matches[0]
    if len({f['id'] for f in result.values()})!=len(result):raise Invalid('Duplicate remote IDs')
    return result
def validate_rows(entries,rows,subset=False):
    expected={e['key']:e['original'] for e in entries}
    if not isinstance(rows,list) or len({r.get('key') for r in rows})!=len(rows):raise Invalid('Invalid/duplicate remote rows')
    actual={r['key'] for r in rows}
    if (actual-set(expected)) or (not subset and actual!=set(expected)):raise Invalid('Remote source key mismatch')
    for r in rows:
        if r.get('original')!=expected[r['key']]:raise Invalid('Remote original differs: '+r['key'])
        if 'stage' not in r:raise Invalid('API snapshot missing review status')
    return {r['key']:{k:r.get(k) for k in ('key','original','translation','stage','context')} for r in rows}
def pull(config,data,request):
    groups=grouped(data);prefix=f'/projects/{config["projectId"]}';before=identity(config,request(prefix+'/files'),groups)
    result={};allrows={}
    for name,f in before.items():
        rows=validate_rows(groups[name],request(prefix+f'/files/{f["id"]}/translation'))
        result[name]=[rows[e['key']] for e in groups[name]];allrows.update(rows)
    after=identity(config,request(prefix+'/files'),groups)
    for name,f in before.items():
        if any(f.get(k)!=after[name].get(k) for k in ('id','name','modifiedAt','updatedAt','total')):raise Invalid('Project changed during snapshot')
    build(data,allrows,'translated')
    return result,dict(projectId=config['projectId'],files={n:f['id'] for n,f in after.items()})

def migrate(config,data,request,apply=False,checkpoint=None):
    groups=grouped(data);prefix=f'/projects/{config["projectId"]}'
    before=identity(config,request(prefix+'/files'),groups,True);snapshots={};plan=[]
    # Preflight EVERY existing file before the first mutation. Unknown remote keys
    # stop this migration; originals and all translation states are retained.
    for name,entries in groups.items():
        if name in before:
            snapshots[name]=validate_rows(entries,request(prefix+f'/files/{before[name]["id"]}/translation'),True)
        plan.append(dict(file=name,operation='update-originals' if name in before else 'create',added=len(entries)-len(snapshots.get(name,{}))))
    if checkpoint:checkpoint('before',dict(files=before,rows=snapshots,plan=plan))
    if not apply:return plan
    for name,entries in groups.items():
        # Never send translation or stage fields to either write endpoint.
        payload=dict(filename=name,items=[dict(key=e['key'],original=e['original'],context=e['context']) for e in entries])
        if name in before:
            current=identity(config,request(prefix+'/files'),groups,True)[name]
            if any(current.get(k)!=before[name].get(k) for k in ('id','name','modifiedAt','updatedAt','total')):raise Invalid('File changed before original update: '+name)
            payload['incremental']=True
            request(prefix+f'/files/{before[name]["id"]}',method='POST',payload=payload)
        else:request(prefix+'/files',method='POST',payload=payload)
        if checkpoint:checkpoint('last-completed',dict(file=name))
    result,resolved=pull(config,data,request)
    for name,old in snapshots.items():
        new={r['key']:r for r in result[name]}
        for key,row in old.items():
            if any(row.get(k)!=new[key].get(k) for k in ('translation','stage')):raise Invalid('Remote translation/status changed during migration; inspect before checkpoint')
    if checkpoint:checkpoint('after',dict(config=resolved,files=len(result),entries=sum(map(len,result.values()))))
    return result,resolved

def main():
    p=argparse.ArgumentParser();p.add_argument('action',choices=['pull','plan','migrate']);p.add_argument('--config',default='paratranz-inventory.json');p.add_argument('--registry',default='baseline/inventory.json');p.add_argument('--out',default='incoming-inventory');a=p.parse_args()
    token=os.getenv('PARATRANZ_TOKEN')
    if not token:raise Invalid('PARATRANZ_TOKEN is required')
    config=load(a.config);data=load(a.registry);out=Path(a.out)
    if out.exists():raise Invalid('Output directory already exists')
    request=lambda path,**kw:api(path,token,**kw)
    if a.action=='plan':
        write(out/'migration-plan.json',migrate(config,data,request));return
    if a.action=='migrate':
        checkpoints=out.with_name(out.name+'-checkpoints')
        result,resolved=migrate(config,data,request,True,lambda n,v:write(checkpoints/(n+'.json'),v))
    else:result,resolved=pull(config,data,request)
    out.parent.mkdir(parents=True,exist_ok=True);tmp=Path(tempfile.mkdtemp(prefix='.inventory-',dir=out.parent))
    try:
        for name,rows in result.items():write(tmp/name,rows)
        os.replace(tmp,out)
    finally:
        if tmp.exists():shutil.rmtree(tmp)
    write(out.with_name(out.name+'-resolved.json'),resolved)
    print('INVENTORY_SYNC_OK files='+str(len(result))+' entries='+str(sum(map(len,result.values()))))
if __name__=='__main__':main()
