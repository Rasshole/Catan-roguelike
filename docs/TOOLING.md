# Værktøjer

## `tools/verify-fresh-clone.sh`

Frisk-klon-test: kloner origin til en temp-mappe, åbner projektet i Unity batchmode (compile/missing-script check), kører EditMode-tests via **Unity CLI** `unity test`, og sletter mappen bagefter (også ved fejl).

Kør efter hver push til `main`. Det er den eneste garanti for at brugerens `git pull` → åbn → Play kan virke, når `Game.unity` er committed (Fase 0.7).

### Brug

```bash
# fra repo-roden; tjekker HEAD som den ser ud på origin-URL'en
./tools/verify-fresh-clone.sh

# valgfri ref (commit, branch, tag)
./tools/verify-fresh-clone.sh origin/main
```

Miljøvariabler:

| Variabel | Default | Betydning |
|----------|---------|-----------|
| `UNITY_EDITOR` | `/home/box/Unity/Hub/Editor/6000.3.15f1/Editor/Unity` | Unity Editor-binær (batchmode open + fallback tests) |
| `UNITY_CLI` | (auto) | Unity CLI (`unity test`). Resolves `~/.local/bin/unity`, then `PATH`. |
| `UNITY_TIMEOUT` | `180` | sekunder for batchmode Editor-open; processen kills så scriptet aldrig hænger |
| `UNITY_TEST_TIMEOUT` | `300` | sekunder for `unity test --timeout` (EditMode) |
| `DISPLAY` | (tom) | hvis unset, køres Unity via `xvfb-run -a` |

### EditMode-tests (Unity CLI)

Primær kommando (fra klon-mappen):

```bash
unity test . --mode EditMode --output /tmp/editmode-results.xml --timeout 300
```

På VM'en: Unity CLI **1.0.0-beta.6** ved `~/.local/bin/unity`, Editor **6000.3.15f1**.

**Fallback:** Hvis `unity` ikke findes, scriptet falder tilbage til Editor `-batchmode -runTests -testPlatform EditMode`. Det kan **ikke** emitte XML på dette projekt — installér CLI i stedet:

```bash
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

`unity test` exit-koder: `0` = pass, `6` = test-fejl (XML læses alligevel), andre = tooling-fejl.

Origin-URL tages fra `git remote get-url origin` i den mappe scriptet ligger i.

### Exit-koder

| Kode | Betydning |
|------|-----------|
| 0 | PASS |
| 1 | FAIL — compile-fejl, missing scripts, Unity-fejl, test-fejl, timeout |
| 2 | **Licens mangler.** Loggen indeholder `No valid Unity Editor license`. Ingen hang. Se `docs/BLOCKED.md`. |

### Game.unity (Fase 0.7)

- Filen **er der:** FAIL hvis den ikke kan loades.
- Filen **mangler endnu:** **WARN**, ikke FAIL. 0.7 er ikke landet. Når scenen committes, skifter samme check til FAIL ved fravær.

Unity-open FAIL'er altid hvis Editoren selv fejler (compile, missing scripts, non-zero uden licens-besked).

### Krav

- `git`, `timeout`, Unity Editor **6000.3.15f1**, Unity CLI **1.0.0-beta.6+** (`unity test`)
- `xvfb-run` når der ingen `DISPLAY` er (batchmode open)
- Gyldig Unity-licens på maskinen (ellers exit 2)

Temp-mappen ryddes via `trap` ved både PASS, FAIL og licens-fejl.

## `tools/core-tests` (EditMode uden Unity)

NUnit-projekt der glob'er `CatanRoguelike.Core` + `Assets/Tests/EditMode`. Bruges når Unity-licens mangler.

```bash
export DOTNET_ROOT=/home/box/.dotnet PATH=/home/box/.dotnet:$PATH
dotnet test tools/core-tests/CatanRoguelike.Core.Tests.csproj
```

## `tools/sim-runner` (headless Core-sim)

net8.0 console-app der kompilerer Core via glob. Spiller N runs med seeds 1..N. **Hænger ikke:** hvert run har wall-clock timeout (default 5s) *og* max-steps/max-days. Exceptions fanges. Aldrig `Console.Read*`. Printer altid én JSON-linje plus en tabel, også hvis alle runs timer ud.

### Brug

```bash
export DOTNET_ROOT=/home/box/.dotnet PATH=/home/box/.dotnet:$PATH

# fra repo-roden
dotnet run --project tools/sim-runner -- --runs 5

# eller fra tools/sim-runner
cd tools/sim-runner
dotnet run -- --runs 5
```

| Flag | Default | Betydning |
|------|---------|-----------|
| `--runs N` | 1 | antal spil, seeds `seed-start` .. `+N-1` |
| `--seed-start N` | 1 | første seed |
| `--timeout-ms N` | 5000 | per-run wall-clock; overskridelse → `timeout`, næste seed |
| `--max-steps N` | 300 | per-run action-cap → `max_steps` |
| `--max-days N` | 12 | stop når `DayNumber` overstiger N → `max_days` |
| `--map small\|medium\|large` | small | kortstørrelse |

### Driver: `full`

`VertexGraph.Canonicalize` er idempotent, så `VertexDistance` / `PlaceSettlement` / AI `GetValidSettlementSpots` terminerer med buildings på brættet. Default-driveren spiller den rigtige Core-loop:

1. `SelectMap` → leader → draft → `ConfirmRunSetup` (AI setup-step)
2. Spiller-setup via `GetValidSettlementSpots` / `PlaceSettlement` / `PlaceRoad`
3. `SkipNightCard` (produktion + shop). `TodayRolls` kopieres først hvis tom, så AI night-plan ikke crasher
4. Dag: shop + evt. human build, derefter `EndPlayerDay` (AI `ExecuteDayTurn` + placement)
5. Stop ved GameOver, max-days, max-steps eller timeout

Timeout-guards er uændrede. Aldrig blokér på Unity-licens.

## Editor debug-hooks (`Catan Roguelike/Debug`)

Editor-only (`Assets/_Project/Scripts/Editor/DebugHooks.cs`, `#if UNITY_EDITOR`, asmdef `includePlatforms: Editor`). Kommer **ikke** med i player-builds.

Kræver Play Mode (undtagen selve panelet). **Ikke Unity-verificeret endnu** — VM'en har ingen Editor-licens.

### Menu og genveje

| Kommando | Genvej |
|----------|--------|
| Open Debug Panel | Ctrl/Cmd+Shift+Alt+P (`%#&p`) |
| Fast-Forward 1 Day | Ctrl/Cmd+Shift+Alt+F (`%#&f`) |
| Give 50 of Each Resource | Ctrl/Cmd+Shift+Alt+R (`%#&r`) |

Øvrige under `Catan Roguelike/Debug`:

- Fast-Forward 5 Days / Auto-Play One Day
- Set Player VP to 9 / Force Win
- Force Rolls (all 1s / all 2s)
- Force Card (Knight, Harbor Charter, Year of Plenty)
- Force Event (alle 6)
- Skip Night Card
- Replay Seed 42

Custom EditorWindow: **Catan Roguelike → Debug → Open Debug Panel** med seed, N dage, VP-slider og knapper.

Fast-forward **kalder ikke** `EndPlayerDay` / `PlaceSettlement` (VertexDistance-stall). Den hopper setup → nat, `SkipNightCard`, og et sikkert dag-skift uden AI-placement.

`GameManager.DebugRestart(seed)` er wrappet i `#if UNITY_EDITOR`.
