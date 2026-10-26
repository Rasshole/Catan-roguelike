# Art & audio credits

## Sound effects (v143)

Every sound effect is **synthesized at runtime in code** by `Assets/_Project/Scripts/Game/GameAudio.cs`
(sine/triangle partials with soft envelopes, filtered noise bursts, small wood-knock and bell models).
No recorded or third-party audio files are used, so there is nothing to license: the sounds are original
works of this project and fall under the repository's own license.

| Sound | Used for | Source |
| --- | --- | --- |
| Click | every HUD button, settings popup | synthesized (`GameAudio.Synthesize`) |
| Road / Settlement / City | building (pass & play, AI and single-player) | synthesized |
| CardDraw | cards flying into a hand (production, trades, steals, events, fish) | synthesized |
| DevBuy / DevPlay | buying / playing development cards | synthesized |
| Dice | the animated two-dice roll | synthesized |
| Robber / Steal | moving the robber, stealing a card | synthesized |
| TradeAccepted / TradeDeclined | trade answers, bank trades, cancelled offers | synthesized |
| TurnStart | a new turn begins | synthesized |
| TimerTick | the last 5 seconds of a timed turn | synthesized |
| QuestDone | a secret quest is completed | synthesized |
| Vote / Auction / Sabotage | party-rules ballots, auctions and shenanigans | synthesized |
| Victory | the match is won | synthesized |
| Ship / ShipMove / Pirate | Seafarers: building or sailing a ship, moving the pirate (v145) | synthesized |

Volume (0–100 %) and mute live in the in-game **Menu → Settings** popup and persist in `PlayerPrefs`
(`catan.sfx.volume`, `catan.sfx.muted`).

If CC0 recordings are ever added, list each file here with its author, source URL and the CC0 license.
