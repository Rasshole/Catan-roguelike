# Render pipeline — beslutning

**Beslutning: bliv på Built-in Render Pipeline.** Ikke URP. Ikke HDRP.

Dato: 2026-08-26. Fase 0.5.

## Hvorfor Built-in

1. **Projektet kører allerede Built-in.** `Packages/manifest.json` har ingen `com.unity.render-pipelines.universal`. Halvvejs-tilstanden (kode der kalder URP-shadere med fallback til `Standard`) var den eneste uacceptable mulighed — pink materials når URP ikke er installeret, eller stille fallback der skjuler fejlen.
2. **VM med software-rendering.** Editoren kører headless/xvfb på Linux. URP er markant tungere uden GPU. Built-in `Standard` er rigeligt til et hex-bordspil med primitiver.
3. **Spillet er et hex-bræt.** Ingen realtids-belysning, post-processing eller shader-graph der retfærdiggør URP. Cylindre og kuber som placeholders; et senere art-pass kan stadig køre på Built-in.
4. **Unity 6 Built-in bruger stadig shadernavnet `Standard`.** Alle runtime/editor-materialer slår den op via `BuiltInMaterials` (`CatanRoguelike.Game`). Ingen URP-pakke tilføjes.

## Hvad der blev ændret

- `Shader.Find("Universal Render Pipeline/Lit")` fjernet fra `GameSceneSetup`, `BoardView` og `BoardInputController`.
- Fælles helper: `Assets/_Project/Scripts/Game/BuiltInMaterials.cs`.

## Hvordan man genovervejer senere

Skift kun hvis art-passet **reelt** kræver URP (Shader Graph, Volume-overrides, 2D-lights). Så er det en fuld migration, ikke en pakke-linje:

1. Tilføj URP i `Packages/manifest.json` og commit URP-asset + renderer.
2. Sæt `GraphicsSettings` / Quality til URP.
3. Erstat `BuiltInMaterials` med URP Lit (eller en ny helper) og konvertér alle materialer.
4. Opdatér denne fil og kør `tools/verify-fresh-clone.sh`.
5. Acceptér tungere Play Mode på VM'en.

Indtil da: **ingen URP-pakke, ingen URP-shader-navne i kode.**
