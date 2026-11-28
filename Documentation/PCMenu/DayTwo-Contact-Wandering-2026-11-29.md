# Day 02: contact and wandering revision

The user reported that the scratcher only swung in the air and asked for more monkeys, with some wandering slowly. This revision affects the dedicated PC level and its journal information.

## Behavior

- Five monkeys instead of three. Three patrol nearby at 0.45, 0.525 and 0.55 metres per second; two stay at their clearings. Patrols include short pauses and use the existing walking animation.
- Approaching within 3.6 metres, an incoming banana, eating and celebration stop a wandering monkey. Terrain probes and body sweeps keep the short paths on walkable ground and away from obstacles. Patrol motion pauses with the game.
- The actual central claw tip is calibrated in the tool prefab. A stroke reaches the current animated body surface, scrubs against it and retracts over 0.66 seconds. The surface comes from the live skinned mesh, not the approximate collision capsule.
- A click starts the motion; it does not award care. A stroke counts only when the tip is within 15 millimetres of the animated surface. In the captured close-up the intended offset is 4 millimetres. Tool changes or leaving range cancel an unfinished stroke. Duplicate inputs during a stroke are blocked.
- Five monkeys receive four alternating requests each: ten feedings and ten scratching tasks. The round lasts 180 seconds with star goals 7 / 14 / 20. The journal, briefing, progress markers and results use the rules asset.

## Verification

Unity 6000.4.8f1, using the editor already installed on this computer. The repository's declared editor version is retained; exact-version testing remains outstanding.

The dedicated Play-mode validator passed 59 checks. It drives the real menu and scene, moves the character controller, throws animated banana projectiles and calls the same care methods as player input. Its completed run recorded all 20 tasks and three stars, including at least 30 confirmed claw contacts. It also checked three moving monkeys, two stationary monkeys, limited patrol bounds, cancellation, pause, replenishment, retry, separate day saves, collection unlock and return to Day 01.

The menu regression suite passed another 61 checks covering menu hover, animated page turns, collections, settings, pause, retry and Day 01 result routing.

Raw results: `DayTwo-Contact-Checks-2026-11-29.txt` and `Menu-Regression-Checks-2026-11-29.txt`.

This is a controlled playthrough, not a claim that a human completed a keyboard/gamepad playtest. Physical controller input still needs a teammate's playtest.

## Video

`C:/Users/xdm28/Desktop/5031/Zoo-PC-Day2-Contact-and-Wandering-Preview.mp4`

The video contains 3,746 frames at 30 fps (124.87 seconds), 1600 x 900, H.264, 27,227,680 bytes. It is a silent recording of the actual Unity camera. It shows all five monkeys, slow patrols, visible claw contact, feeding, replenishment, three-star results, retry and journal/collection navigation. Full decoding of the exported MP4 completed without errors.

The earlier three-monkey video is retained separately for comparison. Source frames and screenshots are in `C:/Users/xdm28/Desktop/5031/.menu-tools/day2-validation/`.

## Windows build

The Windows build succeeded at `Builds/PCDayTwo/A-Day-At-The-Zoo.exe`. A native-window smoke check used real mouse clicks to open Start, turn to Day 02, load the scene, begin the shift and select the scratcher. All five characters were visible and roaming characters changed position over time. The application closed normally; its log contains no managed exceptions or native crash.

The editor build log contains an internal Unity Search index exception during startup, plus existing package/shader warnings. The native log reports that an OpenXR runtime is absent on this PC; this did not prevent PC loading or play. VR packages and settings were not changed for this feature. These are distinct from the successful gameplay checks and build result.

Logs are local under `.menu-tools`: `day2-contact-validation3.log`, `day2-contact-build.log` and `day2-contact-player.log`. Repository editor/package settings and unrelated assets were restored after validation. This revision has not been pushed to GitHub.
