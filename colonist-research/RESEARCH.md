# Colonist.io-style presentation (classic Catan)

Reference layout for the `ColonistStylePrototype` scene.

## Camera and board

- Orthographic, straight top-down view (flat 2D-looking hexes).
- Classic resource hex colours, cream number tokens with pips, 6/8 in red.
- Ocean background, harbour markers on coast edges.
- Board centred in the unobstructed region (account for right HUD column and bottom bar).

## Right column (~300–320px)

Top to bottom:

1. **Player panels** — avatar colour chip, name, VP, hidden card count, longest road, largest army.
2. **Game log** — scrollable event text.
3. **Chat** — input + send (placeholder OK for prototype).

## Bottom bar

Left to right across the board area (not under the right column):

1. **Resource hand** — five resource cards with counts on each card.
2. **Development cards** — stack / count (roguelike night cards hidden in classic mode).
3. **Build** — Road, Settlement, City, Buy dev card (cost tooltips).
4. **Trade** — opens bank / player trade dialog.
5. **Dice** — shows 2d6 total with roll animation hook.
6. **End Turn** — primary action button.

## Flow

Roll → production highlight on hexes → resource icons fly to hand → build/trade → robber move + steal on 7.

## Roguelike

Leaders, draft, shop, day/night roguelike cards stay in code but are **not shown** when `ColonistPresentationMode` is enabled on the scene.
