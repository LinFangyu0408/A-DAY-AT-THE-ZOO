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

## 2026-11-20 第八轮（喂食流程/节奏/教程）
- 致命 bug 1：`CareRound` 构造函数调用 `Random.Range`，MonoBehaviour 字段初始化器禁止 → 抛异常后 Session 后续字段（records/arcPoints）未初始化 → `RegisterTask`/`TraceThrowArc` NRE，喂食永远完不成、score 恒 0%。改为 `DealFeedingTasks()`，在 Start 里调用。
- 致命 bug 2：`MonkeyCareNpc.Update` 进食分支 `record.startedAt` 无判空，教程猴子 record 为 null 时每帧 NRE（日志 3 万条）。
- 致命 bug 3：`CareReadyView` 用 `GameTime` 计时，而 Ready 阶段 gameTime 不推进 → CanvasGroup.alpha 恒 0，整块 READY GO 透明（"按 A 开始"提示消失）。改用 `Time.unscaledTime`。
- Ready 门控：ShowReadyGate 时 `CareInput.ForgetEdges()` + 0.45s 武装期，必须重新按 A 才开始。
- 饥饿轮转：Session 持有唯一 `HungerOwner`，`BeginHungerRotation` / `NotifyFed`（3-6s 随机）依次派单，三猴不会同时饿；首单耐心 55s（firstPatienceSeconds）。
- 头顶耐受度条：删除玩家正前方的 `Care STAMINA HUD`，只保留猴子头顶 CarePanel；画布加 sortingOrder（HUD 10 / 头顶 60 / 教程 80 / Ready 90 / 结算 100），头顶条不再被 SCORE 压住。
- 教程：0/1/2 步先走位与转视角（合计约 15s，带超时兜底），第 3 步才触发饥饿；按键统一成彩色圆形徽章（A 绿 / B 红 / RT 橙），用程序生成的白色圆角底避免贴图颜色相乘发暗。
- 冒烟测试 56 项全过（含"一次只饿一只"、"头顶条在头上"、"喂完 score 同帧刷新"、"Ready 门控不自动开始"）。

## 2026-11-20 第九轮（计分/节奏/提示/收尾）
- **3 星永远拿不到的真因**：`DealFeedingTasks()` 的循环边界自引用 —— `for (i=0; i < PlannedFeedings - tasks[0]-tasks[1]-tasks[2]; i++)`，每加一次总数就增加，判断立刻失效，实际只发出 **8** 个饥饿任务（2/2/4），而进度条需要 9 → 永远差一根。改为先算 `extra = PlannedFeedings - 6` 再一次发牌。
- 计分统一按香蕉根数：0-2 无星 / 3-5 一星 / 6-8 二星 / 9-10 三星（`OneStarFeedings/TwoStarFeedings/ThreeStarFeedings`）。`IsPassing` = ≥6 根（60% 线）。
- 新手引导喂的那根也计分：`CareRound.RegisterTutorialFeeding()`，`FeedingTotal` 含教程分，进度条立刻上涨（早期 11% 不再是"白喂")。
- 喂食时长上限 `MaxFeedSeconds = 3.5s`（场景里 8 秒太久，9 只在 150 秒内根本喂不完）；超时惩罚目标 = min(late, feed+1.2)。
- 头顶饥饿条：`CareProgress.Patience` 改为线性下降（原来是 t²，前半段几乎不动，看起来"没在掉"）；进食期间**保持显示**（上一版进食时隐藏，被用户判为 bug），吃完立即消失。
- 移除满意值：`RefreshUi` 只显示 STAMINA；吃完 → 面板隐藏 + Happy 表情（不再切 SATISFACTION）。
- 教程：步数 7→6，把"瞄准/投掷"合成一步（先讲 release to throw 再让玩家松手），每步都先给出按键徽章与文案，不再"做完才提示"。
- 最后 20 秒：`MonkeyCareSession.FinalSprintSeconds`，HUD 金色横幅 "FINAL 12s · 1 MORE FOR 1 STAR"，时钟变橙；若当前没有饥饿猴子，立刻补派一只（0.4s），杜绝干等。
- 星星 UI 移到 60% 黄线上并盖住黄线（`Score star badge` 用 passMarker 的 x），删掉冗余的 "PASS 60%" 文案。
- 小摊方向提示（方案 A）：`CareDirectionHint`，玩家累计转头 ≥35° 且小摊不在正前 55° 锥内时，屏幕下方出现可旋转箭头 + "BANANA STAND 10m"，开局面朝反方向时先给 6 秒引导；香蕉 ≤2 根时常驻。
- 第二轮 HUD 恢复：`RestartRound` 重新激活所有 "Care ..." 相机子物体（之前结算时被关掉就再也不回来）。
- 冒烟测试 66 项全过。

