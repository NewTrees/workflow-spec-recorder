using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApaFlowRecorder.Core.Models;

public sealed class RecordedStep : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string? _literalValue;
    private string? _variableName;
    private bool _isSensitive;
    private string _successCriteria = string.Empty;
    private string _notes = string.Empty;

    public Guid Id { get; init; } = Guid.NewGuid();
    public RecordedAction Action { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? PageUrl { get; init; }
    public string? PageTitle { get; init; }
    public ElementSnapshot? Element { get; init; }
    public string? ScreenshotPath { get; set; }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string? LiteralValue
    {
        get => _literalValue;
        set => SetField(ref _literalValue, value);
    }

    public string? VariableName
    {
        get => _variableName;
        set => SetField(ref _variableName, value);
    }

    public bool IsSensitive
    {
        get => _isSensitive;
        set => SetField(ref _isSensitive, value);
    }

    public string SuccessCriteria
    {
        get => _successCriteria;
        set => SetField(ref _successCriteria, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

