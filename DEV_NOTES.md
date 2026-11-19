# A Day at the Zoo - 开发记录

## 2026-11-20 UI 重叠与字细问题修复（用户反馈"很糟糕/完全没改"）
- 根因1（重叠）：CareTutorial 面板定位 (-.48,.32,1.50) 与顶部 SCORE HUD (0,.43,1.18) 在世界空间大面积重叠，且 HUD 更靠前遮住指导面板。修复：指导面板移至居中偏下 (0,-.06,1.45)，缩放 .88，数学上 y 范围与 HUD 完全分离。
- 根因2（字细）：Fredoka-Variable.ttf 可变字体的 Bold 字重被 Unity legacy Text 忽略，一直渲染细体。修复：`CareFont.LoadReadable()` 改为优先加载项目内静态粗体 `Resources/Fonts/Arial-Bold.ttf`（从系统 arialbd.ttf 复制，游戏内 UI 文案全英文无中文方块风险），并保留 OS 粗体/Fredoka 逐级 fallback。
- CareRoundView.Legible() 简化为单层对比描边（2.5px），配合真粗体；body 字号 48→54。
- 同步更新 CareFinalSmokeTests 的过时断言（原强制要求 Fredoka）为接受粗体可读字体。
- 验证：跑 `work/monkey-care/final-smoke.request` 触发全套自动测试，**SUCCESS 41 assertions 全通过**；`final-tutorial.png` 截图确认面板居中不重叠、文字粗实清晰不出框。
- 备份在 `_backups/`（.v2 系列）。

## 2026-11-20 游戏界面切图补充（UI切图-游戏界面(1).zip）
- 新素材 17 张导入到 `Assets/MonkeyCare/Resources/UI/GameUI2/`（独立目录，不覆盖旧版高清图）：STAMINA/STAMINA LOGO、SATISFACTION/LOGO、UI SCORE、banana、happy/sad、READY GO、3 种进度条底、数字星级 1-5。
- 对比发现：多数新图与旧图是同一套美术的低分辨率版（旧图分辨率约高一倍），故保留旧图用于 banana/happy/sad/readygo/进度条，仅接入项目真正缺的部分。
- 修复既有 bug：`CareUiSkin.SvgStaminaPanel` 原本错误指向 `satisfaction`（体力面板显示满意度图），改为 `LoadGameUi("STAMINA")`；`SvgStaminaLogo` 改用新闪电图标 `LoadGameUi("STAMINA LOGO")`。
- 新增 `CareUiSkin.LoadGameUi()`（从 UI/GameUI2/ 加载）和 `SvgNumberStar(int)`；结算徽章优先用新数字星级 1/2/3（fallback 到 level1/2/3）。
- 备份移至项目根 `_backups/`（避免 .bak 被 Unity 当资源导入）。已重编译通过（compile 6689ms），无 C# 错误与新素材缺失警告。

## 2026-11-20 UI 素材更新与文字可读性修正

- 结算按钮/星星换成新版切图：`UI/SVG/continue.png`、`restart.png`、`star.png`（来自用户提供的 `UI切图-结算界面(2).zip`）。
- 结算面板仍用 `results_blank.png` + 代码动态填字：新版 `Results Screen.png` 自带烘焙文字，直接替换会和动态文字重叠，故不替换面板本身；显示效果与参考图一致。
- 所有 UI 文字统一走 `CareRoundView.Legible()`：按文字亮度自动加对比描边（深字白边、浅字深边），并启用 `resizeTextForBestFit` 防止文字出框。进度条、结算、香蕉数量、背包、新手引导全部覆盖。
- 新手引导（CareTutorial）重新排版：标题/正文/操作提示改居中、放大文字框、收敛字号上限，彻底解决文字出框；移除各处重复的手动 Outline 避免描边叠加发糊。
- 备份：`CareRoundView.cs.bak`、`CareTutorial.cs.bak`。已重新编译通过，无 C# 错误。

## 2026-11-20 最新试玩修正

- 英文 UI 字体切换为随项目提供的开源字体 **Fredoka**（SIL OFL 许可证），并同步放大教程、HUD、背包、拾取提示与结算清单字号。
- 顶部 SCORE 从贴边位置下移，保留了 SVG 外框和彩色动态填充，同时增加低饱和浅灰轨道；0 / 10 时也能清楚看出进度条。
- 左手背包入口移除后方白色底板；B 键缩小，保留独立深色厚度层。
- 投掷后飞出的香蕉继续独立飞行，下一根香蕉立即重新挂到右手 `GripSocket`。
- 香蕉箱常显圆形 A 按键和向下箭头。库存不足时显示 `PICK UP` / `RESTOCK`，库存已满时仍显示补给点名称。
- 香蕉箱的 A 拾取会把库存补满到 10，并验证猴子进食、满意状态和结算流程。

最后更新：2026-11-20

## 当前玩法

