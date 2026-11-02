# Monkey Habitat Effects

Runtime environmental systems for `Assets/Scenes/Monekey Mountain.unity`.
The scene root object `Monkey Habitat Effects` owns the tuning values and
creates three child systems when Play starts.

## Included systems

- **Living flower garden** (`MonkeyLivingGarden`): a four-state
  `Rest -> Bud -> Bloom -> Recover` Moore-neighbour automaton. Exposure to two
  or more blooming neighbours wakes a dormant cell. Mature blooms periodically
  sow a new cell downwind.
- **Wind response** (`MonkeyWindSway`): the five generated `samor_Tree_*`
  trees receive small, individually phased world-space sway.
- **Rule coloring**: active garden cells receive distinct Bud, Bloom and
  Recover colors. The shader also colors flower stems, leaves and petals by
  vertex color.

The garden adds temporary `MeshCollider` components to the largest mountain
mesh surfaces while placing its grid, so its cells follow the actual terrain.
Those colliders also make the local habitat ground usable by future gameplay
physics.

## Tuning

Select `Monkey Habitat Effects` in the scene:

- `gardenCenter`: center of the flower grid in world space.
- `windDirection`: shared direction for flowers and tree sway.
- `gardenColumns` / `gardenRows`: cellular garden dimensions.
- `swayGeneratedTrees`: enables or disables wind sway on the five generated
  trees.

The scene backup made before adding this component is:

`Assets/Scenes/Monekey Mountain.backup-2026-11-02-pre-habitat-effects.unity`

The systems are runtime-only, so generated clouds and flowers do not
clutter the saved scene hierarchy.

The Polyverse skybox and its original cloud assets are left untouched. The
project no longer creates runtime cloud meshes.
