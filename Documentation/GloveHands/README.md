# 手套动作与效果视频

组长要求的三个手部状态已完成，并提供左右手版本。

- `idle` 保留所提供模型的自然手形。
- `grip` 四指与拇指围合，演示中使用握柄检查姿势。
- `point` 食指伸出，其余手指收拢。

[效果检查视频（26 秒，720p）](Glove-Idle-Grip-Point-Preview.mp4)

[便携 Unity 资源包](GloveHands-Idle-Grip-Point.unitypackage)

[项目内资源与接入说明](../../Assets/GloveHands/README.md)

拉取此分支即可获得完整 `Assets/GloveHands`，无需再次导入资源包。需要将手套导入其他 Unity 项目时，可使用上面的 `.unitypackage`。

演示入口为 `Assets/GloveHands/Demo/GlovePoseDemo.unity`。运行后可自动循环或点击按钮切换姿势。预制体位于 `Assets/GloveHands/Prefabs`，控制器使用整数参数 `Pose`，0/1/2 对应 idle/grip/point。

已在 Unity 6000.4.8f1 中完成 51 项资产、骨骼、蒙皮、状态切换及输入逻辑检查；实际 Play Mode 中，两只手均完成 idle/grip/point 自动切换。视频包含双手正反面近景及 Highland Lake 本地场景副本中的第一人称预览。视频中的姿势由程序切换，实体 VR 头显及手柄尚未测试。

本分支的新增内容集中在 `Assets/GloveHands` 和本目录。具体物品的握持偏移，以及与现有 VR 交互系统的连接方式，参见接入说明。
