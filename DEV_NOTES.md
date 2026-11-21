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

## 2026-11-20 23:00 第十三轮补充：视线硬锁 1.6m
- 之前 `CareEyeHeight` 只在「设备级 origin」时生效（`eyeOffset < .8` 才接管），floor-level 时直接 return，结果按 A 又飞。
- 改为**无条件锁眼**：每帧把整根 rig 上下平移 `delta = (地面+1.6) - 眼睛.y`，眼睛就精确停在**地面 + 1.6m**。不区分 tracking origin 模式、不依赖 rig 层级（head pose 永远相对 rig 根），任何把玩家顶起来的输入（按键/传送/摇杆/recenter）都被这一行抵消。
- 地面探测加 `Terrain.SampleHeight` 兜底；`normal.y` 阈值降到 .5；rig 的 `trackingSpace.localPosition.y` 强制归零（杜绝 tracking space 里的垂直偏移）。
- `[DefaultExecutionOrder(500)]` 保证在围栏(0)之后执行，成为垂直轴的唯一仲裁者。
- 冒烟 90、自检 273 全过（冒烟走桌面路径，眼锁的 VR 真机效果靠探针 `eyeAboveGround` 确认，应 ≈1.60）。

## 2026-11-20 23:50 第十四轮（眼高下调 / A 飞加固 / 香蕉必中 / PC-VR 双 UI）
- **眼高下调**：`CareEyeHeight.StandingEyeHeight` 1.6 → **1.45m**（用户反馈视角偏高）。首帧若无地面命中，用 `head.y - 1.45` 反推地面（坏射线永不致浮空/下沉）；激活时打一条 `[Monkey Care] Eye height locked: floor X, eye 1.45m above` 日志，探针里也能读 `eyeAboveGround`（应 ≈1.45）。
- **A 键上飘**：眼锁保持「无条件」——每帧把整根 rig 平移 `(地面+1.45) - 眼睛.y`，任何输入/传送/recenter 顶起的偏移都被这一行抵消，眼睛不可能再飞。
- **香蕉必中 / 轨迹正确**：`TargetPoint` 加了合法性校验——只有离猴子 1.5m 内、且在胸-头高度区间内的 `Care Contact` 中心才被采用，否则退回 `猴子位置+0.8m`（此前一个零尺寸/游离触发器把 bounds.center 报成世界原点，香蕉就冻在半空）；吸附半径 1.1 → **1.5m**；出手仍是 `BallisticVelocity` 弹道（正确弧线）+ `ContinuousDynamic` + 扫掠检测。
- **PC / VR 双设备 UI**：新增 `CarePlatform`（`IsVr` = XR 设备激活或 VR 手在跑；`ForceVr` 测试钩子）。PC（无头显/手柄）走 `CareUi.Overlay` **屏幕空间平面 HUD**（SCORE 顶部居中、香蕉右上、结算居中），VR Meta 3 保留世界空间漂浮板。冒烟测试 `CarePlatform.ForceVr=true` 强制走 VR 布局以保留原有世界坐标断言。
- 冒烟 **90 断言**全过（VR 布局路径）；自检 273 全过。

## 2026-11-21 00:25 第十五轮（真相：两次录屏都是 PC 回退态）
- 看了录屏才定位真相：两段"VR/PC"视频里 **XR 都没启动**（Editor.log 00:00:51 `xrGetSystem: XR_ERROR_FORM_FACTOR_UNAVAILABLE`），游戏一直在跑**桌面回退**——所以所有 VR-only 修复（眼锁/手部射线/VR 布局）一行都没执行，用户看到的"VR"其实是 PC。
- **平面 HUD 巨型化的真凶**：`CareUi.Overlay` 用固定像素（1000x480），Game 视图 2x 缩放下直接铺满屏幕——那个巨型香蕉面板就是把视野全遮住的"飘在天上"。
- 修复：`CareUi.OverlayRoot`（单一根 Canvas + `CanvasScaler` 2400x1350 参考分辨率）+ `CareUi.Plate`（支持角锚点）；SCORE 略偏左、香蕉用**右上角锚点**（-40,-40）、结算居中，全部随屏幕等比缩放。
- **平台中途切换**：头显戴上后 `VrPlatformSwitcher` 会激活 rig，但 HUD 是按当时的模式建的——新增 `RebuildHudOnPlatformFlip()`：`CareRoundView.BuiltForVr` 与 `CarePlatform.IsVr` 不一致时销毁旧板重建（日志 `HUD rebuilt for VR/PC layout`）。PC 下手部瞄准线也彻底关掉。
- 冒烟 90 断言全过（ForceVr 走 VR 布局）。

## 2026-11-21 00:58 第十六轮（VR 掉回桌面的真因 / 排版对齐参考视频 / 交互统一）
- **"头显已连接但全按 PC 跑"的真因**：Play 开始那一刻头显未被佩戴/Link 未串流，OpenXR 抛 `XR_ERROR_FORM_FACTOR_UNAVAILABLE` 后 **Unity 不会重试**，整个 Play 会话钉死在桌面预览（日志 `[Zoo Platform] 桌面/Xbox 预览已启动`、无 `Eye height locked`）。修复：`VrPlatformSwitcher` 每 5 秒重试 `InitializeLoaderSync()+StartSubsystems()`，头显一就绪自动切 VR（日志 `OpenXR 延迟初始化成功`）。
- **排版对齐参考视频**：SCORE 回到正中（0,300），香蕉计数用右上角锚点内缩 (-150,-70)，不再顶到屏幕边缘；`CanvasScaler` 2400x1350 保证任意窗口尺寸下比例一致。
- **交互统一（VR=PC）**：持蕉显示射线（撤掉 PC 隐藏射线的门控）；射线指向饥饿猴子变橘色 (1,.63,.18) 并震动（Gamepad/OVR/haptic 三通道）；猴子开始吃（EatingNow）立即停震。这些逻辑本来就同源于 MonkeyCareSession，PC 之前只是被 UI 门控挡住。
- 冒烟 90 断言全过。

