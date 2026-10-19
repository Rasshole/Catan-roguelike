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
- Increased displacement, saturation, sun energy, and forest/wheat density.
- Closer on layout and color family but still stylized 3D vs printed board illustration.
  Keeping `ColonistBlenderHexTerrainArt.Enabled = false` until another art pass.
## Pass 6 (v120)
- Re-aligned reference_board.jpg crops via HSV match; lowered exposure/sun/fill and saturation.
- Toned forest/sheep greens; still not photo-close — `Enabled = false`.
## Pass 7 (v121)
- Geometric 3-4-5-4-3 crop centers on reference_board.jpg; ambientCG photo textures + scatter + posterize compositor.
- Still short of printed board; `ColonistBlenderHexTerrainArt.Enabled = false`.
