# Day 02: larger roaming area

The user clarified that roaming monkeys should move beyond the small fixed circles and that any interaction indicator should follow them.

## Changes

The old filled stone circles were static scenery, not hit areas. The previous short patrol radius (1.2-1.35 m) made the monkeys appear confined to those circles. All five circles and their radial stepping stones are now removed from Day 02.

Three monkeys choose reachable destinations 4.5-6 m from their initial positions, on a separate navigation surface covering the nearby 25 x 24 m clearing. They use complete NavMesh paths around scenery, at the same 0.45-0.55 m/s speed. Two monkeys remain stationary. Requesting animals stop when a keeper approaches; resting animals can continue passing by. Care actions and pause still stop motion. A navigation obstacle keeps walking monkeys from crossing the keeper.

The thin yellow/blue ring marks the currently selected monkey. It follows that character's feet and disappears when no animal is selected. It is not a hit-area visualization. The actual body collider remains a child of the character, the aim point is computed from the current transform, and the scratcher uses the live animated skin. Physics transforms are synchronized before targeting queries so agent movement does not leave hit detection at an older position.

The player boundary now covers the same larger clearing, keeping roaming monkeys reachable. Goals, 180-second duration, inventory and star thresholds are unchanged.

## Implementation and tuning

- `ZooDayTwoNavigation`: registers/unregisters only the Day 02 navigation asset when its scene loads/unloads.
- `ZooDayTwoBuilder.Navigation`: bakes the environment render geometry, with water excluded as non-walkable, and saves `Generated/DayTwoWalkableGround.asset`.
- `ZooDayTwoRoaming`: slow NavMeshAgent motion, destination selection, short pauses, avoidance and care stops.
- `ZooDayTwoTargetMarker`: one following selection ring; no collision component.

Navigation uses Unity's [NavMeshBuilder.BuildNavMeshData](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshBuilder.BuildNavMeshData.html) and [NavMeshAgent](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html). It does not add a package dependency or change the project's global agent settings. Each NPC's `roamRadius` controls destination sampling. Expanding beyond the present clearing also requires increasing `playArea` and rebaking the dedicated scene.

## Evidence

The controlled Unity Play-mode run passed 64 checks and completed ten feedings plus ten scratching tasks for three stars. At the opening roaming checkpoint, the three walkers had reached 4.323, 4.357 and 4.419 metres from their initial positions. Their accumulated travel was 7.372, 8.353 and 8.110 metres. This verifies actual displacement, not only an increased Inspector setting.

The checks also verified removal of fixed circles, navigation registration for all five monkeys, unchanged slow speeds, a following selection ring, zero horizontal offset between the interaction collider and moving character, range limits, claw contact, pause, restock, retry, separate progress, collection discovery and Day 01 loading. Raw results: `DayTwo-Free-Roaming-Checks-2026-11-29.txt`.

Test environment: the installed Unity 6000.4.8f1. Exact validation on the repository's declared 6000.3.28f1 and physical keyboard/gamepad playtesting remain outstanding. The editor emitted its existing Search startup exception before the gameplay validator; the gameplay checks completed successfully.

The new silent Unity-camera recording is `C:/Users/xdm28/Desktop/5031/Zoo-PC-Day2-Free-Roaming-Preview.mp4`: 4,237 frames, 30 fps, 1600 x 900, 141.23 seconds, 30,839,627 bytes. FFmpeg decoded the complete video without errors. The opening section shows the broader stroll before the care playthrough. Earlier videos are retained for comparison.

The Windows build completed successfully at `Builds/PCDayTwo/A-Day-At-The-Zoo.exe`. A native UI smoke check opened the menu, turned the journal to Day 02, entered the level, started the shift and switched to the scratcher. The scene displayed five monkeys without the fixed stone circles, walking monkeys changed position, and the selected monkey's thin ring changed from yellow to blue. The application then closed normally. Its log contained no C# exceptions or navigation-agent errors. Existing XR-runtime, duplicate-EventSystem and legacy ray-origin warnings remain; those shared systems were not changed by this update.