## 2026-11-21 02:50 第十八轮（教程左上 / UI 缩小 / 卡顿修复 / 手柄样式切换）
- **教程面板**：PC 端移到极左上角（-640,-175，460×264），不再遮背包与 SCORE；内部元素等比缩放，背景改圆角 RoundedCard；断言放宽为比例等价（24pt/360 ≈ 54pt/800）。
- **SCORE/香蕉/结算缩小**（用户两轮反馈"太小看不清"后定为中等）：全部用 localScale 缩放子元素坐标不变——SCORE .72、香蕉 .72、结算 .78。
- **卡顿主因**：①legacy Text 每帧无条件赋值 `.text` → 整块 Canvas 重建（SCORE/香蕉/时钟/冲刺/每猴状态行共 7 处/帧 + 字符串分配）→ 加 cache，变了才写；②STAMINA 条 RectMask2D 每帧改 sizeDelta → 量化为 2% 步进；③CareAudio.effects 字典里失效 AudioSource 永不清理 → UpdateMusic 顺手回收；④质量档=Ultra（阴影 150m+AA2），建议中等档测试。文内的 RunDemoRecorder 与本现象无关（场景中不存在）。
- **手柄样式切换**：新增 `CarePadStyleToggle`（右下角芯片 "A B XBOX" ⇄ "✕ ○ PS"，鼠标点击或手柄 Select 键切换）；`CareInput.PadStyle` + `LabelsChanged` 事件，`CareUiSkin.KeyBadge` 注册后换样式时自动改字并重染色（A/✕绿、B/○红），排版/尺寸/布局零改动；`AutoDetectStyle()` 按 Gamepad 布局自动选。
- 冒烟 90 断言全过；未提交 GitHub（等用户 PC 试玩确认后一起提交）。

## 2026-11-21 04:05 第十九轮（score 齐平 / 包包常驻 / PS5 自动识别 / 开局卡顿 / 音效诊断 / 低存量闪红）
- **score 与香蕉数字齐平**：PC 平面 HUD 的 SCORE 板改为**顶部锚点**（anchor/pivot (.5,1)，偏移 (60,15)）：只有板的透明上边越过画布顶，印刷 artwork 顶边与香蕉板顶边同高，进度条中心恰好落在香蕉数字同一水平线上（画布参考系 y≈495）。VR 布局不动。
- **包包 UI 左移 + 不再消失**：PC 端角落入口从世界空间画布改为**屏幕空间左下角锚定**（"Care PC Bag Root" overlay，(84,46) 起，scale .72）；取消"首次选蕉后按钮永久隐藏"——`showBagButton = mayOpen && !IsOpen`，B 随时可重开包；回合结算时显式 `HideHud()`（平面按钮不在 Head 锚点下，原隐藏循环管不到）；VR 端位置不变 (-.77,-.46,1.18)。平台中途切换时随 HUD 一起销毁重建（`DestroyPlates()`）。
- **PS5 自动识别**：此前 `AutoDetectStyle()` 无人调用，连着 DualSense 也永远显示 A/B——这就是"识别不了 PS5 手柄"。现在 `CareInput.Read()` 首帧一次性自动检测；`InputSystem.onDeviceChange`（Added/Removed/Reconnected/Disconnected）再次检测（DualSense 被 XR 重扫挤掉重连时自动换字）；检测范围加 `description.product`；设置芯片手动切换改走 `PinStyle()`（pinned 后自动检测不再覆盖）。READY 提示/教程/背包/小摊键帽与文案全部改用 `ConfirmLabel/BackLabel` 动态生成（PS 样式下显示 ✕/○），教程 Update 原有的 label 监听负责换字后重绘当前步骤。
- **开局卡顿**：①音频 8 段 wav 由 Start 单帧同步解码（music.wav ~40MB PCM）改为 `CareAudio.Preload()` **异步逐帧加载**（READY 门先出）；`preloadAudioData=0` 的片段强制 `LoadAudioData()`；②XR 延迟重试从固定 15s×8 改**指数退避**（15→90s，共 8 次），停止重试时打一条日志——每次重试都阻塞主线程并重扫 USB（就是开局 2 分钟内周期性轻卡的来源，也挤掉 DualSense）。
- **音效恢复（诊断+三重加固）**：资源路径本身没问题（`Assets/MonkeyCare/Resources/HighlandLakeAudio/` 与代码一致，场景字段全空走 Resources 兜底）。加固：①`FollowCamera` 改为**把监听器钉在当前渲染相机上**（场景 3 个 AudioListener 全默认 disabled，旧逻辑"找到任一激活监听器就返回"可能挂在从不渲染的相机上）；②音乐**看门狗**（endpoint 被头显运行时抢占导致停止时自动重启）；③preload 强制解码。新增探针音频心跳：`audio.masterVolume/masterPause/listener(.enabled/cameraRendering)/musicSource(.playing/.volume/.loadState)/activeListeners`。运行期实测（04:01 冒烟 Play 中）：music playing=True、loadState=Loaded、volume=0.16、唯一 listener 在 Desktop Camera（cameraRendering=True）——链路健康。
- **低存量闪红**：香蕉数字 <3（0/1/2）时红色闪烁（unscaled 时钟 2.3Hz，62% 亮 / 38% 暗），小摊补货回到 10 后自动停；=3 不再红（阈值从 `<=3` 收紧为 `<3`）。
- **顺手修**：PC READY 根画布（"Care PC Ready Root"）此前从不销毁（每局泄漏一个空 overlay），新增 `DestroyReadyGate()` 统一销毁；小摊键帽标签去掉每帧盲写 `.text`（缓存比较）。
- 冒烟 **92 断言**全过（新增"红色真闪烁"+"3 根不闪红"，键帽断言改为跟随 `ConfirmLabel/BackLabel`）；自检 **273** 全过；两轮冒烟连跑验证。未提交 GitHub（连同十七/十八轮，等用户 PC 试玩确认后一起提交）。

