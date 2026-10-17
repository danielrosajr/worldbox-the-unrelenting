# The Unrelenting v0.10.0 — Origins

Experimental horror mod for **WorldBox 0.51.2 (build 719)** using **NeoModLoader**, tested in development on native Ubuntu/Steam by the mod creator.

## New in v0.10.0

**Origins** supports two ways to begin the curse:

1. **Manual** — apply the existing `Haunted` trait to any unit as before. Always available, regardless of mod configuration.
2. **Rare random origin (opt-in)** — when enabled, the mod attempts a single global random roll at configurable real-time intervals, not a roll per individual. A successful roll gives `Haunted` to ONE random living **human, elf, dwarf, or orc**.

Only one auto-generated origin is allowed **per loaded world per play session**. A world with any Haunted unit will not receive a random origin while the curse is active. On reload, the in-memory "already generated" flag resets; there is no persisted origin metadata in this prototype.

All pursuit, body-hopping, speed probabilities and sudden sprint behavior from v0.9.0 remain unchanged.

## Native settings panel

In NeoModLoader's **Mods** menu, select **The Unrelenting** and use **Mod Configuration** (if your NML version exposes the button). The settings are provided by `default_config.json` and should persist through NML's built-in configuration storage.

| Setting | Default | Description |
| --- | --- | --- |
| Enable rare random origins | OFF | No spontaneous Haunted unless enabled |
| Chance per attempt (%) | 1% | Each *global* roll is independent; no per-unit roll |
| Interval between attempts | 180 real seconds | How often to attempt while world has zero Haunted |
| Quick test mode | OFF | When random origins are ON, generates one guaranteed Haunted after ~10 real seconds |

**Important:** changing the settings does not write directly to the `default_config.json` template; NML saves user choices in its own configuration. Settings are read on each scan (~2 s) and are effective once the configuration window is closed. "Quick test" requires the separate enable switch. It creates **at most one auto-origin** per world load; loading a disposable fresh world resets that counter. The interval/chance values are ignored when quick test mode is on.

## Testing on native Ubuntu

1. Close WorldBox. Replace the previous `Mods/TheUnrelenting` folder with this one, including the NEW `default_config.json`.
2. Create a disposable map with 1+ human/elf/dwarf/orc and other ordinary creatures.
3. Check **The Unrelenting** appears in the mod menu. Open its **Mod Configuration**.
4. **Test A (default):** keep Enable rare random origins OFF; wait >15 seconds; confirm there is no `ORIGINS HAUNTED GERADA`. Manually apply Haunted and verify pursuit as in v0.9.0.
5. **Test B:** new world, enable both **Enable rare random origins** and **Quick test**. Close the configuration window and allow about 10–15 real seconds. Exactly one unit should receive Haunted without any manual action, with an `ORIGINS HAUNTED GERADA` log line. A pursuer should begin its normal chain.
6. **Test C:** disable quick test; optionally set a short interval and high chance for debugging in a NEW world. Do not mistake the configured chance for a per-unit probability.
7. **Test D:** let the auto-marked unit die; verify no extra auto-marked units appear in the *same world session* even after the entity concludes.

Log command:

```bash
grep -nE 'ORIGINS|HAUNTED ENTROU NA FILA|ENTIDADE DESPERTOU|EXECUCAO CONFIRMADA|error CS|Failed to compile|Exception' \
  ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 120
```

### Technical/safety notes

- Manual traits, automatic origins, and the entity are intentionally distinct systems.
- Chance 1% per 180 real seconds implies a mean time of around **5 hours of eligible active attempts**, not a guarantee; quick test is provided to verify functionality.
- `Time.unscaledTime` is used: timers follow real time while game code updates, not WorldBox years. May run while gameplay is paused if Unity updates the mod.
- A new world load resets in-memory associations; saves are **not** a reliable way to persist a currently possessed host, and native traits may be saved with the world. Test in a disposable world only.
- No transmission on killing/reproduction/descendants is implemented yet. These are planned for v0.11 The Witness and v0.12 Bloodline.
- The ZIP is structurally validated but **has not been executed in the actual WorldBox game**.

## GitHub

Replace `Main.cs`, `mod.json`, `README.md`, `LEIA-ME.txt`, `Locales/*.json` and **add** `default_config.json`.

Commit: `feat: add opt-in rare Haunted origins and NML settings panel`

Version: `v0.10.0` (release after testing).
