# Guided Workbench UI Design

## Goal

Use the provided prototype as the visual baseline, then turn the desktop app into a guided workbench that helps first-time users complete the whole path: connect Chrome extension, record representative steps, export the raw recording package, add source materials, and generate the final APA requirement document.

## Chosen Direction

Use the "guided workbench" direction. Keep the prototype's top global toolbar, two main tabs, three-column recording workspace, and two-column generation workspace. Add a persistent progress chain so users can see which step is complete and what to do next.

## Scope

- Add a compact progress strip under the top toolbar.
- Keep the recording page as timeline, preview, and properties, but strengthen empty states and status hierarchy.
- Keep the generation page as source materials plus LLM configuration, but make the primary generation action visible near the top.
- Clarify that local capture service health is not the same as Chrome extension connection.
- Clarify that exporting the raw recording package creates intermediate materials, while `APA-generalized-requirements.md` is the final handoff file.

## Interaction Details

The progress strip shows seven milestones:

1. Local service
2. Chrome extension
3. Recording
4. Recorded steps
5. Raw package
6. Source materials
7. Final document

Each milestone uses text already available from the ViewModel where possible. In the first pass, the strip is informational and does not add navigation or new workflow state.

The recording tab keeps the existing editable metadata row. When no step is selected, the central preview should explain the next recovery action: check extension connection, start recording, then operate in Chrome. The right properties panel should preserve the current priority guidance: business title, variable name, and success criteria matter most.

The generation tab puts the main "generate final APA requirement document" action and status before advanced model settings. Source materials remain editable as paths, but the surrounding labels describe file limits more directly, including PDF text limitation and image-count limits. Prompt template editing remains collapsed as advanced configuration.

## Non-Goals

- Do not add automatic Chrome extension installation.
- Do not add a model connection test in this pass.
- Do not change capture, export, prompt, or LLM generation behavior.
- Do not restructure ViewModel services or core domain models.

## Verification

- Build the solution with `dotnet build ApaFlowRecorder.sln`.
- Run the core tests with `dotnet test ApaFlowRecorder.sln`.
- Launch the WPF app if the environment supports desktop windows and inspect that the progress strip, recording page, and generation page render without layout overlap at the default window size.