## 2026-11-20 第十轮（提示/星星/背包/围栏/回合重排）
- 小摊方位提示改版：删掉深蓝底板与阴影，只剩一个**红色三角**（运行时生成的白色三角形贴图再染色——导入的 arrow 素材本身是深灰剪影，直接染红会变成黑色）+ 一行描边文字；触发条件从"累计转头 ≥35° / 开局 6 秒"改为**只在香蕉 ≤3 根**时出现，且小摊已在正前 55° 锥内时不显示。`CareDirectionHint.LowStockBananas = 3`。
- 香蕉数量 ≤3 变红：`CareRoundView.Update` 把 `Banana count` 文本改成红色 (0.88,0.13,0.13)，与方位提示同一阈值。
- 星星成为进度游标：`MoveStar(ratio)` 让 `Score star badge` 沿轨道滑动（左右各留 60px 边距），0% 贴左端、100% 贴右端；到 6 根（`PassingRatio = 6/9 ≈ 66.7%`）时正好压在黄色通过线上——同时满足"随进度移动"和"盖住黄线"两个诉求。
- 背包只开一次：`CareGloveInventory` 用 `autoEquip` 取代可清空的 `BananaSelected`；第一次 A 选中后永久保持，扔完自动补一根、到小摊补货也自动上膛，香蕉归零不再 `ClearSelection()`；`autoEquip` 后 B 不再打开背包、B 按钮也隐藏，跨回合（`Reset`）保留。
- 场地围栏：新增 `CarePlayAreaBarrier`，以玩家起点为圆心、按"最远猴子 +2.4m / 小摊 +3.2m"算半径（8.5~16m 夹取）生成 18~32 段无渲染 BoxCollider 圆环；VR 摇杆位移会直接平移 tracking space、绕过 Collider，所以额外每帧把玩家根节点（rig / CharacterController）夹回半径内 `ClampInset = .55m`。
- 回合重排：`PlannedFeedings` 9 → **12**（每猴 4 只，`DealFeedingTasks` 不再随机分配，避免四舍五入漏牌）；`Cutoff` 150 → **120s**，`Duration` 130；饥饿轮转在 12 只耗尽前一直派单，最后 20 秒的补派兜底保留 → 结尾不再出现"30 秒没人饿"。
- 星级：`MaxStars = 3`，`Stars = Math.Min(MaxStars, …)`；`ScoreGoal = ThreeStarFeedings = 9` 作为进度条分母 → 满格与 3 星同时发生，喂第 10~12 根只增加分数不增加星。
- 结算"Feeding Amount"改为按 3 星目标显示（9 / 9），不再显示 9 / 12。
- 自检新增 `work/monkey-care/self-tests.request` 入口（`CareSelfTests` 加 `[InitializeOnLoad]` + Tick），纯逻辑 271 项；顺带修掉自检里写死的 `timestamp 200`（Cutoff 缩短后已超时）。
- 冒烟测试 86 项全过（含"星星随进度移动且 6 根时压线"、"香蕉 ≤3 变红"、"提示无底板且箭头为红"、"围栏 27 段且能把越界玩家推回"、"任务 12 个 + 120s"、"12 根仍是 3 星"）。

