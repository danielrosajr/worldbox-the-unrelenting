# The Unrelenting v0.9.0 — Sudden Rush

Experimental horror mod for **WorldBox 0.51.2 (build 719)** and NeoModLoader on native Linux/Steam.

## What's new

A single supernatural pursuer continues chasing all living **Haunted** units in sequence, switching between existing ordinary creatures (sheep, cows, humans, elves, etc.). Its host initially rolls one of three speeds:

- **SLOW** — 50% (`slow` added temporarily)
- **NORMAL** — 35% (natural movement for its species)
- **FAST** — 15% (`fast` added temporarily)

**Sudden Rush:** while inhabiting a slow/normal host, it has a **25% chance at each scheduled opportunity** (first check at 10–17 real seconds after a host change; later checks every 12–22 real seconds), *provided it is 2–55 map units from its target*. If triggered, it temporarily gets `fast` for 3–5.5 real seconds before restoring its previous speed. It does NOT change host to do this. Hosts that already rolled FAST are not boosted again.

These are probabilities per **check**, not per second, and a burst is never guaranteed. Host-hopping, queue management, lethal contact, temporary trait cleanup and the stable Haunted ID are preserved from v0.8.0.

## Curse origin and transmission: intentionally NOT enabled

- **Manual application** of Haunted remains the sole tested origin.
- A future opt-in world rule could occasionally mark **one** eligible creature, using a global interval/cooldown, not a roll per creature or reproduction action.
- Proposed death-transfer rules (killer inherits Haunted; one descendant inherits on supernatural execution; self-inflicted death without a killer ends the curse) **require verified game event/attacker and genealogical information**. This build does not implement them.

This avoids accidentally cursing entire villages while the feature is designed.

## Install/test on Ubuntu

1. Close the game and copy the new `TheUnrelenting` folder over the previous NeoModLoader `Mods/TheUnrelenting` folder.
2. In a disposable world, apply `Haunted` to two or three ordinary units. Add several non-Haunted sheep, cows or people in the same landmass so the entity can change hosts.
3. Wait and watch. If you don't see a rush, give the entity more targets or more time to make attempts.
4. Read the game log:

```bash
grep -nE 'ARRANCADA|CHANCES DE VELOCIDADE|ENTIDADE TROCOU DE HOSPEDEIRO|EXECUCAO CONFIRMADA|error CS|Failed to compile' \
  ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 100
```

Successful new behavior produces `ARRANCADA INICIADA!` and `ARRANCADA ENCERRADA!`, and existing behavior logs `EXECUCAO CONFIRMADA!`.

## Technical notes

- `fast`/`slow` are native traits applied temporarily to eligible hosts. The code tracks exactly what it added, removing its own trait at the end of a burst or possession. It will not deliberately strip natural speed traits from creatures.
- A failed `fast` application cancels the burst and attempts to restore the original `slow` trait when needed.
- This zip has been structurally inspected but **not run in the user's WorldBox**. Sudden Rush is experimental until tested in-game.
- In-world `fast` trait behavior can vary with the creature and game state; a logged burst shows that trait application succeeded, not a guaranteed visible change in speed.
- Do not save valuable worlds while testing: mod state is not persisted across load, and host traits can be included in a game save.

## GitHub update

Replace `Main.cs`, `mod.json`, `README.md` and the included documentation with the new files; keep `Locales`.

Commit: `feat: add sudden speed bursts during Haunted pursuit`
Version: `v0.9.0` (publish release after verifying behavior).
