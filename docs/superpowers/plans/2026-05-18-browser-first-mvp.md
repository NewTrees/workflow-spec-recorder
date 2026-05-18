# Browser-First MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a usable browser-first APA workflow recorder with a WPF review app, Chrome extension capture, and JSON/Markdown export.

**Architecture:** Keep automation intelligence in a small core library, keep the WPF app focused on session workflow and local hosting, and let the Chrome extension own browser-specific capture. Persist the canonical workflow as JSON and render Markdown from that model.

**Tech Stack:** .NET 9, WPF, ASP.NET Core minimal hosting inside the desktop app, xUnit, Chrome Extension Manifest V3, vanilla JavaScript.

---

## File Structure

| Path | Responsibility |
|---|---|
| `src/ApaFlowRecorder.Core/Models/*` | Domain records for events, steps, variables, and sessions |
| `src/ApaFlowRecorder.Core/Services/*` | Step mapping, Markdown rendering, export helpers |
| `src/ApaFlowRecorder.Desktop/Services/*` | Recording coordination and localhost server |
| `src/ApaFlowRecorder.Desktop/ViewModels/*` | WPF presentation state |
| `src/ApaFlowRecorder.Desktop/MainWindow.xaml` | Main workbench layout |
| `tests/ApaFlowRecorder.Core.Tests/*` | Behavior tests for mapping and export |
| `extension/*` | Chrome extension capture package |
| `samples/demo-web-app/*` | Local smoke-test site |

### Task 1: Core workflow model and step mapping

**Files:**
- Create: `src/ApaFlowRecorder.Core/Models/*.cs`
- Create: `src/ApaFlowRecorder.Core/Services/StepFactory.cs`
- Create: `tests/ApaFlowRecorder.Core.Tests/StepFactoryTests.cs`

- [ ] Write failing tests for navigation, fill, password masking, select, upload, and click conversion.
- [ ] Run the test file and verify it fails because the production types do not exist.
- [ ] Implement the smallest model and mapper surface that satisfies the tests.
- [ ] Run the test file and verify it passes.

### Task 2: APA Markdown export

**Files:**
- Create: `src/ApaFlowRecorder.Core/Services/MarkdownExporter.cs`
- Create: `tests/ApaFlowRecorder.Core.Tests/MarkdownExporterTests.cs`

- [ ] Write failing tests proving the Markdown contains the required APA sections, renders variables, and emits an element catalog.
- [ ] Run the test file and verify it fails.
- [ ] Implement Markdown rendering from `WorkflowSession`.
- [ ] Run the test file and verify it passes.

### Task 3: Desktop recording coordinator and export persistence

**Files:**
- Create: `src/ApaFlowRecorder.Desktop/Services/RecordingCoordinator.cs`
- Create: `src/ApaFlowRecorder.Desktop/Services/SessionExportService.cs`
- Create: `src/ApaFlowRecorder.Desktop/Services/LocalCaptureServer.cs`
- Modify: `src/ApaFlowRecorder.Desktop/ApaFlowRecorder.Desktop.csproj`

- [ ] Add tests where practical around export helpers in the core library.
- [ ] Implement session lifecycle, screenshot decoding, JSON export, and Markdown export.
- [ ] Host localhost endpoints for `/health` and `/api/events`.
- [ ] Run build and targeted tests.

### Task 4: WPF workbench UI

**Files:**
- Replace: `src/ApaFlowRecorder.Desktop/MainWindow.xaml`
- Replace: `src/ApaFlowRecorder.Desktop/MainWindow.xaml.cs`
- Create: `src/ApaFlowRecorder.Desktop/ViewModels/MainWindowViewModel.cs`
- Create: `src/ApaFlowRecorder.Desktop/ViewModels/RelayCommand.cs`

- [ ] Build a minimal but usable workbench with controls, timeline, detail editor, and status bar.
- [ ] Wire commands for new/start/pause/stop/export.
- [ ] Bind the selected step editor to mutable step properties.
- [ ] Build the desktop project.

### Task 5: Chrome extension capture

**Files:**
- Create: `extension/manifest.json`
- Create: `extension/background.js`
- Create: `extension/content-script.js`
- Create: `extension/popup.html`
- Create: `extension/popup.js`

- [ ] Capture navigation, clicks, text blur, dropdown changes, and file uploads.
- [ ] Generate element metadata and CSS selector candidates.
- [ ] Capture visible tab screenshots when possible.
- [ ] Post events to the desktop app on localhost and expose connection state in the popup.

### Task 6: Sample site, packaging, and docs

**Files:**
- Create: `samples/demo-web-app/index.html`
- Create: `README.md`

- [ ] Add a smoke-test browser page that exercises all supported action types.
- [ ] Document install, extension loading, recording workflow, and export flow.
- [ ] Publish a runnable Windows build to `dist/ApaFlowRecorder`.
- [ ] Verify build output, tests, and export workflow before reporting completion.

## Self-Review

- The plan covers each approved MVP capability from the design.
- The only intentionally deferred work is explicitly out of scope in the spec.
- File boundaries are narrow enough to keep the first codebase understandable.
- The plan avoids placeholders and keeps the canonical JSON model at the center of export behavior.
