# The Unrelenting v0.8.0 — Unpredictable Pursuit

Experimental NeoModLoader mod for WorldBox **0.51.2 build 719**, tested baseline on native Ubuntu Steam.

## New in v0.8.0

**Each time the supernatural entity assumes a host, it randomly chooses a pursuit speed:**

| Mode | Chance | Change to the host |
| --- | ---: | --- |
| SLOW | 50% | Adds native `slow` trait temporarily |
| NORMAL | 35% | Keeps species' natural speed (no speed trait) |
| FAST | 15% | Adds native `fast` trait temporarily |

The selected speed remains active for that host and is rerolled on the next body-hop. This can create slow stalking, ordinary-speed pursuit or an occasional unexpected sprint. A naturally fast species may still move quickly in NORMAL mode.

Only the traits actually added by the mod are removed when a host is released. Hosts with pre-existing `slow`, `fast`, `agile`, or `plague` are excluded to avoid inadvertently deleting native traits through opposite-trait interactions.

## Preserved from v0.7.0

- Stable Haunted trait ID `ArvakTheUnrelenting_Haunted`.
- Only one supernatural entity; Haunted victims are hunted in a queue.
- Random host selection from ordinary species: human, elf, sheep, cow etc. (dragons/UFOs not eligible).
- Body-hopping to nearby **existing** creatures approximately 22 real seconds after activation and every 32 seconds thereafter (subject to available hosts).
- The current host executes Haunted victims by contact; it survives each kill until the queue is empty.
- The last host disappears at the end of the chain.

## Ubuntu installation

1. Close WorldBox.
2. Replace the old `TheUnrelenting` folder in your **working NeoModLoader Mods directory** with the folder from this ZIP.
3. Open WorldBox; test in a NEW DISPOSABLE WORLD with several eligible ordinary creatures.
4. Apply Haunted to 2-3 units separated by distance. Watch as hosts change and speed is rerolled.
5. In `Player.log`, search for `VELOCIDADE=SLOW`, `VELOCIDADE=NORMAL`, `VELOCIDADE=FAST`, `ENTIDADE TROCOU DE HOSPEDEIRO`, `EXECUCAO CONFIRMADA`.

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' \
  ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 120
```

If you only see one speed in a short test, run longer or let more host changes happen: speed is random, and a single result is not guaranteed. Watch for `NAO FOI POSSIVEL APLICAR TRAIT=fast` and report it if present.

## Limitations and safety

- **Experimental/unverified in-game** in this release: syntax and archive structure checked; only user testing can confirm execution.
- Speed uses native traits, which remain visible in the host information panel; full disguise is still pending.
- `Immortal` and `Super Health` are not absolute invulnerability; `Peaceful` does not guarantee other units will not attack the host.
- Host changes are possession of another existing actor, not morphing a single actor's species.
- Native AI can interfere with pathfinding, especially around oceans, crowds and buildings.
- Do not save a valuable world during testing: active hosts may retain temporary traits in saves, and chain persistence across saves has not been validated.

## GitHub

Replace the existing project files with this ZIP's complete `TheUnrelenting` folder (including `Locales`).

Commit: `feat: randomize pursuer speed with slow, normal and fast modes`
Version: `v0.8.0` (test before publishing a release).
