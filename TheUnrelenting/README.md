# The Unrelenting v0.7.0 — One Entity, Many Faces

Prototype for WorldBox 0.51.2 build 719 / NeoModLoader.

## Current behavior

- The custom `Haunted` trait keeps its previous stable ID: `ArvakTheUnrelenting_Haunted`.
- A **single entity** hunts one target at a time (queue of discovered Haunted creatures).
- On kill, the same entity selects the next target; its current host remains alive until the queue ends.
- Every ~22 s for the first hop and ~32 s later (real elapsed seconds) the entity attempts to **switch its possessed host** to a nearby eligible ordinary creature.
- The old host is not killed and receives cleanup of the native traits added by the mod.
- Eligible host IDs are limited to common civilized units and animals. Dragons, aliens, UFOs and most bosses are excluded.
- The last host disappears after the final Haunted target is killed.

**Important:** this version uses **body-hopping**, not true actor-species/sprite mutation. A sheep becomes the *new* host; the previous human is released. The same actor does not turn into a sheep. This is a deliberately safer prototype while cross-species visual transformation remains unverified on WorldBox 0.51.2.

## Test

1. Close WorldBox. Replace the old `TheUnrelenting` directory with the one in this ZIP in the working NML `Mods` path.
2. Use **only a disposable world**; this mod eliminates units.
3. Spawn at least 15 ordinary units including humans, sheep, cows, and/or other eligible species on one large island.
4. Add Haunted to 2-3 separate units; do not give Haunted to the entity host.
5. Confirm the log has `HAUNTED ENTROU NA FILA`, `ENTIDADE DESPERTOU`, `EXECUCAO CONFIRMADA`, `PROXIMA VITIMA`, `ENTIDADE TROCOU DE HOSPEDEIRO`, and finally `ENTIDADE ENCERRADA`.
6. If the pursuit finishes too fast to see a host change, place targets farther away, or adjust `FirstHostChangeAfter` and `HostChangeInterval` in `Main.cs` (seconds).

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 120
```

## Known limitations

- AI neutrality and true invulnerability are not solved; Peaceful, Immortal and Super Health are provisional native traits.
- Host bodies can be identified through abnormal native traits in their info panel.
- Hosts can die or be attacked normally by the game. The entity will try to reselect one.
- Swimming, oceans, island transitions, saving/loading active chains and the interactions of body switching with the third-person camera mod have not been validated.
- Hopping uses nearby existing creatures and restores only native traits that the mod itself added. Worlds saved while a host is possessed can retain temporary modifications.
- The old actor's normal AI may not instantly resume after body release; this needs testing.
- No magical visual transformation or real-time sprite swap is performed.

## GitHub

Replace existing project files with those in this ZIP (including `Locales`).
Suggested commit: `feat: chain Haunted victims with a single body-hopping pursuer`
Tag / version: `v0.7.0` after validation.
