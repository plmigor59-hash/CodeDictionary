using CodeDictionary;
using ICSharpCode.AvalonEdit.Document;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodeDictionary.Models;

public class EditorTabModel : INotifyPropertyChanged
{
    private string? _filePath;
    private string _title;
    private bool _isDirty;
    private string? _syntaxName;

    public EditorTabModel(string title, string? filePath = null, string? syntaxName = null, string? content = null)
    {
        _title = title;
        _filePath = filePath;
        _syntaxName = syntaxName;
        Document = new TextDocument(content ?? string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TextDocument Document { get; }

    public List<TextSegmentStyle> Segments { get; } = new();

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value)
            {
                return;
            }

            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Header));
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Header));
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
            {
                return;
            }

            _isDirty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Header));
        }
    }

    public string? SyntaxName
    {
        get => _syntaxName;
        set
        {
            if (_syntaxName == value)
            {
                return;
            }

            _syntaxName = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(FilePath)
        ? Path.GetFileName(FilePath)
        : Title;

    public string Header => IsDirty ? $"{DisplayName} *" : DisplayName;

    public void MarkSaved()
    {
        IsDirty = false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
