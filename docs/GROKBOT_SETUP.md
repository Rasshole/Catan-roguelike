# Grokbot — Setup guide (gør dette før du starter)

Step-by-step over alt du skal gøre, inden du sætter Grokbot i gang på Catan Roguelike.

Prompten selv ligger i [`GROKBOT_PROMPT.md`](GROKBOT_PROMPT.md).

---

## Del A — Skal gøres (ellers virker det ikke)

Grokbot kan installere software selv, men den kan ikke logge ind for dig.
Disse tre credentials er de eneste reelle blockers.

### A1. Unity-licens til VM'en

Unity nægter at starte i batchmode uden aktiveret licens. Uden dette kan Grokbot
kun arbejde på `Core` (ren C#) og ikke åbne Unity overhovedet.

> **Kopiér ikke din egen licensfil til VM'en.** Unity-licenser er maskinbundne — en licens
> aktiveret på din PC er knyttet til din PC's hardware-ID og virker typisk ikke på VM'en.
> Licensen skal genereres til VM'en. Brug derfor A eller B nedenfor.

**Metode A — email og password (nemmest, prøv denne først):**

Læg disse ind som secrets hos Grokbot og lad den aktivere sig selv:

| Variabel | Værdi |
|----------|-------|
| `UNITY_EMAIL` | Din Unity-konto email |
| `UNITY_PASSWORD` | Din Unity-konto adgangskode |
| `UNITY_SERIAL` | **Kun** hvis du har Unity Pro/Plus. Personal har ingen serial |

**Metode B — manuel aktivering kørt på VM'en (mest driftsikker):**

Samme fremgangsmåde som CI-systemer bruger. Licensen genereres til VM'en i stedet for
at blive kopieret fra en anden maskine. Grokbot laver næsten alt arbejdet:

1. Grokbot kører på VM'en:
   `Unity -batchmode -nographics -quit -createManualActivationFile -logFile -`
2. Det producerer en `.alf`-fil, som Grokbot lægger et sted du kan hente
3. **Din eneste opgave:** gå til <https://license.unity3d.com/manual>, log ind,
   upload `.alf`-filen, vælg Personal, og download den `.ulf`-fil du får tilbage
4. Giv `.ulf`-filen tilbage til Grokbot, som aktiverer med den

Skriv i din første besked til Grokbot, at hvis metode A fejler, skal den køre
`-createManualActivationFile` og aflevere `.alf`-filen til dig med en kort instruks.

**Kan du ikke finde nogen licensfil på din PC?**

Det er forventeligt og betyder ikke at noget er galt. To grunde:
`C:\ProgramData` er en **skjult mappe** i Windows (paste stien direkte i Explorers
adresselinje), og moderne Unity (2020.1+ med Hub 3.x) bruger en ny licensing-klient der
gemmer licensen i et andet format og en anden sti end den gamle `Unity_lic.ulf`.

Du har alligevel ikke brug for den — se boksen øverst. Vil du kigge alligevel:

```powershell
Get-ChildItem -Path C:\ProgramData\Unity -Recurse -Include *.ulf,*.xml -ErrorAction SilentlyContinue |
  Select-Object FullName
```

### A2. GitHub push-adgang

Grokbot skal kunne pushe til `main` og lave tags.

1. Gå til GitHub → **Settings** → **Developer settings** → **Personal access tokens**
   → **Fine-grained tokens** → **Generate new token**
2. Vælg **kun** dette repository
3. Giv permissions:
   - **Contents: Read and write** (kode + tags)
   - **Metadata: Read-only** (kræves automatisk)
4. Sæt en udløbsdato du er tryg ved
5. Kopiér tokenet — det vises kun én gang
6. Læg det ind som secret hos Grokbot

Alternativt: en **deploy key** med write-adgang, hvis du foretrækker SSH.

### A3. Cursor-adgang fra Grokbot

Hele arbejdsdelingen bygger på at Grokbot kan sende opgaver til Cursor.
Verificér at Grokbot faktisk kan kalde Cursor, og at disse modeller er tilgængelige:

- **Grok 4.6** (nyeste) — default arbejdshest
- **Composer 2.5** (nyeste) — anden mulighed
- **Claude Opus 5** — nødudgang, hårdt loft på 2 prompts

---

## Del B — Anbefalet (10 minutter der fjerner den største risiko)

Repoet mangler i dag **alle `.meta`-filer** (0 af 55 filer i git) og næsten hele
`ProjectSettings/`. Grokbot fikser det i Fase 0, men hvis du gør det selv først,
er du garderet mod at problemet består hvis Unity-licensen driller på VM'en.

Dette er den enkeltstående vigtigste ting i hele opsætningen, fordi Unity binder
alle referencer sammen via GUIDs der bor i `.meta`-filerne. Mangler de, får hver
ny klon nye GUIDs, og en committed scene ender som "Missing (Mono Script)".

1. Klon repoet: `git clone <repo-url>`
2. Åbn **Unity Hub** → **Add** → **Add project from disk** → vælg **repo-roden**
   (mappen med `Assets/`, `Packages/`, `ProjectSettings/`)
3. Vent på import (5–15 min første gang, `Library/` bygges op)
4. Gå til **Edit → Project Settings → Editor** → **Asset Serialization**
   → sæt **Mode** til **Force Text**
5. Kør menuen **Catan Roguelike → Setup Game Scene**
6. Gem alt: **File → Save Project** og **File → Save**
7. Committ det hele:

```bash
git add -A
git commit -m "Add Unity meta files, ProjectSettings and generated Game scene"
git push origin main
```

Tjek at der nu er `.meta`-filer med:

```bash
git ls-files | grep -c '\.meta$'
```

Tallet skal være langt over nul (typisk 60+).

---

## Del C — Din egen PC (kun til gennemsyn)

Til at hente og spille hvad Grokbot laver:

1. **Unity Hub**
2. **Unity 6.3 LTS — præcis `6000.3.15f1`**
   Anden version trigger en projekt-upgrade og kan brække serialisering.
3. **Git** (eller GitHub Desktop)
4. `git clone <repo-url>` — og åbn **repo-roden** direkte i Unity Hub.
   Repoet *er* Unity-projektet; du skal ikke "lægge det ind i" et andet projekt.
5. **Git LFS** hvis Grokbot har sat det op: `git lfs install`

Cursor på din PC er valgfrit — kun hvis du selv vil læse diffs.

---

## Del D — VM-krav at bekræfte

Ting der stopper Grokbot midt i arbejdet hvis de mangler:

| Krav | Hvorfor |
|------|---------|
| **20+ GB fri disk** | Unity Editor er 15–20 GB, plus `Library/` cache |
| **Netværksadgang** | Unity Hub download, GitHub, Cursor |
| **Skærm eller `xvfb`** | Play Mode og PlayMode-tests kræver grafik |
| **Persistent VM** | Ellers geninstalleres Unity hver session. Bed den gemme `Library/` som arkiv hvis VM'en nulstilles |
| **Shell + sudo** | Til at installere Unity Hub, `dotnet`, `git-lfs`, `xvfb` |

---

## Del E — Sanity check før du trykker start

- [ ] Unity-licens leveret til Grokbot (fil eller env vars)
- [ ] GitHub token med Contents: write leveret
- [ ] Cursor-adgang bekræftet, Grok 4.6 tilgængelig
- [ ] VM har 20+ GB disk og netværk
- [ ] Du har besluttet: Grokbot pusher **direkte til `main`** og tagger milestones
- [ ] `GROKBOT_PROMPT.md` pastet ind som system-prompt hos Grokbot

Så skriver du bare: **"kør"**.

---

## Hvad du kan forvente

Grokbot arbejder i denne rækkefølge og rapporterer på dansk efter hver milestone:

| Fase | Indhold |
|------|---------|
| **Fase 0** | Repo-fundament: `.meta`-filer, `ProjectSettings/`, Force Text, `.gitattributes` + LFS, render pipeline-beslutning, frisk klon-test |
| **Fase 1** | Fix spillet: P0 logik-bugs → P1 halvt-wirede systemer → P2 UI/art/infrastruktur → P3 test-dækning |
| **Fase 2** | Tidligere out-of-scope: save/load → setup-bonus → largest army → Act 2 → nummer-tokens → meta progression |

## Filer du skal holde øje med

| Fil | Hvad den fortæller dig |
|-----|------------------------|
| `docs/BLOCKED.md` | **Læs denne.** Problemer Grokbot gav op på, og hvad den mangler fra dig |
| `docs/CHANGELOG.md` | Hvad du kan gøre i spillet nu som du ikke kunne før |
| `docs/PLAYTEST_NOTES.md` | Hvad Grokbot selv synes føles kedeligt eller unfair |
| `docs/AGENT_LOG.md` | Kort log over hvad der er lavet, og hvad der er næste skridt |
| `git tag -l` | Kendte gode versioner du altid kan `git checkout` |
