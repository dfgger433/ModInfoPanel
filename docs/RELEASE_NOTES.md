# ModInfoPanel v1.7.2

> 自动为 mod 内容生成信息栏 / Auto-generated info panels for mod content
> 《Casualties: Unknown》 / BepInEx 5.4.x / 硬依赖 CUCoreLib

## 发布文件

- `ModInfoPanel-1.7.2.zip`
  - `BepInEx/plugins/ModInfoPanel/ModInfoPanel.dll`
  - `BepInEx/plugins/ModInfoPanel/Locale/locale.zh-CN.json`
  - `BepInEx/plugins/ModInfoPanel/Locale/locale.EN.json`
- SHA256：`fc1489a817d59ddb68dc57f22e85f5fd46b0f6a9d53d9dd6b07fa09c4f607952`

---

## 中文

### 简介

ModInfoPanel 只处理 **mod 内容**（原版物品/流体/生物不动）：读取 CUCoreLib 与其他 mod 注册的物品、流体、自定义动物，把「未知伤亡维基中文翻译项目」风格的富文本信息块追加到悬停信息栏。

### 功能

- **物品信息**：背包 / 拖拽 / 世界物品悬停；装备数值（护甲、损耗系数、保温、装备槽、跳跃、容重比）、价值、识别、品质、容器信息。
- **委托 IL 效果提取**：读取 `useAction`/`useLimbAction`（物品）与 `onDrink`/`onApplyToLimb`/`onInject`（液体）的 IL；支持两种乘法顺序、`Mathf.Min/Max/Clamp`、`*=`/`/=`，并递归提取 `CoUtils.DoTimedOp` 等嵌套委托的延迟效果（前缀 `延迟`）。
- **状态效果**：按物品本次数值匹配状态等级说明（如 `大麻素 +10（轻度大麻影响：…）`）；自定义 moodle 悬停框追加 `当前 / 阈值 / 相关物品`。
- **配方行**：`制作(INT)：材料=结果`，含 mod 配方，进入对局后增量刷新。
- **流体**：容器内 mod 流体与自定义世界流体。
- **自定义动物**：生命（红）、掉落、生成、特点。
- **JSON 缓存**：首次生成 `InfoCache/{kind}/{mod}/{id}.json`，之后只校验变化，未变不写盘。
- **手工覆盖**：`overrides.json`（永不自动改写）、`acquisition.json`（分类→获取来源，可编辑热重载）、`CustomData` 约定（`effects` / `info` / `acquire` / `features`）。

### 本版变更

- 生物「生命」行改为项目调色板红 `#e84e40`。
- 修复液体效果行重复的 `(每100mL)`。
- 文档：颜色规范引用与致谢更新。

### 安装

1. 安装 **BepInEx 5.4.x**。
2. 安装 **[CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)**（硬依赖）：`CUCoreLib.dll` → `BepInEx/plugins/`。
3. 解压 `ModInfoPanel-1.7.2.zip` 到游戏根目录（即 `BepInEx/plugins/ModInfoPanel/…`）。

### 依赖与兼容

- 硬依赖：CUCoreLib（LGPL-3.0）。
- 原版物品/流体/生物不受影响。
- 联机：缓存为本地生成，各端各自生成；`overrides.json` / `acquisition.json` 如需一致请自行同步。

### 已知限制

- IL 提取只覆盖简单模式；循环、随机、辅助方法内的复杂逻辑不会提取，可用 `CustomData` 或 `overrides.json` 补充。
- 获取来源只显示可验证项：CUCoreLib `DropPool`、交易（分类在交易池且价值>0）、`acquisition.json` 映射。

### 许可证与致谢

- 本项目：**LGPL-3.0**。
- 硬依赖 [CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)（LGPL-3.0），仅以独立 DLL 形式动态引用。
- 感谢 [未知伤亡维基中文翻译项目](https://github.com/dodo23333/cu-chinese-wiki-translations)（MIT）：信息栏颜色与排版风格参考该项目。
- 感谢 [DeepSeek](https://www.deepseek.com/)：本插件在开发过程中使用了 DeepSeek 模型辅助。

---

## English

### Overview

ModInfoPanel only touches **mod content** (vanilla items/liquids/creatures are left alone): it reads items, liquids and custom animals registered through CUCoreLib and other mods, and appends an info block styled after the [CU Chinese Wiki Translations](https://github.com/dodo23333/cu-chinese-wiki-translations) project to hover tooltips.

### Features

- **Item info**: inventory / drag / world hover; equipment stats (armor, durability loss, insulation, slot, jump, weight/volume), value, recognition, qualities, containers.
- **Delegate IL extraction**: reads `useAction`/`useLimbAction` (items) and `onDrink`/`onApplyToLimb`/`onInject` (liquids); supports both multiplication orders, `Mathf.Min/Max/Clamp`, `*=`/`/=`, and recursively scans nested delegates (e.g. `CoUtils.DoTimedOp` delayed effects, prefixed `延迟`).
- **Status effects**: level description matched by the item's value (e.g. `THC +10 (Mild cannabis effect: …)`); moodle tooltips gain `Current / Thresholds / Related items`.
- **Recipes**: `Craft(INT): ingredients = result`, including mod recipes, refreshed after world-gen.
- **Liquids**: mod liquids inside containers and custom world fluids.
- **Custom animals**: health (red), drops, spawn, traits.
- **JSON cache**: generated once at `InfoCache/{kind}/{mod}/{id}.json`; later launches only rewrite changed entries.
- **Manual overrides**: `overrides.json`, `acquisition.json` (editable, hot-reloaded), and the `CustomData` convention (`effects` / `info` / `acquire` / `features`).

### Changes in this release

- Creature `Health` line now uses the project palette red `#e84e40`.
- Fixed duplicated `(per 100mL)` in liquid effect lines.
- Docs: updated style reference and credits.

### Installation

1. Install **BepInEx 5.4.x**.
2. Install **[CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)** (hard dependency): `CUCoreLib.dll` → `BepInEx/plugins/`.
3. Extract `ModInfoPanel-1.7.2.zip` into the game root (i.e. `BepInEx/plugins/ModInfoPanel/…`).

### Dependencies & compatibility

- Hard dependency: CUCoreLib (LGPL-3.0).
- Vanilla items/liquids/creatures are untouched.
- Multiplayer: caches are generated locally per client; sync `overrides.json` / `acquisition.json` manually if you want identical text.

### Known limitations

- IL extraction covers simple patterns only; loops, random and helper-method logic are not extracted — use `CustomData` or `overrides.json`.
- Acquisition lines only list verifiable sources (CUCoreLib `DropPool`, traders, `acquisition.json` mapping).

### License & credits

- This project: **LGPL-3.0**.
- Hard dependency [CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib) (LGPL-3.0), referenced as a separate DLL only.
- Thanks to the [CU Chinese Wiki Translations](https://github.com/dodo23333/cu-chinese-wiki-translations) project (MIT): the tooltip colors and layout style follow it.
- Thanks to [DeepSeek](https://www.deepseek.com/): this plugin was developed with the assistance of the DeepSeek model.
