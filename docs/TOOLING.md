# Værktøjer

## `tools/verify-fresh-clone.sh`

Frisk-klon-test: kloner origin til en temp-mappe, åbner projektet i Unity batchmode, tjekker compile/missing scripts, kører EditMode-tests, og sletter mappen bagefter (også ved fejl).

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
| `UNITY_EDITOR` | `/home/box/Unity/Hub/Editor/6000.3.15f1/Editor/Unity` | Unity Editor-binær |
| `UNITY_TIMEOUT` | `180` | sekunder; processen kills så scriptet aldrig hænger |
| `DISPLAY` | (tom) | hvis unset, køres Unity via `xvfb-run -a` |

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

- `git`, `timeout`, Unity 6000.3.15f1
- `xvfb-run` når der ingen `DISPLAY` er
- Gyldig Unity-licens på maskinen (ellers exit 2)

Temp-mappen ryddes via `trap` ved både PASS, FAIL og licens-fejl.
