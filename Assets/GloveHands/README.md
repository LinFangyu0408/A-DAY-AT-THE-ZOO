# Quaternius 手套动作

组长要求的三个状态均已制作，原始 `Glove.fbx` 保留在 Source。原 FBX 没有骨骼，本资源新增手掌和五指蒙皮，提供左右手独立网格与动画。

## 使用

1. 导入 `GloveHands-Idle-Grip-Point.unitypackage`，或复制完整 `Assets/GloveHands` 文件夹及其 meta。
2. 打开 `Demo/GlovePoseDemo.unity`，运行后自动循环 idle、grip、point，也可以点击屏幕按钮单独检查。
3. 将 `Prefabs/Glove_Left.prefab`、`Glove_Right.prefab` 放到已有左右手跟踪挂点下。手腕位于原点，+Z 指向手指，+Y 朝向手背；按项目控制器的握持坐标调整父挂点位置和旋转。模型总长约 22 cm，可整体等比调整。
4. 由交互代码调用 `GlovePoseDriver.Idle()`、`Grip()`、`Point()`，或 `SetPose(GlovePose)`。也可以给 Animator 的整数参数 `Pose` 赋值 0、1、2。状态名为小写 `idle`、`grip`、`point`，约 0.18 秒平滑过渡，保持姿势直至下一次切换。

`idle` 保留原模型自然手形；`grip` 四指与拇指围合，用于抓握；`point` 食指伸出，其余手指收拢。每只手含三个独立 `.anim` 文件，可在 Animation 窗口继续编辑骨骼曲线。左右手使用独立蒙皮网格，没有负缩放。

## 可选 VR 手柄输入

预制体上的 `GloveXRInput` 默认关闭。需要使用手柄扳机映射时启用，Left / Right 已分别配置。

- Grip 按下时进入抓握；握物时同时按 Trigger 仍保持抓握。
- 仅按 Trigger 时进入指向；两个输入均松开时回到自然待机。
- 进入阈值 0.55、释放阈值 0.40，减少临界值抖动；设备无效时回到待机。
- 该组件只切换外观，不处理拾取、射线、传送或跟踪。现有交互系统也可以直接驱动 `GlovePoseDriver`，此时保持该适配器关闭，避免两个输入源互相覆盖。

`Wrist/Palm/GripSocket` 是示意握持挂点。演示中的蓝绿色握柄只用于检查姿势；具体香蕉、挠痒工具仍需按各自尺寸调整原交互系统中的握持偏移。

## 验证与演示

Unity 6000.4.8f1 中完成骨骼和曲线绑定、蒙皮权重、左右镜像、idle 原始形状、三个状态与连续中断切换、输入优先级和释放检查。指向姿势食指骨段夹角约 13°，保留低多边形手套的自然弯曲。

效果视频包含双手近景旋转检查和 `HighlandLake_Backup_2026-11-03.unity` 本地副本中的第一人称预览。场景内姿势输入由程序模拟，尚未进行实体头显及手柄验收。独立演示场景可以直接运行；完整 Highland Lake 环境不包含在此便携资源包中。

可在菜单 `Tools / Glove Hands / Rebuild hand poses` 重建资产。该操作会以代码中的默认关节位置与姿势覆盖 Generated、Animations 和 Prefabs 中的生成内容，手工调整动画后请先备份。重建保持已有 Unity GUID。

模型来源为组长提供的 “Glove by Quaternius - l1zv4LaA4I / Glove.fbx”。
