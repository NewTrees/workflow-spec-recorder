using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Desktop.Services;

namespace ApaFlowRecorder.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly RecordingCoordinator _recordingCoordinator = new();
    private readonly SessionExportService _exportService = new();
    private readonly LocalCaptureServer _captureServer;
    private RecordedStep? _selectedStep;
    private string _statusMessage = "正在初始化本地服务...";
    private string _serverStatus = "本地服务未启动";
    private string? _lastExportPath;

    public MainWindowViewModel()
    {
        _captureServer = new LocalCaptureServer(_recordingCoordinator.HandleCaptureEventAsync);
        _recordingCoordinator.StepRecorded += OnStepRecorded;
        _recordingCoordinator.SessionChanged += OnSessionChanged;

        NewSessionCommand = new RelayCommand(CreateNewSession);
        StartRecordingCommand = new RelayCommand(StartRecording);
        PauseRecordingCommand = new RelayCommand(PauseRecording);
        StopRecordingCommand = new RelayCommand(StopRecording);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => Steps.Count > 0);
        OpenExportFolderCommand = new RelayCommand(OpenExportFolder, () => !string.IsNullOrWhiteSpace(LastExportPath));

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

    public string RecordingStatus => _recordingCoordinator.IsRecording
        ? _recordingCoordinator.IsPaused ? "已暂停" : "录制中"
        : "未录制";

    public string? LastExportPath
    {
        get => _lastExportPath;
        private set
        {
            _lastExportPath = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand NewSessionCommand { get; }
    public RelayCommand StartRecordingCommand { get; }
    public RelayCommand PauseRecordingCommand { get; }
    public RelayCommand StopRecordingCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public RelayCommand OpenExportFolderCommand { get; }

    public async Task InitializeAsync()
    {
        await _captureServer.StartAsync();
        ServerStatus = "本地服务已启动：127.0.0.1:8765";
        StatusMessage = "请加载 Chrome 扩展后开始录制。";
    }

    public async ValueTask DisposeAsync()
    {
        await _captureServer.DisposeAsync();
    }

    private void CreateNewSession()
    {
        _recordingCoordinator.NewSession();
        StatusMessage = "已创建新流程。";
    }

    private void StartRecording()
    {
        _recordingCoordinator.Start();
        OnPropertyChanged(nameof(RecordingStatus));
        StatusMessage = "录制中：请在 Chrome 中执行业务流程。";
    }

    private void PauseRecording()
    {
        _recordingCoordinator.Pause();
        OnPropertyChanged(nameof(RecordingStatus));
        StatusMessage = "录制已暂停。";
    }

    private void StopRecording()
    {
        _recordingCoordinator.Stop();
        OnPropertyChanged(nameof(RecordingStatus));
        StatusMessage = "录制已结束，可继续编辑并导出。";
    }

    private async Task ExportAsync()
    {
        LastExportPath = await _exportService.ExportAsync(_recordingCoordinator.CurrentSession);
        StatusMessage = $"已导出到：{LastExportPath}";
        OpenExportFolderCommand.RaiseCanExecuteChanged();
    }

    private void OpenExportFolder()
    {
        if (string.IsNullOrWhiteSpace(LastExportPath) || !Directory.Exists(LastExportPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = LastExportPath,
            UseShellExecute = true
        });
    }

    private void OnStepRecorded(object? sender, RecordedStep step)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            Steps.Add(step);
            SelectedStep = step;
            ExportCommand.RaiseCanExecuteChanged();
            StatusMessage = $"已记录步骤：{step.Title}";
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
        OnPropertyChanged(nameof(RecordingStatus));
        ExportCommand.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
