# A DAY AT THE ZOO

一个使用 Unity 制作的小组项目。

## 开发环境

- Unity Editor：`6000.3.23f1`
- Asset Serialization：Force Text
- 版本控制：只提交 `Assets/`、`Packages/` 和 `ProjectSettings/` 等源文件
- 主入口场景：`Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake_Backup_2026-11-03.unity`
- 辅助场景：`Assets/Scenes/Monekey Mountain.unity`

## 首次打开

1. 安装 git。
2. 克隆本仓库。
3. 在 Unity Hub 中添加仓库根目录。
4. 使用 Unity `6000.3.23f1` 打开项目，等待 Package Manager 和资源导入完成。
5. 打开主入口场景 `Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake_Backup_2026-11-03.unity` 并运行游戏。
6. 如需检查 Monekey Mountain 场景，可打开 `Assets/Scenes/Monekey Mountain.unity`。

如果 Package Manager 提示缺少包，请在 Unity 中执行 **Window > Package Manager**，等待自动恢复；仍缺少时请把错误信息发到协作群。

## 协作约定

- `main` 分支保持可运行，不直接在主分支开发新功能。
- 每个功能从最新 `main` 创建分支，例如 `feat/player-movement`。
- 修改完成后提交、推送分支，并通过 Pull Request 合并到 `main`。
- 场景和 Prefab 尽量由同一时间的一人修改，减少冲突。
- 在 Unity 内移动或重命名资源，确保对应的 `.meta` 文件一起提交。

## 不提交的内容

Unity 会自动生成 `Library/`、`Logs/`、`Temp/`、`obj/`、`UserSettings/`、解决方案工程文件以及 `.unitypackage` 安装归档。这些内容已由 `.gitignore` 排除，克隆后由 Unity 自动重建或从已导入的资源目录读取。
