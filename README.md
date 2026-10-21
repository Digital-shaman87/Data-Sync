# Data-Sync

One-way periodic folder synchronization tool in C#, created for the Veeam SDET (Internal Development in QA) technical task.

## Transparency Note
This repository was built with the assistance of AI (Google Gemini). Rather than generating over-engineered boilerplate, the AI was intentionally directed to take a minimal, human-readable approach while focusing on practical QA edge cases, verification, and maintainability.

## What It Does
Synchronizes a `source` directory to a `replica` directory on a fixed interval so `replica` always matches `source`:
- **Creates & updates files:** Compares file size first (fast check) and falls back to an `MD5` hash check only when file sizes match.
- **Removes stale files & folders:** Deletes files and subdirectories in `replica` that no longer exist in `source`.
- **Logs all operations:** Writes timestamped creation, update, and deletion events to both the console and the log file.

## QA & Edge Cases Handled
- **Overlapping paths blocked:** Prevents `source` and `replica` from being the same folder or nested inside one another.
- **Log file isolation:** Prevents the log file from being placed inside `source` or `replica` (which would otherwise trigger endless syncs).
- **Read-only & locked files:** Clears read-only attributes before overwriting/deleting in `replica`, and catches `IOException` / `UnauthorizedAccessException` per file so a locked file does not crash the periodic loop.
- **Graceful shutdown:** Handles `Ctrl+C` cleanly via `CancellationTokenSource`.

## Usage

### Run Synchronization
```powershell
dotnet run --project "Snyc Content/Snyc Content.csproj" -- <sourcePath> <replicaPath> <intervalSeconds> <logFilePath>
```

### Run Built-in Self-Test
```powershell
dotnet run --project "Snyc Content/Snyc Content.csproj" -- --self-test
```
