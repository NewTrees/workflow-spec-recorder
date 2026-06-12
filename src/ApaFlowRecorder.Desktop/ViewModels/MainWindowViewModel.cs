using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;
using ApaFlowRecorder.Desktop.Services;
using Microsoft.Win32;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using WinOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ApaFlowRecorder.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly RecordingCoordinator _recordingCoordinator = new();
    private readonly SessionExportService _exportService = new();
    private readonly GeneralizedRequirementExportService _generalizedExportService = new();
    private readonly LlmSettingsStore _llmSettingsStore = new();
    private readonly PromptTemplateStore _promptTemplateStore = new();
    private readonly RecordingPackageMaterialCollector _recordingPackageMaterialCollector = new();
    private readonly ExtensionConnectionTracker _extensionConnectionTracker = new();
    private readonly DesktopInteractionCaptureService _desktopCaptureService;
    private readonly LocalCaptureServer _captureServer;
    private RecordedStep? _selectedStep;
    private string _statusMessage = "正在启动本地采集服务...";
    private string _serverStatus = "本地服务未启动";
    private string _extensionConnectionStatus = "尚未检测到 Chrome 扩展。";
    private string? _lastExportPath;
    private string? _lastGeneralizedExportPath;
    private string _sourceMaterialPaths = string.Empty;
    private string _providerName = "MiniMax";
    private string _llmBaseUrl = "https://api.minimaxi.com/v1";
    private string _llmModel = "MiniMax-M2.7";
    private string _llmApiKey = string.Empty;
    private string _extraInstruction = "把录制步骤当成代表性示例，结合粗略需求、输入样例、输出样例和参考资料，生成给 AI 执行/理解的业务需求文档。只写业务步骤、业务输入和业务输出，不写浏览器类型、选择器、URL、桌面程序标识等采集技术细节。";
    private string _promptTemplate = GeneralizedRequirementPromptBuilder.DefaultTemplate;
    private string _generalizedStatus = "请选择资料后生成需求文档。不填 API Key 时会使用规则兜底；配置支持视觉的模型时会结合录制截图分析。";
    private string _lastCaptureSummary = "尚未收到浏览器事件。";
    private int _selectedWorkspaceIndex;
    private bool _isLoadingLlmSettings;
    private bool _isGeneratingFinalRequirement;

    public MainWindowViewModel()
    {
        LoadSavedLlmSettings();
        LoadSavedPromptTemplate();
        _desktopCaptureService = new DesktopInteractionCaptureService(_recordingCoordinator.HandleCaptureEventAsync);
        _captureServer = new LocalCaptureServer(
            _recordingCoordinator.HandleCaptureEventAsync,
            NoteExtensionSeen,
            BuildHealthStatus);
        _recordingCoordinator.CaptureEventReceived += OnCaptureEventReceived;
        _recordingCoordinator.StepRecorded += OnStepRecorded;
        _recordingCoordinator.SessionChanged += OnSessionChanged;

        NewSessionCommand = new RelayCommand(CreateNewSession);
        StartRecordingCommand = new RelayCommand(StartRecording);
        PauseRecordingCommand = new RelayCommand(PauseRecording);
        StopRecordingCommand = new AsyncRelayCommand(StopRecordingAsync);
        CheckExtensionConnectionCommand = new RelayCommand(CheckExtensionConnection);
        OpenExtensionFolderCommand = new RelayCommand(OpenExtensionFolder);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        OpenExportFolderCommand = new RelayCommand(OpenExportFolder, () => !string.IsNullOrWhiteSpace(LastExportPath));
        BrowseSourceMaterialsCommand = new RelayCommand(BrowseSourceMaterials);
        ClearSourceMaterialsCommand = new RelayCommand(ClearSourceMaterials);
        UseMiniMaxPresetCommand = new RelayCommand(UseMiniMaxPreset);
        UseDeepSeekPresetCommand = new RelayCommand(UseDeepSeekPreset);
        SavePromptTemplateCommand = new RelayCommand(SavePromptTemplate);
        ResetPromptTemplateCommand = new RelayCommand(ResetPromptTemplate);
        OpenPromptTemplateFileCommand = new RelayCommand(OpenPromptTemplateFile);
        GenerateGeneralizedRequirementCommand = new AsyncRelayCommand(GenerateGeneralizedRequirementAsync);
        OpenGeneralizedFolderCommand = new RelayCommand(OpenGeneralizedFolder, () => !string.IsNullOrWhiteSpace(LastGeneralizedExportPath));
        OpenFinalRequirementDocumentCommand = new RelayCommand(OpenFinalRequirementDocument, () => IsFinalRequirementGenerated);
        ShowRecordingWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 0);
        ShowGeneralizeWorkspaceCommand = new RelayCommand(() => SelectedWorkspaceIndex = 1);

        SyncFromSession();
    }

    public ObservableCollection<RecordedStep> Steps { get; } = [];

    public string ProjectName
    {
        get => _recordingCoordinator.CurrentSession.ProjectName;
        set
        {
            _recordingCoordinator.CurrentSession.ProjectName = value;
            OnPropertyChanged();
        }
    }

    public string Objective
    {
        get => _recordingCoordinator.CurrentSession.Objective;
        set
        {
            _recordingCoordinator.CurrentSession.Objective = value;
            OnPropertyChanged();
        }
    }

    public string Preconditions
    {
        get => _recordingCoordinator.CurrentSession.Preconditions;
        set
        {
            _recordingCoordinator.CurrentSession.Preconditions = value;
            OnPropertyChanged();
        }
    }

    public RecordedStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            _selectedStep = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string ServerStatus
    {
        get => _serverStatus;
        private set
        {
            _serverStatus = value;
            OnPropertyChanged();
            RefreshWorkbenchProgress();
        }
    }

    public string ExtensionConnectionStatus
    {
        get => _extensionConnectionStatus;
        private set
        {
            _extensionConnectionStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExtensionConnectionAccentBrush));
            OnPropertyChanged(nameof(ExtensionStatusLabel));
            OnPropertyChanged(nameof(ExtensionInstallHint));
            OnPropertyChanged(nameof(ChromeExtensionChecklistText));
            RefreshWorkbenchProgress();
        }
    }

    public string LlmSettingsConfigPath => _llmSettingsStore.ConfigPath;

    public string PromptTemplateConfigPath => _promptTemplateStore.TemplatePath;

    public string ProductName => ProductInfo.Name;

    public string ProductInfoSummary => ProductInfo.DisplaySummary;

    public string RecordingStatus => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? "已暂停" : "录制中"
        : "未录制";

    public string RecordingStateText => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? "录制已暂停" : "正在采集浏览器操作"
        : "等待开始录制";

    public string RecordingIndicatorText => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? "已暂停" : "正在录制"
        : "未录制";

    public MediaBrush RecordingAccentBrush => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? MediaBrushes.DarkOrange : MediaBrushes.Green
        : MediaBrushes.Gray;

    public MediaBrush RecordingBannerBrush => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? new SolidColorBrush(MediaColor.FromRgb(255, 247, 237)) : new SolidColorBrush(MediaColor.FromRgb(254, 242, 242))
        : new SolidColorBrush(MediaColor.FromRgb(234, 247, 241));

    public MediaBrush ExtensionConnectionAccentBrush => IsExtensionRecentlySeen()
        ? new SolidColorBrush(MediaColor.FromRgb(22, 131, 58))
        : new SolidColorBrush(MediaColor.FromRgb(201, 42, 42));

    public string LiveCaptureSummary =>
        $"已记录 {Steps.Count} 步；收到浏览器事件 {_recordingCoordinator.ReceivedEventCount} 个；忽略 {_recordingCoordinator.IgnoredEventCount} 个。";

    public string ExtensionStatusLabel => IsExtensionRecentlySeen()
        ? "Chrome 扩展在线"
        : "Chrome 扩展离线";

    public string ExtensionInstallHint => IsExtensionRecentlySeen()
        ? string.Empty
        : "，离线请安装并启用扩展";

    public string StepCountLabel => $"已记录 {Steps.Count} 步";

    public string StepCountBadge => $"{Steps.Count}";

    public string GeneralizeStatusBadge
    {
        get
        {
            if (IsFinalRequirementGenerated)
            {
                return "已生成";
            }

            if (ParseMaterialPaths().Count > 0 || !string.IsNullOrWhiteSpace(LastExportPath))
            {
                return "可生成";
            }

            return "待生成";
        }
    }

    public string GenerateWorkspaceTitle => "生成需求文档";

    public string GenerationStageTitle
    {
        get
        {
            if (IsGeneratingFinalRequirement)
            {
                return "正在生成需求文档";
            }

            return IsFinalRequirementGenerated ? "需求文档已生成" : "等待生成需求文档";
        }
    }

    public string GenerationStageDetail
    {
        get
        {
            if (IsGeneratingFinalRequirement)
            {
                return "正在读取资料、整理录制步骤并调用模型，请保持窗口打开。生成完成后这里会显示导出入口。";
            }

            return IsFinalRequirementGenerated
                ? $"已生成 APA-generalized-requirements.md，目录：{LastGeneralizedExportPath}"
                : "添加资料或导出原始录制包后，点击“生成需求文档”。";
        }
    }

    public bool IsGenerationIdle => !IsGeneratingFinalRequirement && !IsFinalRequirementGenerated;

    public string ChromeExtensionChecklistText => IsExtensionRecentlySeen()
        ? $"已完成：Chrome 扩展在线，最近心跳 {FormatLastExtensionSeen()}。"
        : "待完成：打开 Chrome 扩展目录，在 chrome://extensions 中加载 extension 文件夹，然后点击“检测 Chrome 扩展”。";

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
        ? "需求文档已生成"
        : "未生成需求文档";

    public string RecordingChecklistText
    {
        get
        {
            if (Steps.Count > 0)
            {
                return $"已记录 {Steps.Count} 步：可暂停检查步骤，停止录制后会自动导出原始录制包。";
            }

            if (_recordingCoordinator.IsRecording && !_recordingCoordinator.IsPaused)
            {
                return "进行中：请在 Chrome 中操作业务页面，记录到步骤后时间线会增加。";
            }

            return "待完成：Chrome 扩展在线后，点击“开始/继续录制”。";
        }
    }

    public string RawExportChecklistText => string.IsNullOrWhiteSpace(LastExportPath)
        ? "待完成：录到关键步骤后，点击“停止”。原始包会自动导出并加入资料集。"
        : "已完成：原始录制包已导出并加入资料集，下一步生成需求文档。";

    public string FinalRequirementChecklistText => string.IsNullOrWhiteSpace(LastGeneralizedExportPath)
        ? "待完成：在生成页添加资料或使用已加入的原始包，点击“生成需求文档”。"
        : "已完成：需求文档已生成，请打开导出目录检查主文件。";

    public string LastCaptureSummary
    {
        get => _lastCaptureSummary;
        private set
        {
            _lastCaptureSummary = value;
            OnPropertyChanged();
        }
    }

    public string ExportHint => Steps.Count > 0
        ? "停止录制后会自动导出原始录制包并加入资料集，再到“生成需求文档”页完成最终文档。"
        : "还没有记录到步骤。仍可导出空流程用于排查，但请先确认 Chrome 扩展已加载、在线，并且处于录制中。";

    public string? LastExportPath
    {
        get => _lastExportPath;
        private set
        {
            _lastExportPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RawExportChecklistText));
            OnPropertyChanged(nameof(SourceMaterialSummary));
            OnPropertyChanged(nameof(SourceMaterialDetailHint));
            RefreshWorkbenchProgress();
        }
    }

    public string? LastGeneralizedExportPath
    {
        get => _lastGeneralizedExportPath;
        private set
        {
            _lastGeneralizedExportPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FinalRequirementChecklistText));
            OnPropertyChanged(nameof(GeneralizedNextStepHint));
            OnPropertyChanged(nameof(FinalRequirementDocumentPath));
            OnPropertyChanged(nameof(IsFinalRequirementGenerated));
            OnPropertyChanged(nameof(IsFinalRequirementResultVisible));
            OnPropertyChanged(nameof(IsGenerationIdle));
            OnPropertyChanged(nameof(GenerationStageTitle));
            OnPropertyChanged(nameof(GenerationStageDetail));
            OpenFinalRequirementDocumentCommand.RaiseCanExecuteChanged();
            RefreshWorkbenchProgress();
        }
    }

    public string SourceMaterialPaths
    {
        get => _sourceMaterialPaths;
        set
        {
            _sourceMaterialPaths = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceMaterialSummary));
            OnPropertyChanged(nameof(SourceMaterialDetailHint));
            RefreshWorkbenchProgress();
        }
    }

    public string SourceMaterialSummary => ParseMaterialPaths().Count == 0
        ? "尚未选择资料。可不录制，直接选择任意需求文档、输入样例、输出样例、截图说明或参考附件生成纯资料处理流程。"
        : $"已选择 {ParseMaterialPaths().Count} 个资料文件。录制包、步骤截图和资料会共同参与生成需求文档。";

    public string SourceMaterialDetailHint
    {
        get
        {
            var paths = ParseMaterialPaths();
            if (paths.Count == 0)
            {
                return "资料可以是需求说明、输入样例、输出样例、截图说明或参考附件；没有界面操作时也可以直接用资料生成。PDF 当前只记录文件名和大小，正文建议转为 Word 或 TXT；图片会作为视觉证据参与，长录制或多图资料会受数量限制。";
            }

            var hasPdf = paths.Any(path => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
            var hasImage = paths.Any(path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp");
            var hints = new List<string>
            {
                "已选资料会在生成时读取；导出的原始录制包会自动作为资料参与。图片会作为视觉证据参与，长录制或多图资料会受数量限制。"
            };
            if (hasPdf)
            {
                hints.Add("PDF 当前只记录文件名和大小，正文建议转为 Word 或 TXT 后再添加。");
            }

            if (hasImage)
            {
                hints.Add("图片会作为视觉证据参与，但发送给模型的图片数量会受限制。");
            }

            return string.Join(" ", hints);
        }
    }

    public string ModelConfigurationHint => string.IsNullOrWhiteSpace(LlmApiKey)
        ? "未填写 API Key：仍可生成规则兜底草稿；复杂业务、复杂页面或多资料场景建议配置大模型。"
        : "已填写 API Key：仅用于调用大模型生成需求文档，不是 Chrome 扩展凭证，也不是 APA 运行凭证。";

    public string GeneralizedNextStepHint => IsFinalRequirementGenerated
        ? "下一步：打开最终文档目录，检查 APA-generalized-requirements.md 后交给 APA 使用。"
        : "生成后会出现最终文档目录入口，可直接找到 APA-generalized-requirements.md。";

    public string? FinalRequirementDocumentPath => string.IsNullOrWhiteSpace(LastGeneralizedExportPath)
        ? null
        : Path.Combine(LastGeneralizedExportPath, "APA-generalized-requirements.md");

    public bool IsFinalRequirementGenerated =>
        !string.IsNullOrWhiteSpace(FinalRequirementDocumentPath) && File.Exists(FinalRequirementDocumentPath);

    public bool IsFinalRequirementResultVisible => IsFinalRequirementGenerated && !IsGeneratingFinalRequirement;

    public int SelectedWorkspaceIndex
    {
        get => _selectedWorkspaceIndex;
        set
        {
            if (_selectedWorkspaceIndex == value)
            {
                return;
            }

            _selectedWorkspaceIndex = value;
            OnPropertyChanged();
        }
    }

    public string ProviderName
    {
        get => _providerName;
        set
        {
            _providerName = value;
            OnPropertyChanged();
            PersistLlmSettings();
        }
    }

    public string LlmBaseUrl
    {
        get => _llmBaseUrl;
        set
        {
            _llmBaseUrl = value;
            OnPropertyChanged();
            PersistLlmSettings();
        }
    }

    public string LlmModel
    {
        get => _llmModel;
        set
        {
            _llmModel = value;
            OnPropertyChanged();
            PersistLlmSettings();
        }
    }

    public string LlmApiKey
    {
        get => _llmApiKey;
        set
        {
            _llmApiKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelConfigurationHint));
            PersistLlmSettings();
        }
    }

    public string ExtraInstruction
    {
        get => _extraInstruction;
        set
        {
            _extraInstruction = value;
            OnPropertyChanged();
        }
    }

    public string PromptTemplate
    {
        get => _promptTemplate;
        set
        {
            _promptTemplate = value;
            OnPropertyChanged();
        }
    }

    public string GeneralizedStatus
    {
        get => _generalizedStatus;
        private set
        {
            _generalizedStatus = value;
            OnPropertyChanged();
        }
    }

    public bool IsGeneratingFinalRequirement
    {
        get => _isGeneratingFinalRequirement;
        private set
        {
            _isGeneratingFinalRequirement = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFinalRequirementResultVisible));
            OnPropertyChanged(nameof(IsGenerationIdle));
            OnPropertyChanged(nameof(GenerationStageTitle));
            OnPropertyChanged(nameof(GenerationStageDetail));
        }
    }

    public RelayCommand NewSessionCommand { get; }
    public RelayCommand StartRecordingCommand { get; }
    public RelayCommand PauseRecordingCommand { get; }
    public AsyncRelayCommand StopRecordingCommand { get; }
    public RelayCommand CheckExtensionConnectionCommand { get; }
    public RelayCommand OpenExtensionFolderCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public RelayCommand OpenExportFolderCommand { get; }
    public RelayCommand BrowseSourceMaterialsCommand { get; }
    public RelayCommand ClearSourceMaterialsCommand { get; }
    public RelayCommand UseMiniMaxPresetCommand { get; }
    public RelayCommand UseDeepSeekPresetCommand { get; }
    public RelayCommand SavePromptTemplateCommand { get; }
    public RelayCommand ResetPromptTemplateCommand { get; }
    public RelayCommand OpenPromptTemplateFileCommand { get; }
    public AsyncRelayCommand GenerateGeneralizedRequirementCommand { get; }
    public RelayCommand OpenGeneralizedFolderCommand { get; }
    public RelayCommand OpenFinalRequirementDocumentCommand { get; }
    public RelayCommand ShowRecordingWorkspaceCommand { get; }
    public RelayCommand ShowGeneralizeWorkspaceCommand { get; }

    public async Task InitializeAsync()
    {
        await _captureServer.StartAsync();
        ServerStatus = "本地采集服务已启动：127.0.0.1:8765";
        ExtensionConnectionStatus = "本地服务已启动，但尚未收到 Chrome 扩展心跳。请先加载扩展并点击“检测 Chrome 扩展”。";
        StatusMessage = "请先检测 Chrome 扩展连接，确认在线后点击“开始/继续录制”。";
        RefreshRecordingTelemetry();
        RefreshWorkbenchProgress();
    }

    public async ValueTask DisposeAsync()
    {
        _desktopCaptureService.Dispose();
        await _captureServer.DisposeAsync();
    }

    private void CreateNewSession()
    {
        _desktopCaptureService.Stop();
        _recordingCoordinator.NewSession();
        LastCaptureSummary = "已新建流程，尚未收到浏览器事件。";
        StatusMessage = "已新建流程。准备好 Chrome 扩展和业务页面后点击“开始/继续录制”。";
        LastExportPath = null;
        LastGeneralizedExportPath = null;
        OpenExportFolderCommand.RaiseCanExecuteChanged();
        OpenGeneralizedFolderCommand.RaiseCanExecuteChanged();
        OpenFinalRequirementDocumentCommand.RaiseCanExecuteChanged();
        RefreshRecordingTelemetry();
    }

    private void StartRecording()
    {
        _recordingCoordinator.Start();
        _desktopCaptureService.Start();
        StatusMessage = "录制中：正在采集 Chrome 页面、下载完成事件和非浏览器页面的桌面点击。采到步骤后时间线会立即增加。";
        RefreshExtensionConnectionStatus();
        RefreshRecordingTelemetry();
        MinimizeMainWindow();
    }

    private void PauseRecording()
    {
        _desktopCaptureService.Stop();
        _recordingCoordinator.Pause();
        StatusMessage = $"已暂停。当前已记录 {Steps.Count} 步，可以导出，也可以点击“开始/继续录制”。";
        RefreshExtensionConnectionStatus();
        RefreshRecordingTelemetry();
    }

    private async Task StopRecordingAsync()
    {
        _desktopCaptureService.Stop();
        _recordingCoordinator.Stop();
        StatusMessage = $"已停止录制。当前已记录 {Steps.Count} 步，正在自动导出原始录制包...";
        RefreshExtensionConnectionStatus();
        RefreshRecordingTelemetry();
        try
        {
            await ExportAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"已停止录制，但自动导出失败：{ex.Message}";
        }
    }

    private async Task ExportAsync()
    {
        LastExportPath = await _exportService.ExportAsync(_recordingCoordinator.CurrentSession);
        var recordingPackageMaterials = _recordingPackageMaterialCollector.Collect(LastExportPath);
        AddSourceMaterialPaths(recordingPackageMaterials);
        StatusMessage = Steps.Count == 0
            ? $"已导出空原始录制包：{LastExportPath}，并已自动加入资料集。如果你刚才有操作，请检查 Chrome 扩展是否已加载。"
            : $"原始录制包已导出并自动加入资料集：{LastExportPath}";
        GeneralizedStatus = recordingPackageMaterials.Count > 0
            ? $"原始录制包已自动加入资料集（{recordingPackageMaterials.Count} 个文件）。可继续添加资料，或直接生成需求文档。"
            : "原始录制包已导出，但未发现可加入资料集的文件。";
        OpenExportFolderCommand.RaiseCanExecuteChanged();
        RefreshRecordingTelemetry();
    }

    private async Task GenerateGeneralizedRequirementAsync()
    {
        IsGeneratingFinalRequirement = true;
        try
        {
            PersistLlmSettings();
            GeneralizedStatus = "正在读取资料，并结合录制步骤与可用截图生成需求文档...";
            var settings = new LlmSettings
            {
                ProviderName = ProviderName,
                BaseUrl = LlmBaseUrl,
                Model = LlmModel,
                ApiKey = LlmApiKey,
                Temperature = 0.1
            };

            var (exportDirectory, result) = await _generalizedExportService.GenerateAndExportAsync(
                _recordingCoordinator.CurrentSession,
                ParseMaterialPaths(),
                settings,
                ExtraInstruction,
                PromptTemplate);

            LastGeneralizedExportPath = exportDirectory;
            GeneralizedStatus = $"已生成：{result.GenerationMode}。导出目录：{exportDirectory}";
            StatusMessage = "需求文档已生成，请打开导出目录检查 APA-generalized-requirements.md。";
            OpenGeneralizedFolderCommand.RaiseCanExecuteChanged();
            OpenFinalRequirementDocumentCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            GeneralizedStatus = $"生成失败：{ex.Message}";
            StatusMessage = "需求文档生成失败，请检查资料文件、模型配置或改用规则兜底。";
        }
        finally
        {
            IsGeneratingFinalRequirement = false;
        }
    }

    private void BrowseSourceMaterials()
    {
        var dialog = new WinOpenFileDialog
        {
            Title = "选择需求资料、输入样例、输出样例或参考附件（可多选）",
            Multiselect = true,
            Filter = "所有资料文件 (*.*)|*.*|常见可解析资料 (*.xlsx;*.docx;*.pptx;*.pdf;*.txt;*.md;*.csv;*.json;*.xml;*.html;*.log)|*.xlsx;*.docx;*.pptx;*.pdf;*.txt;*.md;*.csv;*.json;*.xml;*.html;*.log"
        };
        if (dialog.ShowDialog() == true)
        {
            AddSourceMaterialPaths(dialog.FileNames);
        }
    }

    private void ClearSourceMaterials()
    {
        SourceMaterialPaths = string.Empty;
    }

    private void UseMiniMaxPreset()
    {
        ProviderName = "MiniMax";
        LlmBaseUrl = "https://api.minimaxi.com/v1";
        LlmModel = "MiniMax-M2.7";
        PersistLlmSettings();
    }

    private void UseDeepSeekPreset()
    {
        ProviderName = "DeepSeek";
        LlmBaseUrl = "https://api.deepseek.com";
        LlmModel = "deepseek-chat";
        PersistLlmSettings();
    }

    private void SavePromptTemplate()
    {
        _promptTemplateStore.Save(PromptTemplate);
        StatusMessage = $"提示词模板已保存：{PromptTemplateConfigPath}";
    }

    private void ResetPromptTemplate()
    {
        PromptTemplate = _promptTemplateStore.ResetToDefault();
        StatusMessage = "已恢复默认提示词模板。";
    }

    private void OpenPromptTemplateFile()
    {
        SavePromptTemplate();
        OpenFile(PromptTemplateConfigPath);
    }

    private void OpenExportFolder()
    {
        OpenFolder(LastExportPath);
    }

    private void OpenExtensionFolder()
    {
        var extensionFolder = Path.Combine(AppContext.BaseDirectory, "extension");
        if (!Directory.Exists(extensionFolder))
        {
            extensionFolder = Path.Combine(Environment.CurrentDirectory, "extension");
        }

        if (!Directory.Exists(extensionFolder))
        {
            StatusMessage = "未找到 Chrome 扩展目录。请确认安装包完整，或重新安装 Workflow Spec Recorder。";
            return;
        }

        OpenFolder(extensionFolder);
        StatusMessage = $"已打开 Chrome 扩展目录：{extensionFolder}。请在 chrome://extensions 中点击“加载已解压的扩展程序”，选择此目录。";
    }

    private void OpenGeneralizedFolder()
    {
        OpenFolder(LastGeneralizedExportPath);
    }

    private void OpenFinalRequirementDocument()
    {
        OpenFile(FinalRequirementDocumentPath);
    }

    private static void OpenFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folder,
            UseShellExecute = true
        });
    }

    private static void OpenFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void LoadSavedLlmSettings()
    {
        _isLoadingLlmSettings = true;
        try
        {
            var settings = _llmSettingsStore.LoadOrDefault();
            _providerName = settings.ProviderName;
            _llmBaseUrl = settings.BaseUrl;
            _llmModel = settings.Model;
            _llmApiKey = settings.ApiKey;
        }
        finally
        {
            _isLoadingLlmSettings = false;
        }
    }

    private void LoadSavedPromptTemplate()
    {
        var template = _promptTemplateStore.LoadOrCreateDefault();
        if (LooksLikeLegacyDefaultPromptTemplate(template))
        {
            template = _promptTemplateStore.ResetToDefault();
        }

        _promptTemplate = template;
    }

    private static bool LooksLikeLegacyDefaultPromptTemplate(string template) =>
        template.Contains("每一步必须包含操作对象、处理规则、关键验证方式、失败时如何处理", StringComparison.Ordinal)
        || template.Contains("必须输出 Markdown，并显式描述输入资料、输出成果、循环结构、动态发现规则、字段提取、文件写入、等待/异常/日志策略", StringComparison.Ordinal);

    private void PersistLlmSettings()
    {
        if (_isLoadingLlmSettings)
        {
            return;
        }

        _llmSettingsStore.Save(new LlmSettings
        {
            ProviderName = ProviderName,
            BaseUrl = LlmBaseUrl,
            Model = LlmModel,
            ApiKey = LlmApiKey,
            Temperature = 0.1
        });
        OnPropertyChanged(nameof(LlmSettingsConfigPath));
    }

    private void CheckExtensionConnection()
    {
        if (!IsExtensionRecentlySeen())
        {
            OpenExtensionCheckPage();
        }

        ExtensionConnectionStatus = IsExtensionRecentlySeen()
            ? $"Chrome 扩展在线。最近心跳：{FormatLastExtensionSeen()}"
            : "Chrome 扩展离线：已打开本地检测页。请在 chrome://extensions 加载或刷新扩展，刷新业务页面后再点一次检测。";
        StatusMessage = ExtensionConnectionStatus;
    }

    private static void OpenExtensionCheckPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "http://127.0.0.1:8765/extension-check",
            UseShellExecute = true
        });
    }

    private static void MinimizeMainWindow()
    {
        if (System.Windows.Application.Current?.MainWindow is { } mainWindow)
        {
            mainWindow.WindowState = System.Windows.WindowState.Minimized;
        }
    }

    private void NoteExtensionSeen()
    {
        if (App.Current.Dispatcher.CheckAccess())
        {
            MarkExtensionSeen();
            return;
        }

        App.Current.Dispatcher.Invoke(MarkExtensionSeen);
    }

    private void MarkExtensionSeen()
    {
        _extensionConnectionTracker.NoteHeartbeat();
        RefreshExtensionConnectionStatus();
    }

    private void NoteCaptureEventSource(CaptureEvent captureEvent)
    {
        _extensionConnectionTracker.NoteCaptureEvent(captureEvent);
        if (!ExtensionConnectionTracker.IsBrowserExtensionEvent(captureEvent.EventType))
        {
            return;
        }

        RefreshExtensionConnectionStatus();
    }

    private void RefreshExtensionConnectionStatus()
    {
        ExtensionConnectionStatus = IsExtensionRecentlySeen()
            ? _recordingCoordinator.IsRecording && !_recordingCoordinator.IsPaused
                ? $"Chrome 扩展在线，正在录制。最近心跳：{FormatLastExtensionSeen()}"
                : $"Chrome 扩展在线，等待录制。最近心跳：{FormatLastExtensionSeen()}"
            : "本地服务已启动，但尚未收到 Chrome 扩展心跳。请先加载扩展并点击“检测 Chrome 扩展”。";
    }

    private bool IsExtensionRecentlySeen() =>
        _extensionConnectionTracker.IsRecentlySeen();

    private string FormatLastExtensionSeen() =>
        _extensionConnectionTracker.LastSeenAt?.ToString("HH:mm:ss") ?? "-";

    private object BuildHealthStatus() => new
    {
        status = "ok",
        isRecording = _recordingCoordinator.IsRecording,
        isPaused = _recordingCoordinator.IsPaused,
        recordingStatus = RecordingStatus,
        stepCount = _recordingCoordinator.CurrentSession.Steps.Count,
        receivedEventCount = _recordingCoordinator.ReceivedEventCount,
        acceptedEventCount = _recordingCoordinator.AcceptedEventCount,
        ignoredEventCount = _recordingCoordinator.IgnoredEventCount,
        extensionRecentlySeen = IsExtensionRecentlySeen(),
        lastExtensionSeenAt = _extensionConnectionTracker.LastSeenAt
    };

    private void OnCaptureEventReceived(object? sender, CaptureEventReport report)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            NoteCaptureEventSource(report.CaptureEvent);
            var eventName = RenderEventType(report.CaptureEvent.EventType);
            var page = FirstNonBlank(report.CaptureEvent.PageTitle, report.CaptureEvent.PageUrl) ?? "未知页面";
            LastCaptureSummary = report.Accepted
                ? $"刚收到并记录：{eventName}，页面：{page}"
                : $"刚收到但未记录：{eventName}，原因：{report.Reason}";
            RefreshRecordingTelemetry();
        });
    }

    private void OnStepRecorded(object? sender, RecordedStep step)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            Steps.Add(step);
            SelectedStep = step;
            StatusMessage = $"已记录第 {Steps.Count} 步：{step.Title}";
            RefreshRecordingTelemetry();
        });
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        App.Current.Dispatcher.Invoke(SyncFromSession);
    }

    private void SyncFromSession()
    {
        Steps.Clear();
        foreach (var step in _recordingCoordinator.CurrentSession.Steps)
        {
            Steps.Add(step);
        }

        SelectedStep = Steps.LastOrDefault();
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(Objective));
        OnPropertyChanged(nameof(Preconditions));
        RefreshRecordingTelemetry();
    }

    private void RefreshRecordingTelemetry()
    {
        OnPropertyChanged(nameof(RecordingStatus));
        OnPropertyChanged(nameof(RecordingStateText));
        OnPropertyChanged(nameof(RecordingIndicatorText));
        OnPropertyChanged(nameof(RecordingAccentBrush));
        OnPropertyChanged(nameof(RecordingBannerBrush));
        OnPropertyChanged(nameof(ExtensionConnectionAccentBrush));
        OnPropertyChanged(nameof(ExtensionStatusLabel));
        OnPropertyChanged(nameof(LiveCaptureSummary));
        OnPropertyChanged(nameof(StepCountLabel));
        OnPropertyChanged(nameof(StepCountBadge));
        OnPropertyChanged(nameof(ExportHint));
        OnPropertyChanged(nameof(RecordingChecklistText));
        OnPropertyChanged(nameof(GeneralizeStatusBadge));
        RefreshWorkbenchProgress();
    }

    private void RefreshWorkbenchProgress()
    {
        OnPropertyChanged(nameof(LocalServiceProgressLabel));
        OnPropertyChanged(nameof(ExtensionProgressLabel));
        OnPropertyChanged(nameof(RecordingProgressLabel));
        OnPropertyChanged(nameof(StepProgressLabel));
        OnPropertyChanged(nameof(RawPackageProgressLabel));
        OnPropertyChanged(nameof(SourceMaterialProgressLabel));
        OnPropertyChanged(nameof(FinalDocumentProgressLabel));
        OnPropertyChanged(nameof(GeneralizeStatusBadge));
    }

    private List<string> ParseMaterialPaths()
    {
        return SourceMaterialPaths
            .Split(new[] { "\r\n", "\n", "\r", ";" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private void AddSourceMaterialPaths(IEnumerable<string> paths)
    {
        var existing = ParseMaterialPaths();
        existing.AddRange(paths.Where(path => !string.IsNullOrWhiteSpace(path)));
        SourceMaterialPaths = string.Join(Environment.NewLine, existing.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string RenderEventType(CaptureEventType eventType) => eventType switch
    {
        CaptureEventType.Navigation => "页面跳转",
        CaptureEventType.Click => "点击",
        CaptureEventType.DoubleClick => "双击",
        CaptureEventType.Input => "输入",
        CaptureEventType.Select => "下拉选择",
        CaptureEventType.Upload => "文件上传",
        CaptureEventType.Download => "浏览器下载",
        CaptureEventType.DesktopClick => "桌面点击",
        CaptureEventType.DesktopInput => "桌面输入",
        CaptureEventType.DesktopKey => "桌面键盘",
        CaptureEventType.DesktopDoubleClick => "桌面双击",
        CaptureEventType.Clipboard => "剪贴板",
        CaptureEventType.DesktopClipboard => "桌面剪贴板",
        CaptureEventType.Wait => "等待/校验",
        _ => eventType.ToString()
    };

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