## 2026-11-21 04:55 第二十轮（卡顿定位 / 背包放大 / 徽章清晰 / 头顶条上移）
- **卡顿根因（录屏+日志对时确认）**：录屏 4:09、中位帧 33.3ms（30fps 封顶），第 1-3 分钟出现 5 段"2fps 连续 10 秒"的冻结（t≈55-66 / 70-80 / 93-103 / 126-137 / 177-184s）。Editor.log 的 XR 重试时间戳 04:05:59 / 04:06:10 / 04:06:25 / 04:06:47 / 04:07:21 / 04:08:12（Play 起始 04:05:11）与这五段**逐一对应**：每次 OpenXR 重试都会拆掉 loader、重扫 USB 并刷出 ~30 条堆栈警告，阻塞主线程约 10 秒。累计冻结 97s / 241s。
  - 修复：`VrPlatformSwitcher` 重试预算 8 次 → **1 次**（8s 后），且先用 `Process.GetProcessesByName(OVRServer_x64/OculusClient/vrserver...)` 做廉价存在性探测，运行时没起来就完全不试；运行中每 2s 复查，Link 启动后自动补一次；`F2` 手动重连。实测（3 分钟手动 Play）：仅开局一次 10.5s 停顿，之后 worst 9-75ms。
  - 画质 `m_CurrentQuality` 5(Ultra, shadowDistance 150 / lodBias 2) → 4(Very High, 70 / 1.5)。
  - 去掉每帧字符串分配：`"×"+n`、`n+"%"`、时钟 `mm:ss`、冲刺条改为整数变化才拼串；`Camera.allCameras.FirstOrDefault` 加 0.5s 节流。
  - `CareInputProbe` 新增 `frame.worstMs / frames`（编辑器帧间隔看门狗），下次可直接从探针判断是否还有冻结。
- **背包 UI 放大**：PC 平面板 360x210@.72 → 400x240@.9；图标 128→180；徽章 52→74（字 29→46/52）。VR 世界画布同步 400x240。
- **徽章模糊**：两个原因——①上一轮的 `LabelsChanged` 把徽章字色改成了 `KeyColor(text)`，红字压红底 → 现在恒为白色；②`Label()` 默认 `resizeTextForBestFit` 会把单字按任意尺寸重采样，且 128px 键帽贴图放大后边缘发虚 → 键帽改为 256px 生成、徽章字关闭 best-fit 并保留 1.6px 深色描边；另加 88px 白色圆环垫底、去掉原来 60x60 深色"厚度"块（那圈深色就是"糊"的来源）。
- **猴子头顶进度条偏低**：`CarePanel.Root` 改为**底部锚点**（pivot (0.5,0)），`barOffset.y` 语义变为"卡片下边缘"；`MonkeyCareNpc.Awake` 用渲染器包围盒量出头顶高度，卡片底边 = 头顶 + 0.16m（原来固定 1.45m 居中，0.5m 高的卡片下半张直接插进脑袋）。卡片宽度 1.1→0.88。
- **顺手**：头顶卡片被激活的**那一帧**还挂着猴子的朝向（猴子背对镜头时整张卡上下颠倒）——抽出 `FacePanel()`，激活后立即朝相机。
- 冒烟 **97 断言**（新增 5 条：图标尺寸、字色为白、非 best-fit、徽章尺寸、底部锚点 + 首帧朝向）、自检 **273** 全过。
- 未提交 GitHub（十七～二十轮一起等试玩确认）。

## 2026-11-21 05:25 第二十一轮（启动即识别设备 / PC 通道验证 / 提交）
- **启动即识别设备**：`CareInput.PadStyle` 原来只在**第一帧输入**时检测，READY 门首帧会闪一下 "A"（PS5 手柄实际是 ✕）。现在 `MonkeyCareSession.Start()` 在**建任何键帽之前**调用 `CareInput.ApplyAutoStyle()`，并打一条可核对的日志：
  `[Zoo Platform] 运行模式=PC 平面 HUD 输入=PS5 · DualSense Wireless Controller 确认/返回=✕/○`。
  探针 `CareInputProbe` 新增 `device.mode / device.input / device.padStyle / device.autoStyle / confirm / back / pinned`——不戴头显也能从 `input-probe.txt` 证明启动走了哪套内容。
- **PC 通道进冒烟**：此前整套断言都在 `CarePlatform.ForceVr=true` 下跑，**平面 PC HUD 从未被真正渲染验证**（`Capture()` 走相机渲染，ScreenSpaceOverlay 根本拍不到）。新增 `PcHudPass()`：回合结束后 `ForceVr=false` → `session.RebuildHudForPlatform()`（与头显热插拔同一条路径）→ 断言 overlay 画布、三块板的 artwork **顶边对齐**（实测 delta=0.0）、全部在屏内、样式芯片可点且文案跟随检测结果、背包徽章用 `BackLabel`；截图改用 `ScreenCapture` 写 `final-pc-hud.png`。
  - 顺带新增 `ArtBounds()`：`preserveAspect` 会在 rect 内做 letterbox，量 rect 角会误判"出屏"（SCORE 板上沿本来就有 15px 透明边刻意出屏）。
  - 冒烟 **108 断言**全过。
- **真实启动验证**：新增编辑器小工具 `CareLaunchShot`（丢 `work/monkey-care/shot.request` 即按玩家方式进 Play，1.2s/4s 各拍一张 Game View + 写 `launch-state.txt`）。实测：`flatUi=True padStyle=PlayStation confirm=✕ back=○ readyGateActive=True`，首帧 READY 门就是 "✕ PRESS ✕ TO START"，两帧之间无闪烁/错位。
- **试玩排查记两条（未改，避免动了共享场景内容）**：①画面左下那簇洋红+黄尖刺不是错误着色器，是场景里的 `Flower_high_purple`（Synty 高饱和风格植被）；②`Wooden crate_Lowpoly_White` 的材质引用为空（prefab GUID 已不在工程里），渲染为空但可能仍带碰撞——属环境资产的历史问题，PC/VR 共用，留待用户确认后再动。
- 本轮提交 GitHub。

