# Blender hex tile iteration (v117)

## Pass 1 (baseline Cycles)
- Honest critique: sand bevel was broken (opaque black rim); wheat cubes cast a black shadow blob;
  ore/clay read as floating bubbles; saturation too low vs photo. Side-by-side crops were misaligned.

## Pass 2
- Fixed hex mask/bevel (true sand-yellow rim), striped shader wheat parcels, grid forest canopy,
  displaced ore/clay/desert with procedural materials, better reference crops.
- Still short of photo: mountains need sharper ridges; forest needs more yellow-orange pops;
  clay pits still smooth; overall more painterly hand-tuned contrast needed.

## Pass 3
- Increased displacement, saturation, sun energy, and forest/wheat density; contrast tuning in post.
- Closer on layout and color family but still stylized 3D vs printed board illustration.
- **`ColonistBlenderHexTerrainArt.Enabled` stays `false`** until another art pass matches the photo.
