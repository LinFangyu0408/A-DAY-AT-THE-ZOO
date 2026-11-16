# HighlandLake 局部布局调整记录

2026-11-09：按已确认的“保留岩石站位、核心区优先、外围轻调”方案完成。

- 实际移动10处现有植被：6组灌木、3棵树和1处花。根据原地面高度差调整高度，保持着地。
- 猴子、岩石、补给箱和工具位置不变；没有新增或删除场景对象。
- 场景与编辑器修改前副本逐块比较：仅10个对应Prefab实例发生修改，新增0、删除0。
- 三个NPC传送落点空间检查通过。
- 已检查实际场景俯视图和NPC近景，核心区域视野与通行空间扩大，外围森林保留。

## 备份与对比

修改前文件及MonkeyCare目录：work/layout-backup-20261109-014029/

含编辑器当时未保存内容的副本：work/layout-backup-20261109-014029/HighlandLake_EditorBeforeLayout_20261109-015732.unity

实际渲染截图：work/monkey-care/layout-before/ 与 work/monkey-care/layout-after/

逐项位移：work/monkey-care/layout-changes.json

文件结构比较：work/monkey-care/layout-file-audit.json

## 验证边界

这些是Unity实际场景的编辑器相机渲染，不是头显截图。头显双眼显示、坐姿/站姿体验、实际遮挡和帧率尚未验收。整轮Play回归以work/monkey-care/play-tests.txt的最终完成记录为准；RUNNING不代表通过。
