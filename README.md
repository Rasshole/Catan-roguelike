# Catan Roguelike (Unity)

Catan-inspireret roguelike prototype med dag/nat-cyklus, yield-rolls, shop, udviklingskort og 1 AI-modstander.

## Krav

- **Unity 2022.3 LTS** (eller nyere LTS — ret `ProjectSettings/ProjectVersion.txt` hvis din version afviger)
- Windows PC target

## Sådan starter du (på din PC)

1. Pull repoet
2. Åbn mappen i **Unity Hub** (2022.3 LTS eller din LTS-version)
3. Kør menuen **Catan Roguelike → Setup Game Scene** — dette opretter `Game.unity` (se forklaring nedenfor)
4. Åbn `Assets/_Project/Scenes/Game.unity` og tryk **Play**

### Hvad betyder "Setup Game Scene"?

Unity-spil består af en **scene-fil** (`.unity`) med kamera, lys, GameManager osv. Den scene er **ikke gemt i git** endnu — den genereres af et editor-script første gang du åbner projektet. Du skal bare køre menu-punktet én gang; derefter ligger scenen på din computer og du kan åbne den direkte.

**Kontroller:**
- **Klik på brættet** — grøn markør = settlement (hjørne), gul markør = vej (kant)
- **Q / E** — roter kamera
- **IMGUI-panel** (venstre) — ressourcer, rolls, kort, shop, build-mode

## Spilflow (v1 prototype)

1. **Setup:** AI placerer 2 settlements + roads, derefter spilleren 2 settlements + roads
2. **Nat:** Rolls for næste dag vises (max 1×0 og 1×2 globalt, 50/50 tie-break)
3. **Nat:** Træk 1 kort, spil max 1 (hånd max 5)
4. **Dag:** Produktion → shop → byg/sabotage → afslut dag → AI tur
5. **Win:** 10 VP (settlements, cities, longest route)

## Mappestruktur

```
Assets/_Project/Scripts/
  Core/          # Ren C# spil-logik (testbar uden Unity)
  Game/          # MonoBehaviours, 3D board, placeholder UI
Assets/Tests/    # EditMode unit tests
```

## Design-dokumentation

Se plan-filen for fuld design-spec og faser.

## Tests

Unity → **Window → General → Test Runner** → EditMode → Run All

## Kendte begrænsninger (prototype)

- Placeholder 3D (cylindre/primitiver) — ikke endelig bordspils-look endnu
- Bygning via "auto valid spot" knapper — hex-klik kommer senere
- Ingen save/load
- Ingen meta progression / leaders

## Næste skridt

- [ ] Bekræft Unity-version
- [ ] Klik-placering på hex-hjørner
- [ ] Bedre 3D bordspils-look (bord, hex-kanter, miniature-bygninger)
- [ ] Robber + kort-targeting i UI
