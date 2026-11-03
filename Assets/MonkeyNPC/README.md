# 猴子动作库

Unity 项目的版本配置保持为 `6000.3.23f1`。功能分支：`feat/monkey-animation-library`。

## 当前动作来源

| 状态 | 来源 | 播放方式 |
| --- | --- | --- |
| StandingIdle | Suriyun `IdleA` | 循环待机 |
| SittingIdle | Suriyun `Sit` | 坐下后保持末帧 |
| Hungry | Mixamo `Stomach Hit`，裁取第 8–35 帧 | 双手靠近腹部，循环 |
| Eating | Mixamo `Drinking`，裁取第 10–92 帧 | 手抬到嘴边并切换咀嚼表情，播完进入 Happy |
| Happy | Suriyun `Victory` | 单次，播完回到 StandingIdle |
| Walking | Suriyun `Walk` | 原地循环 |
| Running | Suriyun `Run` | 原地循环 |
| Scratching | Mixamo `Zombie Scratch Idle` | 单次，播完回到 StandingIdle |
| Hi | Suriyun `Hi` | 保留射线打招呼所需的状态名，单次后回到 StandingIdle |

## 接入 Mixamo

公开分支附带可直接预览的 6 个已有动作。另 3 个动作由项目成员登录 [Mixamo](https://www.mixamo.com/) 下载到本地。Adobe 允许项目组内共享这些素材，并限制公开分发源文件。参见 [Adobe 的 Mixamo 素材分发说明](https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400)。

- `Assets/MonkeyNPC/Animations/Mixamo/Hungry.fbx`
- `Assets/MonkeyNPC/Animations/Mixamo/Eating.fbx`
- `Assets/MonkeyNPC/Animations/Mixamo/Scratching.fbx`

下载时选择 30 FPS。Unity 导入面板的 Animation 页签中，将 Hungry 裁取到第 8–35 帧，将 Eating 裁取到第 10–92 帧，Scratching 使用整段。应用设置后运行 `Tools > Monkey NPC > Build Complete Library`。生成的完整控制器、Prefab 和动作库用于本地或组内协作。更新公开分支前，在不含 Mixamo FBX 的工作副本运行 `Preview Available Animations`，确保控制器和库只引用可公开的动作。

生成器为每个 Mixamo 骨骼创建自己的 Humanoid Avatar，再重定向到猴子的 Avatar。生成的猴子 Prefab 使用 Suriyun 棕色 `01` 外观。`MonkeyHandContact` 根据猴子手臂长度修正捂肚子和进食时的手部落点。`MonkeyFace` 根据当前动作切换原包面部材质，包括待机眨眼、饥饿、咀嚼、开心、困惑和打招呼。

## 在 Unity 中生成和预览

1. 等待资源导入、C# 编译完成。
2. 运行 `Tools > Monkey NPC > Preview Available Animations`。准备好组内 Mixamo 文件后，可运行 `Build Complete Library`。
3. 在 `Generated` 目录中查看动作 `.anim`、`MonkeyNPC.controller`、动作库 `.asset` 和猴子 Prefab 变体。
4. 运行 `Tools > Monkey NPC > Create Demo Scene`，进入 Play Mode，逐项检查动作按钮及喂食流程。
5. 在主场景选中猴子，运行 `Tools > Monkey NPC > Apply Library to Selected Monkeys`，检查后保存场景。此操作支持 Undo。

只需要检查当前已导入的动作时，可以运行 `Preview Available Animations`。

生成器修改自有目录中的副本，保留 Suriyun 原始 FBX、控制器、材质和 Prefab。重复构建会更新已有动作文件，保留其 `.meta` GUID。

## 给另一位主程序员的调用接口

在猴子 Animator 所在物体上获取 `ADayAtTheZoo.Monkeys.MonkeyNpcAnimator`：

```csharp
monkey.ShowHunger();  // 饥饿
monkey.Feed();        // Eating -> Happy -> StandingIdle
monkey.Stand();
monkey.Sit();
monkey.Walk();
monkey.Run();
monkey.Scratch();
monkey.Wave();
```

`Feed()`、`TryPlay()` 返回是否成功开始播放。动画库缺失或状态不存在时返回 false，并在 Console 提示原因。

Walk 和 Run 只控制动画，实际位置由导航或移动系统控制。Root Motion 关闭。Hi 状态名保留，兼容现有 `ChibiMonkeyHiRayInteraction` 的直接 CrossFade。

## 验证清单

- 猴子及各 Mixamo 文件的 Avatar 均为 valid Humanoid。
- 8 项需求逐一预览；检查四肢扭曲、手穿身体、脚底滑动及异常位移。
- 进食无需实物，吃完进入开心动作并回到待机。
- 挠痒、打招呼播放一次后回到待机。
- 坐姿保持，站、走、跑可互相切换；进食中收到新动作请求能够切换。
- 主场景的鼠标、回车和 XR 射线打招呼仍可用。
- 用团队指定的 Unity `6000.3.23f1` 检查主场景，确认无编译或运行错误。

## 已发现的项目环境问题

`Packages/manifest.json` 中的 `com.meta.xr.sdk.core` 指向原作者电脑上的 `F:/.../com.meta.xr.sdk.core-207.0.1.tgz`，该 tgz 未包含在仓库中。整项目运行验证前需要恢复这个包或改成团队可访问的包源；本次动作源码没有擅自改动 XR 依赖。

已在 Unity 2022.3.62f2 和 Unity 6000.4.8f1 的独立项目里生成 9 个动作，验证饥饿循环、挠痒后回待机、以及 `Feed()` 触发的 Eating → Happy → StandingIdle。完整版视频在 Unity 2022.3 中用同一套动作与棕色猴子 Prefab 录制。团队版本的主场景仍待验证。