## 2026-11-21 14:55 第二十二轮（结算两键 A/B / CONTINUE 回主页 / 头顶文字清晰化）
- **结算卡片两个按钮**：原来 CONTINUE 是**烧在图里的文字**、RESTART 是个**没有字的绿圆图标**，两者风格不成对，且"重来"没有可读标识。改为同款胶囊按钮（BlueButton / GreenButton，九宫格切片，否则圆角在放大时被拉成糊边）+ 实时文字 `CONTINUE` / `RESTART` + **左侧键帽**（`ConfirmLabel`/`BackLabel`，PS5 下自动变 ✕/○，样式切换时同步换字）。
- **功能**：`A / ✕ = CONTINUE`，`B / ○ = RESTART`（结算态在 `MonkeyCareSession.Update` 里优先处理，射线瞄准点击仍保留）：
  - `ReturnHome()`：清空结算、关闭背包、重置回合，回到**主页占位**= READY 待机门（真正的主页 UI 到位后，只需把 `ReturnHome()` 里的 `ShowReadyGate()` 换成主页视图，其它不动）。
  - `RestartRound()`：同一套重置后 `AcceptReadyGate()` 直接进 GO 节拍，立刻重开一局（不再停在待机门）。
- **顺带修掉两个真 bug**：
  1. 场景里**没有 EventSystem**——平面 HUD 上的 `Button`（右下角样式芯片、结算按钮）鼠标点击全都没反应，只有手柄 Select 有效。新增 `CareUi.EnsureEventSystem()`（EventSystem + InputSystemUIInputModule，仅建一次）。
  2. 结算/回主页时**背包面板必定弹出来盖住画面**：冻结与重置里的 `foreach (Transform child in Head) if (child.name.StartsWith("Care "))` 白名单把 `Care Backpack` 一起强制 `SetActive(true/false)`，绕过了 `CareGloveInventory` 的开关状态。两处循环都排除 `Care Backpack`，由背包自己管。
- **猴子头顶卡片文字糊**：`Care Status` 原本 900px 宽、挂在卡片**下方**（y=-170，卡片只有 640 宽 290 高）且是**纯白字 + 阴影**，实际是白字压在草地上，再被 best-fit 缩放到更小 —— 这就是"根本看不清"。现在：移进卡片内（y=-115，576×56）、34px、深色墨（比赛中 HURRY 在墨色↔红之间闪，吃完变绿）、关闭 best-fit 改 Overflow；标题 `STAMINA/SATISFACTION` 字号 31→40、同样关闭 best-fit 并加深，米色遮罩下延到 y=-10（高 268）避免露底字。
- 冒烟 **121 断言**（新增：键帽文案跟随手柄、B 真重开、A 回主页且背包已关闭、主页无背包残留、PC 两个按钮可鼠标点击）、自检 **273** 全过。

## 2026-11-21 15:35 第二十三轮（欢快 BGM + 倒计时滴滴滴）
- **旧 BGM 的问题**：`music.wav` 60s 单声道，频谱重心只有 **271 Hz**、8kHz 以上能量仅 **2%**——是低沉环境铺底，不是音乐。欢快度不够的根因在这里。
- **新 BGM（原创，无版权）**：`work/music/synth.py` 用 numpy 从谐波合成鼓、贝斯、Rhodes、马林巴、铜管，导出 `Resources/HighlandLakeAudio/music_care.wav`（66.2s 立体声 32kHz，A 段→B 段→A'→B' 配器递增）。
  - 候选 A `bgm_a_funk.wav`：116 BPM 放克/拉丁，F 大调，I-vi-IV-V 类进行，最接近《胡闹厨房》。
  - 候选 B `bgm_b_cartoon.wav`：132 BPM 卡通/马戏团，G 大调，拨弦 comping + 木鱼。
  - 指标：A 重心 3730Hz（低 36%/中 27%/高 13%），替代了原来的 271Hz。
  - 循环接缝：尾部 2s 叠加回开头 + 两端 0.8ms 淡入淡出 → seam=0.0000，无咔哒。
- **前稳后快（Overcooked 做法）**：`CareAudio.UpdateMusic(bool, float progress)`，`urgency = InverseLerp(.45,1,progress)`，`pitch = 1 + urgency²·0.18`（>92% 再 +0.02），用 `MoveTowards` 以 0.09/s 平滑 → 实测 1.000 → 1.200，约 +3 半音。曲体本身也是 A→B→A'→B' 逐段加配器，双保险。
- **倒计时滴滴滴**：`CareAudio.UpdateCountdown(remaining, running)` + 运行时合成 `BuildTick()`（880/1046.5/1318.5Hz，三谐波 + 2ms click）和 `BuildPulse()`（70→48Hz 低频）。
  - 30s 内 1Hz、10s 内 2Hz、5s 内 4Hz，每阶段进入时立刻响一声；最后 5 秒每拍加低频 pulse。
  - 只在 `begun && RoundStarted && !Frozen && !readyGate && !goSequence` 时计时，待机门与结算页保持安静。
- **加载优先级坑**：`Preload()` 先把旧的 `music` 塞进字段，`Default()` 见到非空就直接返回，导致新曲永远不生效（冒烟第一版 `audio.track=music` 抓到）。改为 Preload 先加载 `music_care`，`music` 仅作回退。
- 探针新增 `audio.musicSource ... pitch=`、`audio.countdown=... stage=... track=...`。
- 冒烟 **129 断言**（新增 6 条：加载的是 music_care、三个 beep 已合成、阶段 0/1/2 递进、窗口外静音、冻结静音、pitch 1.0→1.2 且能复位），自检 **273** 全过。
- 真实启动实测：`musicSource=music_care playing=True volume=0.16 pitch=1.000 loadState=Loaded`。
- 演示音频：`work/music/countdown_demo.wav`（最后 35 秒，50 声，pitch 1.049→1.196）。
- **未提交**：BGM 有 A/B 两个候选，等用户选定后再一起提交。

## 2026-11-21 16:00 第二十四轮（定稿卡通版 BGM：曲内真加速）
- **用户选定**：候选 B（`bgm_b_cartoon`）可以，A 放克不行 → 以 B 的乐队（马林巴主奏 + 拨弦 comping + 木鱼 + 走步贝斯）为准，重写成正式曲。
- **新曲 `work/music/make_zoo_theme.py`**：G 大调，36 小节，**分段真变速**（按小节独立排时间轴，不是固定网格）：
  - `A` 8 小节 @132 BPM（节奏适中，轻 kit）→ `A2` 8 小节 @132（同旋律，16 分 hat + 切分 kick + 木鱼 + 高八度薄叠音）→ `B` 16 小节 **@156 BPM（+18% 真加速）**（四踩 + 16 分马林巴 + 铜管 stab + ghost kick）→ `T` 4 小节 @168（军鼓滚奏 + 上行音阶 + 铜管推进）回落接开头。
  - 59.4s，尾部叠回开头 + 0.8ms 淡入淡出，seam 无咔哒；频谱重心 4551Hz（低 47%/中 50%/高 1.2%，比 A 版 3442Hz 更亮更"卡通"）。
