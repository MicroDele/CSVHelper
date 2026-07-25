# CSV Reader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimal Windows desktop CSV viewer/editor for UTF-8 CSV files.

**Architecture:** A C# WinForms app displays CSV data in a `DataGridView`. CSV parsing and serialization are isolated in `CsvDocument.cs`; UI behavior lives in `MainForm.cs`.

**Tech Stack:** C# 14, .NET 10, WinForms, UTF-8 text files.

## Global Constraints

- UTF-8 only.
- Features are limited to open CSV, table display, edit cell, save, copy cell/header, and full-row highlight on selection.
- Publish runnable files to `dist/`.

---

### Task 1: CSV Parser

**Files:**
- Create: `CsvReaderApp/CsvDocument.cs`

**Interfaces:**
- Produces: `CsvDocument.Parse(string text)` returning headers and rows.
- Produces: `CsvDocument.ToCsvText()` returning UTF-8-ready CSV text.

- [x] Support quoted commas, escaped quotes, multiline fields, and serialization.

### Task 2: Desktop UI

**Files:**
- Create: `CsvReaderApp/MainForm.cs`

**Interfaces:**
- Consumes: `CsvDocument.Parse` and `CsvDocument.ToCsvText`.

- [x] Build WinForms UI with Open and Save buttons.
- [x] Load UTF-8 CSV into the grid.
- [x] Allow normal `DataGridView` cell editing.
- [x] Highlight the full row when a cell is selected.
- [x] Add context menu commands for copying selected cell and selected header.

### Task 3: Publishing

**Files:**
- Output: `dist/CsvReaderApp.exe`

- [x] Publish Release output to `dist/`.

### Task 4: Verification

**Files:**
- Build: `CsvReaderApp/CsvReaderApp.csproj`

- [x] Run `dotnet build .\CsvReaderApp\CsvReaderApp.csproj -c Release`.
