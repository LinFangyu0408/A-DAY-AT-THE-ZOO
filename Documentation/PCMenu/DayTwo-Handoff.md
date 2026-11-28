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

Aim with the center reticle. Feeding works at range; scratching requires being within three meters. Each scratch requires a separate input and has a short cooldown. The tool is reusable. The supply indicator gives direction and distance to the banana stand.

## Rules and integration

`Assets/ZooPCMenu/DayTwo/Generated/DayTwoRules.asset` contains duration, capacity, patience, stroke count, interaction ranges, task count, concurrent-request phase and star thresholds. Current values are described in `DayTwo-Design.md`.

Both care types contribute equally to the score. Day 02 stores its result in `ZooPC.Day2.*`, leaving `ZooPC.Day1.*` intact. Retry reloads Day 02 with a fresh timer, inventory and task state. Returning from Day 02 reopens its journal page. Main-menu Start and Continue retain their existing behavior of opening Day 01.

Beginning Day 02 discovers `back-scratcher` in Facilities through `ZooPC.Collection.back-scratcher`. The other slots remain unknown. Animals continues to show the original unlocked monkey type. The back-scratcher portrait is rendered from the supplied 3D tool; its inventory illustration comes from the existing project artwork.

## Build and editing

Use **Zoo → PC Menu → Build Windows preview**. With Day 02 configured, the output is `Builds/PCDayTwo/A-Day-At-The-Zoo.exe`, containing the menu and both PC levels. This leaves earlier preview builds in their separate folders.

**Zoo → PC Menu → Clean build Windows preview** rebuilds scripts and player data with `BuildOptions.CleanBuildCache` when diagnosing stale or corrupt build output. It preserves the imported source assets; see Unity's [clean-build documentation](https://docs.unity3d.com/6000.4/Documentation/Manual/build-clean-build.html).

**Zoo → Day Two → Build dedicated Day Two scene** regenerates the dedicated scene and generated models from the supplied November 23 scene. Save any hand-edited Day 02 scene before using this rebuild command. Existing rule-asset values are preserved.

The Day 02 gameplay is isolated under `Assets/ZooPCMenu/DayTwo`. It reuses the team's models, clips, sound files and desktop movement controller. The original `MonkeyCare` runtime, VR files and original level scenes are not modified. Only the copied Day 02 scene disables its original care/VR components and binds the new PC session.

The dedicated stone pads use mesh colliders. Do not replace them with a primitive cylinder's default capsule collider: flattening the mesh does not flatten that capsule and can leave an invisible dome blocking throws and movement.

The Play-mode validator `ZooDayTwoValidation` checks the actual menu, scene, movement, care calls, animation timing and persistence. It drives a controlled playthrough and restores test save data afterwards; it is excluded from the player build.