- **播放层加速收窄**：`CareAudio.UpdateMusic` 的 `pitch = 1 + urgency²·0.10`（原 .18），>92% 再 +.015。曲内已 +18%，叠加 1.18×1.18 会飘成"急救车"，现在峰值 1.115（冒烟实测 `music pitch end=1.115`）。
- **资源替换**：`Resources/HighlandLakeAudio/music_care.wav` 换成本曲（meta/GUID 不动），旧 A 版留 `work/music/music_care_A_funk_old.wav`。
- 探针加 `audio.musicSource ... len=`，真实启动实测 `music_care playing=True len=59.4 loadState=Loaded pitch=1.000`（证明 Unity 真导入了新文件，不是旧缓存）。
- 冒烟 **129 断言**全过；真实启动截图 `launch-ready.png` 正常。**未提交**，等用户听过新曲再决定。

## 2026-11-21 16:20 第二十五轮（双音轨 / 音乐对齐倒计时 / 5 秒倒数音效）
- **双音轨**：`CareAudio` 现在两条 bed —— `Care Ambient Music`（`music.wav` 慢速环境，READY/GO 待机播）与 `Care Music`（`music_care` 卡通曲，回合播）。两条都 loop、spatialBlend 0、常驻播放，靠 `UpdateMusic` 里各自 `MoveTowards` 音量做**交叉淡入**，不切 clip，所以起跑不会在拍子中间硬切。
  - `BeginRoundMusic()`：`AcceptReadyGate()`（按 A/✕）调用，回合曲 `time=0`、pitch 复位、从 0 淡入；`PauseToAmbient()`：`ShowReadyGate()`（启动 / CONTINUE 回主页 / RESTART 前）调用，回到慢速 bed。
  - 实测（`launch-audio.txt`）：待机 `Care Ambient Music volume=0.14`、`Care Music volume=0.00` → 确认后 `round=True Care Music volume=0.160 / ambient 0.000`。
- **音乐对齐倒计时**：曲长 59.4s、回合 120s，直接循环会让加速段漂走。`AlignLastMinute()` 在剩余时间首次 ≤ 曲长时把 `musicSource.time` 定位到 `len - remaining`，使**曲子的最后一拍正好落在回合最后一秒** → 156 BPM 段覆盖最后半分钟，T 段收尾。冒烟实测 `remaining=50 → time=9.42`。
- **5 秒倒数音效**：新增 `BuildFinalTick()`（G4 木槌音 + 96Hz 闷击）与独立 `Care Final Countdown` source，最后 5 秒每秒一声，`pitch = 1 + step*0.1225` → G4/A4/B4/C5/D5 五级上行；叠在原有 4Hz beep + 低频 pulse 上。`FinalStep()` 同时驱动探针断言（5s=0 / 3s=2 / 1s=4）。
- **修掉一个真 bug（倒计时基准错）**：HUD 时钟与回合冻结都用 `CareRound.Cutoff = 120`，而音频一直传 `CareRound.Duration = 130`。后果是最后 5 秒的滴滴/倒数音效**在实际结束前 10 秒才该响、而冻结时还剩 10s 不满足条件，等于永远不触发**。现在 `UpdateMusic/UpdateCountdown` 统一用 `Cutoff - gameTime`，`roundProgress` 也用 Cutoff 归一（pitch 末段正好 1.115）。
- **饥饿哨声保留**：`Whistle()` 链路未动，新增 `HungerCallReady` 探针与断言 `the hungry monkey whistle is still wired`，防止以后被音乐改动误删。
- 工具：`CareLaunchShot` 支持 request 文本含 `confirm` → 反射调 `AcceptReadyGate()` 走"玩家按下 A"的真实路径，并写 `launch-audio.txt`（两条 source 的 volume/time/playing）。**注意 static 字段会被 EnterPlaymode 的 domain reload 清掉，必须存 SessionState**（第一版就这么丢的）。
- 冒烟 **137 断言**全过；真实启动待机/确认两种状态均实测通过。试听素材：`work/music/countdown5_demo.wav`（前 10s 音乐、后 5s 倒数）。

## 2026-11-21 16:30 第二十六轮（120 秒完整版 BGM，取消播放层变速）
- **用户反馈"到中期音乐就加速了，很奇怪"**：根因是 59.4 s 的曲子在 120 s 回合里循环 —— 29 s 处第一次进 B 段（156 BPM），60.6 s 处被 `AlignLastMinute` 重定位回 0（A 段），88 s 再次进 B 段。听感就是"中段忽然加速 + 回跳"。
- **新曲 `work/music/make_zoo_full_120.py`**：**120.00 s 一遍覆盖整局**，段落直接钉在时钟上：
  `A 16@132 (0-29.09) → A2 16@132 (29.09-58.18) → B 16@156 (58.18-82.80) → B2 16@156 (82.80-107.41) → T 9@171.6 (107.41-120.00)`，合计 32×1.818182 + 32×1.538462 + 9×1.398601 = 120.000。
  - 新写素材：`A_LIFT`（第 9-16 小节上扬乐句）、B2 段加铜管对句 + 16 分沙锤 + ghost kick、T 段军鼓滚奏（16→32 分）+ 属和弦齐奏 + 主和弦重击 + 马林巴下行收尾，末尾 1.2 s 淡出（回合结束不是循环，避免硬切）。
