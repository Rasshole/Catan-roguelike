# Catan Roguelike (Unity)

Catan-inspireret roguelike prototype med dag/nat-cyklus, yield-rolls, shop, udviklingskort og 1 AI-modstander.

## Krav

- **Unity 2022.3 LTS** (eller nyere LTS — ret `ProjectSettings/ProjectVersion.txt` hvis din version afviger)
- Windows PC target

## Kom i gang

1. Åbn **Unity Hub** → **Add** → vælg denne mappe (`Catan-roguelike`)
2. Unity importerer projektet (første gang kan tage et par minutter)
3. Kør menuen **Catan Roguelike → Setup Game Scene** (opretter `Assets/_Project/Scenes/Game.unity` med kamera, bord og GameManager)
4. Åbn `Game`-scenen og tryk **Play**

## Kontroller (placeholder)

- **Q / E** — roter kamera omkring brættet
- **IMGUI-panel** (venstre) — ressourcer, rolls, kort, shop, byg-knapper

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
