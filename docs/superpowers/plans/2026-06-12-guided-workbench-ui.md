# Guided Workbench UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the current WPF shell into a guided workbench based on the approved prototype direction.

**Architecture:** Keep the existing single-window WPF structure and ViewModel. Add presentation-only ViewModel properties for progress text and state, then bind them from `MainWindow.xaml` without changing capture, export, or generation services.

**Tech Stack:** .NET 9, WPF XAML, C#, xUnit.

---

### Task 1: Add Workbench Progress Properties

**Files:**
- Modify: `src/ApaFlowRecorder.Desktop/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Add progress label properties**

Add read-only properties near the existing checklist properties:

```csharp
public string LocalServiceProgressLabel => ServerStatus.Contains("已启动", StringComparison.Ordinal)
    ? "本地服务已启动"
    : "本地服务启动中";

public string ExtensionProgressLabel => IsExtensionRecentlySeen()
    ? "Chrome 扩展在线"
    : "等待 Chrome 扩展心跳";

public string RecordingProgressLabel => _recordingCoordinator.IsRecording
    ? _recordingCoordinator.IsPaused ? "录制已暂停" : "正在录制"
    : "尚未开始录制";

public string StepProgressLabel => Steps.Count > 0
    ? $"已记录 {Steps.Count} 步"
    : "尚未记录步骤";

public string RawPackageProgressLabel => string.IsNullOrWhiteSpace(LastExportPath)
    ? "未导出原始包"
    : "原始包已加入资料";

public string SourceMaterialProgressLabel => ParseMaterialPaths().Count == 0
    ? "未选择资料"
    : $"已选择 {ParseMaterialPaths().Count} 个资料";

public string FinalDocumentProgressLabel => IsFinalRequirementGenerated
    ? "最终文档已生成"
    : "未生成最终文档";
```

- [ ] **Step 2: Raise progress notifications**

Update existing setters and refresh methods so each property refreshes when state changes:

```csharp
private void RefreshWorkbenchProgress()
{
    OnPropertyChanged(nameof(LocalServiceProgressLabel));
    OnPropertyChanged(nameof(ExtensionProgressLabel));
    OnPropertyChanged(nameof(RecordingProgressLabel));
    OnPropertyChanged(nameof(StepProgressLabel));
    OnPropertyChanged(nameof(RawPackageProgressLabel));
    OnPropertyChanged(nameof(SourceMaterialProgressLabel));
    OnPropertyChanged(nameof(FinalDocumentProgressLabel));
}
```

Call `RefreshWorkbenchProgress()` at the end of `RefreshRecordingTelemetry()`, after setting `ServerStatus`, after setting `LastExportPath`, after setting `LastGeneralizedExportPath`, and after setting `SourceMaterialPaths`.

### Task 2: Add Progress Strip To Main Window

**Files:**
- Modify: `src/ApaFlowRecorder.Desktop/MainWindow.xaml`

- [ ] **Step 1: Add a progress item style**

Add a small progress-card style in `Window.Resources`:

```xml
<Style x:Key="ProgressStepStyle" TargetType="{x:Type Border}">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrushToken}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMedium}" />
    <Setter Property="Padding" Value="10,8" />
    <Setter Property="Margin" Value="0,0,8,0" />
</Style>
```

- [ ] **Step 2: Insert the progress strip**

Change the root grid to four rows: toolbar, progress strip, tabs, status bar. Insert a `UniformGrid` in row 1 with seven milestones bound to the new properties.

### Task 3: Strengthen Recording Empty State

**Files:**
- Modify: `src/ApaFlowRecorder.Desktop/MainWindow.xaml`

- [ ] **Step 1: Replace the preview empty text**

Use a centered stack in the screenshot preview area that says:

```xml
<StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Width="360">
    <TextBlock Text="还没有可预览的步骤" FontWeight="SemiBold" HorizontalAlignment="Center" />
    <TextBlock Text="先确认 Chrome 扩展在线，再点击开始录制，然后在业务页面操作。记录到步骤后，这里会显示截图和成功判定。" TextWrapping="Wrap" TextAlignment="Center" Margin="0,8,0,0" />
</StackPanel>
```

Keep the existing screenshot `Image` binding above it so real screenshots still render.

### Task 4: Rebalance Generation Page

**Files:**
- Modify: `src/ApaFlowRecorder.Desktop/MainWindow.xaml`
- Modify: `src/ApaFlowRecorder.Desktop/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Update generation copy**

Change labels from generic "资料" to "资料集", and add near the source material hint:

```csharp
"PDF 当前只记录文件名和大小，正文建议转为 Word 或 TXT；图片会作为视觉证据参与，长录制或多图资料会受数量限制。"
```

- [ ] **Step 2: Keep primary actions high**

Ensure the generate button and final document button remain above source material path editing and model configuration. Do not move prompt template editing out of the collapsed expander.

### Task 5: Verify

**Files:**
- Test: `ApaFlowRecorder.sln`

- [ ] **Step 1: Build**

Run:

```powershell
dotnet build ApaFlowRecorder.sln
```

Expected: exit code 0.

- [ ] **Step 2: Test**

Run:

```powershell
dotnet test ApaFlowRecorder.sln
```

Expected: exit code 0.
