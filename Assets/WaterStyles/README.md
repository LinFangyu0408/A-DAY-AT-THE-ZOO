# Monkey Mountain Water Styles

Open `Assets/Scenes/Monekey Mountain.unity`.

Select the Hierarchy object named:

`WATER STYLE - SELECT HERE`

In the Inspector:

1. Use the `Selected Style` drop-down, or
2. Click one of the six numbered style buttons.

The water changes immediately in both Edit and Play mode.

The six material assets are also available in:

`Assets/WaterStyles/Materials`

You can assign any of those materials directly to the `Water Renderer` slot.
Keep the material used by the scene inside this material folder, because Unity
material assets outside the material slot can be shared by accident.

## Style map

| Option | Reference look | Main quality |
| --- | --- | --- |
| 01 Clear Sparkling | Turquoise lagoon with small glints | Clear water with slow normal flow |
| 02 Toon Shallow Pool | Saturated cyan/blue pool and white shore foam | Banded highlights and strong foam |
| 03 Translucent Low-poly Bay | Flat, glassy triangle planes | Strong facet normal and low opacity |
| 04 Teal Low-poly Tone | Dark teal underwater color | Denser tint, low reflection, subdued sparkle |
| 05 Bright Sparkling Shallows | Bright blue shallows with broad glare | Strongest moving specular and sparkles |
| 06 Midnight Low-poly Sea | Dark blue water with pale glints | Night palette, faceted surface, slow movement |

The two clear-lagoon reference images use the same visual family, so they share
option 01. The Low-poly Ocean surface and underwater references are represented
as options 03 and 04.

All styles use one built-in-pipeline shader. This avoids changing the project
from the Built-in Render Pipeline to URP merely to preview water.

## Custom placement

Move `WATER STYLE - SELECT HERE` in the Scene view to the pond or river area.
Change `Water Surface` width/length and the mesh resolution on
`WaterSurfaceMesh`. Keep `Apply In Edit Mode` enabled when trying presets.

If the selector is deleted, use:

`Tools > A Day at the Zoo > Install Water Styles`

and select the Monkey Mountain scene first.
