# C logo

"C" for CodeIsNotEvil, in Kode Mono Bold and the orange `#DE5833` shared by the repo's logos.
`C-logo.svg` is the source; every other file here is rendered from it, each size straight from the
vector rather than scaled down from a bigger file.

| Files | What for |
|---|---|
| `C-logo.svg` | Anywhere that takes SVG. Transparent background. |
| `C-logo-{1024,512,256,128,64,32}.png` | Sites that don't take SVG. Transparent background. |
| `C-logo-glint-{1024,512,256,128,64,32}.gif` | Animated profile pictures (GitHub and others): a glint sweeps across the logo every three seconds. The background is solid `#181614`, because GIF transparency is on/off per pixel and leaves jagged edges in a round crop. |

Regenerate them after changing the SVG (run from the repo root):

```sh
# the SVG itself: Scripts/text_logo.py C Assets/c-logo/C-logo.svg
for n in 1024 512 256 128 64 32; do
  rsvg-convert -w $n -h $n Assets/c-logo/C-logo.svg -o Assets/c-logo/C-logo-$n.png
done
Scripts/glint_gif.py Assets/c-logo/C-logo.svg 'Assets/c-logo/C-logo-glint-{size}.gif'
```
