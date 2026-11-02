# Treehouse Zoo Diorama

This is a procedural low-poly reconstruction of the supplied reference image.

- Unity scene: `TreehouseZooDiorama.unity`
- Unity prefab: `TreehouseZooDiorama.prefab`
- Imported model: `Meshes/TreehouseZooDiorama.fbx`
- Unity materials: `Materials/`
- Editable source: `../../04_源文件与构建记录/TreehouseZooDiorama~/Blender/TreehouseZooDiorama.blend`
- Generator: `../../04_源文件与构建记录/TreehouseZooDiorama~/Blender/build_treehouse_diorama.py`
- Blender preview: `../../04_源文件与构建记录/TreehouseZooDiorama~/TreehouseZooDiorama_Preview.png`
- Unity verified preview: `../../04_源文件与构建记录/TreehouseZooDiorama~/QA/Treehouse_Unity_Preview.png`

The central treehouse, deck, ladder, fences, rocks, grass, foreground terrain,
background trees, and distant mountain forms are separate mesh objects. They can
be hidden, replaced, or detached in Unity without rebuilding the rest of the
scene. The source folder ends with `~`, so Unity ignores the Blender source and
the preview while the FBX remains a normal imported asset.

Open `Assets/Environment Aseets/02_第三方环境资源包/TreehouseZooDiorama/TreehouseZooDiorama.unity`
to view the imported scene. To regenerate its lighting and camera wrapper from
the FBX, use `Tools > A Day At The Zoo > Rebuild Treehouse Diorama Scene`.
The regeneration command builds the scene additively and saves a new scene; it
does not replace or save the currently open scene.
