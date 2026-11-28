"""Encode six localized small deck backs at their original atlas dimensions."""
import argparse, gzip, hashlib, io, json, struct
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SIZES={name+'DeckBack':(512,512) if name=='CitizenDawn' else (600,600) for name in ('BaronBlade','AkashBhuta','CitizenDawn','GrandWarlordVoss','Omnitron','TheMatriarch')}
def build(source,output):
    rows=json.loads((source/'manifest.json').read_text())['entries']
    if len(rows)!=6 or {r['sprite'] for r in rows}!=set(SIZES):
        raise ValueError('Exactly six base villain deck backs required')
    stream=io.BytesIO();stream.write(b'SOTMBCK1');stream.write(struct.pack('<i',6))
    for row in rows:
        path=source/row['file'];size=(row['width'],row['height'])
        if size!=SIZES[row['sprite']] or hashlib.sha256(path.read_bytes()).hexdigest()!=row['sourceSha256']:
            raise ValueError('Deck back source or dimensions differ from manifest')
        with Image.open(path) as image:
            if image.width!=image.height: raise ValueError('Square source required')
            image=image.convert('RGBA').resize(size,Image.Resampling.LANCZOS)
            raw=image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
        name=row['sprite'].encode('ascii');stream.write(struct.pack('<i',len(name)));stream.write(name)
        stream.write(struct.pack('<iii',*size,len(raw)));stream.write(raw)
    output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(gzip.compress(stream.getvalue(),mtime=0));return output
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',type=Path,default=ROOT/'art/villain-deckbacks');p.add_argument('--out',type=Path,default=ROOT/'build/villain-deckbacks.bytes');a=p.parse_args();print(build(a.source,a.out))