## 2026-11-20 第十一轮（VR 三连修：UI 消失 / 玩家上飘 / 香蕉打不中）
- **VR 里整块 UI 消失**：所有世界空间画布原来 `SetParent(camera.transform)`，而 `Start()` 里 `Camera.allCameras.FirstOrDefault(...)` 抓到的常常还是**桌面替身相机**——头显 rig 晚几帧才激活，桌面相机随即被禁用，HUD 跟着一起消失。新增 `CareHeadAnchor`（每帧同步到 `rig.centerEyeAnchor`，桌面回退到 playerCamera），`MonkeyCareSession.Head` 暴露该锚点，`CareRoundView / CareGloveInventory / CareTutorial / CareDirectionHint / READY 画布 / bananaAnchor` 全部改挂锚点；结算时遍历 `Head` 而不是相机来隐藏其它 HUD。
- **按住 A 往上飞**：`Locomotion` 的地面吸附会把 raycast 命中的物体顶面当成地面（踩到猴子/小摊/围栏就会被抬起来），加上摇杆位移直接平移 rig 绕过碰撞，玩家能一路飘到空中。`CarePlayAreaBarrier` 新增**垂直护栏**：记录回合开始时的 root 高度，每帧把 `root.position.y` 夹在 `startY + MaxRise(1.2m)` 以内，且这条夹紧放在水平提前返回之前，站在围栏内也会生效。
- **香蕉弹不到猴子**：`ThrowAimPoint` 的 VR 分支原来是 `hand.position + hand.forward * max(3, rayDistance*.5)`（凭空猜一个点），出手速度又是 `hand.forward * throwSpeed` 的**纯直线**，重力一拉就落到猴子脚下。改为：瞄准点 = 手射线的真实命中点（命中猴子时取 `Care Contact` 触发器中心），出手速度统一走 `CareItem.BallisticVelocity`，使抛物线与射线终点一致。
- 冒烟测试 86 → **90 断言**（新增：SCORE/BANANA 挂在 `Care Head Anchor` 下、`session.Head` 指向锚点、弹道解算落点误差 <0.2m、垂直护栏把抬高 6m 的玩家拉回）。
- 顺带确认：`Oculus Mirror` 在 Quest Link V2 + OpenXR 下始终停在 "Waiting for VR session"（OVR 服务端 `hmd0Valid=false`，Mirror 只认 OVR 会话），VR 预览改用 Unity Game 视图的 **Left Eye**。

## 2026-11-20 第十二轮（只能走路 / 香蕉必然进猴子手里）
- **往上飘的真凶**：`Locomotion.EnforceIslandFloorLimit()` 把玩家高度锁到 `Island_1` **碰撞体包围盒的顶部** `bounds.max.y` —— 那是整座岛的**最高山丘**，不是脚下的地面，所以一有输入就把玩家弹到几米高。改为 `bounds.min.y + floorClearance`（网格底部），并且只在玩家掉到地面以下 2 米时才拉起；`groundOnly` 模式下整段跳过。
- **禁止传送**：新增 `Locomotion.groundOnly` / `allowTeleport`，会话启动即设成"只能走路"。`TryTeleport` 直接返回 false；会话里地面弧线（grip+trigger）也一并关闭。之前射线落在猴子头顶/小摊棚顶/岩石上，一传送就站到物体上——这是"漂浮"的另一半原因。
- **垂直硬护栏收紧**：`CarePlayAreaBarrier.MaxRise` 1.2 → **0.15m**，`MaxDrop` 0.9m；每帧同时夹 **rig 根** 与 **Locomotion 自己的 transform**（rig 通常是 "Player and Locomotion" 的子物体，只夹子物体父级会一直累积漂移），并把 `trackingSpace.localPosition.y` 夹在基准 ±6cm（Locomotion 的蹲伏/重定位会改这个值，头就会凭空升高）。`startY` 改在第一次 LateUpdate 里从 rig 根捕获（不再从眼睛相机取，否则会把玩家锁在 1.7m）。
- **香蕉浮在半空**：三个原因一起修
  1. `BallisticVelocity` 的飞行时间上限 .7s → **1.05s**，十米投掷从 ~20m/s 降到 ~11m/s，不再一帧跨越 25cm 从猴子的 `Care Contact` 触发器里穿过去。
  2. 出手时设 `CollisionDetectionMode.ContinuousDynamic`，并对飞行做**扫掠检测**（`SphereCastAll` 半径 12cm），帧间穿模也能补上命中。
  3. **瞄准辅助**：投掷瞬间若准星正对着饥饿猴子，该猴子作为 `assist` 目标，香蕉进入其接食点 1.1m 内直接吸附到它手上；再加**看守**：被接住却 1.2s 没开始吃（任务已关闭/回合冻结/结算被拒）就还给玩家，绝不再悬在空中。
