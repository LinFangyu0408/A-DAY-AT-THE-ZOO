# A Day at the Zoo - 开发记录

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
