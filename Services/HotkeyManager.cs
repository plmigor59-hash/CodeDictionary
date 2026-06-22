using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace CodeDictionary.Services;

public class HotkeyAction
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DefaultGesture { get; set; } = "";
    public string CurrentGesture { get; set; } = "";
    public string Scope { get; set; } = "Global";
}

public class HotkeyManager
{
    private readonly Dictionary<string, HotkeyAction> _actions = new();
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public HotkeyManager(string storageDir)
    {
        _storagePath = Path.Combine(storageDir, "hotkeys.json");
        InitializeDefaults();
        Load();
    }

    public IReadOnlyCollection<HotkeyAction> Actions => _actions.Values;

    private void InitializeDefaults()
    {
        Add("ToggleBreakpoint", "Точка останова (F9)", "F9", "Global");
        Add("ContinueDebug", "Продолжить отладку (F5)", "F5", "Global");
        Add("DebugStepOver", "Шаг с обходом (F10)", "F10", "Global");
        Add("ToggleBookmark", "Закладка (Alt+F2)", "Alt+F2", "Editor");
        Add("NextBookmark", "След. закладка (Ctrl+Alt+Down)", "Ctrl+Alt+Down", "Editor");
        Add("PrevBookmark", "Пред. закладка (Ctrl+Alt+Up)", "Ctrl+Alt+Up", "Editor");
        Add("ShowBookmarkList", "Список закладок (F3)", "F3", "Editor");
        Add("ToggleHelp", "Помощь (F1)", "F1", "Global");
        Add("CloseHelp", "Закрыть помощь (Esc)", "Esc", "Global");
        Add("SaveFile", "Сохранить файл (Ctrl+S)", "Ctrl+S", "Global");
        Add("OpenFile", "Открыть файл (Ctrl+O)", "Ctrl+O", "Global");
        Add("AddEntry", "Новая запись (Ctrl+N)", "Ctrl+N", "Global");
        Add("GoToLine", "Перейти к строке (Ctrl+G)", "Ctrl+G", "Editor");
        Add("GoToDefinition", "Перейти к определению (F12)", "F12", "Editor");
        Add("FindNext", "Найти далее", "", "Global");
        Add("FindPrevious", "Найти ранее", "", "Global");
        Add("SaveEntry", "Сохранить запись", "", "Global");
        Add("DeleteEntry", "Удалить запись", "", "Global");
        Add("FormatCode", "Форматировать код", "", "Editor");
        Add("ToggleTerminal", "Терминал", "", "Global");
        Add("ToggleAnalyze", "Анализ кода", "", "Global");
        Add("ToggleDescription", "Описание", "", "Global");
        Add("RunScript", "Запуск скрипта", "", "Global");
    }

    private void Add(string id, string displayName, string defaultGesture, string scope)
    {
        _actions[id] = new HotkeyAction
        {
            Id = id,
            DisplayName = displayName,
            DefaultGesture = defaultGesture,
            CurrentGesture = defaultGesture,
            Scope = scope
        };
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
                return;

            var json = File.ReadAllText(_storagePath);
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (saved == null) return;

            foreach (var kvp in saved)
            {
                if (_actions.TryGetValue(kvp.Key, out var action))
                {
                    action.CurrentGesture = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading hotkeys: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var data = new Dictionary<string, string>();
            foreach (var action in _actions.Values)
            {
                data[action.Id] = action.CurrentGesture;
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving hotkeys: {ex.Message}");
        }
    }

    public KeyGesture? GetGesture(string id)
    {
        if (!_actions.TryGetValue(id, out var action))
            return null;

        var gestureStr = action.CurrentGesture;
        if (string.IsNullOrWhiteSpace(gestureStr))
            return null;

        try
        {
            var converter = new KeyGestureConverter();
            return converter.ConvertFromString(gestureStr) as KeyGesture;
        }
        catch
        {
            return null;
        }
    }

    public void SetGesture(string id, string gestureStr)
    {
        if (_actions.TryGetValue(id, out var action))
        {
            action.CurrentGesture = gestureStr;
            Save();
        }
    }

    public void ResetAll()
    {
        foreach (var action in _actions.Values)
        {
            action.CurrentGesture = action.DefaultGesture;
        }
        Save();
    }

    public string GetDisplayText(string id)
    {
        return _actions.TryGetValue(id, out var action) ? action.DisplayName : id;
    }
}
