# Highland Lake / Low Poly Zoo Environment

## 打开场景

在 Unity 6.3 主工程中打开：

`Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake.unity`

场景默认相机使用宽景构图。按 Play 后，湖面波浪、瀑布和低多边形草地会轻轻摆动。

## VR 与桌面移动

- 未连接头显时自动使用 `Desktop Walking Player`。
- 桌面与 Meta XR 玩家加载后都会自动落在 `Island_1` 的有效碰撞表面。
- 桌面相机眼高与参考场景一致，为地面以上 `1.70 m`。
- `W/A/S/D` 行走，`Shift` 加速；按住右键拖鼠标转向。
- 滚轮上下移动；按住滚轮并拖动鼠标，可前后左右平移。
- 连接头显或启用 XR 模拟器后自动切换到 Meta XR 综合 Interaction Rig。
- VR 沿用现有 Dreamcore 移动组件，仅扩展摇杆映射：
  - 左摇杆前后移动、左右横向平移；基础速度 `1.20 m/s`，死区 `0.20`。
  - 左摇杆推到死区外接近满幅时冲刺，倍率 `2.25`；点按左摇杆可切换持续冲刺。
  - 右摇杆向左/右轻拨瞬转 `30°`；保持推住超过 `0.18 s` 后连续转向。
  - 右摇杆向前/后进入传送瞄准；白色手柄射线负责指向地面并判定落点。
  - 白色射线落点为可站立表面时显示青色落点标记，不可站立时显示红色标记；松手传送到有效落点。
  - 自定义蓝色 `Dreamcore Teleport Arc` 不再显示，移动仍由白色射线控制。
  - 点按右摇杆切换数字下蹲；按左手柄 `Y` 重置视角。
  - 传送发射速度 `7.00`，落地前仍进行人体胶囊空间检查，湖面不可传送。
- Meta 自带 `FirstPersonLocomotor` 已停用，避免和 Dreamcore 风格输入同时触发。
- VR 相机链路为 `[BuildingBlock] Camera Rig > Meta XR Interaction`。综合 Interaction Rig 已显式绑定外部 OVRCameraRig、CenterEyeCamera 和左右眼 Anchor。
- OVR Camera Rig 使用 Floor Level 原点，场景根位置为地面上方 `0.03 m`；实际眼高由头显提供。

如果 Quest 2 只看到黑屏和小漏斗：

1. 停止 Play，保存当前改动，然后在 Project 中重新打开本场景。
2. 确认 `[BuildingBlock] Camera Rig` 在 Hierarchy 中是激活状态。
3. 确认 Quest Link 中头显已连接，且项目的 Standalone OpenXR Loader 为启用状态。
4. 再按 Play。控制台不应再出现 `FirstPersonLocomotor` 缺少 Player Origin 的断言。

修改前场景备份：`Scenes/HighlandLake_BeforeVR.unity`。需要重新应用玩家和手柄时使用
`Tools > Environment > Setup VR Walking and Teleport`。

## 场景内容

- Low-Poly Forest Nature Set：不同造型的乔木、灌木、草丛、花、苔石。
- Low Poly Island Pack：木桥、岩壁上的假山石，以及少量热带树形变化。
- 可游玩地面为 `Y=0` 平地面，仅在湖泊椭圆范围内凹下形成湖盆。
- 湖面 `Y=-0.18 m`，湖底 `Y=-1.25 m`；湖水为不透底的双层蓝色低多边形水面。
- 程序化低多边形风动草地包含 `8000` 簇、`35995` 片草叶，并合并为一个网格以降低 VR 绘制开销。
- 出生点 `(0, -20)` 周围另有高密度草地区域，避免第一人称视角下草叶过于稀疏。
- 环形土路、瀑布岩台、远景环山和云团保留。

## Shader

`Shaders/HighSaturationLowPoly.shader` 是场景对象与地形的统一风格 shader：

- 可调饱和度、明度、环境光强度。
- 两段式光照和轻微边缘光，保留明显的大色块。
- 支持原模型贴图、单色材质和顶点色。
- 材质中的 `_Tint` 可给同一模型做色调变化。

瀑布使用 `Shaders/StylizedWaterfall.shader`。

草地风动使用 `Shaders/LowPolyWindGrass.shader`：

- 顶点风动只作用于草叶上半部，底部保持固定。
- 世界坐标波叠加局部轻颤，形成轻微、不同步的微风效果。
- 单片草叶为低多边形三角面，最终全部合并到 `Low Poly Wind Grass`。

湖水使用 `Shaders/DeepLakeWater.shader`，不需要读取屏幕深度和湖底颜色。

## 重建

菜单 `Tools > Environment > Create Highland Lake Scene` 会按同一布局重建场景并刷新预览图。场景内容都在 `Environment Aseets` 下，原素材包不需要再次导入。

## 开源参考与许可

草地风动调研参考了 GitHub 上的 MIT 开源项目
[elringus/grass-bending](https://github.com/elringus/grass-bending)。该插件主要服务 Unity Terrain 的 alpha billboard，本项目使用自定义 Mesh 地面，因此没有引入其运行时依赖，而是用项目内低多边形草网格和自写 Shader 实现同一类轻风摆动。

原 Stylized Water 2.0 仍保留为素材/参考，Copyright (c) 2019 Daniel Shervheim，BSD 3-Clause License。完整许可见 `../02_第三方环境资源包/Stylized Water/LICENSE.txt`；当前湖面已改用本项目的不透底 Shader。

Low-Poly Forest Nature Set 和 Low Poly Island Pack 的使用仍受各自 Asset Store 授权约束。
