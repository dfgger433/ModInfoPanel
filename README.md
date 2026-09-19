# ModInfoPanel

> Auto-generate info panels for mod content in **Casualties: Unknown**.
> 为《Casualties: Unknown》的 mod 内容自动生成信息栏。

[![License: LGPL-3.0](https://img.shields.io/badge/license-LGPL--3.0-blue)](https://www.gnu.org/licenses/lgpl-3.0.html)
[![BepInEx 5.4](https://img.shields.io/badge/BepInEx-5.4.x-orange)](https://github.com/BepInEx/BepInEx)
[![CUCoreLib](https://img.shields.io/badge/dependency-CUCoreLib-green)](https://github.com/jimmyking9999999/CUCoreLib)

[中文](#中文) | [English](#english)

---

## 中文

### 简介

ModInfoPanel 只处理 **mod 内容**（原版物品/流体/生物不动）：读取 CUCoreLib 与其他 mod 注册的物品、流体、自定义动物，把 [未知伤亡维基中文翻译项目](https://github.com/dodo23333/cu-chinese-wiki-translations) 风格的富文本信息块追加到悬停信息栏。

### 功能特性

| 内容 | 显示位置 | 数据来源 |
|------|----------|----------|
| mod 物品 | 背包 / 拖拽 / 世界物品悬停 | `CUCoreLib.Registries.ItemRegistry` + `Item.GlobalItems` |
| mod 流体 | 按住 Shift 展开时插入到对应液体描述之后、性质之前 | `CUCoreLib.Registries.LiquidRegistry` |
| 世界 mod 流体 | 流体悬停信息栏 | `CUCoreLib.Registries.LiquidTileRegistry` |
| 自定义动物 | 生物悬停信息栏 | `CUCoreLib.Registries.BuildingEntityRegistry`（`Animal=true`） |
| 配方 | `制作(INT)：材料=结果`（含 mod 配方） | `Recipes.recipes`（进入对局后增量刷新） |
| 状态效果 | 数值 + 按数值匹配的等级说明 | 委托 IL + `MoodleRegistry.AddMoodle` 阈值 |
| moodle 悬停框 | `当前 / 阈值 / 相关物品` | 状态类字段反射读取 |

其他：

- **委托 IL 效果提取**：读取 `useAction`/`useLimbAction`（物品）与 `onDrink`/`onApplyToLimb`/`onInject`（液体）的 IL，支持两种乘法顺序、`Mathf.Min/Max/Clamp`、`*=`/`/=`，并递归提取 `CoUtils.DoTimedOp` 等嵌套委托的延迟效果（前缀 `延迟`）。
- **JSON 缓存**：首次生成 `InfoCache/{kind}/{mod}/{id}.json`，之后启动只校验变化，未变不写盘；`overrides.json` 可手写覆盖，`acquisition.json` 可编辑获取来源映射，`CustomData` 供 mod 作者补充效果行。
- **液体信息位置**：折叠时不显示；按住 Shift（展开描述）时插入到对应液体的描述之后、`具有性质：…` 之前；不生成液体制作行（汉化描述自带）。

### 生成示例（物品）

```
<color=#72d572>┃心情 +1
┃疼痛 +3
┃肌肉健康 -5
┃大麻素 +10（轻度大麻影响：体内流淌的大麻物质造成了一定的视觉变形…）
┃分类：Medicine
┃价值：3
┃识别所需INT：2</color>
<color=#ffa726>┃消耗 20.5% 耐久</color>
<color=#ffee58>┃制作(4)：1水芦藤(>=50%)+1(热源)=3水芦烟</color>
<color=orange>特点：耐久归零销毁，重量随耐久变化，可合成</color>
<color=#9e9e9e>来源模组：com.yourName.CUDrugEx</color>
```

颜色约定（调色板来源：[未知伤亡维基中文翻译项目](https://github.com/dodo23333/cu-chinese-wiki-translations)）：蓝 `#91a7ff`=装备/体积数值，绿 `#72d572`=效果数值与数据，橙 `#ffa726`=腐败/消耗，黄 `#ffee58`=获取/制作，红 `#e84e40`=生物生命，`<color=orange>`=特点单行，灰 `#9e9e9e`=来源模组。

### 安装

1. 安装 **BepInEx 5.4.x**（BepInEx 5.4 系列）。
2. 安装 **[CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)**（硬依赖）：`CUCoreLib.dll` → `BepInEx/plugins/`。
3. 安装本插件：

```
BepInEx/plugins/ModInfoPanel/ModInfoPanel.dll
BepInEx/plugins/ModInfoPanel/Locale/locale.zh-CN.json
BepInEx/plugins/ModInfoPanel/Locale/locale.EN.json
```

### 缓存与手工覆盖

```
BepInEx/plugins/ModInfoPanel/
├─ ModInfoPanel.dll
├─ Locale/…
├─ overrides.json          ← 手工覆盖，永不自动改写（可含颜色标签）
├─ acquisition.json        ← 分类→获取来源映射，可编辑热重载
└─ InfoCache/
   ├─ items/{mod}/{id}.json
   ├─ liquids/{mod}/{id}.json
   ├─ liquidTiles/{mod}/{id}.json
   └─ creatures/{mod}/{id}.json
```

`overrides.json` 示例：

```json
{
  "items":       { "someitem": "手工物品文本" },
  "liquidTiles": { "sometile": "手工世界流体文本" },
  "creatures":   { "someanimal": "手工生物文本，可含 {health}" },
  "liquids":     { "someliquid": "<color=#91a7ff>┃每升价值：10</color>" }
}
```

`CustomData` 约定（mod 作者可在 `CustomItemInfo.CustomData` 写入 string 或 string[]）：

| 键 | 效果 |
|----|------|
| `effects` | 追加到“效果”段（如 `"心情 +1"`, `"大麻素 +10"`） |
| `info` | 追加到“数据”段 |
| `acquire` | 替换自动获取行 |
| `features` | 追加到“特点”段 |

### 配置

`BepInEx/config/com.local.modinfopanel.cfg`

| 键 | 默认 | 说明 |
|----|------|------|
| `General.Enabled` | true | 总开关 |
| `General.ShowItems` | true | mod 物品信息 |
| `General.ShowLiquids` | true | mod 流体信息（Shift 详情中对应液体描述之后 / 世界流体悬停） |
| `General.ShowCreatures` | true | 自定义动物信息 |
| `General.ExpandOnly` | false | 仅按住展开描述键（Shift）时显示 |
| `General.DescriptionMode` | Append | `Append` / `FillMissing` / `Replace` |
| `General.ShowSourceMod` | true | 显示来源 mod GUID |
| `General.ShowCustomData` | false | 显示未使用的 CustomData（调试） |
| `General.ShowRecipes` | true | 显示“制作(INT)：…”行 |
| `General.ShowStatusDescriptions` | true | 状态数值后附加等级说明 |
| `General.ShowMoodleInfo` | true | moodle 悬停框追加当前值/阈值/相关物品 |
| `Language.Mode` | Auto | `Auto` / `ZhCN` / `EN` |
| `Cache.Enabled` | true | 启用 JSON 缓存 |
| `Cache.ForceRebuild` | false | 每次启动强制重建缓存 |
| `Cache.OverridesFile` | overrides.json | 手工覆盖文件名 |
| `Creature.UseRuntimeValues` | false | 生物血量用定义值 / 运行时值 |
| `Advanced.UseVanillaLocaleBaseline` | true | 用原版 `Lang/EN.json` 识别非 CUCoreLib mod 物品 |
| `Advanced.DebugLog` | false | 调试日志 |

### 兼容性

- **原版**：不修改原版物品/流体/生物。
- **联机**：缓存为本地生成，各端各自生成；`overrides.json` / `acquisition.json` 如需一致请自行同步。

### 从源码构建

把仓库克隆到游戏目录（推荐，默认相对路径 `..\..`）：

```powershell
git clone <repo-url> "D:\Steam\steamapps\common\Casualties Unknown Demo\mods\ModInfoPanel"
cd "D:\Steam\steamapps\common\Casualties Unknown Demo\mods\ModInfoPanel"
dotnet build -c Release
```

仓库在其它位置时，用 `GameRoot` 指定游戏根目录：

```powershell
dotnet build -c Release -p:GameRoot="D:\Steam\steamapps\common\Casualties Unknown Demo"
```

产物：`bin\Release\ModInfoPanel.dll` 与 `bin\Release\Locale\`，复制到 `BepInEx/plugins/ModInfoPanel/` 即可。

### 已知限制

- IL 效果提取只支持简单模式（常量加减、两种乘法顺序、`Mathf.Min/Max/Clamp`、`*=`/`/=`、嵌套委托）；循环、随机、辅助方法内的复杂逻辑不会提取，可用 `CustomData` 或 `overrides.json` 补充。
- 获取来源只显示可验证项：CUCoreLib `DropPool`、交易（分类在交易池且价值>0）、`acquisition.json` 映射。

### 许可证与致谢

本项目采用 **LGPL-3.0**（见 [LICENSE](LICENSE)）。

硬依赖 [CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)（LGPL-3.0），本插件仅以独立 DLL 形式动态引用，未内嵌其代码或二进制。

#### 致谢

- [未知伤亡维基中文翻译项目](https://github.com/dodo23333/cu-chinese-wiki-translations)（MIT）：信息栏颜色与排版风格参考该项目。
- [DeepSeek](https://www.deepseek.com/)：本插件在开发过程中使用了 DeepSeek 模型辅助。

---

## English

### Overview

ModInfoPanel only touches **mod content** (vanilla items/liquids/creatures are left alone): it reads items, liquids and custom animals registered through CUCoreLib and other mods, and appends an info block styled after the [CU Chinese Wiki Translations](https://github.com/dodo23333/cu-chinese-wiki-translations) project to hover tooltips.

### Features

| Content | Where | Source |
|---------|-------|--------|
| Mod items | Inventory / drag / world hover | `CUCoreLib.Registries.ItemRegistry` + `Item.GlobalItems` |
| Mod liquids | Inserted after the matching liquid's description and before its qualities, while Shift-expanded | `CUCoreLib.Registries.LiquidRegistry` |
| Mod world fluids | Fluid hover tooltip | `CUCoreLib.Registries.LiquidTileRegistry` |
| Custom animals | Creature hover tooltip | `CUCoreLib.Registries.BuildingEntityRegistry` (`Animal=true`) |
| Recipes | `Craft(INT): ingredients = result` (incl. mod recipes) | `Recipes.recipes` (refreshed after world-gen) |
| Status effects | Value + level description matched by value | delegate IL + `MoodleRegistry.AddMoodle` thresholds |
| Moodle tooltips | `Current / Thresholds / Related items` | reflected status fields |

Also:

- **Delegate IL extraction**: reads `useAction`/`useLimbAction` (items) and `onDrink`/`onApplyToLimb`/`onInject` (liquids), supporting both multiplication orders, `Mathf.Min/Max/Clamp`, `*=`/`/=`, and recursively scans nested delegates (e.g. `CoUtils.DoTimedOp` delayed effects, prefixed `延迟`).
- **JSON cache**: generated once at `InfoCache/{kind}/{mod}/{id}.json`; later launches only rewrite changed entries. `overrides.json` for manual text, `acquisition.json` for editable loot-source mapping, `CustomData` for mod authors.
- **Liquid info placement**: hidden when collapsed; while Shift-expanded it is inserted after the matching liquid's description and before its qualities. Liquid recipe lines are not generated (the localized description already has them).

### Example output (item)

```
<color=#72d572>┃Happiness +1
┃Pain +3
┃Muscle health -5
┃THC +10 (Mild cannabis effect: …)
┃Category: Medicine
┃Value: 3</color>
<color=#ffa726>┃Consumes 20.5% durability</color>
<color=#ffee58>┃Craft(4): 1 musharm (>=50%) + 1 (heat source) = 3 musharm cigarette</color>
<color=orange>Traits: destroyed at 0 condition, weight scales with condition, combinable</color>
<color=#9e9e9e>Source mod: com.yourName.CUDrugEx</color>
```

### Installation

1. Install **BepInEx 5.4.x**.
2. Install **[CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)** (hard dependency): `CUCoreLib.dll` → `BepInEx/plugins/`.
3. Copy the plugin:

```
BepInEx/plugins/ModInfoPanel/ModInfoPanel.dll
BepInEx/plugins/ModInfoPanel/Locale/locale.zh-CN.json
BepInEx/plugins/ModInfoPanel/Locale/locale.EN.json
```

### Cache & overrides

```
BepInEx/plugins/ModInfoPanel/
├─ ModInfoPanel.dll
├─ Locale/…
├─ overrides.json          ← manual text, never auto-rewritten
├─ acquisition.json        ← category → loot sources, hot-reloaded
└─ InfoCache/
   ├─ items/{mod}/{id}.json
   ├─ liquids/{mod}/{id}.json
   ├─ liquidTiles/{mod}/{id}.json
   └─ creatures/{mod}/{id}.json
```

`CustomData` keys (string or string[] on `CustomItemInfo.CustomData`): `effects`, `info`, `acquire`, `features`.

### Configuration

`BepInEx/config/com.local.modinfopanel.cfg` — see the Chinese table above (same keys).

### Compatibility

- **Vanilla content**: untouched.
- **Multiplayer**: caches are generated locally per client; sync `overrides.json` / `acquisition.json` manually if you want identical text.

### Building from source

Clone into the game folder (default relative `..\..`):

```powershell
git clone <repo-url> "D:\Steam\steamapps\common\Casualties Unknown Demo\mods\ModInfoPanel"
cd "D:\Steam\steamapps\common\Casualties Unknown Demo\mods\ModInfoPanel"
dotnet build -c Release
```

Or from anywhere with an explicit game root:

```powershell
dotnet build -c Release -p:GameRoot="D:\Steam\steamapps\common\Casualties Unknown Demo"
```

Output: `bin\Release\ModInfoPanel.dll` and `bin\Release\Locale\`.

### Known limitations

- IL extraction covers simple patterns only (constant add/sub, both multiplication orders, `Mathf.Min/Max/Clamp`, `*=`/`/=`, nested delegates). Loops/random/helper-method logic is not extracted; use `CustomData` or `overrides.json`.
- Acquisition lines only list verifiable sources (CUCoreLib `DropPool`, traders, `acquisition.json` mapping).

### License & credits

Licensed under **LGPL-3.0** (see [LICENSE](LICENSE)).

Hard dependency: [CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib) (LGPL-3.0). This plugin references it as a separate DLL only and does not embed its code or binaries.

#### Credits

- [CU Chinese Wiki Translations](https://github.com/dodo23333/cu-chinese-wiki-translations) (MIT): the tooltip colors and layout style follow this project.
- [DeepSeek](https://www.deepseek.com/): this plugin was developed with the assistance of the DeepSeek model.
