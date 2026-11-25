using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BulkExtensionChanger.Models;

public sealed record ExtensionOption(string Extension, string DisplayName, string Description, bool IsCustom = false)
{
    public override string ToString() => DisplayName;
}

public enum CollisionMode
{
    AutoSuffix,
    Skip,
    Overwrite
}

public sealed record CollisionModeOption(CollisionMode Mode, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record UndoEntry(string OriginalPath, string RenamedPath);

public sealed class FileRenameItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _newFileName = string.Empty;
    private string _targetPath = string.Empty;
    private string _statusText = string.Empty;
    private bool _hasConflict;
    private bool _isUnchanged;

    public required string OriginalPath { get; init; }
    public required string FileName { get; init; }
    public required string DirectoryPath { get; init; }
    public required string CurrentExtension { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewFileName
    {
        get => _newFileName;
        set
        {
            if (_newFileName != value)
            {
                _newFileName = value;
                OnPropertyChanged();
            }
        }
    }

    public string TargetPath
    {
        get => _targetPath;
        set
        {
            if (_targetPath != value)
            {
                _targetPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasConflict
    {
        get => _hasConflict;
        set
        {
            if (_hasConflict != value)
            {
                _hasConflict = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsUnchanged
    {
        get => _isUnchanged;
        set
        {
            if (_isUnchanged != value)
            {
                _isUnchanged = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
