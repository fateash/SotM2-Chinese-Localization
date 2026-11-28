"""Export the unmodified Simplified Chinese Bold face from a licensed Noto TTC.

Requires fonttools in the build environment. Retain OFL.txt in the output package.
"""
import argparse
from pathlib import Path
from fontTools.ttLib import TTCollection

def extract(collection, output):
    fonts = TTCollection(collection)
    matches = [f for f in fonts.fonts if f['name'].getDebugName(6) == 'NotoSansCJKsc-Bold']
    if len(matches) != 1 or matches[0]['OS/2'].usWeightClass != 700:
        raise ValueError('Expected exactly one Simplified Chinese Bold face, weight 700')
    output.parent.mkdir(parents=True, exist_ok=True)
    matches[0].save(output)
    fonts.close()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('collection', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    extract(args.collection, args.output)
