"""Author the flat HUD symbol separately from Blender's inventory renders.

The existing buckshot silhouette supplies the distressed outside edge. Interior
negative-space polygons depict cut sheet metal, not a lit shell or another animal.
The SVG is the editable vector output; PNG is the game's raster input.
"""
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

ART = Path(__file__).resolve().parents[1]
ICONS = ART.parent / 'Assets/Shotgun/Icons'
SIZE = (183, 436)
PIECES = [
    [(49, 120), (104, 105), (119, 132), (101, 152), (94, 180), (60, 169), (65, 146)],
    [(102, 195), (133, 186), (144, 236), (126, 263), (96, 247), (109, 226), (94, 210)],
    [(47, 236), (78, 224), (91, 250), (80, 273), (96, 294), (66, 315), (47, 294), (58, 268)],
]


def points_text(points):
    return ' '.join(f'{x},{y}' for x, y in points)


def main():
    baseline = Image.open(ICONS / 'ico_DoubleBarrelBuckshotHUD.png').convert('RGBA')
    assert baseline.size == SIZE
    alpha = baseline.getchannel('A')
    left, right = [], []
    for y in range(SIZE[1]):
        visible = [x for x in range(SIZE[0]) if alpha.getpixel((x, y)) >= 180]
        if visible:
            left.append((min(visible), y))
            right.append((max(visible), y))
    outline = left + right[::-1]
    svg = ET.Element('svg', xmlns='http://www.w3.org/2000/svg', width='183', height='436', viewBox='0 0 183 436')
    ET.SubElement(svg, 'title').text = 'Scrapshot: cut metal in a distressed shell silhouette'
    mask = ET.SubElement(ET.SubElement(svg, 'defs'), 'mask', id='shell', maskUnits='userSpaceOnUse', x='0', y='0', width='183', height='436')
    ET.SubElement(mask, 'polygon', points=points_text(outline), fill='white')
    for piece in PIECES:
        ET.SubElement(mask, 'polygon', points=points_text(piece), fill='black')
    ET.SubElement(svg, 'rect', width='183', height='436', fill='white', mask='url(#shell)')
    ET.indent(svg)
    (ART / 'source/ScrapshotHUD.svg').write_text(ET.tostring(svg, encoding='unicode') + '\n', encoding='utf-8')
    # Rasterize the same vector polygons with supersampling; no Blender dependency.
    scale = 4
    mask_image = Image.new('L', (SIZE[0] * scale, SIZE[1] * scale))
    draw = ImageDraw.Draw(mask_image)
    draw.polygon([(x * scale, y * scale) for x, y in outline], fill=255)
    for piece in PIECES:
        draw.polygon([(x * scale, y * scale) for x, y in piece], fill=0)
    mask_image = mask_image.resize(SIZE, Image.Resampling.LANCZOS)
    image = Image.new('RGBA', SIZE, (255, 255, 255, 0))
    image.putalpha(mask_image)
    # BC3 requires a multiple-of-four texture width. Keep the logical Sprite
    # at 183 px; this transparent column is excluded by its manifest rect.
    padded = Image.new('RGBA', (184, SIZE[1]), (0, 0, 0, 0))
    padded.paste(image, (0, 0))
    padded.save(ICONS / 'ico_DoubleBarrelScrapshotHUD.png')
    print('Rendered Scrapshot HUD: 184 x 436 texture / 183 x 436 Sprite, one transparent right column')


if __name__ == '__main__':
    main()
