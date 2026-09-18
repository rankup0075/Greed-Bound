A side-view 2D roguelike where **you choose the cards that make your enemies stronger**.

Every round you pick a card that raises the danger of the fight. The same card also raises your
reward. There is no card that only helps you — the only way to get richer is to make the next
battle worse for yourself. Push too far and the run ends; stop too early and you leave the run's
real rewards on the table.

> **This project is being developed entirely through vibe coding.**
> Design, code, numbers, and art are all produced by working conversationally with an AI
> (Claude Code) rather than by hand-writing systems up front. See
> [Vibe Coding](#vibe-coding) below for how the workflow actually runs.

---

## Status

**In development.** The game is playable end to end — title → lobby → battle → shop → game over —
and all core systems are implemented. What remains is mostly art: sprites are still being produced,
so much of the game currently renders with placeholder pixel art.

| | |
|---|---|
| Engine | Unity 6000.3.11f1 (URP, 2D) |
| Platform | PC (Steam), **keyboard only**, 1920×1080 |
| Art | Pixel art, 640×360 reference resolution, 36 PPU |
| Team | 1 person — design, code, and art all done in collaboration with AI |
| Language | The game and its design documents are in Korean |

---

## The Core Loop

```
[Pick a card] → [Greed?] → [Battle] → [Round clear / reward]
      ↑                                        │
      └──────── every 3 rounds: [Shop] ←───────┘
```

1. Three cards are offered — pick one. Every card buffs the **enemies**.
2. **Greed**: you may take one of the two cards you passed on. Its Risk *and* Reward are ×1.3.
3. Fight.
4. Clear the round, collect gold scaled by your accumulated Reward multiplier.
5. Every 3 rounds a shop opens; every 5 rounds a boss shows up.
6. Repeat until you die. Your run converts into **Souls**, the meta currency.

### Two axes, one choice

Each card raises two numbers at once:

- **Risk** — globally buffs enemies (`multiplier = 1 + risk × 0.004`), and **grants a free relic every
  50 risk**. Past 150 risk you enter **Overload**: an extra elite every round, reward cap raised to
  ×8.0, and relics now arrive every 35 risk instead of 50.
- **Reward %** — `reward multiplier = 1 + total reward% / 100`, applied to round clear gold.

The 25 cards are deliberately split into three risk:reward ratios — **1:1 (relic-leaning)**,
**1:2.5 (balanced)**, **1:5 (gold-leaning)** — so "always take the scariest card" and "always take the
richest card" produce genuinely different builds. Before this split, simulation showed both
strategies converging on identical outcomes, which is why the ratios were redistributed.

---

## Features

**Cards** — 25 cards across 5 families: stat buffs, behavior changes, swarms, environment rules,
and curses. Same-family synergies rewrite combat rules rather than just adding numbers, and 8
cross-family card pairs form named combos.

**Combat** — 3-hit melee combo, thrown daggers, a dash, and one of 3 unlockable skills (Whirlwind /
Ground Slam / Sword Wave). Every threat has a telegraph window — nothing in this game damages you
without warning first.

**Enemies** — normal, small, elite, ranged mage, shield bearer, charger, and a boss
("The Fallen Knight-Commander") every 5 rounds. Enemies path across one-way platforms, respect
platform clearance based on their own size, and have their wind-up cancelled when slammed.

**Three reward layers**
- **Relics** (13) — permanent for the run, granted automatically at Risk thresholds
- **Shop** (10 items) — bought with gold, permanent for the run, prices scale with round
- **Souls** — meta currency earned on death, spent on **9 permanent lobby upgrades** (full unlock
  costs 2,370 souls, roughly 20 runs)

**Scenes** — Title, Lobby (skill selection + permanent upgrades), Battle, Shop (a walkable shop map
with goods laid out on stalls), and a dedicated 4-stage Tutorial course.

---

## Controls

| Key | Action |
|---|---|
| ← → | Move |
| C | Jump — `↓ + C` drops through a one-way platform |
| Left Shift | Dash (**no i-frames**) |
| Z | Melee (3-hit combo) |
| X | Throw dagger |
| A | Skill |
| 1 | Health potion |
| Enter | Interact / confirm |
| Tab | Inventory |
| R | Reroll cards / refresh shop stock (requires an upgrade) |
| M | Mute |
| ESC | Pause · Settings |

---

## Running it

1. Install **Unity 6000.3.11f1**.
2. Open this folder as a Unity project.
3. Open `Assets/Scenes/TitleScene.unity` and press Play.
   (`TitleScene` is first in Build Settings; `MainScene` is the battle scene if you want to jump
   straight into combat.)

There is no external dependency beyond the packages in `Packages/manifest.json`.

---

## Project layout

```
Assets/
  Scripts/       89 C# files
    Core/        game loop, deferred-action queue, round manager, run state
    Player/      movement, attacks, stats, skills
    Enemy/       enemy AI, EnemyModifiers
    Combat/      hit resolution, daggers, hazards (fire, lightning, explosions)
    Map/         platforms, bounds, camera, arena layouts
    Cards/       CardData, synergies, card effects
    Relics/      relic catalog and effects
    Meta/        souls, permanent upgrades, save data, lobby
    Shop/        shop catalog and shop map flow
    UI/          HUD, card selection, PixelUi toolkit
    Tutorial/    tutorial flow and practice targets
    Editor/      scene builders, card generator, pixel-art converter (excluded from builds)
  Data/          25 card assets, relics, shop items (ScriptableObjects)
  Scenes/        TitleScene · LobbyScene · MainScene · ShopScene · TutorialScene
  Art/  Audio/  Prefabs/
AI_Source/       raw AI-generated art before pixel conversion
md/              the design documents (see below)
```

Scenes are **built by editor scripts** rather than hand-assembled in the Inspector — see the
`Greed Bound` menu in the Unity editor (`전투 맵 생성` = build battle arena, `튜토리얼 씬 생성` = build
tutorial scene, and so on). This keeps layouts reproducible and lets spatial
constants live in code (`ArenaLayout`, `ShopLayout`, `TutorialLayout`) instead of being scattered
across scene files.

---

## Documentation

Three living documents in [`md/`](md/), written in Korean:

| File | What it is |
|---|---|
| [`GreedBound_Unity_Spec.md`](md/GreedBound_Unity_Spec.md) | **The single source of truth.** Every number in the game — card values, physics constants, enemy stats, formulas, implementation rules. If code and spec disagree, the spec is right. |
| [`GreedBound_Progress.md`](md/GreedBound_Progress.md) | Session-by-session log: what was done, what was decided and why, what comes next. |
| [`GreedBound_Art_Guide.md`](md/GreedBound_Art_Guide.md) | How art is generated with AI and converted into game-ready pixel art. |

---

## Vibe Coding

**This project is built with vibe coding** — the developer describes intent, plays the result, and
gives feedback; the AI writes the systems, tunes the numbers, and records the decisions. Nearly
every system in this repository was produced that way.

That only works with structure around it, so the project runs on a few rules:

**The spec is the memory.** AI sessions do not carry context between them, so the numbers live in
`GreedBound_Unity_Spec.md`, not in anyone's head. Every session starts by handing the spec to the AI.
If a value needs to change, **the spec is updated first, then the code** — never the other way
around.

**Balance is decided by simulation, not by vibes.** When a number is in question, the options are
simulated and compared before anything is written. The risk/reward redistribution is the clearest
example: simulating 9 rounds showed that "risk-first" and "reward-first" strategies ended in exactly
the same place (245 risk / 4.4 relics / ~5,300 gold), which is what proved the card ratios were
broken and drove the 1:1 / 1:2.5 / 1:5 split.

**Every decision is logged with its reason.** `GreedBound_Progress.md` records not just what changed
but why — including the ideas that were rejected and what went wrong. Bugs are logged with their
root cause, so the same mistake isn't re-derived three sessions later.

**Play feedback drives the loop.** Changes end in a "needs checking" list; the developer plays,
reports what felt wrong, and that becomes the next change. Several systems here exist purely because
something felt bad in play — the boss getting stuck under platforms, fire zones dealing damage with
no hit reaction, cards showing their numbers more prominently than their effects.

**Art is AI-generated too.** Source images are generated, then run through a custom in-editor pixel
converter (`Editor/PixelConverter.cs`) that quantizes to an Endesga 32 palette and downsamples to the
game's pixel grid. Prompts and settings live in the art guide.

---

## Credits

Made by one developer in collaboration with Claude. The original playtested HTML prototype
(a single-file Canvas 2D build) has since been lost — the spec document is now the only surviving
record of its tuned values, which is exactly why it is treated as authoritative.

