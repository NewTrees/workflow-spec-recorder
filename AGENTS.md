# Repository Guidelines

## Project Structure & Module Organization

This repository contains the Workflow Spec Recorder desktop app and its browser capture companion.

- `ApaFlowRecorder.sln` is the solution entry point.
- `src/ApaFlowRecorder.Core/` contains workflow models, exporters, Office readers, and LLM services.
- `src/ApaFlowRecorder.Desktop/` contains the WPF app, view models, local capture server, and export coordination.
- `extension/` contains the Chrome extension (`manifest.json`, popup, background, content scripts).
- `tests/ApaFlowRecorder.Core.Tests/` contains xUnit tests for core behavior.
- `samples/demo-web-app/` is a simple browser target for manual capture testing.
- `docs/superpowers/` stores design and implementation notes.

Generated outputs such as `bin/`, `obj/`, `dist/`, `sessions/`, and `exports/` are ignored and should not be committed.

## Build, Test, and Development Commands

Run commands from `apa-flow-recorder/`.

- `dotnet restore ApaFlowRecorder.sln` restores NuGet packages.
- `dotnet build ApaFlowRecorder.sln` builds the core library, WPF app, and tests.
- `dotnet test ApaFlowRecorder.sln` runs the xUnit suite.
- `dotnet run --project src/ApaFlowRecorder.Desktop/ApaFlowRecorder.Desktop.csproj` launches the desktop recorder.
- `powershell -ExecutionPolicy Bypass -File scripts/start-demo-site.ps1` serves `samples/demo-web-app/` on port `8080`.

On machines where `dotnet` is not on `PATH`, use `& 'C:\Program Files\dotnet\dotnet.exe' ...`.

## Coding Style & Naming Conventions

C# projects target .NET 9 with nullable reference types and implicit usings enabled. Use 4-space indentation, file-scoped namespaces, PascalCase for public types and members, camelCase for locals and parameters, and `Async` suffixes for asynchronous methods. Keep core logic in `ApaFlowRecorder.Core`; WPF orchestration belongs in `ApaFlowRecorder.Desktop`. Keep extension JavaScript browser-API-focused and named by role.

## Testing Guidelines

Use xUnit for automated tests. Place tests in `tests/ApaFlowRecorder.Core.Tests/`, name files after the unit under test, and use descriptive method names such as `Renders_required_sections_variables_and_element_catalog_in_readable_chinese`. Add or update tests when changing exporters, workflow models, prompts, or parsing behavior.

## Commit & Pull Request Guidelines

Existing commits use Conventional Commits, for example `chore: scaffold recorder solution and write MVP docs` and `feat: add desktop recorder UI and browser extension bridge`. Keep using `<type>: <imperative summary>`, commonly `feat`, `fix`, `test`, `docs`, or `chore`.

Pull requests should include purpose, testing performed, linked issue or design note when available, and screenshots or a brief recording for WPF or Chrome extension UI changes. Note any LLM provider or local configuration needed for manual verification.

## Security & Configuration Tips

Do not commit API keys, session captures, exports, or generated requirement documents. Keep provider settings local to the app configuration or manual test environment, and sanitize sample workflow data before sharing it in issues or PRs.
