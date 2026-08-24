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

1. **Kort:** Vælg 7 / 13 / 19 hex i startmenuen
2. **Run start:** Vælg leader → draft 2 unique buildings
2. **Setup:** AI placerer 2 settlements + roads, derefter spilleren 2 settlements + roads
3. **Nat:** Rolls for næste dag vises (max 1×0 og 1×2 globalt, 50/50 tie-break)
4. **Nat:** Træk 1 kort, spil max 1 (hånd max 5); tilfældigt event (~22 %)
5. **Dag:** Produktion → shop → byg/sabotage → afslut dag → AI tur
6. **Level-up:** Hver 5. dag (max 3 gange) — vælg perk
7. **Win:** 10 VP (settlements, cities, longest route)

## Kortstørrelser

Vælg i **startmenuen** når spillet starter (eller sæt standard på **GameManager → Map Size**):

| Map Size | Hexes |
|----------|-------|
| Small | 7 |
| Medium | 13 (klassisk Catan-form uden yderhjørner) |
| Large | 19 (klassisk Catan) |

## Mappestruktur

```
Assets/_Project/Scripts/
  Core/          # Ren C# spil-logik (testbar uden Unity)
  Game/          # MonoBehaviours, 3D board, placeholder UI
Assets/Tests/    # EditMode unit tests
```

## Design-dokumentation

- `docs/IMPLEMENTATION_STATUS.md` — kort checkliste
- `docs/MISSING_AND_GAPS.md` — detaljeret liste over mangler / ikke-wired

## Tests

Unity → **Window → General → Test Runner** → EditMode → Run All

## Kendte begrænsninger (prototype)

- Placeholder 3D (cylindre/primitiver) — ikke endelig bordspils-look endnu
- IMGUI placeholder UI — ikke uGUI
- Ingen save/load eller meta progression mellem runs
- Se `docs/MISSING_AND_GAPS.md` for fuld liste over huller og halvt-implementeret logik

## Næste skridt (forslag)

- [ ] Fix bonus-VP refresh-bug
- [ ] Bedre 3D bordspils-look
- [ ] Generiske 3:1-porte + Bandit Raid vej-vælger i UI
- [ ] Integrationstests
