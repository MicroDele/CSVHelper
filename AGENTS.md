# Repository Guidelines

## Project Structure & Module Organization

This repository contains a small .NET Windows Forms CSV viewer/editor. The source tree is `CsvReaderApp/`:

- `Program.cs` starts the WinForms app and supports `--inspect <csv> [output]` for lightweight parsing checks.
- `MainForm.cs` owns the UI, file open/save flow, grid behavior, and context menu actions.
- `CsvDocument.cs` contains CSV parse and serialization logic.
- `Assets/` stores the application icon.

Build outputs are under `bin/`, `obj/`, and published `dist/`; do not edit generated files directly.

## Build, Test, and Development Commands

Run commands from the repository root:

```powershell
dotnet build .\CsvReaderApp\CsvReaderApp.csproj -c Release
dotnet publish .\CsvReaderApp\CsvReaderApp.csproj -c Release -o .\dist
dotnet .\CsvReaderApp\bin\Release\net10.0-windows\CSVHelper.dll --inspect sample.csv
```

Use the first command for compile verification, the second to update the runnable `dist\CSVHelper.exe`, and `--inspect` for quick parser smoke checks without opening the UI. If `dist\CSVHelper.exe` is running during publish, close or terminate it before retrying.

## Coding Style & Naming Conventions

Use C# with nullable reference types and implicit usings enabled. Keep four-space indentation, file-scoped namespaces, `PascalCase` for types and methods, and `camelCase` for private fields and local variables. Prefer small, focused event handlers in `MainForm.cs`; move CSV-specific logic to `CsvDocument.cs` instead of embedding it in UI code.

## Testing Guidelines

No dedicated test project currently exists. For parser changes, add or use `--inspect` scenarios with representative CSV files, including quoted fields, embedded commas, CRLF line endings, and empty data. For UI changes, build the app and manually verify the affected workflow in the WinForms app.

## Commit & Pull Request Guidelines

Git history is not available in this checkout, so use concise imperative commit messages such as `Fix grid selection clearing` or `Update CSV escaping`. Pull requests should describe the behavior change, list verification commands, mention any manual UI checks, and include screenshots only for visible UI changes.

## Agent-Specific Instructions

Preserve user changes, avoid broad refactors, and prefer minimal patches plus explicit build verification.
