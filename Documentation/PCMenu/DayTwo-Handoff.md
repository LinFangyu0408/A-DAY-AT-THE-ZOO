# Day 02 PC handoff

Base: `v2-final` (`2fbb68d775bf1a95c97ce7423f6c8cccc446f622`). The PC menu work from `12f7d6b` is retained. The user confirmed feeding + scratching as the second-level direction.

## Open and play

Open `Assets/ZooPCMenu/Scenes/PC_MainMenu.unity`, press Play, choose Start, turn to Day 02 and select Begin Day 02. The new scene can also be opened directly:

`Assets/ZooPCMenu/DayTwo/Scenes/PC_DayTwoLakeside.unity`

The briefing explains the new tool before the timer starts. Day 02 is directly accessible for team testing. Day 01 remains playable from the previous journal page.

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Start shift | Space or button click | South / A |
| Move | WASD | Left stick |
| Look | Hold right mouse and move | Right stick |
| Select banana | 1 or tool card | West / X |
| Select scratcher | 2 or tool card | North / Y |
| Throw / individual scratch | Left click | Right trigger |
| Restock near the stand | E | South / A |
| Pause | Esc | Start |

Aim with the center reticle. Feeding works at range; scratching requires being within three meters. Each scratch requires a separate input and a complete 0.66-second reach, contact and retract cycle. The contact point follows the animated body mesh; a click alone does not count. The tool is reusable. The supply indicator gives direction and distance to the banana stand.

Three monkeys wander slowly across the clearing, with destinations up to 6 m from their initial position. They navigate around trees and rocks on a separately baked Day 02 NavMesh. Move close to a requesting monkey and it stops for care. Throws hold the intended monkey still and follow its current aim point until arrival. Two monkeys remain stationary. Pausing freezes both roaming and care animations.

## Rules and integration

`Assets/ZooPCMenu/DayTwo/Generated/DayTwoRules.asset` contains duration, capacity, patience, stroke count, interaction ranges, task count, concurrent-request phase and star thresholds. Current values are described in `DayTwo-Design.md`.

Both care types contribute equally to the score. Day 02 stores its result in `ZooPC.Day2.*`, leaving `ZooPC.Day1.*` intact. Retry reloads Day 02 with a fresh timer, inventory and task state. Returning from Day 02 reopens its journal page. Main-menu Start and Continue retain their existing behavior of opening Day 01.

Beginning Day 02 discovers `back-scratcher` in Facilities through `ZooPC.Collection.back-scratcher`. The other slots remain unknown. Animals continues to show the original unlocked monkey type. The back-scratcher portrait is rendered from the supplied 3D tool; its inventory illustration comes from the existing project artwork.

## Build and editing

Use **Zoo → PC Menu → Build Windows preview**. With Day 02 configured, the output is `Builds/PCDayTwo/A-Day-At-The-Zoo.exe`, containing the menu and both PC levels. This leaves earlier preview builds in their separate folders.

**Zoo → PC Menu → Clean build Windows preview** rebuilds scripts and player data with `BuildOptions.CleanBuildCache` when diagnosing stale or corrupt build output. It preserves the imported source assets; see Unity's [clean-build documentation](https://docs.unity3d.com/6000.4/Documentation/Manual/build-clean-build.html).

**Zoo → Day Two → Build dedicated Day Two scene** regenerates the dedicated scene and generated models from the supplied November 23 scene. Save any hand-edited Day 02 scene before using this rebuild command. The rebuild configures the requested five-monkey revision (180 seconds, star goals 7 / 14 / 20); other rule-asset values are preserved.

The Day 02 gameplay is isolated under `Assets/ZooPCMenu/DayTwo`. It reuses the team's models, clips, sound files and desktop movement controller. The original `MonkeyCare` runtime, VR files and original level scenes are not modified. Only the copied Day 02 scene disables its original care/VR components and binds the new PC session.

The old fixed stone circles and radial stepping stones have been removed. The small colored ring is a selection cue that follows the currently aimed-at monkey; it is not the hit area. Interaction uses the monkey's moving body collider/aim point and the live skinned surface for scratching.

The Play-mode validator `ZooDayTwoValidation` checks the actual menu, scene, movement, care calls, animation timing and persistence. It drives a controlled playthrough and restores test save data afterwards; it is excluded from the player build.

Patience expiry now triggers a 4.2-second close-up monkey attack with five synchronized impacts and temporarily locked controls. The original task remains recoverable afterward. See `DayTwo-Patience-Attack-2026-11-29.md` for timing, input locks and recovery. The dedicated entry point `ADayAtTheZoo.PC.Editor.ZooDayTwoBuilder.ValidateAttack` prepares clip references and records an attack test with game audio.

## Contact and movement implementation

`ZooDayTwoRoaming.cs` owns PC-only NavMeshAgent destinations, slow movement and stopping rules. `ZooDayTwoNavigation.cs` registers the dedicated baked navigation asset only while Day 02 is loaded; `ZooDayTwoBuilder.Navigation.cs` rebuilds it from environment render meshes, with water marked non-walkable. No global navigation settings or extra package dependencies are required. `ZooDayTwoTargetMarker.cs` follows the current target. `ZooDayTwoScratchContact.cs` samples one animated skin only while scratching; it does not add a live physics mesh collider or modify the shared monkey assets. `Scratch contact tip` in the generated tool prefab is calibrated from the actual central metal claw. The rules asset drives journal totals, briefing and results.
