import gzip
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import build_villain_deckbacks as pack

class DeckBackArtTests(unittest.TestCase):
    def test_original_dimensions_orientation_and_opaque_edges(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'backs.bytes'
            data=pack.build(ROOT/'art/villain-deckbacks',path).read_bytes()
            self.assertEqual(data,pack.build(ROOT/'art/villain-deckbacks',path).read_bytes())
            s=io.BytesIO(gzip.decompress(data));self.assertEqual(s.read(8),b'SOTMBCK1')
            integer=lambda:struct.unpack('<i',s.read(4))[0]
            self.assertEqual(integer(),6)
            rows=json.loads((ROOT/'art/villain-deckbacks/manifest.json').read_text())['entries']
            for row in rows:
                self.assertEqual(s.read(integer()).decode('ascii'),row['sprite'])
                w,h,n=integer(),integer(),integer()
                self.assertEqual((w,h),pack.SIZES[row['sprite']]);self.assertEqual(n,w*h*4)
                image=Image.frombytes('RGBA',(w,h),s.read(n)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                with Image.open(ROOT/'art/villain-deckbacks'/row['file']) as source:
                    expected=source.convert('RGBA').resize((w,h),Image.Resampling.LANCZOS)
                    self.assertEqual(image.tobytes(),expected.tobytes())
                self.assertEqual(image.getchannel('A').getextrema(),(255,255))
            self.assertEqual(s.read(),b'')

    def test_duplicate_or_nonbase_assets_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)
            (p/'manifest.json').write_text(json.dumps({'entries':[{'sprite':'OmnitronDeckBack'}]*6}))
            with self.assertRaisesRegex(ValueError,'Exactly six'):
                pack.build(p,p/'bad.bytes')
