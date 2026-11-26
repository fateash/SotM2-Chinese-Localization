"""Read-only ParaTranz file snapshot. Credentials from environment, never serialized."""
import argparse,json,os,sys,tempfile,shutil
from pathlib import Path
from urllib.request import Request,build_opener,HTTPRedirectHandler
from urllib.error import HTTPError,URLError
from catalog import load,write,registry,compile_catalog,Invalid

class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): raise Invalid('Unexpected API redirect; token not forwarded')

def api(path,token):
    request=Request('https://paratranz.cn/api'+path,headers={'Authorization':'Bearer '+token,'Accept':'application/json','User-Agent':'SotM2-Localization-Pipeline/0.1.0'})
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
    if set(files)!={'ui.json','cards-body.json'} or any(type(v) is not int or v<=0 for v in files.values()) or len(set(files.values()))!=2:
        raise Invalid('Set distinct positive file IDs for ui.json and cards-body.json')
    entries=registry(data); prefix=f'/projects/{project}'
    def listing():
        items=request(prefix+'/files')
        if not isinstance(items,list):raise Invalid('Expected files array')
        selected={}
        for name,fid in files.items():
            found=[f for f in items if f.get('id')==fid]
            if len(found)!=1 or found[0].get('name')!=name:raise Invalid('Remote file identity mismatch: '+name)
            selected[name]=found[0]
        return selected
    before=listing();result={};allrows={}
    for name,fid in files.items():
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
        expected={e['key'] for e in entries if (e['kind']=='ui.label')==(name=='ui.json')}
        if {x['key'] for x in normalized}!=expected:raise Invalid('Incomplete/wrong remote file: '+name)
        bykey={x['key']:x for x in normalized}
        result[name]=[bykey[e['key']] for e in entries if e['key'] in bykey]
    after=listing()
    for name in files:
        if any(before[name].get(k)!=after[name].get(k) for k in ('modifiedAt','updatedAt','total')):
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
