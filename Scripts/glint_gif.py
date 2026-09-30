#!/usr/bin/env python3
"""Render a square SVG logo as an animated GIF with a glint sweeping across it.

  Scripts/glint_gif.py Assets/C-logo.svg Assets/C-logo-glint.gif

Made for the GitHub profile picture. GitHub takes a PNG, JPG or GIF under 1 MB and smaller than
3000 x 3000 pixels, so the default is 2999 px and the script fails if the file comes out at 1 MB or
more. The logo is rendered from the SVG at full size (the SVG is always the source, see
text_logo.py), on a solid background: GIF transparency is on/off per pixel, which would leave jagged
edges once GitHub crops the picture to a circle.

The first frame is the plain logo and is held for a while, so anything that shows only the first frame
of a GIF still gets a clean picture. Then the glint crosses the logo once and the loop starts over.
Only the logo's own pixels light up; the background never changes.

Needs rsvg-convert (librsvg), ImageMagick 7 (magick), Pillow and NumPy.
"""
import argparse
import subprocess
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

LIMIT_BYTES = 1_000_000


def hex_rgb(value: str) -> np.ndarray:
  value = value.lstrip("#")
  return np.array([int(value[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def render_alpha(svg: Path, size: int, padding: float) -> np.ndarray:
  """The logo's coverage (0..1) at size x size, shrunk by padding so the circle crop keeps it whole."""
  inner = round(size * (1 - 2 * padding))
  with tempfile.TemporaryDirectory() as work:
    png = Path(work) / "logo.png"
    subprocess.run(["rsvg-convert", "-w", str(inner), "-h", str(inner), str(svg), "-o", str(png)], check=True)
    logo = np.asarray(Image.open(png).convert("RGBA"), dtype=np.float32)[..., 3] / 255
  alpha = np.zeros((size, size), dtype=np.float32)
  offset = (size - inner) // 2
  alpha[offset:offset + inner, offset:offset + inner] = logo
  return alpha


def main() -> None:
  parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
  parser.add_argument("svg", type=Path)
  parser.add_argument("output", type=Path)
  parser.add_argument("--size", type=int, default=2999, help="width and height; GitHub needs < 3000")
  parser.add_argument("--color", default="#DE5833", help="the logo colour, as in the SVG")
  parser.add_argument("--background", default="#181614", help="the site's dark background")
  parser.add_argument("--padding", type=float, default=0.06, help="margin on each side, as a fraction of the size")
  parser.add_argument("--frames", type=int, default=20, help="frames for one sweep of the glint")
  parser.add_argument("--frame-ms", type=int, default=40)
  parser.add_argument("--hold-ms", type=int, default=3000, help="how long the plain logo shows between glints")
  parser.add_argument("--width", type=float, default=0.14, help="half-width of the glint band, in diagonal units (0..1)")
  parser.add_argument("--strength", type=float, default=0.85, help="how close to white the glint's centre gets")
  parser.add_argument("--levels", type=int, default=24,
                      help="brightness steps in the glint; flat steps compress far better than a smooth ramp")
  args = parser.parse_args()

  size = args.size
  alpha = render_alpha(args.svg, size, args.padding)[..., None]
  background, color = hex_rgb(args.background), hex_rgb(args.color)
  base = background * (1 - alpha) + color * alpha

  # Position along the diagonal from top-left (0) to bottom-right (1). The band starts and ends fully
  # outside the logo, so the sweep begins and finishes on the plain logo.
  ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
  diagonal = ((xs + ys) / (2 * (size - 1)))[..., None]
  del xs, ys
  rows, cols = np.nonzero(alpha[..., 0] > 0)
  start = (rows.min() + cols.min()) / (2 * (size - 1)) - args.width
  end = (rows.max() + cols.max()) / (2 * (size - 1)) + args.width

  frames = [base]
  for step in range(1, args.frames + 1):
    centre = start + (end - start) * step / (args.frames + 1)
    glint = np.clip(1 - np.abs(diagonal - centre) / args.width, 0, 1) ** 2
    # At 2999 px each step is a wide flat stripe, which GIF's LZW packs well. GitHub shows the picture
    # at 460 px at most, where the steps blend back into a smooth shine.
    glint = np.round(glint * args.levels) / args.levels * args.strength * alpha
    frames.append(base + (255 - base) * glint)

  images = [Image.fromarray(np.rint(f).astype(np.uint8), "RGB") for f in frames]
  # One palette for every frame, taken from all of them, so colours don't flicker between frames.
  sample = Image.new("RGB", (512, 512 * len(images)))
  for index, image in enumerate(images):
    sample.paste(image.resize((512, 512), Image.Resampling.LANCZOS), (0, 512 * index))
  palette = sample.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)

  with tempfile.TemporaryDirectory() as work:
    raw = Path(work) / "raw.gif"
    indexed = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    durations = [args.hold_ms] + [args.frame_ms] * args.frames
    indexed[0].save(raw, save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)
    # Unchanged pixels become transparent in every frame after the first, so each frame only stores
    # the glint band. Without this the 2999 px frames don't fit into 1 MB.
    subprocess.run(["magick", str(raw), "-layers", "OptimizeTransparency", "+remap", str(args.output)], check=True)

  bytes_written = args.output.stat().st_size
  print(f"Wrote {args.output}: {size}x{size}, {len(images)} frames, {bytes_written / 1000:.0f} kB")
  if bytes_written >= LIMIT_BYTES:
    raise SystemExit(f"{args.output} is {bytes_written} bytes; GitHub needs less than {LIMIT_BYTES}. Try fewer --frames.")


if __name__ == "__main__":
  main()