- `MonkeyCareNpc.Complete`：`completedAt` 下限钳到 1 秒（回合开始时 `<1` 的时间戳会被 `Round.Complete` 判为"还没开始"，导致吃不完、香蕉永远冻在嘴上）；任务结算仍失败时清空需求、1.5s 后重新要食，而不是死循环。
- 输入探针新增高度遥测：`playerRoot y` / `rig y` / `eyeY` / `trackingLocalY` / `locomotion groundOnly` / `barrier startY`，下次"还在飘"可以直接看出是哪一层在动。
- 自检 271 → **273**（长距离弹道速度 <15m/s 且落点误差 <0.2m）；冒烟 **90 断言**全过（抬 6 米一帧内回到 +0.03m）。

## 2026-11-20 第十三轮（眼高 / 手部射线 / HUD 出不了视野）
- **视角太低像坐在地上**：这套 rig 是**设备级（eye-level）XR origin** —— HMD 位姿几乎贴在 rig 根上（探针 `eyeY - rig y ≈ 0.02`），所以 **rig 的 Y 就是眼睛高度**。之前的代码把 rig.y 当作“脚底”，`floorClearance` 只加 0.24m，导致眼睛离地 ~0.5m。新增 `CareEyeHeight`（`[DefaultExecutionOrder(200)]`）：每帧探测脚下可行走地面（跳过猴子/香蕉/围栏/自身碰撞体，只取 `normal.y ≥ .6` 且不高于上一帧地面 1.1m 的命中），把眼睛固定在 **地面 + 1.58m**；地面以 6m/s 平滑跟随，不瞬移。围栏检测到 rig 活着时不再接管垂直轴（两者不再打架）。
- **手部射线**：Meta 交互 rig 自带的 `RayInteractor` 会在两只手上各画一条指射线。`MonkeyCareSession.SuppressHandRays()` 每 0.5 秒把 rig 下所有 RayInteractor 的 `LineRenderer` 关掉（interactor 悬停时会自己打开，所以需要周期性再关）。香蕉瞄准线（右手、只在持蕉时出现）保留。
- **HUD 在头显里看不到**：三块 UI 都贴在视野边缘 —— SCORE 在正上方 21°、香蕉数在右 33°/上 20°、教程在左 31°，基本落在头显有效视野之外。统一往中间收：SCORE `(0,.34,1.12)` 宽 .70（16.9°）；香蕉 `(.50,.26,1.12)` 宽 .24（偏航 24.1°/俯仰 13.1°）；教程 `(-.40,.09,1.08)` 宽 .44（-20.3°）。冒烟测试改成**角度断言**（偏航 ≤30°、俯仰 5~22°）而不是写死坐标。
- 探针新增 `ground y` / `eyeAboveGround`，可以直接判断眼高是否正常（应 ≈1.58）。
- 期间 Unity 编辑器崩过一次（日志里正常 shutdown + JobTempAlloc 泄漏），已用 `dangerouslyDisableSandbox` 直接拉起 `Unity.exe -projectPath` 恢复。
- 冒烟 **90 断言**全过；自检 273 全过。
