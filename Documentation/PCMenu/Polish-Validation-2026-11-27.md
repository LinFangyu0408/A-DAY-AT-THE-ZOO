# PC menu visual polish and collection catalogs

Tested on Windows with the installed Unity **6000.4.8f1**, built-in rendering pipeline. This revision follows local menu implementation `91eadb6`.

## Result

**61 Play mode checks passed, zero failed.** Exact assertions are recorded in `polish-play-mode-checks.txt`.

- Crafted logo and fabric button assets are assigned; the display font is Lilita One, matching the supplied HTML reference. Caveat provides handwritten notes.
- Hover changes button scale; particles, leaf drift, parallax, logo movement and screen fades were visually reviewed in the real Unity render.
- Animals contains one unlocked first-level monkey. Facilities contains one unlocked banana crate. Both portraits are isolated renders of the actual tutorial models with their original materials.
- Each catalog has five unknown placeholder slots. Locked entries expose neither a name nor an appearance, showing a padlock and `???` instead. The placeholders do not announce future content.
- Start and Continue open Day 01. Forward and backward book turns work; Day 02 is unavailable.
- Volume is applied and persisted. Reduced motion is checked through the real UI and makes book turns immediate; animated mode can be restored.
- The dedicated PC tutorial loads. Pause stops its clock, Resume continues, Retry creates a fresh round, and results Continue returns to the Day 01 journal.

Button checks raycast through the real uGUI event system before dispatching pointer events. The result check advances the actual round timer through editor-only test code; it is a navigation check, not evidence of a manual feeding playthrough. The validator restores temporary settings and progress, and is excluded from player builds.

## Visual evidence

`C:/Users/xdm28/Desktop/5031/Zoo-PC-Menu-Polished-Preview.mp4`

48.43 seconds, 1,453 frames, 1600 × 900, 30 fps, H.264, silent. Captured from the running Unity cameras. ffmpeg decoded all frames successfully. The older preview is retained separately.

The main-menu fabric button was additionally checked for unwanted background alpha. The final generated asset has transparent outer corners and no visible rectangular haze in the actual menu render.

## Standalone player

Windows x64 build succeeded. Output: `Builds/PCMenuPolished/A-Day-At-The-Zoo.exe`.

The new player was launched separately from the earlier preview. Native desktop mouse clicks verified Start, Animals, Facilities, their animated page turns, the correct model portraits and locked cards, and return to the main menu. Broader gameplay route checks were performed in Play mode as described above. Keep the executable with its data and DLL folders when sharing the playable build.

## Scope and limits

This revision changes only `Assets/ZooPCMenu` and `Documentation/PCMenu`. Original gameplay, VR, package declarations and repository Unity version remain unchanged. The build helper restores temporary shader-source modifications after producing the PC player.

A focused asset check passed: all feature files have paired metadata, feature GUIDs are unique, and all seven new art/font references resolve to their intended feature assets. Authored C# and Markdown pass `git diff --check`; original font license text is preserved verbatim.

The supplied project still contains an existing unresolved `Wooden crate_Lowpoly_White` GUID (`c1617a05d44cea542bd73f5635ddf181`), already recorded in the first validation report. It is unrelated to the correctly textured banana-crate catalog portrait. The editor also logged its existing Search initialization exception and shutdown allocation warnings; neither prevented validation or the successful player build.

Unity 6000.3.28f1, other operating systems and physical gamepad input have not been tested on this machine. The current catalog unlock flags live in `MenuAssets.asset`; future gameplay unlock events must update an appropriate persistent progress source when additional types are implemented.
