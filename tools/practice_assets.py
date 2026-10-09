"""Create disposable, original practice assets using only the Python standard library."""
from __future__ import annotations

import argparse
import math
from pathlib import Path
import struct
import wave
import zlib


def png_bytes() -> bytes:
    width, height = 640, 480
    pixels = bytearray()
    for y in range(height):
        pixels.append(0)
        for x in range(width):
            inside = (x - 320) ** 2 + (y - 240) ** 2 < 150 ** 2
            pixels.extend((36, 87, 167, 255 if inside else 0))

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))

    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(pixels)) + chunk(b'IEND', b'')


def pdf_bytes(label: str) -> bytes:
    stream = f'BT /F1 28 Tf 72 720 Td ({label}) Tj ET'.encode('ascii')
    objects = [
        b'<< /Type /Catalog /Pages 2 0 R >>',
        b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
        b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
        b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
        f'<< /Length {len(stream)} >>\nstream\n'.encode() + stream + b'\nendstream',
    ]
    result = bytearray(b'%PDF-1.4\n')
    offsets = [0]
    for number, obj in enumerate(objects, 1):
        offsets.append(len(result))
        result.extend(f'{number} 0 obj\n'.encode() + obj + b'\nendobj\n')
    start = len(result)
    result.extend(f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode())
    for offset in offsets[1:]:
        result.extend(f'{offset:010d} 00000 n \n'.encode())
    result.extend(f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n'.encode())
    return bytes(result)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path('build/practice-assets'))
    output = parser.parse_args().output
    # Refuse to overwrite a practice session or user file.
    output.mkdir(parents=True, exist_ok=False)
    (output / 'practice.png').write_bytes(png_bytes())
    (output / 'practice.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" width="640" height="480"><circle cx="320" cy="240" r="150" fill="#2457A7"/></svg>\n', encoding='utf-8')
    for suffix in ('a', 'b'):
        (output / f'practice-{suffix}.pdf').write_bytes(pdf_bytes('Practice page ' + suffix.upper()))
    (output / 'practice.pdf').write_bytes(pdf_bytes('Practice document'))
    (output / 'notes.txt').write_text('Practice notes. No personal information.\n', encoding='utf-8')
    (output / 'practice.csv').write_text('ID,Item,Units,Price\n001,A,2,10\n002,B,3,20\n003,C,4,15\n', encoding='utf-8')
    with wave.open(str(output / 'practice.wav'), 'wb') as audio:
        audio.setparams((1, 2, 44100, 0, 'NONE', 'not compressed'))
        # Five short quiet tones, suitable for timing exercises, not speech recognition.
        samples = (int(3000 * math.sin(2 * math.pi * 440 * i / 44100)) if i % 44100 < 8820 else 0 for i in range(5 * 44100))
        audio.writeframes(b''.join(struct.pack('<h', value) for value in samples))
    print(f'Created 8 synthetic assets in {output.resolve()}. These are fixtures, not test results.')


if __name__ == '__main__':
    main()
