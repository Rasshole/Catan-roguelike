# Catan Roguelike (Unity)

Catan-inspireret roguelike prototype med dag/nat-cyklus, yield-rolls, shop, udviklingskort og 1 AI-modstander.

## Krav

- **Unity 6.3 LTS** (`6000.3.15f1`)
- **Udvikling og playtest:** macOS, Unity Editor
- **Primært build-mål:** Windows standalone (bygges fra Editor; ingen færdig macOS-player i repo)

## Sådan starter du

1. Pull repoet
2. Åbn mappen i **Unity Hub** med **Unity 6.3 LTS (6000.3.15f1)**
3. Åbn `Assets/_Project/Scenes/Game.unity` og tryk **Play**

**Hvis scenen mangler:** kør **Catan Roguelike → Setup Game Scene** én gang (opretter `Game.unity` lokalt). Scenen ligger normalt allerede i git.

**Kontroller:**
- **Klik på brættet** — grøn markør = settlement (hjørne), gul markør = vej (kant)
- **Q / E** — roter kamera
- **IMGUI-panel** (venstre) — ressourcer, rolls, kort, shop, build-mode

## Spilflow (v1 prototype)

1. **Kort:** Vælg 7 / 13 / 19 hex i startmenuen
2. **Run start:** Vælg leader → draft 2 unique buildings
3. **Setup:** AI placerer 2 settlements + roads, derefter spilleren 2 settlements + roads
4. **Nat:** Rolls for næste dag vises (max 1×0 og 1×2 globalt, 50/50 tie-break)
5. **Nat:** Træk 1 kort, spil max 1 (hånd max 5); tilfældigt event (~22 %)
6. **Dag:** Produktion → shop → byg/sabotage → afslut dag → AI tur
7. **Level-up:** Hver 5. dag (max 3 gange) — vælg perk
8. **Win:** 10 VP — settlements, cities, longest route (≥5 veje), bonus-VP (fx Harbor Charter, LongRoadBonus-perk)

Under spillet viser IMGUI løbende VP-breakdown. Ved game over: run-summary med seed, dag, kort, leader og VP-fordeling + **Restart**.

**Shop & porte:** Daglig shop (3 handler; 3. er ofte risky 2:1 med robber-konsekvens i knaptekst). Porte 2:1 (ressource) og 3:1 (generisk) påvirker priser; årsag vises ved hver handel.

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
Assets/Tests/
  EditMode/      # Core unit + integration tests
  PlayMode/      # Scene boot smoke tests (Game.unity wiring)
```

## Design-dokumentation

- `docs/IMPLEMENTATION_STATUS.md` — kort checkliste
- `docs/MISSING_AND_GAPS.md` — detaljeret liste over mangler / ikke-wired

## Tests

Unity → **Window → General → Test Runner** → EditMode → Run All

**PlayMode (scene smoke):** Test Runner → PlayMode → Run All. På headless Linux kræver PlayMode grafik — brug `xvfb-run`:

```bash
# Unity CLI (anbefalet på VM/CI)
xvfb-run -a unity test . --mode PlayMode --output /tmp/playmode-results.xml --timeout 300

# Eller Editor direkte
xvfb-run -a Unity -runTests -batchmode -projectPath . -testPlatform PlayMode \
  -testResults playmode-results.xml -logFile -
```

EditMode uden grafik:

```bash
unity test . --mode EditMode --output /tmp/editmode-results.xml --timeout 300
```

Se også `docs/TOOLING.md`.

## Kendte begrænsninger (prototype)

- Placeholder 3D (cylindre/primitiver) — ikke endelig bordspils-look
- IMGUI placeholder UI — ikke uGUI
- Events kun som tekstlinje — ingen visuel effekt på brættet
- Ingen save/load eller meta progression mellem runs
- PlayMode scene-boot smoke findes (`GameSceneSmokeTests`); fuld IMGUI/UI-interaktion mangler stadig
- Se `docs/MISSING_AND_GAPS.md` for fuld liste

## Næste skridt (forslag)

- [ ] Rig UI (uGUI) + art pass
- [ ] Visuelle events på brættet
- [x] PlayMode-tests (scene boot smoke)
