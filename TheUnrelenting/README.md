# The Unrelenting — v0.11.0 The Witness

WorldBox 0.51.2 (build 719), NeoModLoader, Ubuntu/Linux.

## Summary

This release preserves Origins, body hopping, victim queue, randomized speed and sudden rush. It adds **The Witness**, a conservative detection path when a Haunted unit dies by an external cause.

**Important limit:** the observed kill-count increment + proximity is a **probable killer** rather than an authenticated attacker/death event. It will not handle every weapon, death type, or ambiguous battle. It errs on the side of **no transmission**.

## NML settings

New group **The Witness**:

- `EnableWitnessTracking` — on by default, logs deaths and probable killers.
- `EnableKillerTransfer` — **off by default**, experimental transfer to a uniquely identified probable killer.
- `MaxKillerDistance` — 8 tiles by default (2–15).
- `VerboseWitnessLogs` — off by default (noisy debug diagnostics).

Your preexisting Origins settings remain under **Origins**.

## Test plan (use a disposable world)

1. Close WorldBox and replace the entire `TheUnrelenting` mod folder with this package.
2. Disable Origins automatic starts and keep Witness Tracking **on**, Killer Transfer **off** initially.
3. Place a few humans or orcs in a fighting scenario; mark **only the intended victim** Haunted.
4. Let a normal creature kill this victim before the entity reaches it. Check the log.
5. Look for `WITNESS ASSASSINO PROVAVEL!` (one nearby actor increased its `kills` counter), or a clear `SEM AUTOR SEGURO` message.
6. Once attribution looks correct in a controlled test, enable `EnableKillerTransfer`; repeat using a NEW victim.
7. Inspect the probable killer's trait list for Haunted and the next target assignment. Check that the entity's own executions **do not** transmit Haunted via Witness.
8. Test a crowded battle to confirm ambiguous deaths do not transmit.

Command:

```bash
grep -nE 'WITNESS|HAUNTED ENTROU|EXECUCAO CONFIRMADA|error CS|Failed to compile' ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 120
```

## Diagnostic interpretation

- `WITNESS ASSASSINO PROVAVEL!` — one plausible candidate; **not** incontrovertible proof of the killer.
- `WITNESS TRANSMISSAO AO ASSASSINO PROVAVEL!` — Haunted added to that candidate when enabled.
- `WITNESS MORTE HAUNTED SEM AUTOR SEGURO!` — no transfer; no unique nearby kill increment.
- `WITNESS CONTADOR DE ABATES INACESSIVEL!` — cannot read the counter; no transmission.

## Known limitations

- This build reads `ActorData.kills` by reflection. The exact update timing remains unverified in live-game combat.
- A 2-second snapshot may miss a killing actor who dies, moves away, gets several kills, or uses ranged attacks.
- Proximity + counter delta may still select an unrelated killer in a busy battle. Therefore transfer is opt-in and experimental.
- Witness tracking is not a Harmony damage-event hook and does not classify fire, lightning, or accidents.
- No Trauma Witness or family inheritance yet; those belong to future releases.
- Identity tracking uses in-memory Actor references and resets on world reload, as in prior versions.

## GitHub

Replace source and locales, add Witness configuration items in default_config.json, version `v0.11.0`.

Commit: `feat: add experimental Witness killer detection and curse transfer`

Rollback: install the v0.10.2 ZIP after closing WorldBox.
