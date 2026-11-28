# Day 02: patience attack

Previously, running out of patience only added one scratching stroke or one second of eating. The new response is a brief cartoon attack, followed by a return to normal care.

## Behaviour

- An unserved request attacks once when its 28-second patience expires. Feeding already in progress does not attack.
- The 4.2-second close-up uses the actual culprit's appearance, the asset's ATK1 and Angry clips, and expression 16. A render-only copy rushes towards the camera; the real NPC stays on its navigation surface.
- Five strikes trigger matching synthetic palm-impact sounds, a camera recoil, brief amber flashes, graphic impact bursts and optional gamepad rumble. The pounce has its own whoosh. These sounds are generated locally in the game and require no external download.
- Gameplay HUD and held tools are hidden. Movement, look, tool selection, feeding, scratching and restocking are blocked both in the session and the round model. An in-progress scratch is cancelled.
- The round clock, other animals' patience and in-flight bananas pause for the forced sequence. Existing extra-care penalties remain. There is no health system or game-over penalty added.
- Escape can pause the sequence. Returning to the journal or retrying cleans up the actor, camera, effects, sound and rumble. After normal recovery, interaction resumes and the same monkey can be helped.
- An overdue request cannot attack repeatedly. Separate overdue requests are handled oldest first with a short recovery gap. A finished shift cannot start a new attack.
- The existing reduced-motion setting removes camera shake and rumble and lowers flash opacity.

## Integration

`ZooDayTwoRules.cs` owns the pending/active attack state and rejects care mutations while active. `ZooDayTwoAttack.cs` connects that state to session input and recovery. `ZooMonkeyAttackView.cs` owns the temporary stage, manual animation graph, camera restoration and audio. `ZooAttackImpactGraphic.cs` draws the feedback without a texture dependency.

`Resources/ZooPCMenu/DayTwoAttack.asset` references existing character clips and an expression material. It can be regenerated with **Zoo / Day Two / Prepare patience attack assets**. Attack duration and impact timing are near the top of `ZooMonkeyAttackView.cs`; normal patience remains in `DayTwoRules.asset`.

The dedicated validator waits for real request expiry, checks interaction locks, pause and recovery, then tests reduced motion and leaving during a second attack. Its video/audio capture uses Unity's [AudioRenderer](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioRenderer.html) so impacts are recorded from the game output along with the camera frames.

## Validation

The dedicated attack run passed 48 checks in Unity 6000.4.8f1. It waited for the actual 28-second patience expiry, verified five timed impacts, checked submitted flash/speed-line mesh geometry, injected movement/refill/tool keys while locked, paused mid-attack, verified exact camera restoration, fed the original culprit afterward, tested reduced motion and left the scene during another attack. Raw results: `DayTwo-Patience-Attack-Checks-2026-11-29.txt`.

The existing complete Day 02 playthrough also passed all 64 regression checks after this change: ten feedings plus ten scratching tasks, three stars, roaming and contact checks, pause, restock, retry, collections and returning to Day 01. Raw results: `DayTwo-Patience-Regression-Checks-2026-11-29.txt`. The editor's pre-existing Search startup exception occurred before the gameplay validator; both gameplay runs completed successfully.

The run recorded 1,460 camera frames and 48 kHz stereo output directly from Unity. The shareable excerpt is `C:/Users/xdm28/Desktop/5031/Zoo-PC-Day2-Patience-Attack-Preview.mp4`: 22 seconds, 1600 x 900, 30 fps, H.264/AAC, 11,565,762 bytes. It shows patience expiry, the close-up sequence, a brief pause/resume check and feeding after recovery. FFmpeg decoded the entire excerpt without errors.

The Windows build succeeded at `Builds/PCDayTwo/A-Day-At-The-Zoo.exe`. A native UI check entered Day 02 through the journal, waited for natural patience expiry, visibly confirmed the attacking monkey and impact graphics, then confirmed the return to the normal camera/HUD and successful tool switching. The application closed normally. Its log contained no C# exceptions or attack/navigation errors; existing XR/runtime and legacy scene warnings remain.

Physical gamepad rumble and the repository's declared Unity 6000.3.28f1 have not been tested on this machine. The implementation uses the already-installed 6000.4.8f1; unrelated editor upgrades are excluded from the change.
