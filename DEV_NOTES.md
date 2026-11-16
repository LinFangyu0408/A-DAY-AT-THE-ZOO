# A Day at the Zoo - 开发记录

最后更新：2026-11-17

## 当前玩法

- 当前只开放“喂香蕉”关卡，香蕉箱和挠痒箱在运行时隐藏。
- 香蕉通过手套项目栏取得，不在场景中直接拾取。
- 一只指定猴子先进入新手引导饥饿状态，玩家完成一次喂食后开始正式关卡。
- 新手导览进入场景立即显示在画面左上角，不播放中央 READY GO。
- 每只猴子一局需要完成 3 次喂食，一局共 9 次任务。

## Xbox 桌面试玩按键

- 左摇杆：移动
- 右摇杆：转动视角
- B：打开或关闭手套项目栏
- A：在项目栏中选择香蕉
- 按住 RT：显示抛物线瞄准轨迹
- 松开 RT：沿当前抛物线投出香蕉
- 橙色轨迹命中猴子时：手柄震动

香蕉在投出时立即扣除 1 个；未命中不退还。香蕉实际碰到猴子后，猴子进入进食状态并持续增加满意值。

## 主要文件

- `Assets/MonkeyCare/Runtime/MonkeyCareSession.cs`：会话、输入、抛物线瞄准、投掷、手柄震动
- `Assets/MonkeyCare/Runtime/CareTutorial.cs`：左上角新手导览
- `Assets/MonkeyCare/Runtime/CareGloveInventory.cs`：手套项目栏
- `Assets/MonkeyCare/Runtime/CareItem.cs`：香蕉持有、物理投掷、进食状态
- `Assets/MonkeyCare/Runtime/CareRound.cs`：任务和香蕉库存
- `Assets/MonkeyCare/Runtime/CareInput.cs`：Xbox、XR、键鼠输入兼容
- `Assets/Environment Aseets/01_HighlandLake_可玩场景/Scripts/DesktopPlayerController.cs`：桌面/Xbox 移动与视角

## 下次继续

1. 在 Unity 中 Stop 后等待编译完成，再 Play，验证最近一次完整试玩。
2. 检查香蕉是否在右侧手上、抛物线是否只在按住 RT 时出现。
3. 检查项目栏和右上角 HUD 是否在香蕉抛出后立即减 1。
4. 继续调整关卡节奏、猴子满意度速度、震动强度，再进入中文 UI 整理。
5. 中文流程稳定后再添加英文双语。

## 环境

- Unity：6000.3.28f1
- 当前通过 Xbox 手柄在 Unity Editor 中模拟 XR 交互
- 本地 Meta XR 安装包位于 `Packages/UnityPackages/`，该目录不提交到 Git
