using ICSharpCode.AvalonEdit.Document;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodeDictionary.Models;

public class EditorTabModel : INotifyPropertyChanged
{
    private string? _filePath;
    private string _title;
    private bool _isDirty;
    private string? _syntaxName;
    private readonly bool _isClosable;
    private readonly Stack<EntrySnapshot> _undoStack = new();
    private readonly Stack<EntrySnapshot> _redoStack = new();

    public EditorTabModel(string title, string? filePath = null, string? syntaxName = null, string? content = null, bool isClosable = true)
    {
        _title = title;
        _filePath = filePath;
        _syntaxName = syntaxName;
        _isClosable = isClosable;
        Document = new TextDocument(content ?? string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TextDocument Document { get; }

    public List<TextSegmentStyle> Segments { get; } = new();

    public HashSet<int> Bookmarks { get; } = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event Action? UndoRedoChanged;

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
            OnPropertyChanged(nameof(IsFromFile));
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

    public CodeEntry? Entry { get; set; }

    public bool IsEntryTab => Entry != null;

    public string DisplayName => !string.IsNullOrWhiteSpace(FilePath)
        ? Path.GetFileName(FilePath)
        : Title;

    public string Header => IsDirty ? $"{DisplayName} *" : DisplayName;

    public bool IsFromFile => !string.IsNullOrWhiteSpace(_filePath);

    public bool IsClosable => _isClosable;

    public void MarkSaved()
    {
        IsDirty = false;
    }

    public void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(Header));
    }

    public void PushUndo()
    {
        _undoStack.Push(new EntrySnapshot
        {
            Title = _title,
            SyntaxName = _syntaxName,
            Code = Document.Text,
            EntryCode = Entry?.Code,
            EntryDescription = Entry?.Description,
            EntryCategory = Entry?.Category,
            EntryTags = Entry?.Tags,
            EntrySyntax = Entry?.Syntax
        });
        _redoStack.Clear();
        UndoRedoChanged?.Invoke();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        _redoStack.Push(new EntrySnapshot
        {
            Title = _title,
            SyntaxName = _syntaxName,
            Code = Document.Text,
            EntryCode = Entry?.Code,
            EntryDescription = Entry?.Description,
            EntryCategory = Entry?.Category,
            EntryTags = Entry?.Tags,
            EntrySyntax = Entry?.Syntax
        });

        var snapshot = _undoStack.Pop();
        ApplySnapshot(snapshot);
        UndoRedoChanged?.Invoke();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        _undoStack.Push(new EntrySnapshot
        {
            Title = _title,
            SyntaxName = _syntaxName,
            Code = Document.Text,
            EntryCode = Entry?.Code,
            EntryDescription = Entry?.Description,
            EntryCategory = Entry?.Category,
            EntryTags = Entry?.Tags,
            EntrySyntax = Entry?.Syntax
        });

        var snapshot = _redoStack.Pop();
        ApplySnapshot(snapshot);
        UndoRedoChanged?.Invoke();
    }

    private void ApplySnapshot(EntrySnapshot snapshot)
    {
        _title = snapshot.Title;
        _syntaxName = snapshot.SyntaxName;
        Document.Text = snapshot.Code;
        if (Entry != null)
        {
            Entry.Code = snapshot.EntryCode ?? "";
            Entry.Description = snapshot.EntryDescription ?? "";
            Entry.Category = snapshot.EntryCategory ?? "";
            Entry.Tags = snapshot.EntryTags ?? new List<string>();
            Entry.Syntax = snapshot.EntrySyntax ?? "";
        }
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SyntaxName));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Header));
    }

    public void ClearUndoRedo()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UndoRedoChanged?.Invoke();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private struct EntrySnapshot
    {
        public string Title;
        public string? SyntaxName;
        public string Code;
        public string? EntryCode;
        public string? EntryDescription;
        public string? EntryCategory;
        public List<string>? EntryTags;
        public string? EntrySyntax;
    }
}
