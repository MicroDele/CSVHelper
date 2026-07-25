# CSV 搜索对话框设计

- 日期：2026-07-25
- 状态：已实现（迭代中）
- 关联：取代主窗体内嵌搜索条（前序设计见 `2026-07-25-search-design.md`）

## 目标

把主窗体内嵌搜索条改为「工具栏图标按钮 + 非模态弹出对话框」（Notepad++ 风格），保留并增强搜索能力：手动查找、整单元格精确匹配（可切换）、上一个/下一个循环导航、命中计数。

## 非目标（YAGNI）

- 区分大小写切换
- 正则表达式
- 替换功能
- 过滤式（隐藏不匹配行）
- 搜索历史下拉
- **实时搜索（输入即搜）/ 失焦透明**：曾尝试。实时搜索需防抖 timer 增加复杂度；失焦透明（设 `Form.Opacity`）在窗口激活时序触发 `Win32Exception(87)`。两者均已移除，改为手动查找。

## 架构方案

搜索逻辑与状态留在 `MainForm`，对话框为纯 UI 触发器：

- `SearchMatches`/`HighlightMatches`/`NavigateToCurrentMatch`/`NavigateMatch` 等保留在 `MainForm`。
- `ApplySearch(query, wholeCell)` 执行搜索；`SearchMatches` 按整单元格 / 部分匹配两种模式。
- `SearchDialog` 只负责 UI，通过事件通知 `MainForm` 执行搜索；`MainForm` 回调 `SearchDialog.UpdateMatchCount` 更新计数。

**理由**：高亮与导航必须操作 `grid`（位于 `MainForm`），状态留在 `MainForm` 改动最小；对话框轻量、可独立理解。

## UI

### 工具栏

`multiSortButton` 右侧 `searchButton`（图标按钮，`UiIconKind.Search`，tooltip `Search (Ctrl+F)`），点击打开/聚焦搜索对话框。未加载文件时禁用。

### 搜索对话框（`SearchDialog`）

- 外观：`FixedDialog`、`StartPosition.Manual`（手动居中于主窗体——`CenterParent` 对非模态 `Show` 不可靠）、`UiTheme.ApplyForm`；**非模态**（`Show(owner)`）。
- 布局（`ClientSize 360 × 128`）：
  - `Find:` + `TextBox` + 查找按钮（放大镜 `UiIconKind.Search`）
  - `CheckBox`「Match whole cell」
  - `↑ Previous` / `↓ Next` 按钮（复用 `Up`/`Down` 图标）
  - 计数 `Label`（`3 / 15` 或 `No matches`）
  - 关闭用窗口右上角 ×（不再设额外 Close 按钮）

## 交互

- **Ctrl+F**：打开对话框；已打开则聚焦输入框并全选。
- **对话框内 Esc**：全局（`KeyPreview`）关闭对话框。
- **查找**：点放大镜按钮或回车 → 触发搜索（高亮所有命中 + 定位第一个）。**手动触发，无防抖、不实时**。
- **Enter / Shift+Enter**：查找 / 上一个。
- **Prev / Next**：在已查找的命中间循环导航 + 滚动到可见。
- **Match whole cell 切换**：若已输入查询则立即重搜。
- **关闭（× / Esc）**：清除表格高亮、重置计数；`MainForm` 缓存上次查询/选项，下次打开回填并自动执行一次。

## 匹配

- 部分匹配（默认）：`value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0`。
- 整单元格匹配（勾选）：`value.Equals(query, StringComparison.OrdinalIgnoreCase)`。
- **空查询**：搜索所有空单元格（`value.Length == 0`，即 CSV `,,` 之间的空）；无空单元格时状态栏 `No empty cells`。

## 对话框与主窗体接口

`SearchDialog` 暴露：

- 属性 `string Query`、`bool WholeCell`（初始值由构造传入）。
- 事件 `SearchRequested`（查找按钮 / 回车 / 复选框变化触发，无防抖）、`NavigateRequested(int direction)`；关闭复用 `FormClosed`。
- 方法 `UpdateMatchCount(int current, int total)`、`FocusQuery()`、`ClearInput()`。

`MainForm` 内部方法（对话框事件处理调用）：

- `ApplySearch(string query, bool wholeCell)`
- `NavigateMatch(int direction)`
- `ResetSearch()`（清除高亮、重置匹配状态与计数）
- `OpenSearchDialog()`（创建/聚焦对话框、订阅事件、手动居中）

## 代码改动

- `MainForm.cs`：删 `searchPanel` 及字段；加 `searchButton`/`searchDialog`/`lastSearchQuery`/`lastSearchWholeCell`；`ApplySearch` 加 `wholeCell`；`SearchMatches` 支持 `wholeCell`；`Ctrl+F` 打开对话框；`OpenSearchDialog` 手动居中 + 事件订阅。
- `SearchDialog.cs`：纯 UI，手动查找，`KeyPreview` 全局 Esc。
- `UiTheme.cs`：`UiIconKind.Search`；`ApplyIconButton` 区分启用/禁用态（禁用时图标/边框/背景整体暗淡）。

## 边界情况

- 未加载文件：`searchButton` 禁用。
- 空查询：搜索所有空单元格；无则 `No empty cells`。
- 无匹配：`No matches`，Prev/Next 禁用。
- 对话框已打开再点按钮 / Ctrl+F：聚焦输入框，不重复创建。
- 关闭后重新打开：回填上次查询/选项并自动执行一次。

## 测试

无测试项目。手动验证：手动查找（点按钮 / 回车）、部分/整单元格切换、多命中导航循环、计数、Ctrl+F、Esc 关闭清高亮、关闭重开回填、排序/编辑后重算、空表、未加载按钮禁用、禁用态视觉。改完 `dotnet publish` 到 `dist`。

## 源码目录

当前源码以 `CsvReaderApp/` 为准，发布到 `dist/`（构建约定见 `AGENTS.md`）。
