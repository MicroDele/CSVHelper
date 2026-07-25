# CSV 搜索功能设计

- 日期：2026-07-25
- 状态：已批准，待实现

## 目标

为 CSVHelper 增加「按单元格搜索」功能：全表部分匹配、不区分大小写、上一个/下一个导航、匹配计数、高亮所有命中。

## 非目标（YAGNI）

- 区分大小写切换
- 仅当前列搜索
- 整词匹配（whole word）
- 替换功能
- 过滤式（隐藏不匹配行）

## 方案：视图层搜索

直接遍历 `grid` 显示中的单元格（`grid.Rows × Columns`），命中存为 `(rowIndex, colIndex)` 列表，用单元格 `Style` 染色高亮。

**理由**：列头可排序，排序后显示顺序 ≠ `DataTable` 行顺序；遍历视图层可保证排序、列宽变化后高亮与导航始终一致。备选的数据层搜索在排序后索引映射易错，过滤式与「高亮+导航」范式不符，均不采用。

## UI

- 顶部工具栏下方新增 `searchPanel`：`TextBox`（占位提示 `Search…`）+ `Prev(↑)` / `Next(↓)` 按钮 + 计数 `Label`（如 `3 / 15`）。
- 实时搜索：`TextChanged` 触发，~200ms 防抖，避免大表每次按键全扫。
- 快捷键：`Ctrl+F` 聚焦搜索框、`Enter` 下一个、`Shift+Enter` 上一个、`Esc` 清空并清除高亮。

## 匹配

- 全表、部分匹配、不区分大小写（`IndexOf(..., StringComparison.OrdinalIgnoreCase)`）。
- 抽出纯逻辑方法 `SearchMatches(DataGridView grid, string query) -> List<(int row, int col)>`，便于将来加单测。

## 高亮与导航

- 命中单元格 `BackColor = #FFF3B0`（淡黄）。
- 当前定位的命中 `BackColor = #FFC24D`（深黄），与其余命中区分。
- `Prev/Next` 在命中列表内**循环**移动，`grid.CurrentCell` 跳转 + `FirstDisplayedScrollingRowIndex` 滚动到可见。
- 计数显示 `(currentIndex + 1) / total`。

## 与现有逻辑协调

- 高亮优先级：单元格 `Style`（命中色）高于行默认背景；现有选中行淡蓝（`#E8F0FE`）逻辑保留，颜色不冲突。
- 清除时机统一走「重新执行搜索」路径（先重置旧高亮，再画新）：搜索框清空 / 切换文件 / 编辑单元格（`MarkDirty` 时若搜索框非空则重算）/ 列头排序后。

## 边界情况

- 空查询 → 清除全部高亮，计数隐藏。
- 无匹配 → `0 / 0`，状态栏提示 `No matches`。
- 空表 → 不报错，正常返回空命中列表。

## 双目录同步

`CsvReaderApp/` 与 `csvReader/CsvReaderApp/` 两份保持同步修改（见 `AGENTS.md`），改完均执行 `dotnet build` 验证。

## 测试

当前无测试项目。核心匹配逻辑抽成可测方法以便将来加单测；UI 层手动验证：部分匹配、大小写、多命中导航循环、清空清除高亮、排序后行为、空表。
