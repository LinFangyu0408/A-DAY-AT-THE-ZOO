# Day 02 validation — 2026-11-28

Implementation baseline: `v2-final` at `2fbb68d775bf1a95c97ce7423f6c8cccc446f622`, with the existing PC menu polish from `12f7d6b`. Local feature branch: `feat/pc-day2-care`.

## Play-mode evidence

Unity 6000.4.8f1: **48 Day 02 checks passed** and **61 existing menu / Day 01 regression checks passed**. The configured Day 02 entry is now tested as available, replacing the old Coming Soon assertion.

| Area | Observed result |
| --- | --- |
| Round rules | Briefing consumes no shift time; one then two active requests; late requests remain recoverable; deadline stops play; finished rounds reject mutations. |
| Feeding | Aimed throw consumes one banana; the actual flight arrives; the eating animation runs before the care point is awarded. |
| Scratching | Out-of-range input is rejected; three separate nearby strokes succeed; duplicate same-frame strokes are rejected. |
| Supply / pause | Actual character-controller movement reaches the stand and refills the bag; distant refill is rejected; pause freezes time and blocks care actions. |
| Full care loop | Controlled playthrough completed all **6 feedings + 6 scratching requests = 12 / 12 tasks**, producing three stars. |
| Routing / saving | Day 02 retry resets its own scene; return opens its own journal page; Day 02 progress does not overwrite Day 01; original PC Day 01 still loads and returns correctly. |
| Collection | Starting the shift discovers the actual back-scratcher model in Facilities; banana crate remains discovered; other slots remain unknown and locked. |
| Existing UI | Fonts, artwork, hover, hidden collection information, reduced motion, real page turns, settings, pause, Day 01 retry and Day 01 result return pass the regression run. |

The Day 02 validator drives the actual runtime care methods, targeting, flight, animation timing and CharacterController movement. It does not directly award points or set completion counters. This is a controlled automated playthrough, not a human difficulty study. Input-device smoke checks are recorded separately below. Both validators restore the temporary progress/settings used by the tests.

## Recording

`Zoo-PC-Day2-Lakeside-Care-Preview.mp4` is saved beside the project in the `5031` folder: **1600 × 900, 30 fps, 2,620 frames, 87.33 seconds, 20,696,629 bytes**. All frames were decoded successfully after encoding.

The recording uses actual Unity camera frames from the Day 02 playthrough. It includes the journal, briefing, both care interactions, pause, supply refill, three-star result, retry and the discovered-tool collection. The recording is silent; gameplay audio remains implemented in the player. Raw snapshots and check logs are in the local `.menu-tools/day2-validation` folder.

## Scope and compatibility

The new scene and gameplay live under `Assets/ZooPCMenu/DayTwo`; supporting changes are limited to the PC menu and its documentation. The original source scene, shared `MonkeyCare` scripts and VR files are not changed.

The project still declares Unity 6000.3.28f1. Validation used the user's installed 6000.4.8f1; running this version on 6000.3.28f1 remains unverified. The PC build temporarily disables XR startup and uses the existing scoped shader-build workaround, restoring shared settings and shader sources afterwards.

The editor emits existing Search initialization / unavailable XR-runtime diagnostics and shutdown allocation diagnostics. These are not reported as successful runtime checks. The new gameplay validator terminates on runtime exceptions.

## Standalone loading defect found during testing

The first independent Windows run crashed when loading Day 02. A clean build reproduced the failure, so cache cleanup was not the fix. Resolving the native stack with the installed Unity PDB identified a `StreamedBinaryRead` / `MonoBehaviour` field-read failure. Inspection of the built scene found a scene-local `CarePickupMarker` script whose component had only the 32-byte MonoBehaviour header: its `Vector3 anchor`, `phase` and `anchorReady` payload was absent. The Editor could not resolve this inherited component, while the player resolved its class and attempted to read the missing fields.

The Day 02 scene generator removes that unused legacy marker; the new session already supplies its own marker. `ZooPCSceneScriptRepair` removes the same invalid embedded component/script records from the dedicated PC Day 01 copy before a build. The existing Day 01 `CareFarmEnvironment` creates a valid marker at runtime, as it already does when the Editor cannot resolve the inherited component. The repair only accepts `Assets/ZooPCMenu/*.unity` descendants and preserves other scene data. This compatibility fix changes neither the shared original component nor the original source/VR scenes.

## Final Windows player checks

The final Windows x64 build succeeded. Inspection of both built scene files confirmed that neither contains the malformed embedded marker payload. Native-window checks then verified:

- Start opens Day 01, real page turning reaches Day 02, and Begin Day 02 loads its briefing successfully.
- The briefing button starts the live timer; the scratcher button switches the visible held tool.
- A mouse click outside scratching range shows `MOVE CLOSER TO SCRATCH`; late requests increase the required strokes.
- An unattended shift ends at the deadline with zero stars; Work Log returns to Day 02 and records that result.
- Previous returns to Day 01; Begin Day 01 loads the original Ready / Go tutorial screen without a native crash.

The final player log contains no C# exception or scene-corruption crash. It still emits existing unavailable-XR-runtime, sample ray-origin and duplicate-EventSystem warnings inherited from the project. The native window was closed normally after checking.

The desktop automation tool's injected keyboard presses did not register in the Unity player, so this run does **not** claim a hardware keyboard or gamepad playtest. Keyboard/gamepad bindings are implemented; their device-level verification and a human difficulty playtest remain outstanding. The complete three-star care playthrough and pause/retry checks above were verified through the automated Unity runtime driver.

The final source diff is confined to `Assets/ZooPCMenu/` and `Documentation/PCMenu/`. Unity's unrelated material/package/settings upgrades were restored, including the original 6000.3.28f1 version declaration. Raw automated check lists are attached as `DayTwo-Checks-2026-11-28.txt` and `Menu-Regression-2026-11-28.txt`.
