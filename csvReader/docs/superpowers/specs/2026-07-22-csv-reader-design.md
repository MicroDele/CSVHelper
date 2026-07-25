# CSV Reader Desktop Design

Goal: build a minimal Windows desktop CSV viewer/editor for UTF-8 CSV files.

Scope:
- Open a CSV file and show it as a table.
- Edit individual cells in the table.
- Copy the selected cell or selected column header.

Architecture:
- `CsvReaderApp` is a C# WinForms desktop application.
- `MainForm.cs` hosts the `DataGridView` UI, open/save commands, row highlighting, and copy commands.
- `CsvDocument.cs` owns CSV parsing and serialization.
- `dist/CsvReaderApp.exe` is the runnable desktop program.

Constraints:
- UTF-8 only for the first version.
- Save is explicit through the Save button or `Ctrl+S`; closing prompts when changes are unsaved.
- No row or column add/delete in this first scope.
- Requires the installed .NET 10 desktop runtime.