- **关掉播放层变速**：`UpdateMusic` 不再做 `pitch = 1 + urgency²·0.10`。120 s 的曲一旦被 rate 1.1 播就会在结束前 3 s 放完并 loop 回 A 段，等于又制造一次"中期加速"。加速感完全交给编曲（132→156 BPM）。pitch 恒为 1.000。
- **对齐逻辑加长度判定**：`AlignLastMinute(results, remaining, roundLength)`，当 `length >= roundLength - 1.5`（曲已覆盖整局）时直接返回，不重定位。短曲（≤59 s）仍保留原行为。
- 冒烟改成三条新断言：`cue length 118~122`、`full-length cue is never re-cued mid-round`（remaining=50 时 time 仍 0.00）、`transport stays at 1:1`(pitch 1.000)。**137 断言全过**。
- 真实启动：`Care Music volume=0.160 time=3.20 pitch=1.000`，`len=120.0`。旧 59 s 版备份 `work/music/music_care_59s_backup.wav`。

## 2026-11-21 17:00 第二十七轮（120 秒整 / 饥饿提速 / 按钮音效 / 结算胜利曲）
- **时长统一 120 s**：`CareRound.Duration = Cutoff = 120`（原来 Duration=130 只当计分分母，导致曲长 120 而回合 130，末尾音乐先结束）。HUD 显示 2:00，冻结与音乐尾音同帧。
- **饥饿节奏分段提速**（`MonkeyCareSession.HungerGapMinAt/MaxAt`）：`Pace = Clamp01((t-25)/(90-25))`
  - t=0~25s：3.0–6.0 s（教程与开头原速，不动）
  - t=60s：2.2–4.4 s
  - t=115s：1.5–3.0 s
  只改\"下一次饥饿等待\"，猴子耐心（30s / 首次 55s）与喂食 3.5s 全部不变，避免为了提速让人更容易超时。`NotifyFed` 用 `Random.Range(HungerGapMinAt(TaskTime), HungerGapMaxAt(TaskTime))`。
- **9 个任务可完成验证**：教程香蕉算 1 个（`TutorialFeedings`），回合内还需 8 个。冒烟用最慢档位做最坏时间线：8×(当前最大间隔 + 3.5 s 进食 + 2 s 走位) = **86.9 s** 完成，剩 33 s 余量（`PACE eight feedings done at t=86.9`）。
- **按钮音效**：`CareAudio.BuildClick()` 运行时合成（1180/2360 Hz 短 tick + 176 Hz 木体，110 ms），非空间 `Care Ui Click` 源。触发点：READY 按 A/✕、结算 CONTINUE/RESTART（`ResetFromResults`）、背包开关（`CareGloveInventory.Toggle`）。真实启动实测按下瞬间 `Care Ui Click playing=True`。
- **结算胜利曲**：`work/music/make_victory.py` 生成 `jingle_victory.wav`（140 BPM，G 大调，8 小节 13.71 s，铜管号角 + 马林巴 + 拨弦 + 鼓组 + 钟琴，尾部小军鼓过门接回开头）。`CareAudio.PlayVictory()` 在回合冻结分支调用，`Care Victory Music` 源 loop、0.34/s 淡入到 0.70，同时回合曲/待机曲淡到 0；`PauseToAmbient()`/`BeginRoundMusic()` 都会 `StopVictory()`，所以 CONTINUE 回主页或 RESTART 不会把号角带过去。
- **实测**（真实启动，`results` 请求跑真实冻结路径）：`victory=True victoryClip=jingle_victory`，`Care Victory Music volume=0.544 playing=True time=1.60`，回合曲与待机曲 volume=0.000；`launch-results.png` 结算卡完整。
- 冒烟 **145 断言**全过（新增 CLOCK / PACE ×3 / click / victory ×3）。
- 坑：新音效资源名必须与 `Resources.Load` 的字符串一致（字段名 `victoryMusic` ≠ 文件 `jingle_victory`，第一版就因此 VictoryReady=False）。另：`ScreenCapture` 前若编辑器弹出 \"Load Scene\" 模态框会卡住整个 update 循环，需 `WM_CLOSE` 关掉后重跑。

## 2026-11-21 17:05 第二十八轮（结算改为一次性胜利音效，不再循环 BGM）
- **用户指令**：\"胜利搞个胜利音效好了，不要 BGM\" → 结算界面不要循环背景乐，只要一记短促的胜利音效。
- **新素材 `work/music/make_victory_sting.py` → `jingle_victory_sting.wav`**：3.25 s，152 BPM，G 大调。三记上行铜管 G4–B4–D5（\"哒-哒-DAAA\"），第三记落在展开的 G 大三和弦（G3 B3 D4 G4 B4 D5）上长鸣，叠钟琴六音上行 + 定音鼓滚奏 + 开放踩镲，混响 2.4 s 后自然收尾，末 0.4 s 余弦淡出到 0（末尾 50 ms 峰值 0.00000，不会咔哒）。
- **`CareAudio` 改造**：
  - 源改名 `Care Victory Music` → `Care Victory Sting`，`loop = false`。
  - `PlayVictory()` 直接 `volume = victoryVolume`（不再从 0 淡入，短音效淡入会削掉冲击力），`Stop()` 后从头 `Play()` 一次。
  - `UpdateMusic` 删掉\"结算时每帧把 fanfare 重新 Play\"的循环维持逻辑，只在离开结算时淡到 0。这是关键：旧代码会在 clip 播完后立刻重启，等于永久 BGM。
  - 新增探针属性 `VictoryLoops` / `VictoryClipLength` / `VictoryAudible`；默认 `victoryVolume` 0.70 → 0.85（场景序列化值仍 0.70，实际生效 0.70）。
- **资源**：删除 `Resources/HighlandLakeAudio/jingle_victory.wav`(+meta)，新增 `jingle_victory_sting.wav`。旧的 13.71 s 循环版备份在 `work/music/jingle_victory_loop_backup.wav`。
- **验证**：冒烟 **147 断言**全过（新增 `sting is one-shot` / `short hit not background track`，原 `fades in` 改为 `fires at full level`）。真实启动 `results` 路径抓两次：
  - 结算后 1.58 s：`Care Victory Sting volume=0.700 time=1.58 playing=True loop=False`
  - 再 3.8 s：`Care Victory Sting time=0.00 playing=False loop=False`（播完即停，不再重启）
  - 同期 `Care Music` / `Care Ambient Music` 均 `volume=0.000`，`launch-results.png` 卡片完整。

