# String Filter Design

## Goal

Add a first-pass row filter to CSVHelper. A user selects one column, one comparison operator, and one comparison string. The grid then shows only matching rows.

## Scope

- Add a Filter button to the existing top toolbar, beside Search.
- Open a modal `FilterDialog` with:
  - a column drop-down populated from the loaded CSV;
  - an operator drop-down: `=`, `>`, `<`, `>=`, `<=`;
  - a text box for the comparison value;
  - Apply and Clear buttons.
- Allow one active condition only in this version.
- Display a status such as `Filtered: 32 / 100 rows` while a filter is active.
- Keep sorting usable with the filtered view.

## Semantics

- Every CSV column remains a `string`, as it is today.
- All five operators use the same string-comparison semantics as the current `DataView` sorting. There is no numeric, date, or locale-independent conversion layer.
- Applying a filter changes only the `DataView` shown by the `DataGridView`; it never removes or alters `DataTable.Rows`.
- Saving writes every original data row, including rows currently hidden by the filter.
- Clear restores all rows and removes the filtered status.
- Loading or reloading a file clears the active filter.

## Architecture

- `FilterDialog` owns only input controls and exposes the selected column, operator, and value after `OK`.
- `MainForm` owns the active filter state and constructs a safely escaped `DataView.RowFilter` expression for the current `DataTable`.
- Existing `ApplySort()` continues to set `DataView.Sort`; filtering and sorting coexist on that view.
- The status update derives visible rows from `table.DefaultView.Count` and total rows from `table.Rows.Count`.

## Validation and Errors

- The Filter button remains disabled until a CSV is loaded.
- Empty comparison text is valid: it permits filtering empty cells and lexicographic comparison with the empty string.
- Column identifiers and string literals must be escaped when building `RowFilter`, so CSV headers or values containing special characters cannot break the expression.
- If a `RowFilter` expression cannot be applied, leave the previous filter intact, show an error, and do not mark the document dirty.

## Verification

- Build the Release project with the repository command.
- Add parser-independent filter coverage for equality, each relational operator, empty comparison values, quoted/special-character values, filter clearing, and interaction with sort.
- Manually verify the toolbar dialog, visible-row count, clear action, and that save includes hidden rows.

## Deferred

- Multiple conditions and AND/OR grouping.
- Per-column header filter buttons, checkbox value lists, and persisted filters.
