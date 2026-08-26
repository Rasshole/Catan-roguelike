# Blokeret

Korte blockers brugeren skal handle på. Alt andet kører videre udenom.

## 1. Unity-licens (blokerer 0.1, 0.2, 0.7 og batchmode)

Ingen gyldig Editor-licens på VM'en. Batchmode printer `No valid Unity Editor license` og stopper (exit 2 i `tools/verify-fresh-clone.sh` — hænger ikke).

ALF ligger **uden for repoet**: `/workspace/unity-activation/Unity_v6000.3.15f1.alf`

**Hvad du skal gøre:**

1. Gå til https://license.unity3d.com/manual
2. Log ind, upload `.alf`, vælg Personal
3. Download den returnerede `.ulf` og læg den tilbage på VM'en

Indtil `.ulf` er aktiveret:

- ingen batchmode
- ingen `.meta`-generering (0.1)
- ingen `ProjectSettings/`-dump (0.2)
- ingen committed `Game.unity` (0.7)
- ingen EditMode via Unity

Core (ren C#) kan stadig kompileres med `dotnet`.

## 2. Mac standalone-modul (blokerer OSX-player fra Linux-VM)

Linux-host kan ikke hente Mac Editor-target tarballs for `6000.3.15f1` (404). Der findes kun en macOS `.pkg`.

Konsekvens: **OSX standalone-player kan ikke bygges fra denne Linux-VM.** Play Mode i Editoren er platformuafhængigt og virker, når licensen er på plads. macOS-build skal ske på en Mac (eller når Unity udgiver Linux-host Mac-modulet).