## 2026-11-21 17:32 第二十九轮（120 秒 / 平滑饥饿曲线 / 星级绑定时钟 / 教程不占时）
- **时长**：`CareRound.Cutoff = Duration = 120`（第二十七轮已统一，本轮复验 `CLOCK cutoff=120 duration=120`）。
- **饥饿曲线改平滑**（`MonkeyCareSession`）：
  - 旧：线性 `Clamp01((t-25)/65)`，间隔 3-6 → 1.5-3.0。线性在 25 s 与 90 s 两个拐点斜率突变，听感/手感上是"某一秒突然变快"。
  - 新：`HungerPaceAt` 用 **smoothstep** `t²(3-2t)`，窗口 30→95 s，间隔 3-6 → **1.6-3.2**。两端斜率为 0，实测每秒最大漂移 **0.065 s**（断言 < 0.10），全程单调不增。
  - 新增 `RollHungerGap()`：随机后 `Clamp(1.6, 6.0)`，两端都不会产生 0 间隔或长空档。
- **星级与时钟绑定**：新增 `SecondsPerStarFeeding = Cutoff / ThreeStarFeedings = 13.33 s`，注释写明"改 Cutoff 必须重推星级门槛"；`Stars` 改为显式阶梯判断。门槛仍 3/6/9，与进度条 `/9`、及格线 6 一致。
- **修掉两个真 bug**：
  1. **教程白吃时间**：教程期间 `gameTime` 也在累加，而 `FinishTutorial()` 又把它清零 → 玩家实际可用时间 = 120 - 教程时长（且 HUD 会从 1:58 倒着走再跳回 2:00）。现在 `if (!TutorialActive) gameTime += dt`，HUD 在教程期间稳定显示 **02:00**（截图实测）。
  2. **音乐与时钟错位**：`FinishTutorial()` 清零时钟但没重置音乐 → 120 s 的曲子提前"教程时长"结束。现在结束时调 `BeginRoundMusic()`，音乐从第一小节重起，与 120 s 时钟严格同起点。
- **验证**：冒烟 **152 断言**全过（新增 smoothstep 平滑性/单调性、练习段原速、终局 1.6-3.2、星级门槛绑定时钟）。最坏时间线（每次都用最慢间隔 + 3.5 s 进食 + 2 s 走位）8 次喂食在 **88.4 s** 完成，余量 31.6 s → 3 星（含教程 1 单）稳可完成。
- **真实启动**：`round=True track=music_care time=1.19 volume=0.160`、待机曲 0.000；HUD `02:00`；结算后 sting `playing=False time=0.00`（播完即停），结算卡不再被教程卡遮挡。
- **工具修复**：`CareLaunchShot` 的确认意图改为落 `work/monkey-care/shot.active` 文件（domain reload 会清 SessionState 镜像，之前两次跑都卡在 READY）；确认/采样等 `MusicCued()`；截图延后到教程卡销毁之后。
- **资源**：`music_care.wav` 改 `loadType: 2`（Streaming），冷启动不再一次性解压 15 MB PCM，开局音乐起得明显更早。

## 2026-11-21 17:56 第三十轮（修“第一只猴子一直吃不完、倒计时不动”）
- **用户反馈**：\"现在有些卡在第一个猴子一直没吃完，然后倒计时一直不动\"。两个症状是同一个根因。
- **根因**：上一轮为了\"教程不占时间\"把 `gameTime` 在教程期间停掉了，但 **吃香蕉的进度也用这个时钟**：
  - `MonkeyCareNpc.UpdateGuidedTutorial` 里 `delta = Mathf.Max(0, TaskTime - appearedAt - Progress.Waited)`，`TaskTime = min(GameTime, Cutoff)` → 教程期间恒为 0 → `Progress.Advance(0, true)` 永不完成 → 教程第 6 步（等 `TutorialHungerSatisfied`）永远不满足 → `TutorialActive` 永远 true → 时钟当然也不动。**死锁闭环**。
  - 附带第二个死锁：`GloveInventory.Tick(..., !Round.Frozen && gameTime >= .1f)`，教程期间 `gameTime == 0` → 背包按钮不出现、B 也打不开 → 教程第 4 步（开背包）同样卡死。
- **修法（拆成两个时钟，而不是停掉唯一那个）**：
  - `gameTime` 恢复为**连续会话时钟**（动画、喂食进度、所有 `waitUntil` 都用它），永不冻结。
  - 新增 `float lessonHold`：教程进行中每帧 `lessonHold = gameTime`，把差值吃掉；`RoundElapsed = Max(0, gameTime - lessonHold)` 才是**回合时钟**（倒计时 / 120 s 音乐 / 星级 / 冲刺提示 / 冻结判定全部改用它）。`FinishTutorial()` 只把 `lessonHold` 定格，不再清 `gameTime`。
  - `TaskTime`：教程期间走连续时钟，回合内走 `RoundElapsed`（两个域不会混：引导需求在教程结束时被 `EndGuidedTutorial` 清掉）。
  - `MonkeyCareNpc.NextIn` 改用 `TaskTime`（原来减 `GameTime`，教程结束后每个计时器都像\"早就该来了\"）。
  - 背包许可改成 `!Round.Frozen`（教程即回合的一部分）。
  - 教程加**防死锁阀门**：单步超过 45 s 或整段超过 120 s 直接 `Finish()` 交棒给回合，任何一步都不可能再锁死整局。
