# Over It Together - Modding Plan

Online multiplayer, races and live splits for Getting Over It with Bennett Foddy.

## Summary

- Feasible. Unity 2020.3.25f1, Mono, no anti-cheat, plain C# game code. Route: BepInEx 5 plugin.
- The game already ships Steamworks.NET (`Assembly-CSharp-firstpass.dll` + `steam_api64.dll`) and initialises Steam
  (`SteamManager`). So Steam lobbies, the Steam invite overlay and Steam P2P networking are all usable with no server.
- Players are shown as see-through "ghosts": copies of the pot, hammer and Diogenes with all colliders and physics
  removed, driven by network data. They can never collide with you.
- Everyone needs the mod (unlike Chained Teams). Delivered as one launcher `.exe` + a GitHub release zip.

## Recon (2026-10-21)

| | |
|---|---|
| Steam app | 240720, build 8111718, `C:\Program Files (x86)\Steam\steamapps\common\Getting Over It` |
| Engine | Unity 2020.3.25f1, Mono, `GettingOverIt_Data\Managed\Assembly-CSharp.dll` (532 decompiled files, mostly libraries) |
| Anti-cheat | none |
| Networking libs | Steamworks.NET (SteamMatchmaking, SteamNetworking P2P, SteamNetworkingMessages) |
| Saves | `%USERPROFILE%\AppData\LocalLow\Bennett Foddy\Getting Over It` - backed up to `~\.universal-modder\backups\getting-over-it-saves\` |
| Prior art | No current multiplayer mod. Old "Glorious Multiplayer" (Skipcast, 2017) is dead. Thunderstore has 11 small BepInEx mods (Free_Fly, Replay_Pot, Rising_Tide...) so BepInEx 5 works on this build. |

### Game classes that matter

- `PlayerControl` - input -> `HingeJoint2D hj` (hammer angle) + `SliderJoint2D sj` (hammer extension). `potMesh`, `tip`.
- `PoseControl` - draws the body: `dudeMeshHub`, `hammerMeshHub`, `potMeshHub`, IK hands, look target.
- `SaveState` - the whole player state: hinge/slider pos+vel, playerPos/Rot, 6 rigidbody positions/angles/velocities,
  timePlayed, speedrun flag.
- `Saviour` - save/load. `Load(SaveState)` restores any state; `ResetPlayerButNotDialogue()` restarts at the bottom.
- `ProgressMeter` - distance along a `CurvySpline` route (`progress`) -> exact "% to top" and split points.
- `DebugSpawn` - dev-only spawn points along the map (children sorted by name), disabled in release builds.
- `RewardLogic` / `GravityControl` / `WellDone` - the ending (space).
- `SteamManager` - Steam init; `MainMenuButton`/`Loader` - menu and scene flow (scene 0 = menu).

## Features and verdicts

| # | Feature | Verdict | How |
|---|---|---|---|
| 1 | Online multiplayer, 2-8 players at once | Feasible | Each player sends its pose ~20x/s over Steam P2P; others draw a ghost. Ghosts have no colliders. |
| 2 | See each other, no collision | Feasible | Ghost = cloned visuals only (no Rigidbody2D/Collider2D/scripts), semi-transparent, name tag, colour per player. |
| 3 | In-game "Multiplayer" button: host, invite, join | Feasible | A button on the main menu opens a small panel: Host lobby / Invite friends (Steam overlay) / Leave. Joining through a Steam invite or "Join game" on a friend works automatically. |
| 4 | Race mode | Feasible | Host starts: everyone resets to the bottom (`ResetPlayerButNotDialogue`), 3-2-1 countdown with input paused, shared host clock. |
| 5 | Autosplits | Feasible | Split points as route distance (`ProgressMeter.progress`) for the community's sections; per player, shown in a corner panel. |
| 6 | Live standings | Feasible | Corner panel: each player's % to top, current section, time. |
| 7 | Win + results | Feasible | First to reach the top (ending trigger / max progress) wins; results table for everyone. |
| 8 | Rematch | Feasible | Host button resets everyone and counts down again. |
| 9 | Practice | Feasible | Save/load state (`Saviour.Load`), named teleports (`DebugSpawn` points + your own marks). |
| 10 | Version check | Feasible | Check build/Application.version + required types; disable cleanly. |
| 11 | Launcher `.exe` + GitHub release | Feasible | BepInEx needs its `winhttp.dll` proxy in the game folder. The launcher adds it before launching and removes it on exit, so a normal Steam launch stays vanilla. |

## Testing alone

- Ghost rendering: record your own run and play it back as a ghost (no network needed).
- Networking: a dev-only UDP loopback transport so two copies of the game on this PC can talk.
- Steam lobbies/invites: need a friend (or a second PC/Steam account) for the final check.

## Extra ideas (all optional, off by default)

- **Ghost of your best run** to race against when practicing alone.
- **Spectate** a friend's camera after you finish (or while you're stuck at the bottom).
- **"Fell" alerts** in chat when someone loses a lot of height, with how much.
- **Catch-up rubber band**: none. (This is Getting Over It.)
- **Emotes** (a few keys: wave, laugh, point) shown above your ghost.
- **Shared fall counter / biggest-fall leaderboard** for the session.
- **Co-op "pull" mode**: no physics contact, but a team score = sum of heights.
- **Split export** to LiveSplit `.lss` / CSV.

## Build order

1. BepInEx 5 in a dev install, hello-world plugin, read the player.
2. Ghost from a recorded run (solo).
3. Network layer: UDP loopback for dev, Steam P2P for real.
4. Main menu button + lobby panel (host, invite, join, leave).
5. Race: countdown, reset, clock, splits, standings panel, finish, rematch.
6. Practice tools.
7. Launcher `.exe`, release zip, GitHub repo, plain README.
