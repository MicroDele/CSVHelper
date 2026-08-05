# String Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a one-condition, string-based row filter that changes only the CSV grid view.

**Architecture:** `FilterDialog` captures a column, comparison operator, and text value. A small `StringFilter` helper converts that selection into a safely escaped `DataView.RowFilter` expression; `MainForm` owns the active filter, applies it to the existing `DataTable.DefaultView`, and reports visible-row count.

**Tech Stack:** C# 14 / .NET 10 WinForms, `System.Data.DataView`, existing PowerShell smoke-test convention.

## Global Constraints

- CSV columns and filter operands stay strings; do not parse numbers or dates.
- Support exactly `=`, `>`, `<`, `>=`, and `<=`.
- Filtering never removes, changes, or omits rows when saving the CSV.
- Sorting and filtering must coexist on the same `DataView`.
- Loading or reloading clears any active filter.
- Preserve the existing UI theme and minimal-patch style.

---

### Task 1: Create and verify the filter-expression boundary

**Files:**
- Create: `CsvReaderApp/StringFilter.cs`
- Create: `tests/StringFilter.Tests.ps1`

**Interfaces:**
- Produces: `internal sealed record StringFilter(string ColumnName, string Operator, string Value)`.
- Produces: `string StringFilter.ToRowFilterExpression()`.
- Consumes: no UI state; output is assigned to `DataView.RowFilter`.

- [x] **Step 1: Write the failing PowerShell test**

Create `tests/StringFilter.Tests.ps1` that loads the built `CSVHelper.dll`, constructs a `DataTable` with `Name`, `Rank`, and `A]B` string columns, adds values including `O'Brien`, and asserts each generated expression can be assigned to `DefaultView.RowFilter` and returns the expected count for `=`, `>`, `<`, `>=`, and `<=`.

- [x] **Step 2: Run the test to verify it fails**

Run: `pwsh -File .\tests\StringFilter.Tests.ps1`

Expected: FAIL because `StringFilter` is not defined.

- [x] **Step 3: Implement the minimal helper**

Create `StringFilter` with constructor validation for the five allowed operators. Escape `]` in column names for bracketed `DataView` identifiers and escape `'` in values by doubling it. Return `[{escapedColumn}] {Operator} '{escapedValue}'`.

- [x] **Step 4: Build and rerun the test**

Run: `dotnet build .\CsvReaderApp\CsvReaderApp.csproj -c Release; pwsh -File .\tests\StringFilter.Tests.ps1`

Expected: build succeeds and all five filter cases pass.

- [x] **Step 5: Commit**

Run: `git add CsvReaderApp/StringFilter.cs tests/StringFilter.Tests.ps1; git commit -m "Add string filter expressions"`

### Task 2: Add the filter dialog and toolbar entry point

**Files:**
- Create: `CsvReaderApp/FilterDialog.cs`
- Modify: `CsvReaderApp/UiTheme.cs`
- Modify: `CsvReaderApp/MainForm.cs`

**Interfaces:**
- Consumes: `FilterDialog(IReadOnlyList<string> columns, StringFilter? current)`.
- Produces: `StringFilter? FilterDialog.Result`; `null` represents Clear.
- Consumes: `MainForm.ApplyFilter(StringFilter? filter)`.

- [x] **Step 1: Add the initially failing UI integration references**

In `MainForm`, add a disabled `filterButton`, wire it to `OpenFilterDialog()`, and call the missing `FilterDialog` and `ApplyFilter` methods. Build to confirm unresolved symbols.

- [x] **Step 2: Implement FilterDialog**

Create a fixed modal dialog styled with `UiTheme`, containing Column and Operator combo boxes, a Value text box, Apply, Clear, and Cancel controls. Prepopulate from the current filter. Apply sets `DialogResult.OK` with a non-null `Result`; Clear sets `DialogResult.OK` with `Result = null`; Escape and Cancel close without changes.

- [x] **Step 3: Add the toolbar icon and dialog invocation**

Add `UiIconKind.Filter` and draw a small funnel. Position the Filter button after Search. In `OpenFilterDialog()`, pass current table column names and active filter; call `ApplyFilter(dialog.Result)` only after OK.

- [x] **Step 4: Build**

Run: `dotnet build .\CsvReaderApp\CsvReaderApp.csproj -c Release`

Expected: 0 warnings and 0 errors.

- [x] **Step 5: Commit**

Run: `git add CsvReaderApp/FilterDialog.cs CsvReaderApp/UiTheme.cs CsvReaderApp/MainForm.cs; git commit -m "Add filter dialog"`

### Task 3: Apply, clear, and preserve filters through grid operations

**Files:**
- Modify: `CsvReaderApp/MainForm.cs`
- Modify: `tests/StringFilter.Tests.ps1`

**Interfaces:**
- Consumes: `StringFilter? activeFilter` and `DataTable.DefaultView`.
- Produces: `ApplyFilter(StringFilter? filter)` and `UpdateFilterStatus(DataTable table)` behavior.

- [x] **Step 1: Extend the failing test with data-view behavior**

Add assertions that applying then clearing a generated filter returns the full number of rows and that setting `DefaultView.Sort = "[Name] ASC"` before filtering still yields the filtered expected order.

- [x] **Step 2: Implement MainForm filter state**

Add `activeFilter`. `ApplyFilter` sets `table.DefaultView.RowFilter` to empty for clear or to `filter.ToRowFilterExpression()` for apply. If setting the new expression throws, restore the old RowFilter, show an error, and keep `activeFilter` unchanged. On success, clear grid selection, refresh any active search, and set status to `Filtered: {visible} / {total} rows`; clear restores the normal loaded status.

- [x] **Step 3: Clear filter on file load and keep filter after sorting**

Reset `activeFilter` and `table.DefaultView.RowFilter` when `LoadCsvFile` succeeds. Leave `ApplySort` responsible only for `DefaultView.Sort`, so its existing behavior cannot clear `RowFilter`. Enable the filter button when a file is loaded.

- [x] **Step 4: Build and run the smoke tests**

Run: `dotnet build .\CsvReaderApp\CsvReaderApp.csproj -c Release; pwsh -File .\tests\StringFilter.Tests.ps1`

Expected: build succeeds and the filter tests pass.

- [x] **Step 5: Commit**

Run: `git add CsvReaderApp/MainForm.cs tests/StringFilter.Tests.ps1; git commit -m "Apply grid row filters"`

### Task 4: Publish-ready verification

**Files:**
- Modify: none unless verification uncovers a defect.

**Interfaces:**
- Verifies: release build and published WinForms artifact.

- [x] **Step 1: Run final static checks**

Run: `git diff --check; dotnet build .\CsvReaderApp\CsvReaderApp.csproj -c Release; pwsh -File .\tests\StringFilter.Tests.ps1`

Expected: no whitespace errors, 0 build warnings/errors, tests pass.

- [x] **Step 2: Produce distribution artifact**

Run: `dotnet publish .\CsvReaderApp\CsvReaderApp.csproj -c Release -o .\dist`

Expected: `dist\CSVHelper.exe` is updated.

- [x] **Step 3: Report manual verification boundary**

State that build, expression tests, and publish output were verified; opening the WinForms dialog, applying a filter, and saving hidden rows require a manual UI check.
