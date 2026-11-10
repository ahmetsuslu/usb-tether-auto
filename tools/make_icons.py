# -*- coding: utf-8 -*-
"""USB sembollu tepsi/uygulama ikonlarini uretir (Pillow ile)."""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), '..', 'src', 'UsbTetherAuto', 'Resources')
SIZES = [16, 20, 24, 32, 48, 64, 128, 256]
SS = 8  # supersample

STATES = {
    'trayOff':      (0x6B, 0x72, 0x80, 255),   # gri  - cihaz yok
    'trayConnected':(0x25, 0x63, 0xEB, 255),   # mavi - bagli, tethering kapali
    'trayTethered': (0x16, 0xA3, 0x4A, 255),   # yesil- tethering acik
    'trayWarn':     (0xD9, 0x77, 0x06, 255),   # amber - acik ama veri akmiyor
    'trayError':    (0xDC, 0x26, 0x26, 255),   # kirmizi - hata
    'usb':          (0x25, 0x63, 0xEB, 255),   # uygulama ikonu
}

FG = (255, 255, 255, 255)


def rounded_rect(d, box, r, fill):
    d.rounded_rectangle(box, radius=r, fill=fill)


def draw_usb(d, s, stroke):
    """USB uc-catal sembolu. s = kenar uzunlugu (supersample dahil)."""
    def P(x, y):
        return (x * s, y * s)

    w = stroke

    # dikey govde
    d.line([P(0.50, 0.26), P(0.50, 0.80)], fill=FG, width=w)

    # alt daire (dolu)
    r = 0.085 * s
    cx, cy = P(0.50, 0.815)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=FG)

    # ust ok (dolu ucgen)
    d.polygon([P(0.50, 0.10), P(0.385, 0.30), P(0.615, 0.30)], fill=FG)

    # sol dal -> dolu daire ucu
    d.line([P(0.50, 0.60), P(0.285, 0.60)], fill=FG, width=w)
    d.line([P(0.285, 0.60), P(0.285, 0.45)], fill=FG, width=w)
    r2 = 0.075 * s
    cx, cy = P(0.285, 0.405)
    d.ellipse([cx - r2, cy - r2, cx + r2, cy + r2], fill=FG)

    # sag dal -> dolu kare ucu
    d.line([P(0.50, 0.70), P(0.715, 0.70)], fill=FG, width=w)
    d.line([P(0.715, 0.70), P(0.715, 0.60)], fill=FG, width=w)
    h = 0.075 * s
    cx, cy = P(0.715, 0.525)
    d.rectangle([cx - h, cy - h, cx + h, cy + h], fill=FG)


def render(size, color):
    s = size * SS
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rounded_rect(d, [0, 0, s - 1, s - 1], int(s * 0.23), color)
    # kucuk boyutlarda cizgi kalinligini goreli olarak artir
    stroke = max(int(s * 0.062), 2)
    if size <= 20:
        stroke = max(int(s * 0.075), 2)
    draw_usb(d, s, stroke)
    return img.resize((size, size), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, color in STATES.items():
        frames = [render(sz, color) for sz in SIZES]
        path = os.path.join(OUT, name + '.ico')
        frames[-1].save(path, format='ICO',
                        sizes=[(f.width, f.height) for f in frames],
                        append_images=frames[:-1])
        print('yazildi:', os.path.relpath(path, os.path.dirname(__file__)))
    # gorsel kontrol icin onizleme seridi
    prev = Image.new('RGBA', (len(STATES) * 72, 72 + 40), (250, 250, 250, 255))
    dark = Image.new('RGBA', (len(STATES) * 72, 40), (32, 32, 32, 255))
    prev.paste(dark, (0, 72))
    for i, (name, color) in enumerate(STATES.items()):
        prev.paste(render(64, color), (i * 72 + 4, 4), render(64, color))
        small = render(20, color)
        prev.paste(small, (i * 72 + 26, 82), small)
    prev.save(os.path.join(os.path.dirname(__file__), 'icon-preview.png'))
    print('onizleme: tools/icon-preview.png')


if __name__ == '__main__':
    main()
