"""Encode approved RGBA artwork for Unity; no artistic redraw or nonuniform scaling."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import struct
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]

def build(source, output):
    manifest = json.loads((source / 'manifest.json').read_text())
    entries = manifest['entries']
    stream = io.BytesIO()
    stream.write(b'SOTMLOG1')
    stream.write(struct.pack('<i', len(entries)))
    for row in entries:
        path = source / row['file']
        if hashlib.sha256(path.read_bytes()).hexdigest() != row['sourceSha256']:
            raise ValueError('Artwork differs from reviewed source: ' + row['file'])
        with Image.open(path) as image:
            if image.mode != 'RGBA' or image.getchannel('A').getextrema()[0] != 0:
                raise ValueError('Transparent RGBA artwork required')
            # Generated PNGs may contain nearly invisible alpha=1 specks far
            # outside the artwork. Use visible support for layout, retaining
            # a small fringe for antialiasing rather than fitting to those specks.
            bbox = image.getchannel('A').point(lambda value: 255 if value >= 8 else 0).getbbox()
            if not bbox:
                raise ValueError('Empty logo')
            bbox = (max(0,bbox[0]-2),max(0,bbox[1]-2),min(image.width,bbox[2]+2),min(image.height,bbox[3]+2))
            artwork = image.crop(bbox)
            width, height = row['width'], row['height']
            optical_scale = row.get('opticalScale', 1.0)
            if not 0.5 <= optical_scale <= 1.0:
                raise ValueError('Invalid optical scale')
            scale = min((width - 8) / artwork.width, (height - 8) / artwork.height) * optical_scale
            artwork = artwork.resize((max(1, round(artwork.width * scale)), max(1, round(artwork.height * scale))), Image.Resampling.LANCZOS)
            canvas = Image.new('RGBA', (width, height))
            canvas.paste(artwork, ((width-artwork.width)//2, (height-artwork.height)//2))
            # Unity raw textures start at the bottom-left, PNG at the top-left.
            raw = canvas.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
        name = row['sprite'].encode('ascii')
        stream.write(struct.pack('<i', len(name)))
        stream.write(name)
        stream.write(struct.pack('<iii', width, height, len(raw)))
        stream.write(raw)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(gzip.compress(stream.getvalue(), mtime=0))
    return output

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, default=ROOT/'art/villain-logos')
    parser.add_argument('--out', type=Path, default=ROOT/'build/villain-logos.bytes')
    args = parser.parse_args()
    print(build(args.source, args.out))
