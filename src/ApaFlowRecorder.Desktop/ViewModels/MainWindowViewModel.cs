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
    private string _extensionConnectionStatus = "尚未检测到浏览器插件。";
    private string? _lastExportPath;
    private string? _lastGeneralizedExportPath;
    private string _sourceMaterialPaths = string.Empty;
    private string _providerName = "MiniMax";
    private string _llmBaseUrl = "https://api.minimaxi.com/v1";
    private string _llmModel = "MiniMax-M2.7";
    private string _llmApiKey = string.Empty;
    private string _extraInstruction = "把录制步骤当成代表性示例，结合粗略需求、输入样例、输出样例和参考资料，推断完整业务意图，生成给自动化工作流生成工具理解并执行的准确、详细需求文档。";
    private string _promptTemplate = GeneralizedRequirementPromptBuilder.DefaultTemplate;
    private string _generalizedStatus = "请选择任意资料文件后生成自动化需求。不填 API Key 时会使用规则兜底；配置支持视觉的模型时会结合录制截图分析。";
    private string _lastCaptureSummary = "尚未收到浏览器事件。";
    private bool _isLoadingLlmSettings;

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
        StopRecordingCommand = new RelayCommand(StopRecording);
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
        }
    }

    public string ExtensionConnectionStatus
    {
        get => _extensionConnectionStatus;
        private set
        {
            _extensionConnectionStatus = value;
            OnPropertyChanged();
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
        ? _recordingCoordinator.IsPaused ? "● 已暂停" : "● 正在录制"
        : "● 未录制";

    public MediaBrush RecordingAccentBrush => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? MediaBrushes.DarkOrange : MediaBrushes.Red
        : MediaBrushes.Gray;

    public MediaBrush RecordingBannerBrush => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? new SolidColorBrush(MediaColor.FromRgb(255, 247, 237)) : new SolidColorBrush(MediaColor.FromRgb(254, 242, 242))
        : new SolidColorBrush(MediaColor.FromRgb(234, 247, 241));

    public string LiveCaptureSummary =>
        $"已记录 {Steps.Count} 步；收到浏览器事件 {_recordingCoordinator.ReceivedEventCount} 个；忽略 {_recordingCoordinator.IgnoredEventCount} 个。";

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
        ? "可以导出原始录制包并自动加入资料集，再切到泛化需求页生成动态 APA 文档。"
        : "还没有记录到步骤。仍可导出空流程用于排查，但请先确认 Chrome 扩展已加载并且处于录制中。";

    public string? LastExportPath
    {
        get => _lastExportPath;
        private set
        {
            _lastExportPath = value;
            OnPropertyChanged();
        }
    }

    public string? LastGeneralizedExportPath
    {
        get => _lastGeneralizedExportPath;
        private set
        {
            _lastGeneralizedExportPath = value;
            OnPropertyChanged();
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
        }
    }

    public string SourceMaterialSummary => ParseMaterialPaths().Count == 0
        ? "尚未选择资料。可不录制，直接选择任意需求文档、输入样例、输出样例、截图说明或参考附件生成纯资料处理流程。"
        : $"已选择 {ParseMaterialPaths().Count} 个资料文件。录制包、步骤截图和资料会共同参与泛化，资料用于推断完整业务意图。";

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

    public RelayCommand NewSessionCommand { get; }
    public RelayCommand StartRecordingCommand { get; }
    public RelayCommand PauseRecordingCommand { get; }
    public RelayCommand StopRecordingCommand { get; }
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

    public async Task InitializeAsync()
    {
        await _captureServer.StartAsync();
        ServerStatus = "本地采集服务已启动：127.0.0.1:8765";
        ExtensionConnectionStatus = "本地服务已启动，等待浏览器插件心跳。请打开扩展弹窗或刷新业务页面。";
        StatusMessage = "请先检测浏览器插件连接，确认在线后点击“开始录制”。";
        RefreshRecordingTelemetry();
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
        StatusMessage = "已新建流程。准备好浏览器后点击“开始录制”。";
        LastExportPath = null;
        LastGeneralizedExportPath = null;
        OpenExportFolderCommand.RaiseCanExecuteChanged();
        OpenGeneralizedFolderCommand.RaiseCanExecuteChanged();
        RefreshRecordingTelemetry();
    }

    private void StartRecording()
    {
        _recordingCoordinator.Start();
        _desktopCaptureService.Start();
        StatusMessage = "录制中：正在采集 Chrome 页面、下载完成事件和非浏览器页面的桌面点击。采到步骤后左侧时间线会立即增加。";
        RefreshRecordingTelemetry();
    }

    private void PauseRecording()
    {
        _desktopCaptureService.Stop();
        _recordingCoordinator.Pause();
        StatusMessage = $"已暂停。当前已记录 {Steps.Count} 步，可以导出，也可以点击“开始/继续录制”。";
        RefreshRecordingTelemetry();
    }

    private void StopRecording()
    {
        _desktopCaptureService.Stop();
        _recordingCoordinator.Stop();
        StatusMessage = $"已停止录制。当前已记录 {Steps.Count} 步。";
        RefreshRecordingTelemetry();
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
            ? $"原始录制包已自动加入资料集（{recordingPackageMaterials.Count} 个文件）。可继续添加资料，或直接生成 APA 需求文档。"
            : "原始录制包已导出，但未发现可加入资料集的文件。";
        OpenExportFolderCommand.RaiseCanExecuteChanged();
        RefreshRecordingTelemetry();
    }

    private async Task GenerateGeneralizedRequirementAsync()
    {
        try
        {
            PersistLlmSettings();
            GeneralizedStatus = "正在读取资料，并结合录制步骤与可用截图生成 APA 需求...";
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
            StatusMessage = "泛化 APA 需求文档已生成。";
            OpenGeneralizedFolderCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            GeneralizedStatus = $"生成失败：{ex.Message}";
            StatusMessage = "泛化需求生成失败，请检查输入文件或模型配置。";
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
        _promptTemplate = _promptTemplateStore.LoadOrCreateDefault();
    }

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
            ? $"浏览器插件在线。最近心跳：{FormatLastExtensionSeen()}"
            : "尚未收到浏览器插件心跳。已打开本地检测页，请确认 Chrome 已加载新版扩展，然后回到桌面端再点一次检测。";
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
        ExtensionConnectionStatus = _recordingCoordinator.IsRecording && !_recordingCoordinator.IsPaused
            ? $"浏览器插件在线，正在录制。最近心跳：{FormatLastExtensionSeen()}"
            : $"浏览器插件在线，等待录制。最近心跳：{FormatLastExtensionSeen()}";
    }

    private void NoteCaptureEventSource(CaptureEvent captureEvent)
    {
        _extensionConnectionTracker.NoteCaptureEvent(captureEvent);
        if (!ExtensionConnectionTracker.IsBrowserExtensionEvent(captureEvent.EventType))
        {
            return;
        }

        ExtensionConnectionStatus = _recordingCoordinator.IsRecording && !_recordingCoordinator.IsPaused
            ? $"浏览器插件在线，正在录制。最近心跳：{FormatLastExtensionSeen()}"
            : $"浏览器插件在线，等待录制。最近心跳：{FormatLastExtensionSeen()}";
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
        OnPropertyChanged(nameof(LiveCaptureSummary));
        OnPropertyChanged(nameof(ExportHint));
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
