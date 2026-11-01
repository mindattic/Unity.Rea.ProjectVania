# Unity.Rea.ProjectVania

A Unity 6 rebuild of *ProjectVania*, a 2009 XNA 3.0 (C#) 2D side-scrolling platformer prototype: a
Castlevania-flavored character (sprites named Morris, Shanoa, Leo and Richter) that runs, jumps,
kneels, slides and wall-slides across a 16-pixel tile grid in front of a painted mountain-farmhouse
backdrop, with a loose-follow camera, a slide-down typed console, a tile-painting map editor and a
main menu, all wrapped in a hand-built component "Engine".

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-black)](ProjectSettings/ProjectVersion.txt) [![Status](https://img.shields.io/badge/status-complete-brightgreen)](docs/BIBLE.md)

The rebuild is complete: every system the original had plays in Unity 6, matched to the original's
per-frame arithmetic and verified by an end-to-end scripted run, and the repo is ready to continue
development on a modern architecture (an engine-free, tested Core; data-driven tuning; everything composed
in code).

## Why

The original is an engine-first sandbox from 2009 whose platforming feel is worth keeping exactly:

- **Per-frame, hand-tuned platforming.** Acceleration 14000, a 200 px/s run cap, ground and air drag
  of 0.58 and 0.65 per frame, a fixed 0.6 s jump curve that rises about 76 px and that releasing the key does not shorten, a wall slide that caps
  at 83 px/s, a kneel on a hard landing and a slide out of a run.
- **A grid you can paint while you play.** One key flips the running game into a map editor: a tile
  palette, a cursor that scrolls the camera, left click to place a solid tile, right click to erase,
  and the player can be dragged around with the mouse.
- **A console that is part of the game.** A tilde-key console slides down over the world and runs
  `say`, `yell`, `fade`, `play music`, `mute`, `console text red` and friends.
- **Everything is a swappable component.** Texture, font, effect, audio, input, camera, scene, GUI,
  script and map-editor managers behind interfaces, handed to every object in one bundle.

See [`docs/RECONSTRUCTION.md`](docs/RECONSTRUCTION.md) for the full system-by-system record read from
the original C# source and content files, and [`docs/BIBLE.md`](docs/BIBLE.md) for the rebuild's
brief. Work is tracked as stories under epic `U.VAN-A` in
[`docs/USER_STORIES.md`](docs/USER_STORIES.md), written in build order.

## Status

Complete. Every story of epic `U.VAN-A` is built and verified (Core tests, EditMode tests, a headless
build and `unity.ps1 capture` screenshots); the BIBLE records each decision and every fixed defect of the
original. The stories, in the order they were built:

- Engine-free Core, a fixed 60 Hz simulation and the command/event model
- Data-driven tuning, content and settings registry
- Project hygiene: URP, Input System, Unity.Shared, code-built scenes
- Input bindings (keyboard and Xbox pad) per game state
- The 16 px collision grid and its queries
- Player physics and action state machine
- Player animation, characters and rendering
- Map, backdrop and tile rendering
- Camera (loose, locked, cursor, free; scroll, zoom, rotate)
- Game states and the main menu
- The typed console and its script language
- HUD, message stack, fade lens and debug readout
- Audio cues, music and mute
- Map editor
- Post-process effects
- Everything the original left unwired, kept as data
- Asset parity sweep and the end-to-end scripted verification run

## Build, test, run

Unity 6000.4.3f1 runs headless; nothing needs the Editor open.

| Command | What it does |
|---|---|
| `.\run-core-tests.ps1 [-Filter X]` | The engine-free Core tests (`dotnet test`, no Unity) |
| `.\unity.ps1 compile` | Imports and compiles the project in batch mode |
| `.\unity.ps1 test [-Filter X]` | The EditMode tests (results in `Logs\editmode.xml`) |
| `.\unity.ps1 build` | The Windows player in `Build\ProjectVania` |
| `.\unity.ps1 capture` | Renders the game's states to `Logs\Capture\*.png` (needs graphics) |
| `.\play.ps1 [-Sound] [-Build]` | Launches the built player, muted unless `-Sound` |

## Architecture

| Assembly | Holds |
|---|---|
| `Unity.Rea.ProjectVania.Core` | The rules, pure C# (`noEngineReferences`): the 60 Hz world, commands and events, characters, grid, camera, menu, console, HUD, editor, effects arithmetic and the settings registry |
| `Unity.Rea.ProjectVania.Runtime` | Presentation only: the code-built bootstrap, the Input System device, URP rendering, UI Toolkit views, audio |
| `Unity.Rea.ProjectVania.Editor` | Asset importers, the pipeline and scene setup, the batch build and the capture run |
| `Unity.Rea.ProjectVania.Tests` / `Tests/Core.Tests` | EditMode tests and the `dotnet test` Core suite |

Every tuning figure, text and content definition is a JSON file in `Assets/StreamingAssets/Settings`,
validated on load; state changes only through commands carrying a player id, so presentation never mutates
the game.

### Muted launches

`play.ps1` launches muted: it sets `MINDATTIC_CLI_MUTED=1` and passes `-muted`, so a CLI or agent starting
the game never blasts audio. Pass `-Sound` to launch with audio, `-Build` to rebuild first (`.\unity.ps1
build`), `-Wait` to wait for the player to exit. Either switch works on its own when you start
`ProjectVania.exe` (or the editor) yourself:

```
$env:MINDATTIC_CLI_MUTED = '1'; .\Build\ProjectVania\ProjectVania.exe
.\Build\ProjectVania\ProjectVania.exe -muted
```

The mute is the shared `com.mindattic.unity.shared` session mute: read before the first scene loads, it
makes the player profile's settings report music and SFX muted for that run only, and `SessionAudio`
silences the listener (the cue and song players follow audio.json and the console's `mute`, which still
works underneath). The player's saved `settings.json` is never written, so the next normal launch has
sound. The `-batchmode` runs in `unity.ps1` (compile, test, build, capture) are not playable launches and
are unaffected.

### Online

The game can be played online on the shared Unity.Multiplayer kit (BIBLE, **Online**). Local play is unchanged; the
online scene is opened with `-online` (or `.\play.ps1 -Online`) and offers Host, Join by IP, Join by code and Play
alone. Up to three players each drive their own character (Morris, Shanoa, Leo) in the host's one game, kept in step
by a lockstep the host orders. `Assets/Scenes/Online.unity` is written in code by **MindAttic > ProjectVania > Rebuild
Online Scene** (`Unity.Rea.ProjectVania.EditorTools.Online.OnlineSceneBuilder.BuildAll` in batch); `mp-host game smoke`
runs a dedicated server with two bots.

## Original source

`R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\ProjectVania` (the fuller copy; its `Engine`
project is the game) and `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\XNA 2D\ProjectVania`
(an earlier, smaller copy). Read-only reference, never edited by this project.