- 当前只开放“喂香蕉”关卡，香蕉箱和挠痒箱在运行时隐藏。
- HUD 使用本次 `SVG.zip` 提供的 SVG 素材：SCORE 进度卡、香蕉数量卡、猴子头顶的 STAMINA / SATISFACTION 面板、Happy / Sad 表情、ReadyGo、结算卡、星级图和 CONTINUE / RESTART 按钮；圆形按键徽章保持英文 `A/B/RT/L/R`。
- SVG 结算卡原始固定文字已清理，当前由 Unity 动态显示 `Feeding Amount`、`Resolved Status`、`Patience Penalty`、`Total Score`、及格状态、香蕉数量和三星评价，避免旧素材文字与游戏数据重叠。
- 主 HUD 分区固定：喂香蕉进度在顶部中央，香蕉数量在右上角，背包入口在左下角；背包入口使用新提供的独立背包图标，并显示动态 Xbox B / PS5 O 按键。
- 背包使用本次导入的 BAG 素材，香蕉位于第一格，带数量角标和动态 Xbox A / PS5 X 按键提示。美术中烘焙的 `BAG`、`SATISFACTION` 和进度条白色分隔线已用中文覆盖层处理。
- 猴子进食时头顶面板切换为中文“满意进度”，彩色进度条随进食增长；未进食时显示“剩余耐心”。
- 香蕉通过背包取得，不在场景中直接拾取。
- 左右手套在 VR 运行时跟随 Meta 的左右控制器锚点；没有可用头显时自动使用桌面/Xbox 手部预览。
- 香蕉根节点直接挂在右手 `GripSocket` 下，转头和移动手柄时保持在手套内，不产生世界坐标漂移。
- 一只指定猴子先进入新手引导饥饿状态，玩家完成一次喂食后开始正式关卡。
- 新手导览进入场景立即显示在画面左上角，不播放中央 READY GO。
- Unity Editor 中切换 Console、Hierarchy 或 Inspector 不会隐藏导览和玩法 HUD。
- 每只猴子一局需要完成 3 次喂食，一局共 9 次任务。
- 玩家出生点背后有已保存到场景中的小摊，远处有背景风车；编辑模式即可看到，不必先进入 Play。
- 小摊按其自身 X/Z 位置重新采样实际地面，不再沿用玩家脚下高度，也不会命中自身棚顶而悬空。
- 小摊使用木纹与橙/奶油条纹布材质，两个香蕉箱放在柜台上；材质 Shader 会按当前 Built-in / URP 渲染管线自动选择。
- 箱堆上方有漂浮的黄色向下三角箭头和“拾取”标签，始终面向玩家并轻微上下浮动。
- 猴子开始饥饿时会播放在猴子位置生成的 3D 哨声，声源带空间衰减，玩家可通过左右方向判断是哪只猴子在叫。
- 桌面/Xbox 玩家使用 CharacterController 贴地移动，最大可行坡度 42°；离地后关闭水平惯性，不能借移动输入漂到空中。

## 手柄桌面试玩按键

- 左摇杆：移动
- 右摇杆：转动视角
- 返回/项目栏：Xbox 按 B，PS5 按 O
- 确认/选择香蕉：Xbox 按 A，PS5 按 X
- 按住 RT：显示抛物线瞄准轨迹
- 松开 RT：沿当前抛物线投出香蕉
- 橙色轨迹命中猴子时：手柄震动

当前支持同时连接 Xbox 和 PS5 手柄；移动与视角输入会从所有已连接 Gamepad 中读取幅度最大的摇杆，避免 `Gamepad.current` 指向空闲手柄时无法移动。

香蕉在投出时立即扣除 1 个；未命中不退还。香蕉实际碰到猴子后会锁定目标并持续增加满意值。飞行中和进食中的香蕉不会再次从背包自动装回右手，避免香蕉在猴子嘴边被拉走、导致喂食永远无法完成。真实 RT 投掷到 Happy 状态的自动化检查已通过。

## 主要文件

- `Assets/MonkeyCare/Runtime/MonkeyCareSession.cs`：会话、输入、抛物线瞄准、投掷、手柄震动
- `Assets/MonkeyCare/Runtime/CareTutorial.cs`：左上角新手导览
- `Assets/MonkeyCare/Runtime/CareGloveInventory.cs`：背包界面与香蕉选择
- `Assets/MonkeyCare/Runtime/CareItem.cs`：香蕉持有、物理投掷、进食状态
- `Assets/MonkeyCare/Runtime/CareRound.cs`：任务和香蕉库存
- `Assets/MonkeyCare/Runtime/CareInput.cs`：Xbox、XR、键鼠输入兼容
- `Assets/MonkeyCare/Runtime/CareFarmEnvironment.cs`：出生点谷仓、屋内香蕉箱堆与远景风车
- `Assets/MonkeyCare/Editor/CareFarmSceneLayout.cs`：把谷仓、风车、箱堆和拾取提示保存到编辑场景
- `Assets/MonkeyCare/Resources/UI/Kenney/`：CC0 UI 控件与许可证
- `Assets/MonkeyCare/Resources/UI/Backpack/`：BAG 背包、香蕉、数量角标和后续物品素材
- `Assets/MonkeyCare/Resources/UI/SVG/`：从用户提供 `SVG.zip` 渲染并接入的 HUD、进度条、表情、ReadyGo、结算与按钮素材
- `Assets/MonkeyCare/Resources/Fonts/LemiJiquTi.ttf`：当前中文 UI 主字体“乐米吉趣体”
- `Assets/Environment Aseets/01_HighlandLake_可玩场景/Scripts/DesktopPlayerController.cs`：桌面/Xbox 移动与视角