- **附带一致性调整**：教程期间只放慢速 bed（`BeginAfterReadyGo` → `PauseToAmbient()`），确认 A 时若接下来是教程则**不**启动回合曲（`TutorialNext`）；`FinishTutorial()` 再 `BeginRoundMusic()`。这样"教程不占时钟"与"120 s 曲子 1:1 对应 120 s 时钟"两件事互不打架，也不会出现开头连听两遍前奏。
- **HUD**：`CareRoundView.Update(round, elapsed, lesson)` 新增 `lesson` 参数，教程期间时钟位显示 **LESSON**（白色，同数字色，琥珀色在亮天空上太淡），教程结束立刻变回 `02:00` 并重新开始读秒。之前冻结的 `02:00` 看起来就像坏了。
- **验证**：
  - 冒烟 **156 断言**全过（新增 4 条：教程期间 `RoundElapsed` 恒 0、教程时钟持续推进、HUD 显示 LESSON、教程结束变 02:00）。关键是把原有的\"引导喂食\"整段用 `TutorialActive=true` 跑，旧代码下这一条会超时失败。
  - 逻辑自检 **273 断言** PASS。
  - 真实启动（PC 平面 HUD，PS5 手柄）：
    - `confirm` 跑：`gate→confirmed→tutorial=True elapsed=0.00`，`Care Music volume=0.000 / Care Ambient 0.144`，截图里猴子头顶出现 STAMINA 板、教程卡走到 `OPEN THE BAG`、时钟位写 **LESSON**。
    - `results` 跑：`elapsed=120.00` 后正常冻结出结算卡（0/9、Next Star 3 MORE、CONTINUE/RESTART）。
  - 工具：`CareLaunchShot` 加 `trace`（每 0.4 s 落 `launch-trace.txt`：auto/confirmed/cued/gate/tutorial/round/elapsed）与 `long`（跑到教程喂食步并抓 `launch-lesson.png`）。
- **坑**：改完编辑器脚本直接落 `shot.request` 会用到**旧程序集**（Unity 未自动刷新，`Assembly-CSharp-Editor.dll` 时间戳没变），表现为\"确认没生效、trace 文件不存在\"。必须先 `python work/send_keys.py ctrl r` 等 dll 时间戳更新再跑。

## 2026-11-21 18:40 第三十一轮（倒计时只在最后 5 秒、教程时间计入两分钟、节奏跟音乐分段提速）
- **用户要求**：① 倒计时提示音第 5 秒才开始，不要 25 秒就响；② 新手教程耗时计入整体 2 分钟，不再单独排除；③ 后续音乐节奏加快时游戏节奏同步提速，且计时边界与节奏切换要平滑。
- **倒计时音效**：`CareAudio.CountdownWindow` 30 → **5**。原来 30 s 内 1 Hz、10 s 内 2 Hz、5 s 内 4 Hz，一局有 1/4 时间在滴滴响，真正的倒数反而听不出来。现在只有最后 5 秒：0.25 s 一记高木鱼 + 每拍低频脉冲 + 每秒一记 G4→D5 五级木槌（`FinalStep`）。`tickLow/tickMid` 两个没用到的音色直接删掉，`CountdownReady` 改判 `tickHigh && finalClip`。`lastFinalStep` 仍每帧跟随 `FinalStep(remaining)`（探针/断言要看）。
- **教程计入时钟**：删掉 `lessonHold` 与整个"折扣"概念。`RoundElapsed = min(gameTime, Cutoff)`，`TaskTime = RoundElapsed`（不再分教程/回合两个域）。`gameTime` 从按 A 那一帧开始连续走，教程、练习、正式回合共用。
  - 按 A 立刻 `BeginRoundMusic()`（不再等教程结束），`FinishTutorial()` 不再重置时钟、也不再重启曲子 —— 曲子与时钟仍保持 1:1，58.2 s / 107.4 s 的段落变化落在对应秒数上。
  - `BeginAfterReadyGo()` 教程期间不再 `PauseToAmbient()`（否则会把刚起的回合曲压掉）。
  - HUD 时钟位去掉 `LESSON`，教程期间直接读秒（截图实测：教程 OPEN THE BAG 时显示 **01:42**）。
  - 若玩家在教程里把 120 s 耗完：`Update` 里 `TutorialActive && gameTime >= Cutoff` 先 `FinishTutorial()`，再让 LateUpdate 正常冻结，教程卡不会浮在结算上；`CareTutorial.Update` 也加了 `Round.Frozen` 兜底。
- **节奏跟音乐（`CareTempo`）**：`CareRound.cs` 新增 `CareTempo`，把 `make_zoo_full_120.py` 的分段（A/A2 132 BPM 0–58.2、B/B2 156 BPM 58.2–107.4、T 171.6 BPM 107.4–120）写成一个 map，外加 `Bridge=6s` 的 `Bridge01()` smoothstep 门（下拍前 3 s 起、后 3 s 落）。
  - 饥饿间隔改成四级台阶（值取自 `PaceStepMin/Max`）：`3.0–6.0`（132 BPM，教程/练习原速不变）→ `2.4–4.8`（58.2 s）→ `2.0–4.0`（82.8 s，B2 满编）→ `1.6–3.2`（107.4 s 尾声）。终点还是老的 1.6–3.2，只是分三段走到。
  - 每秒最大变化 0.294 s（旧曲线 0.10 以内、但需要 65 s 才走完）；现在是"跟着音乐换挡"，仍在 6 s 内平滑过渡，无跳变。
  - 三星可达成性（含教程）：30 s 教程交接 + 8 次满间隔投喂（每次 3.5 s 咬 + 2 s 走回）= **113.6 s < 120 s**；平均是 100.4 s。
- **验证**：冒烟 **157** 断言全过（改/加了：`LESSON` 时钟两条、`02:00`→`01:53` 读秒、25 s/8 s 必须静音、`PACE lift=4.80 outro=3.20`、最差 1 s 步进 0.294、八次喂食 113.6 s）；逻辑自检 **273** PASS。
  - 真实启动 `confirm lesson trace long`：`tutorial=True round=False elapsed=23.71`，`Care Music time=18.01 / volume=0.160`（与同一时刻 elapsed≈18.4 对齐，说明教程里曲子就在跑），截图时钟 **01:42**；`results` 跑 `elapsed=120.00` 冻结、结算卡 0/9 + CONTINUE/RESTART 正常，胜利音效一次播完。
- **坑**：编辑器一直卡在 play mode（`EditorApplication.isPlayingOrWillChangePlaymode`）时 `final-smoke.request` 不会被消费，表现是"请求文件躺在那里不动"。先 `send_keys.py ctrl p` 确认工具栏 Play 变回三角（截图核对物理坐标要按 2× DPI 换算），再落请求。另：改完脚本必须等 `Assembly-CSharp.dll` 时间戳晚于 `.cs` 再跑。
