"""One-time exact migration of two previously visually accepted Baron Blade lines."""
import os
from catalog import Invalid,load,write
from sync_inventory import api,identity,grouped,validate_rows

BEFORE={
 'card.BaronBladeCharacter.body.003':'{BaronBlade}将被摧毁时，改为：{Flip}',
 'card.BaronBladeCharacter.body.004':'若反派弃牌堆中有15张或更多牌，{BaronBlade}的地月冲击光束便会启动，将月球拉向地球。<b>游戏结束。</b>',
}
AFTER={
 'card.BaronBladeCharacter.body.003':'{BaronBlade}被摧毁时改为{Flip}',
 'card.BaronBladeCharacter.body.004':'若反派弃牌堆有15张或更多牌，{BaronBlade}的地月冲击光束启动，将月球拉向地球。<b>游戏结束。</b>',
}

def corrections(rows):
 result=[]
 for key,old in BEFORE.items():
  row=rows[key]
  if row['translation'] not in (old,AFTER[key]):raise Invalid('Approved fix conflict: '+key)
  if row['translation']!=AFTER[key]:result.append(dict(key=key,translation=AFTER[key],stage=row['stage']))
 return result

def apply(config,data,request,checkpoint):
 groups=grouped(data);name='villians/BaronBlade.json';prefix='/projects/21585'
 files=identity(config,request(prefix+'/files'),groups)
 if files[name]['id']!=3595651:raise Invalid('Baron Blade file ID mismatch')
 endpoint=prefix+'/files/3595651/translation'
 before=validate_rows(groups[name],request(endpoint))
 changes=corrections(before);checkpoint('baron-before',before)
 # Recheck exact translations/stages before the narrow, two-key update.
 current=validate_rows(groups[name],request(endpoint))
 if current!=before:raise Invalid('Baron Blade file changed during preflight')
 if changes:request(endpoint,method='POST',payload=dict(filename='BaronBlade.json',items=changes,force=True))
 after=validate_rows(groups[name],request(endpoint))
 for key,row in before.items():
  expected=AFTER.get(key,row['translation'])
  if after[key]['translation']!=expected or after[key]['stage']!=row['stage'] or after[key]['context']!=row['context']:raise Invalid('Baron fix verification failed: '+key)
 checkpoint('baron-after',after)
 return len(changes)

if __name__=='__main__':
 token=os.getenv('PARATRANZ_TOKEN')
 if not token:raise Invalid('PARATRANZ_TOKEN required')
 count=apply(load('paratranz-inventory.json'),load('baseline/inventory.json'),lambda path,**kw:api(path,token,**kw),lambda name,value:write('incoming-inventory-checkpoints/'+name+'.json',value))
 print('APPROVED_BARON_FIX_OK changed='+str(count))
