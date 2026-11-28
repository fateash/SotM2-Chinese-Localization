"""Production artwork encoding and integration package boundary checks."""
import gzip
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
import build_villain_logos as pack

class VillainArtTests(unittest.TestCase):
    def test_six_approved_sources_encode_with_unity_orientation_and_padding(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = pack.build(ROOT/'art/villain-logos', Path(tmp)/'logos.bytes')
            data = output.read_bytes()
            self.assertEqual(data, pack.build(ROOT/'art/villain-logos', output).read_bytes())
            stream = io.BytesIO(gzip.decompress(data))
            self.assertEqual(stream.read(8), b'SOTMLOG1')
            integer = lambda: struct.unpack('<i', stream.read(4))[0]
            self.assertEqual(integer(), 6)
            expected = json.loads((ROOT/'art/villain-logos/manifest.json').read_text())['entries']
            for row in expected:
                self.assertEqual(stream.read(integer()).decode('ascii'), row['sprite'])
                width, height, length = integer(), integer(), integer()
                self.assertEqual((width, height, length), (row['width'], row['height'], row['width']*row['height']*4))
                image = Image.frombytes('RGBA', (width,height), stream.read(length)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                bbox = image.getchannel('A').getbbox()
                self.assertIsNotNone(bbox)
                self.assertGreaterEqual(bbox[0], 3)
                self.assertGreaterEqual(bbox[1], 3)
                self.assertLessEqual(bbox[2], width-3)
                self.assertLessEqual(bbox[3], height-3)
                self.assertEqual(image.getpixel((0,0))[3],0)
            self.assertEqual(stream.read(), b'')

    def test_changed_art_is_not_silently_built(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            (root/'image.png').write_bytes(b'changed')
            (root/'manifest.json').write_text(json.dumps({'entries':[{'file':'image.png','sourceSha256':'0'*64}]}))
            with self.assertRaisesRegex(ValueError,'reviewed source'):
                pack.build(root,root/'out.bytes')

    def test_raw_encoding_preserves_top_bottom_and_alpha(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            image=Image.new('RGBA',(2,2))
            image.putdata([(255,0,0,255),(255,0,0,255),(0,0,255,255),(0,0,0,0)])
            image.save(root/'image.png')
            row=dict(file='image.png',sourceSha256=hashlib.sha256((root/'image.png').read_bytes()).hexdigest(),sprite='test',width=10,height=10)
            (root/'manifest.json').write_text(json.dumps({'entries':[row]}))
            data=gzip.decompress(pack.build(root,root/'out.bytes').read_bytes())
            raw=data[12+4+4+12:]
            # In Unity bottom-up raw bytes the bottom blue pixel precedes the top red.
            offset=lambda x,y:4*(y*10+x)
            self.assertEqual(raw[offset(4,4):offset(4,4)+4],bytes([0,0,255,255]))
            self.assertEqual(raw[offset(4,5):offset(4,5)+4],bytes([255,0,0,255]))
