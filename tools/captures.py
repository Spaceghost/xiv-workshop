#!/usr/bin/env python3
"""Encode the software renderer's PPM captures as PNG without extra packages."""
from pathlib import Path
import struct
import zlib

def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

root = Path(__file__).resolve().parents[1] / "build/ui-test"
for ppm in root.glob("*.ppm"):
    with ppm.open("rb") as source:
        assert source.readline() == b"P6\n"
        width, height = map(int, source.readline().split())
        assert source.readline() == b"255\n"
        rgb = source.read()
    assert len(rgb) == width * height * 3
    rows = b"".join(b"\0" + rgb[i:i+width*3] for i in range(0, len(rgb), width*3))
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")
    ppm.with_suffix(".png").write_bytes(png)
    print(ppm.with_suffix(".png"))
