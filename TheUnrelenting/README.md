# The Unrelenting — v0.2.0 (Haunted detection)

Experimental horror mod for WorldBox **0.51.2 (build 719)** using NeoModLoader, tested by the player on Ubuntu for v0.1.1. **v0.2.0 has not yet been tested in-game.**

## What's new

- Preserves the `Haunted` trait and existing saved trait ID (`ArvakTheUnrelenting_Haunted`).
- Every 2 real seconds, looks for living units with Haunted.
- Logs **HAUNTED DETECTADO** only once per detected unit, and **HAUNTED DEIXOU DE ESTAR ATIVO** if the unit no longer appears (e.g. trait removed, death, or world switched).
- Uses temporary `GetHashCode()` IDs for debug only: **these are NOT persistent character IDs across saves**.
- No pursuing creature or changes to unit stats yet.

## Install

1. Close WorldBox.
2. Locate the **working** NeoModLoader `Mods` folder in the game installation.
3. Replace only the old `TheUnrelenting` folder with the `TheUnrelenting` folder inside this ZIP.
4. Launch game, open or create a world, apply **Haunted** to one unit.
5. Wait about 2 seconds and inspect Player.log. Remove Haunted and check the second message.

## Diagnostic command (Ubuntu)

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' \
  "$HOME/.config/unity3d/mkarpenko/WorldBox/Player.log" | tail -n 40
```

Expected message examples:

```text
THE UNRELENTING: DETECTOR HAUNTED ATIVO (verificacao a cada 2 segundos).
THE UNRELENTING: HAUNTED DETECTADO! TEMP_ID=...
THE UNRELENTING: HAUNTED DEIXOU DE ESTAR ATIVO! TEMP_ID=...
```

The last message is NOT proof the player manually removed the trait: it also fires if the actor dies or disappears from the current world.

## GitHub update

Replace `Main.cs`, `mod.json`, `Locales/*.json` and `README.md`. Do not rename the trait's ID.
Commit: `feat: detect Haunted units in the active world`
Version: `v0.2.0`; publish a release only after testing.

## Relevant examples

- https://github.com/WorldBoxOpenMods/ModExample/blob/master/ExampleModCode.cs
- https://github.com/WorldBoxOpenMods/ModLoaderPullRequests/blob/master/api/BasicMod.cs
