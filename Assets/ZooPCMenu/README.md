# PC 主菜单与饲养员工作日志

入口场景：`Assets/ZooPCMenu/Scenes/PC_MainMenu.unity`。

在 Unity 打开入口场景后按 Play，或使用菜单 **Zoo → PC Menu → Open main menu**。
Windows 打包使用 **Zoo → PC Menu → Build Windows preview**，当前输出到 `Builds/PCDayTwo/`。
这个构建命令包含 PC 菜单、PC 新手关和第二关，不替换项目的全局 Build Settings。
第二关操作、规则与场景重建说明见 [DayTwo-Handoff.md](../../Documentation/PCMenu/DayTwo-Handoff.md)。

## 当前流程

- Start Game 和 Continue 都打开日志的 Day 01。
- Day 01 是现有 PC 新手关。Begin Day 01 进入试玩。
- Next / Previous 运行 EndlessBook 的真实骨骼翻页动画。Day 02 可进入独立的湖畔照顾关：三分钟内完成五只猴子的十次喂食、十次挠痒任务；其中三只在约 6 米的目的地范围内缓慢散步并绕开环境障碍，靠近有需求的猴子时它会停下。固定地面圈已移除；细高亮圈只表示当前选中的猴子，并跟随它移动。
- Animals / Facilities 是已解锁类型的收藏图鉴。初始解锁普通猴子和香蕉箱；开始第二关后解锁痒痒挠。其余卡片显示锁、`???`，不显示未知类型的名称或外形。可以切回 Logs。
- 设置包含主音量、全屏、减少动态效果。设置保存在 PlayerPrefs。
- PC 关卡中按 Esc 或手柄 Start 暂停；可以继续、重试、调整设置、返回日志。
- 结算的 Work Log 返回当前关卡的日志页；Retry 重开当前关卡。第一关原有 Restart 行为保留。
- 两关分别保存最佳星级和照顾记录，互不覆盖。Continue 当前不是关卡中途存档，不恢复未完成的一局。

## 接入范围

开发基线是 `v2-final` 的 `2fbb68d775bf1a95c97ce7423f6c8cccc446f622`，与此前 `handoff/2026-11-23` 指向同一提交。
`PC_TutorialDay1.unity` 由交接分支指定的 `HighlandLake_Backup_2026-11-23.unity` 复制生成，添加 PC 导航组件。
原场景、共享 MonkeyCare 玩法脚本和 VR 场景没有为此功能修改。
新增 `ZooPCSessionBridge` 只放在 PC 场景，绑定结算按钮和快捷键。
PC 新手关副本还移除了一个无效的场景内 `CarePickupMarker` 脚本记录，避免独立程序读取缺失字段时崩溃；原有玩法会在运行时重新创建拾取提示。PC 构建前会检查并修复这类记录，处理范围限定在本目录的 PC 场景。

如果组员更新了原新手关，可使用 **Zoo → PC Menu → Rebuild dedicated PC scenes** 重新生成 PC 副本。
注意：该命令会重建本功能的两个场景，手动修改 PC 副本前请先保存自己的版本。

## 素材来源

- 封面 `Art/MenuCover.png` 和日志纸张 `Art/JournalPaper.png`：本任务通过 imagegen 生成。
- `Art/JournalTitle.png` / `Art/JournalCard.png`：组员提供的 SVG 内嵌 PNG 原样提取，未把参考图中的假数值当作实时数据。
- `Art/MonkeyMeadow.png`：Unity 中实际新手关场景的截图。
- `Assets/EndlessBook/Book`：用户提供的 EndlessBook 1.13.3 包中的运行所需内容；作者 echo17。
- `Art/PageTurn.wav`：同一用户提供包内 Demo 02 的翻页声音。
- 主标题为 imagegen 制作的手工布艺字标；按钮使用独立的刺绣布料透明素材。
- Lilita One 复用个人作业主菜单使用的字体；Caveat 用于手写注记，Fredoka-SemiBold 用于正文。新增字体附带原 OFL 授权文件。
- 图鉴的猴子和香蕉箱由第一关的实际模型单独渲染，使用原材质；不是生图替代的角色外形。
- 用户的 Not-me-Meow HTML 仅用于布局、视觉节奏与交互参考；此功能在 Unity 内实现。

