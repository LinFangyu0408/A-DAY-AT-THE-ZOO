# Environment Aseets 资源分类

## 目录说明

- `01_HighlandLake_可玩场景`
  Highland Lake 场景及其模型、材质、贴图、Shader、编辑器脚本、预览图和验证文档。
  入口场景：`01_HighlandLake_可玩场景/Scenes/HighlandLake.unity`

- `02_第三方环境资源包`
  已导入 Unity、可直接在项目中引用的第三方资源，包括 Low Poly Island、POLYGON
  Tropical Jungle、Farland 森林、Parametric Trees、Monkey Habitat Effects、
  Treehouse Zoo Diorama 和 Stylized Water。

- `03_UnityPackage安装包`
  保留的原始 `.unitypackage` 安装包；需要重新导入或更换 Unity 项目时使用。

- `04_源文件与构建记录`
  Treehouse Zoo Diorama 的 Blender 源文件、构建日志和 QA 预览。该目录名以 `~`
  结尾的部分资源会被 Unity 忽略，这是预期行为。

## 使用提示

- 移动或重命名整个资源包时，请同时移动同名 `.meta` 文件。
- `01_HighlandLake_可玩场景/Editor` 内包含场景重建和验证菜单；如再次移动该目录，
  需要同步修改其中保存的 `Assets/...` 固定路径。
- 第三方资源包保持完整目录结构，避免破坏材质、Prefab、Shader 和脚本之间的内部引用。
