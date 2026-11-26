"""Read-only ParaTranz file snapshot. Credentials from environment, never serialized."""
import argparse,json,os,sys,tempfile,shutil
from pathlib import Path
from urllib.request import Request,build_opener,HTTPRedirectHandler
from urllib.error import HTTPError,URLError
from catalog import load,write,registry,compile_catalog,Invalid,file_for

class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): raise Invalid('Unexpected API redirect; token not forwarded')

def api(path,token):
    request=Request('https://paratranz.cn/api'+path,headers={'Authorization':'Bearer '+token,'Accept':'application/json','User-Agent':'SotM2-Localization-Pipeline/0.1.1'})
    try:
        with build_opener(NoRedirect).open(request,timeout=30) as res:
            blob=res.read(16*1024*1024+1)
            if len(blob)>16*1024*1024: raise Invalid('API response too large')
            return json.loads(blob.decode('utf-8-sig'))
    except HTTPError as e: raise Invalid('ParaTranz HTTP '+str(e.code)+'; no snapshot committed') from None
    except URLError: raise Invalid('ParaTranz network error; no snapshot committed') from None

def fetch(config,data,request):
    project=config.get('projectId');files=config.get('files',{})
    if type(project) is not int or project<=0:raise Invalid('Set a positive projectId in paratranz-project.json')
    entries=registry(data); prefix=f'/projects/{project}'
    if not isinstance(files,dict) or set(files)!={file_for(e) for e in entries}:
        raise Invalid('Configured files must exactly match registry file groups')
    ids=[v for v in files.values() if v is not None]
    if any(type(v) is not int or v<=0 for v in ids) or len(set(ids))!=len(ids):
        raise Invalid('File IDs must be distinct positive integers or null for exact-name discovery')
    remote_names=config.get('remoteNames',{})
    if not isinstance(remote_names,dict) or set(remote_names)-set(files):
        raise Invalid('Remote name mapping contains unconfigured files')
    names={name:remote_names.get(name,name) for name in files}
    if any(not isinstance(v,str) or not v for v in names.values()) or len(set(names.values()))!=len(names):
        raise Invalid('Remote file names must be nonempty and distinct')
    def listing():
        items=request(prefix+'/files')
        if not isinstance(items,list):raise Invalid('Expected files array')
        selected={}
        for name,fid in files.items():
            remote_name=names[name]
            found=[f for f in items if (f.get('id')==fid if fid is not None else f.get('name')==remote_name)]
            if len(found)!=1 or found[0].get('name')!=remote_name:raise Invalid('Remote file identity mismatch: '+name)
            if type(found[0].get('id')) is not int or found[0]['id']<=0:raise Invalid('Invalid remote file ID')
            selected[name]=found[0]
        return selected
    before=listing();result={};allrows={}
    if len({f['id'] for f in before.values()})!=len(files):raise Invalid('Duplicate resolved file ID')
    for name,remote in before.items():
        fid=remote['id']
        rows=request(prefix+f'/files/{fid}/translation')
        if not isinstance(rows,list):raise Invalid('Expected file translation array')
        normalized=[]
        for row in rows:
            if not isinstance(row,dict) or 'stage' not in row:raise Invalid('Missing API review stage')
            item={k:row.get(k) for k in ('key','original','translation','stage')}
            item['context']=row.get('context') or ''
            k=item['key']
            if not isinstance(k,str) or k in allrows:raise Invalid('Duplicate remote key')
            allrows[k]=item;normalized.append(item)
        expected={e['key'] for e in entries if file_for(e)==name}
        if {x['key'] for x in normalized}!=expected:raise Invalid('Incomplete/wrong remote file: '+name)
        bykey={x['key']:x for x in normalized}
        result[name]=[bykey[e['key']] for e in entries if e['key'] in bykey]
    after=listing()
    for name in files:
        if any(before[name].get(k)!=after[name].get(k) for k in ('id','name','modifiedAt','updatedAt','total')):
            raise Invalid('Remote project changed while downloading; retry later')
    compile_catalog(data,allrows,'translated',allow_empty=True)
    return result

def main():
    p=argparse.ArgumentParser();p.add_argument('--config',default='paratranz-project.json');p.add_argument('--registry',default='baseline/registry.json');p.add_argument('--out',default='incoming');a=p.parse_args()
    token=os.getenv('PARATRANZ_TOKEN')
    if not token:raise Invalid('PARATRANZ_TOKEN environment variable is required')
    out=Path(a.out)
    if out.exists():raise Invalid('Snapshot destination already exists; choose a new directory')
    result=fetch(load(a.config),load(a.registry),lambda path:api(path,token))
    out.parent.mkdir(parents=True,exist_ok=True);temp=Path(tempfile.mkdtemp(prefix='.sotm2-snapshot-',dir=out.parent))
    try:
        for name,rows in result.items():write(temp/name,rows)
        os.replace(temp,out)
    finally:
        if temp.exists():shutil.rmtree(temp)
    print('PULL_OK '+str(sum(map(len,result.values())))+' rows; remote is read-only')

if __name__=='__main__':
    try:main()
    except (Invalid,OSError,ValueError,KeyError,TypeError) as e:
        print('FAILED: '+str(e),file=sys.stderr);sys.exit(1)
