# Day 02 — Lakeside Care

Development basis: the team's `v2-final` branch at `2fbb68d`, using `HighlandLake_Backup_2026-11-23.unity`. The existing PC menu and journal are retained. Day 02 is a separate scene; the team's source scene and VR implementation are retained.

The supplied chat specifies the version and scene. The user subsequently confirmed feeding plus scratching, with a reusable back scratcher, as the second-level direction. All tuning lives in a dedicated Day Two rules asset.

- Five monkeys, four alternating requests each: ten feeding and ten scratching tasks in total.
- Three-minute shift. One active request initially; up to two after 45 seconds.
- Six bananas carried. Missed throws consume a banana; the supply stand refills the bag.
- Three of the five monkeys stroll at 0.45-0.55 m/s across the walkable clearing, choosing destinations up to 6 m from their starting position; two remain stationary. Paths use the dedicated baked navigation mesh to go around scenery. Requesting animals stop when a keeper approaches within 3.6 m; resting animals can continue walking past. Incoming bananas, eating and celebration also stop movement. The player is a navigation obstacle. Fixed stone circles and radial stepping stones are removed; a thin ring marks only the current selected monkey and follows its feet. That ring is a selection cue, not a hit area; the body collider, aim point and scratch surface follow the moving character.
- A reusable back scratcher. Move close, aim at the itchy monkey, and perform three separate strokes. The calibrated central metal claw reaches the animated torso, scrubs along its surface, then retracts. Progress is awarded only during contact (within 15 mm of the current baked skin), never when the click starts. Switching tools or moving out of reach cancels an uncompleted stroke. A minimum stroke interval prevents click spam from resolving the task in one frame.
- Patience warning after 28 seconds; a late request needs one extra stroke or one extra second of feeding. It remains recoverable.
- Both task types earn one care point. Star thresholds: 7 / 14 / 20. Two stars is passing.
- Mouse and keyboard: WASD move, hold right mouse to look, 1 banana, 2 scratcher, left mouse use, E restock, Esc pause. Gamepad controls are shown in-game.
- A pre-round briefing introduces the new tool before the clock starts. Restart resets the current day. Results return to the Day 02 log entry.
- Animals stays an unlocked-species collection. The new care tool is added to Facilities when the Day 02 shift begins; unencountered entries remain unknown.

Day 02 is available directly from the next journal page for this team playtest. No completed Day 01 save is required to inspect the new work.
