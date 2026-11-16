# Monkey A/B comparison

- A_Restored: original lowpoly proportions with cream face restored.
- B_Reference: smaller head, dark chestnut coat, vertical eyes, crown tuft and curled tail inspired by the supplied reference.

Use the Unity prefabs under `Assets/MonkeyLowpolyComparison`, or open `Monkey_AB_Comparison.unity`. Each prefab includes explicit body and alpha-cutout facial materials plus a looping greeting preview. Do not assign the opaque body material to the expression submesh: its transparent pixels have black RGB data, and an opaque shader displays those pixels as a black face.

Validation: both variants were imported and rendered in Unity 6000.3.23f1 using the project's Built-in lowpoly shader. Facial material uses Standard Cutout, `_ALPHATEST_ON`, cutoff 0.3 and render queue 2450. Texture import retains alpha. Skinning and 91 bones were checked; the greeting clip was sampled in Unity.

The HighlandLake scene has not been changed by this comparison task. The existing scene's full Animator controller and action collection have not been reconnected/tested. B's new curled tail is weighted to the pelvis to retain its shape; it does not currently reproduce the original segmented tail animation. B is a reference-inspired remodel, not an exact reconstruction.

Raw GLB/FBX files and Blender source remain in `work/monkey_lowpoly_sample/compare`. FBX alone does not reliably transfer shader/transparency settings; use the delivered Unity prefab and accompanying material assets.
