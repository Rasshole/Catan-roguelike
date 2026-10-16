#!/usr/bin/env python3
"""Write Unity TextureImporter .meta for Resources/Art/Board PNGs (readable, sRGB)."""
from pathlib import Path
import secrets

ROOT = Path(__file__).resolve().parents[1] / "Assets/_Project"
BOARD = ROOT / "Resources/Art/Board"
ICONS = ROOT / "Resources/Art/Icons"
BOARDGAME = ROOT / "Resources/Art/Boardgame"

TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

DOC_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid() -> str:
    return secrets.token_hex(16)


def write_folder(path: Path) -> None:
    meta = path / ".meta"
    if meta.exists():
        return
    meta.write_text(FOLDER_META.format(guid=guid()), encoding="utf-8")
    print(f"folder meta {path}")


def write_png_metas(folder: Path) -> None:
    if not folder.is_dir():
        return
    for png in sorted(folder.glob("*.png")):
        meta_path = png.with_suffix(".png.meta")
        if meta_path.exists():
            text = meta_path.read_text(encoding="utf-8")
            if "isReadable: 1" not in text:
                text = text.replace("isReadable: 0", "isReadable: 1")
                meta_path.write_text(text, encoding="utf-8")
                print(f"patched {meta_path.name}")
            continue
        meta_path.write_text(TEXTURE_META.format(guid=guid()), encoding="utf-8")
        print(f"wrote {meta_path.name}")


def main() -> None:
    write_folder(ROOT / "Resources" / "Art")
    write_folder(BOARD)
    write_folder(ICONS)
    write_folder(BOARDGAME)
    write_folder(BOARDGAME / "Dice")
    write_folder(BOARDGAME / "Cards")
    write_folder(ROOT / "Resources" / "Art" / "Models")
    write_folder(ROOT / "Resources" / "Art" / "Models" / "Nature")
    art_doc = ROOT / "Art" / "ART_LICENSES.md"
    if not (art_doc.parent / "ART_LICENSES.md.meta").exists():
        (art_doc.parent / "ART_LICENSES.md.meta").write_text(DOC_META.format(guid=guid()), encoding="utf-8")
        print("ART_LICENSES.md.meta")

    write_png_metas(BOARD)
    write_png_metas(ICONS)
    write_png_metas(BOARDGAME / "Dice")
    write_png_metas(BOARDGAME / "Cards")


if __name__ == "__main__":
    main()
