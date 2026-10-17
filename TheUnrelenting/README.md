# The Unrelenting — v0.11.1 Relentless Pursuit

WorldBox 0.51.2 build 719 • NeoModLoader • Ubuntu/Linux.

Built on the tested v0.11.0 The Witness code. Keeps Haunted, Origins, recurring origin attempts, one-entity victim queue, body hopping, randomized speed, sudden rush, probable-killer detection and experimental killer transmission.

## New in v0.11.1

- **Adaptive pursuit**: movement orders every 1.6 s when >50 tiles away, 0.8 s at 15–50, 0.35 s at 5–15, and 0.18 s at <=5. Entire-world/kill-counter scan stays once every 2 real seconds to reduce CPU usage.
- **Anti-distraction**: `cancelAllBeh()` once every 4.5 real seconds followed by `goTo(target.current_tile, false)` to counter sleep/normal activities. May interfere with some native AI actions.
- **Stall rescue**: after 18 real seconds without a 1+ tile improvement, try jumping into another eligible existing body at least 4 tiles closer to the victim. Prefer bodies 6–22 tiles from the victim. This does NOT make terrestrial creatures swim and does not guarantee inter-island crossing without a body on the other side.
- New NML settings group **Pursuit AI** for toggles, timeout, extra logs. Defaults ON for adaptive movement, anti-distraction and rescue.
- No global debug/pathfinding setting changes; no teleports of an existing unit; no extra trait markers.

## Install

Close WorldBox, back up any valued world and replace your existing `Mods/TheUnrelenting` folder with the included folder. Use a disposable world for this experimental version.

## Test A — follow moving target

Apply Haunted to one unit on the same landmass as several animals. Observe movement and check log for `PROGRESSO CADEIA`. The log now includes `PROXIMA_CORRECAO_SEG`.

## Test B — ocean

Create two islands. Place the target Haunted on one; on the other add a candidate host (e.g. sheep). **Place a third compatible unmarked body on the target's island** to make a water-escape via possession possible. After about 18 real seconds without progress, check for `PURSUIT RESGATE POR TROCA DE CORPO!`. Without any eligible target-side body, it is expected that the entity may remain unable to reach the target. This is a known limitation, not success.

## Test C — sleep

Keep the entity's host alive for long enough to see whether native sleep interrupts pursuit. `Override normal activities` can be toggled off for comparison.

## Diagnostics

```bash
grep -nE 'PURSUIT|PROGRESSO CADEIA|WITNESS TRANSMISSAO|CONTATO COM A VITIMA|EXECUCAO CONFIRMADA|error CS|Failed to compile|ERRO NA PERSEGUICAO' ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 120
```

`PURSUIT SEM CAMINHO / SEM RESGATE DISPONIVEL!` indicates possible water/obstacle/stalled pursuit with no available alternative body; it does not prove water is the cause. `PURSUIT RESGATE POR TROCA DE CORPO!` confirms a fallback reassignment and reports previous/new distance.

## Notes and limitations

- This code has not been compiled inside WorldBox in this environment; this is an experimental build.
- The rescue heuristic uses distances and evidence of stalled progress rather than guaranteed landmass/path connectivity. An eligible body on a closer *different* island might still fail and require another hop.
- Continuous rapid `goTo()` calls can affect game navigation; test with low population before using on larger maps.
- The mod does not truly make land animals swim. A host already across a sea with **no additional eligible body** on the victim side can remain stranded.
- Do not rely on in-memory state across world reload; previous limitation still applies.

## GitHub

Replace all files with the release package. Commit: `feat: add adaptive pursuit AI and stalled-host rescue`
Version: `v0.11.1`. Roll back to `v0.11.0` if necessary.
