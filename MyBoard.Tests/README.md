# MyBoard automated tests

Run on Windows with the .NET 10 SDK and Windows Desktop runtime. The xUnit project targets `net10.0-windows` and references the WPF application. Internal production types are exposed only to this test assembly through `InternalsVisibleTo`.

```powershell
dotnet restore MyBoard.slnx
dotnet test MyBoard.Tests/MyBoard.Tests.csproj --configuration Debug --no-restore --logger "trx;LogFileName=tests.trx"
dotnet build MyBoard.slnx --configuration Debug --no-restore
dotnet build MyBoard.slnx --configuration Release --no-restore
git diff --check
```

There are 70 executed test cases, including theory rows. No tests are skipped. TRX output is under `MyBoard.Tests/TestResults/` (git-ignored).

## Isolation

Every storage test creates a GUID-named directory under the OS temporary directory and removes it in `Dispose`. Board, palette, and image paths are passed explicitly, so `AppStoragePaths` is never initialized by tests. Tests do not change environment variables, inspect or migrate real AppData/OneDrive data, or use the network. HTTP handlers return in-memory responses, including an interrupted response body. Clipboard tests replace the OS marker callback and reset both clipboard content and cut state before and after each test; the Windows clipboard is untouched.

WPF document and bitmap tests run on a dedicated STA thread, propagate assertion failures, and shut down their dispatcher. The assembly disables parallel execution because the application clipboard is static. No windows are displayed, and no sleeps are used. The STA join timeout is a hang guard, not a timing assertion.

## Coverage

| Test file | Behaviors |
| --- | --- |
| `PersistenceTests.cs` | Full mixed-tree equality across four levels; concrete types, IDs, parents, geometry, colors, text formatting, image paths/ratios; empty items; recursive legacy migration and no text resurrection; defaults and invalid JSON; nonfinite values; atomic replacement, backups, recovery, failure cleanup, locked destination, stale editing sessions, future versions and local image relocation. |
| `BoardAndClipboardTests.cs` | Deep cloning of mixed nested trees; copied snapshots and reusable paste; cut removal and cleanup; relative group spacing; destination parent IDs; one/many duplication; single/multi/clear selection; alias-safe selection; empty operations; title commit; model synchronization and notifications. |
| `UndoRedoTests.cs` | Every `IUndoableCommand`: add, delete, move, resize, rename and color. Initial/execute/undo/redo state, original object identity and ordering; stack flags, LIFO, empty operations, redo invalidation and `Record` without reexecution. |
| `CoordinateAndEditingTests.cs` | Pan/zoom transformations including negative coordinates; pointer anchoring, zoom bounds, resize minima and image ratio, selection rectangles in all directions; editor/handled-event routing policy, rename eligibility, Enter style policy, placeholder state. |
| `NoteDocumentTests.cs` | All six existing block types (the app has `SmallText`, not a separate small-heading type); multiple blocks and runs, combined formatting, inline code, text/highlight colors, links, empty documents, whitespace, newlines, Unicode/emoji, palette fallbacks, nested spans and WPF line breaks, selected-range formatting isolation, list types. |
| `ImageAndPaletteTests.cs` | Case-insensitive extensions and URL queries; unique managed copies; valid PNG encoding/decoding; aspect ratios, missing/corrupt images; injected downloads, data URIs and failure cleanup; palette defaults/persistence, order, case-insensitive duplicates, ten-color limit, invalid input and immutable default-row movement. |

## Persistence contract

- Saves remain root-board JSON, with `FormatVersion: 1` added at the root. A missing version means legacy version 1. Newer versions throw `UnsupportedBoardVersionException`; they are neither replaced nor recovered using an older backup.
- A missing primary returns `null`. Missing optional properties use model defaults. Empty/invalid JSON, unknown discriminators, items lacking discriminators, explicit null collections and invalid numeric geometry are rejected (`JsonException`, `NotSupportedException` or `InvalidDataException`). Nothing replaces a rejected save unless a valid backup was explicitly loaded by the service.
- Null/missing legacy note documents are normalized; nonempty legacy `Content` becomes a normal block/run only if the document has no populated runs. Modern content wins. Obsolete `Content` is cleared in memory to prevent resurrection after edits.
- Load may recover a corrupt primary from `board.json.bak`, without modifying either file. On the next successful save, the corrupt primary is preserved under `board.json.corrupt.<guid>` and the good backup remains intact. There is no recovery UI yet.
- Save validates before writing, flushes a uniquely named sibling temporary file, and atomically replaces the primary. Ordinary successful replacements retain the preceding primary as `.bak`. Exceptions clean up the current attempt's temporary file. A failure injected immediately before replacement verifies that both old files remain unchanged. Sudden process termination/power failure can still leave an orphan temporary file; startup scavenging is not implemented.
- One `BoardSaveService` belongs to each editing session. Load/save records the exact file contents; an external change causes the next save to fail until reloaded. An exclusive `board.json.lock` handle protects the check-and-replace section between local cooperating instances. The empty lock file intentionally remains after release to avoid a delete/recreate race. This is not distributed locking across OneDrive clients or protection against writers that ignore the protocol.
- Missing image paths are retained; relocated GUID-named images are resolved only inside the selected store's `Images` folder. Image decoding failures fall back to square dimensions at the view-model boundary; download/import services propagate errors before producing partial files.
- Palette loading retains its existing tolerant fallback for unreadable JSON. Invalid picker input is rejected. The palette does not use the board's versioning or stale-session protocol.

## Deferred cases and known limits

No task model, recurrence engine, task search/filtering, subtask progress or debounced autosave exists. These features were not added. Future task tests should cover completion timestamps, due-today/overdue and undated tasks, priority/status/tag filters, nested global search and breadcrumbs, subtask progress, daily/weekly/monthly/weekday/end-of-month recurrence, date-only semantics and time zones. Autosave should use an injected `TimeProvider` when introduced.

There is no managed-image inventory or garbage collector; referenced/unreferenced image identification and deletion protection await that feature. Existing breadcrumb navigation is not exercised because `MainViewModel` constructs application storage; testing it requires an additional constructor seam.

Shortcut tests verify the extracted policy used by the window, including leaving text copy/cut/paste/delete to the editor and ignoring already-handled events. They do not simulate WPF tunneling/bubbling, physical keyboard input, focus transfers, or guarantee platform command routing. Escape editor/popover precedence, real text cutting while a note is selected, typing-format inheritance, caret placement, checklist toggling, and popup interactions remain for an STA editor integration layer or focused manual checks. Enter tests establish the current policy (heading/code to normal, quote continues), not the full caret-splitting implementation. Formatting a `TextRange` is tested without UI automation.

The service reports persistence failures as exceptions; startup, save-failure dialogs and user-directed conflict resolution are not covered. No live OneDrive integration, hardware/power-loss simulation, distributed conflict resolution, actual Windows clipboard integration, full UI automation, large-board performance/stress testing, accessibility audit, installer/update/uninstaller validation, or cross-machine deployment tests are included.

Repository inspection found no `-LAPTOP-PLBC9PJQ` copies. The existing compile/page exclusions remain. The pre-existing untracked `CLAUDE.md` was preserved.