## 下次继续

1. 进入 Play 后向右摇杆转身，实际确认从玩家视角看小摊、柜台香蕉箱和“拾取”箭头的大小及距离。
2. 检查背包打开、香蕉选择、香蕉在右侧手套内以及抛物线是否只在按住 RT 时出现。
3. 检查背包、右上角 HUD 和柜台上香蕉数量是否在香蕉抛出后立即减 1。
4. 实机确认猴子饥饿哨声的空间方向；当前为运行时合成的短促双音哨声，后续可替换正式录音。
5. 继续调整关卡节奏、猴子满意度速度、震动强度。
6. 中文流程稳定后再添加英文双语。
7. UI 已使用 Kenney CC0 基础包配合原导入 UI；如之后在 Asset Store 账号中取得 `2D Casual UI HD`，可再做整套视觉统一。

## 环境

- Unity：6000.3.28f1
- 当前通过 Xbox 手柄在 Unity Editor 中模拟 XR 交互
- 本地 Meta XR 安装包位于 `Packages/UnityPackages/`，该目录不提交到 Git

## 2026-11-20 UI 第四轮（按用户参考截图）
- 教程面板移到左上角（-0.60, .42, 1.30，scale .62），不再挡玩家视角。
- HUD 换成参考图样式：SCORE 白面板（GameUI2/UI SCORE.png，彩虹条用运行时切片做活进度）+ 金星徽章 + 右上分数"0.00"格式 + 面板下计时器；香蕉面板（banana.png）右侧"×N"；新增 STAMINA 面板（STAMINA.png + 渐变条 + 闪电 logo），值为有需求猴子的平均进度。
- 震动改为：只有按住扳机瞄准"饥饿中"猴子才触发（MonkeyCareSession.LateUpdate foodAimed 加 Input.TriggerHeld）。
- 结算界面按参考图3重做：白卡 + LEVEL COMPLETE 蓝缎带 + Feeding Amount / Resolved Status(%) / Facilities Added + Total Score + 三星 + Next Star + CONTINUE 胶囊 + 绿色圆重启按钮。
- 冒烟测试 41 断言通过；注意 request 触发前先确认 Unity 已完成脚本重编译（ScriptAssemblies 两个 dll mtime）。

## 2026-11-20 第五轮微修
- 结算卡 "Next Star" 文本框收窄右移（宽460 @ -160），不再出卡片左缘。
- HUD "PASS 60%" 标注右移（+80）避开中央金星徽章。
- CareFinalSmokeTests 结算段补 StopAtCutoff 循环，截图与真实结算一致。
- 触发测试前务必确认 Library/ScriptAssemblies 两个 dll mtime 晚于源码（Ctrl+R 后轮询），否则 request 会用旧程序集跑出假通过。

## 2026-11-20 第六轮（小摊后方+碰撞）
- 小摊移到玩家初始视角后方偏左（-forward*6.5 + left*3.4），转身才可见；摊位正面朝外。
- 新增 AddBoxBlockingColliders：摊位柜台周围 3 个 BoxCollider 阻挡玩家穿过。
- 教程面板微调 (-0.52,.34,1.32) scale .56。
- work/monkey-care/layout-editor-state.txt 是 Unity 侧心跳（playing/compiling/updating），判断编辑器状态优先读它。
- 用户端显示旧 UI 的原因：Play 会话先于编译开始，Unity Play 模式不热重载——退出 Play 重进即可。

## 2026-11-20 第七轮（8 项反馈）
- 回合 230s→150s；NextTime 去 85/155s 门控；喂完 5-9s 随机再请求；开局 3-6s 首次饥饿。
- 吃香蕉期间不震动（EatingNow）、头顶进度条隐藏、吃完触发 Happy 动画。
- SCORE 条去 0/9 只留百分比；轨道精确对齐贴图+圆角奶油空槽。
- 结算卡修复 SDF 漏 radius 的假圆角；卡+阴影共享 96px 大圆角。
- Unity 用特定启动方式会静默退出，用 Bash 后台方式启动。
