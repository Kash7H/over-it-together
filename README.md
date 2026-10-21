# Over It Together

Online multiplayer for Getting Over It with Bennett Foddy. Climb at the same time as your friends, see each other on the mountain, and race to the top with live speedrun splits.

## Install

1. Download `OverItTogether-vX.zip` from [Releases](../../releases/latest).
2. Unzip it anywhere.
3. Double-click `OverItTogether.exe`. The game starts with the mod on. Keep the small window open while you play; it puts your game back to normal when you quit.

To play without the mod, launch Getting Over It from Steam as normal.

If Windows shows "Windows protected your PC", click **More info**, then **Run anyway**.

## Playing with friends

1. Everyone starts the game with `OverItTogether.exe`.
2. One player clicks **Multiplayer** on the main menu (or presses **Esc** in game), then **Host a lobby** and **Invite friends**.
3. Friends accept the Steam invite.
4. The host opens the **Race** tab and clicks **Start race**.

Friends must already have the game open through `OverItTogether.exe` when they accept the invite. If Steam starts the game for them, it starts without the mod.

## Features

- **Online multiplayer:** up to 8 players climbing at the same time.
- **No collisions:** other players are shown as tinted copies with name tags. You can't bump into each other.
- **Races:** the host starts a race. Everyone is moved to the start, a countdown runs, then go.
- **Live splits:** the speedrun community's sections (Tutorial, Chimney, Slide, Furniture, Orange Hell, Anvil, Bucket, Ice Mountain, Tower), with the same trigger points as the LiveSplit autosplitter. They're timed for every player and shown in the top left.
- **Standings:** everyone's progress to the top and current section, plus results at the end.
- **Practice:** save and load your position, and teleport to any section.
- **Your own climb is safe:** races and practice never touch your save unless you choose to keep the new position.
- **Update-safe:** if the game updates, the mod turns itself off instead of breaking the game.

## In-game menu

Press **Esc** (or **F1**) in a level to open the menu. **Main menu** is a button inside it.

| Tab | What's in it |
|---|---|
| Multiplayer | Host a lobby, invite friends, see who's playing, leave |
| Race | Start or cancel a race, pick the finish line, results |
| Practice | Save and load position, teleport to a section, back to the start |
| Settings | Show or hide other players, name tags, the HUD and messages |
| Controls | Keys and a quick how-to |

After a race or practice, the menu offers two choices:
- **Return to my own climb** puts you back exactly where you were.
- **Keep this climb** makes where you are now your real progress.

Finishing a race never plays the game's ending.

## Status (v0.2)

- **Tested in the real game:** players seeing each other, races, splits, results, practice tools, and save protection. These were tested with two copies of the game on one PC.
- **Not yet tested:** Steam lobbies and invites between two real players, and the in-game menu visuals.

Report bugs in [Issues](../../issues).

## Building from source

Requires Getting Over It (Steam) and the .NET SDK.

```powershell
.\tools\build-release.ps1
```

This downloads BepInEx, builds the mod and the launcher, and writes `release\OverItTogether-v<version>.zip`.

| Folder | Contents |
|---|---|
| `src/` | The mod (BepInEx 5 plugin, C#) |
| `launcher/` | Source for `OverItTogether.exe` |
| `docs/` | Player guide; `docs/dev/` has technical notes |
| `tools/` | Build script |

Uses [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1). Contains no game files.
