# PC menu and keeper journal validation

Baseline: `handoff/2026-11-23`, `2fbb68d775bf1a95c97ce7423f6c8cccc446f622`.
Local Unity editor: **6000.4.8f1**, Windows, built-in rendering pipeline.

## Play mode — 41 checks passed

The validator runs the real Unity scenes, uGUI event system, book Animator and gameplay session. Button checks first raycast their screen center and verify the intended Button is the top hit, then dispatch its pointer-click event.

- Main menu boot; Start and Continue both open Day 01.
- Animated forward and backward turns; Day 02 cannot load a scene.
- Animals and Facilities guide tabs; return to menu.
- Volume applies immediately and survives closing and reopening Settings.
- Load the dedicated PC tutorial scene.
- Pause freezes the real round clock; Resume continues it.
- Retry loads a fresh Day 01.
- Real round results show; Work Log returns to the Day 01 journal.

The automated result check advances the real round timer to its cutoff through test code. It verifies navigation, not a completed feeding playthrough. Test settings and saved progress are restored afterwards. Editor-only validation code is excluded from the player build.

## Recorded evidence

`Zoo-PC-Menu-and-Journal-Preview.mp4`: 45.70 seconds, 1600 × 900, 30 fps, H.264, 1,371 frames, silent. Rendered from the actual running Unity cameras; every frame decoded successfully in ffmpeg verification.

The video covers the menu, physical page turn, locked Day 02, guides, settings, PC tutorial, pause, retry, results, and return to the journal. The book's page textures follow the supplied mesh's UV range so right-hand content remains visible while turning.

## Windows player — build and launch passed

The Windows x64 player built successfully with Unity 6000.4.8f1 and launched without a headset. In the actual desktop window, mouse clicks on Start and Next opened the journal and animated the turn to the locked Day 02. The Settings panel was also observed in the running player. The broader route, pause, retry and result checks above were performed in Play mode.

Output: `Builds/PCMenu/A-Day-At-The-Zoo.exe`. Keep the executable together with its generated data and DLL folders when sharing the playable build. The video can be shared as a single MP4.

## Scope

Feature assets and scripts are isolated under `Assets/ZooPCMenu`; the supplied book runtime is under `Assets/EndlessBook`. The tutorial is a dedicated scene copy with a PC navigation bridge. Shared gameplay scripts, original tutorial/VR scenes and the repository's Unity version and package declarations are retained.

Unity 6000.3.28f1, other operating systems and a physical gamepad have not been tested on this machine.

Asset-reference audit: 209 distinct per-file references checked, no new GUID conflicts or orphan `.meta` files. The source tutorial already has one unresolved model GUID (`c1617a05d44cea542bd73f5635ddf181`, `Wooden crate_Lowpoly_White`); this existing reference is also present in the dedicated scene copy. No menu or book asset references are missing.