日志的纸面内容渲染为纹理后应用到 3D 模型，因此文字、纸纹和照片会随翻页弯曲。
每个日期占一个日志条目，右页为当天的关卡卡片，左页为照片或工作笔记。

## 编辑与扩展

`Resources/ZooPCMenu/MenuAssets.asset` 保存图片、字体、书本 prefab 和场景引用。
`ZooJournal.cs` 管理八个纸面（两天日志、动物图鉴、设施图鉴）与实际翻页。
`ZooPCFlow.cs` 管理菜单、设置、暂停、加载和本地记录。
`MenuAssets.asset` 的 `animals` / `facilities` 数组配置图鉴条目。`IsUnlocked` 综合初始 `unlocked` 字段与 `ZooPC.Collection.*` 发现记录决定是否显示名称和模型；预留卡片不是已公布的新类型。
使用 **Zoo → PC Menu → Refresh menu art and collection portraits** 可从 PC 关卡重新拍摄第一只猴子及名为 `banana box` 的箱子。此命令重置当前图鉴为第一关的默认解锁状态；增加后续关卡内容前请改用实际游戏的解锁数据源。
菜单具有视差、漂浮叶片、光点、字标轻摆、按钮悬停缩放／高光和画面淡入；减少动态效果选项会停用运动并立即翻页。
Day 02 使用独立场景 `DayTwo/Scenes/PC_DayTwoLakeside.unity`、独立回合状态和进度键；场景、构建列表和日志路由已接入。第一关与原始 VR 内容不为第二关修改。

`Runtime/ZooPCMenuValidation.cs`（整份代码受 `UNITY_EDITOR` 条件限制，不进入玩家构建）提供 Play mode 自动验证与 Unity 相机录制。
完整验证会用测试代码将真实回合时钟推进到结束来检查结算跳转，不把该步骤当作人工完成喂食的证据。
验证会恢复它临时改变的玩家设置和进度。

## 验证与构建说明

2026-11-28 第二关初版曾完成 48 项 Play mode 检查；当前五只猴子版本新增游走、停步、工具真实接触后计分与取消动作的验证。工具头贴合当前骨骼动画的身体表面，不再仅在镜头前挥动。扩大活动范围后，最新 64 项关卡检查全部通过，完整完成 20 个任务并获三星；详细证据见 `Documentation/PCMenu/DayTwo-Free-Roaming-2026-11-29.md`。上一版接触修复及 61 项菜单回归的记录继续保留。
此前 2026-11-27 的 61 项菜单视觉改版检查保留为历史记录，本次回归已将 Day 02 检查更新为入口可用。
项目原来的 Unity 版本声明与包配置保留；尚未在交接项目声明的 6000.3.28f1 上运行验证。
Windows x64 新版预览已经构建并启动成功；独立窗口的开始按钮、图鉴翻页、动物与设备内容已检查。新版记录位于 `Documentation/PCMenu/Polish-Validation-2026-11-27.md`，初版记录保留在 `Documentation/PCMenu/Validation-2026-11-27.md`。

`ZooPCShaderBuildScope` 仅在内置渲染管线的 PC 构建期间选择原 Shader Graph 的 Built-in target，并临时移除未使用的 URP 全局资源入口，避免 Unity 6000.4 枚举数百亿个无用着色器变体。
构建结束后恢复原 Shader Graph 文件和全局资源引用，不为菜单更换玩法材质。
Unity 6000.4 额外使用临时 Shader Build Settings，在变体枚举之前排除 URP 专用关键字；关键字集合包含无关键字的 `_` 分支。实现参考 [Unity KeywordDeclarationOverride API](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Shaders.ShaderBuildSettings.KeywordDeclarationOverride.html)，旧版 Unity 不编译这段 6000.4 专用代码。
如果强制结束构建导致中断，可先使用 **Zoo → PC Menu → Recover interrupted PC shader build** 恢复 Library 中备份的 Shader Graph 源文件。
