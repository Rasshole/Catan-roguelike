# V1 Prototype — Implementation Status

Last updated after gap-fill pass.

## Done (playable prototype scope)

- [x] Unity 2022.3 LTS project structure
- [x] 7-hex / 13-hex maps, all 5 resources
- [x] 3D placeholder board (table + hex cylinders)
- [x] Setup: AI 2 settlements + roads, then player 2 + roads
- [x] Day/night turn loop
- [x] Yield rolls (15/55/25, max 1×0 and 1×2, 50/50 tie-break)
- [x] Rolls at night, production uses today's rolls
- [x] Multi-hex production per settlement/city
- [x] Catan placement rules (distance, roads, connectivity)
- [x] Catan costs + threshold pricing
- [x] Daily shop (3 deals)
- [x] Cards: draw 1, play 1, max hand 5 — all 12 cards implemented
- [x] Road Builder (free road) + Master Builder (25% off next build)
- [x] Embargo + Harbor Charter
- [x] Robber (tile block + knight steal + day move)
- [x] Route sabotage (Bandit Raid) + disabled road visuals
- [x] Longest route VP (≥5 roads)
- [x] AI heuristic + hidden intents + weaker card pool
- [x] VP win at 10
- [x] Placeholder IMGUI (setup, build, cards, shop, robber)
- [x] EditMode tests (rolls, placement, production)

## Explicitly out of scope (per your choice)

- [ ] Meta progression
- [ ] Leaders
- [ ] Roguelike upgrades / draft uniques
- [ ] Save/load
- [ ] Act 2 / late-game 0–3 rolls
- [ ] Events (storm, famine)

## Still polish / future (not blocking logic)

- [x] Click-to-place on hex vertices and edges
- [x] Vertex-accurate 3D building/road positions
- [x] Shop: 3 daily trades + port bonus (2:1)
- [ ] Proper hex mesh + miniature art (bordspils-look)
- [ ] Real UI (uGUI / UI Toolkit) instead of IMGUI
- [ ] Committed Game.unity scene (use editor menu to generate)
- [ ] Longest-road algorithm edge cases on branched graphs
- [ ] AI uses shop + embargo in practice
- [ ] Integration / full-run automated tests
- [ ] Ports affecting shop rates (designed, not wired)
- [ ] Risky shop deals with robber trigger

## How to test when back at PC

1. Open in Unity 2022.3 LTS
2. **Catan Roguelike → Setup Game Scene**
3. Play — setup UI should appear after AI finishes
4. Test Runner → EditMode → Run All
