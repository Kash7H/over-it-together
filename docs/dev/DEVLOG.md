# DEVLOG - Over It Together

Resume here. Newest first.

## Paths
- Game: `C:\Program Files (x86)\Steam\steamapps\common\Getting Over It` (Unity 2020.3.25f1 Mono, game 1.7, build 8111718). BepInEx 5.4.23.5 installed there for dev (vanilla file list: `_private\tools\vanilla_root_listing.txt`).
- Real save = registry `HKCU\Software\Bennett Foddy\Getting Over It` (SaveGame0/1 XML, NumSaves). Backup: `_private\save-backups\*.reg`. Check with the goisave.py snippet (decodes playerPos/timePlayed).
- Decompiled game code: `_private\decomp\csharp` (never commit).
- Dev scripts: `_private\test.ps1` (single copy), `_private\dual.ps1` (two copies over loopback, needs steam_appid.txt which it creates; delete after), `_private\arrange.ps1`, `_private\shots.ps1`.
- Dev commands: write lines to `BepInEx\config\oit_cmd_<port>.txt` (race [s], finish N, cancel, tp SpawnNN, tpxyz x y, top, save, load, start, where, panel).

## 2026-10-21 - Session 1b
- Splits now = the community splits from the official LiveSplit autosplitter (Tairoon GettingOverIt.asl): Tutorial, Chimney, Slide, Furniture, Orange Hell, Anvil, Bucket, Ice Mountain, Tower (race finish by default; host can pick lower). Order-independent like the ASL. Built, NOT yet tested in game (last test run was cut off).
- "Keep this climb" button added next to "Return to my own climb".
- Game folder restored to vanilla. Dev BepInEx install parked in `_private\dev-install` (move its contents back into the game folder to resume). Real save verified untouched after every test.
- Next: test new splits + panel in dual copies (low areas only), then launcher exe, release zip, GitHub repo, plain README.

## 2026-10-21 - Session 1
Done and verified in the real game:
- Ghosts: pot + body (45 bones, rot+pos) + hammer head + pole (skinned, 2 external bones -> stand-in bones). Captured at end of frame (after PoseControl IK). Tinted, name tag, no colliders.
- Loopback transport: two copies see each other, HUD standings, feed.
- Race: host countdown (all reset to start, input frozen), GO, per-player splits broadcast, winner, results. Host-chosen finish line.
- Progress guard (Guard.cs): any race/teleport/practice load first stores your climb and pauses game saving; "Return to my own climb" restores it. Blocks NumWins + Steam achievements/stats while holding. Ceiling y 420 keeps players out of the ending.
- Game pauses when unfocused -> mod sets Application.runInBackground while in a lobby.

Not yet verified:
- Steam lobby / invite / P2P with a real second account (needs a friend).
- Main menu Multiplayer button and F1 panel visuals (code written, not screenshotted).
- Split names are placeholders (Split 1..10 at Spawn03..Spawn21, Finish at Spawn22). Rename in BepInEx/config/OverItTogether.splits.txt.

Next:
1. Screenshot/verify menu button + panel + guard "Return to my climb" in a single dev copy (OIT_DEV_PORT set so it can't save).
2. Launcher exe (BepInEx winhttp.dll proxy added on launch, removed after exit) + release zip + GitHub repo + plain README.
