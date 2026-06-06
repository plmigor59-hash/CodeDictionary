using CodeDictionary.Models;
using CodeDictionary.Properties;
using CodeDictionary.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;  // Для OpenFileDialog и SaveFileDialog
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace CodeDictionary;

public partial class MainWindow : Window
{
    private const string CategorySeparator = "/";
    private const string UncategorizedCategoryName = "Без категории";

    private readonly DataService _dataService;
    private CodeDictionaryData _data;
    private List<CodeEntry> _filteredEntries;
    private CodeEntry? _currentEntry;
    private bool _isInitialized;
    private string _selectedCategoryPath = string.Empty;
    private object? _draggedTreeItem;
    private Point _dragStartPoint;
    private AppTheme _currentTheme;
    private AppState _appState;
    private IHighlightingDefinition? _darkCSharpHighlighting;
    private IHighlightingDefinition? _lightCSharpHighlighting;
    private IHighlightingDefinition? _darkCppHighlighting;
    private IHighlightingDefinition? _lightCppHighlighting;
    private IHighlightingDefinition? _standart1CHigh;
    private IHighlightingDefinition? _dark1CHigh;
    private IHighlightingDefinition? _darkXMLHigh;
    private IHighlightingDefinition? _darkHTMLHigh;     
    private string? _currentFilePath = null;  // Хранит путь к текущему открытому файлу0
    private SnackbarNotification _snackbar = new SnackbarNotification();
    private List<TextSegmentStyle> _textSegments = new();
    private readonly ObservableCollection<EditorTabModel> _editorTabs = new();
    private EditorTabModel? _activeEditorTab;
    private EditorTabModel? _entryEditorTab;
    private bool _ignoreTreeSelectionChange;
    private bool _isSwitchingEditorTab;
    private bool _isUpdatingEditorContent;
    private bool _suppressSyntaxSelectionChange;


    public MainWindow()


    {
        // Загружаем кастомную тему подсветки для тёмного и светлого режимов
      

        InitializeComponent();
        LoadWindowState();
        _dataService = new DataService();
        _data = new CodeDictionaryData();
        _filteredEntries = new List<CodeEntry>();
        _isInitialized = false;
        _currentTheme = AppTheme.Dark;
        _appState = new AppState();

        LoadCustomHighlighting();

       
        InitializeEditorTabs();
       


        // Инициализируем список шрифтов и настроек подсветки
        InitializeFontSettings();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        CodeTextBox.TextArea.TextView.LineTransformers.Add(new CustomColorTransformer(() => _textSegments));

        // Подписываемся на изменения текста
        CodeTextBox.TextChanged += CodeTextBox_TextChanged;
        TitleTextBox.TextChanged += EntryField_TextChanged;
        DescriptionTextBox.TextChanged += EntryField_TextChanged;
        TagsTextBox.TextChanged += EntryField_TextChanged;
      

    }

    private void InitializeEditorTabs()
    {
        _editorTabs.Clear();

        //_entryEditorTab = new EditorTabModel("Запись", isClosable: false);
        //_editorTabs.Add(_entryEditorTab);

        if (EditorTabs != null)
        {
            EditorTabs.ItemsSource = _editorTabs;
            EditorTabs.SelectedItem = _entryEditorTab;
        }

        ActivateEditorTab(_entryEditorTab, selectInTabControl: false);
    }

    private EditorTabModel? FindEntryTab(Guid entryId)
    {
        return _editorTabs.FirstOrDefault(tab => tab.Entry?.Id == entryId);
    }

    private EditorTabModel OpenEntryTab(CodeEntry entry)
    {
        var existingTab = FindEntryTab(entry.Id);
        if (existingTab != null)
        {
            ActivateEditorTab(existingTab);
            return existingTab;
        }

        var tab = new EditorTabModel(entry.Title, syntaxName: entry.Syntax, isClosable: true)
        {
            Entry = entry
        };

        tab.Document.Text = entry.Code;
        tab.MarkSaved();
        LoadSegmentsIntoTab(tab, entry);
        _editorTabs.Add(tab);
        ActivateEditorTab(tab);
        return tab;
    }

    private void CodeTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isSwitchingEditorTab || _isUpdatingEditorContent || _activeEditorTab == null)
        {
            return;
        }

        _activeEditorTab.IsDirty = true;
        SyncActiveEntryTabFromForm();
        UpdateSegmentsAfterTextChange();
    }

    private void EntryField_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSwitchingEditorTab || _isUpdatingEditorContent)
        {
            return;
        }

        SyncActiveEntryTabFromForm();
    }

    private void SyncActiveEntryTabFromForm()
    {
        var activeTab = GetActiveEditorTab();
        if (activeTab?.Entry == null)
        {
            return;
        }

        var entry = activeTab.Entry;
        entry.Title = TitleTextBox.Text;
        entry.Description = DescriptionTextBox.Text;
        entry.Code = CodeTextBox.Text;
        entry.Category = NormalizeCategoryPath(CategoryComboBox.Text);
        entry.Tags = TagsTextBox.Text
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        entry.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;
        activeTab.Title = entry.Title;
        activeTab.SyntaxName = entry.Syntax;
        activeTab.IsDirty = true;
        _currentEntry = entry;
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSwitchingEditorTab || EditorTabs?.SelectedItem is not EditorTabModel tab)
        {
            return;
        }

        ActivateEditorTab(tab, selectInTabControl: false);
    }

    private EditorTabModel? GetActiveEditorTab()
    {
        return _activeEditorTab ?? _entryEditorTab;
    }

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not EditorTabModel tab)
        {
            return;
        }

        CloseEditorTab(tab);
        e.Handled = true;
    }

    private void CloseEditorTab(EditorTabModel tab)
    {
        if (!tab.IsClosable)
        {
            return;
        }

        if (tab.IsDirty &&
            !CustomMessageBox.ShowQuestion($"Закрыть вкладку '{tab.DisplayName}' без сохранения?", "Подтверждение"))
        {
            return;
        }

        var wasActive = ReferenceEquals(_activeEditorTab, tab);
        var tabIndex = _editorTabs.IndexOf(tab);
        if (tabIndex < 0)
        {
            return;
        }

        _editorTabs.RemoveAt(tabIndex);

        if (_editorTabs.Count == 0)
        {
            InitializeEditorTabs();
            return;
        }

        if (wasActive)
        {
            var nextIndex = Math.Min(tabIndex, _editorTabs.Count - 1);
            ActivateEditorTab(_editorTabs[nextIndex]);
        }
        else
        {
            RefreshEditorTabHeaders();
        }
    }

    private void RefreshEditorTabHeaders()
    {
        foreach (var tab in _editorTabs)
        {
            tab.NotifyHeaderChanged();
        }
    }

    private void ActivateEditorTab(EditorTabModel? tab, bool selectInTabControl = true)
    {
        if (tab == null)
        {
            return;
        }

        _isSwitchingEditorTab = true;
        try
        {
           

            _activeEditorTab = tab;
            _textSegments = tab.Segments;

            if (CodeTextBox.Document != tab.Document)            {
                CodeTextBox.Document = tab.Document;
            }
        
            CodeTextBox.TextArea.TextView.Redraw();
            _currentFilePath = tab.FilePath;
            this.Title = !string.IsNullOrWhiteSpace(tab.FilePath)
                ? $"{Path.GetFileName(tab.FilePath)} "
                : $"{tab.Title} ";

            _suppressSyntaxSelectionChange = true;

            //TitleTextBox.Text = tab.Title;
            try
            {
                if (!string.IsNullOrWhiteSpace(tab.SyntaxName))
                {
                    SyntaxHighlightingComboBox.SelectedItem = tab.SyntaxName;
                }
            }
            finally
            {
                _suppressSyntaxSelectionChange = false;
            }

            ApplySyntaxHighlighting(tab.SyntaxName);

            if (selectInTabControl && EditorTabs != null && !ReferenceEquals(EditorTabs.SelectedItem, tab))
            {
                EditorTabs.SelectedItem = tab;
            }
        }
        finally
        {
            _isSwitchingEditorTab = false;
        }
    }

    //private void MoveEditorTabToFront(EditorTabModel tab)
    //{
    //    var currentIndex = _editorTabs.IndexOf(tab);
    //    if (currentIndex > 0)
    //    {
    //        _editorTabs.Move(currentIndex, 0);
    //    }
    //}

    private EditorTabModel? FindEditorTab(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return _editorTabs.FirstOrDefault(tab =>
            !string.IsNullOrWhiteSpace(tab.FilePath) &&
            string.Equals(Path.GetFullPath(tab.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    private EditorTabModel OpenEditorTab(string filePath)
    {
        var existingTab = FindEditorTab(filePath);
        if (existingTab != null)
        {
            TitleTextBox.Text = Path.GetFileName(filePath);
            ActivateEditorTab(existingTab);
            return existingTab;
        }

        var content = File.ReadAllText(filePath);

        var tab = new EditorTabModel(Path.GetFileName(filePath), filePath, GetSyntaxSelectionForFile(filePath), content, isClosable: true);
        tab.MarkSaved();
        _editorTabs.Add(tab);
        TitleTextBox.Text = Path.GetFileName(filePath);
        ActivateEditorTab(tab);
        return tab;
    }

    private string? GetSyntaxSelectionForFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".cs" => _currentTheme == AppTheme.Dark ? "Темная C#" : "Стандартная (C#)",
            ".cpp" or ".cxx" or ".cc" or ".hpp" or ".h" => _currentTheme == AppTheme.Dark ? "Темная C++" : "Стандартная (C++)",
            ".bsl" or ".os" => _currentTheme == AppTheme.Dark ? "Темная (1C)" : "Стандартная (1C)",
            ".xml" or ".xsd" or ".xaml" => _currentTheme == AppTheme.Dark ? "Темная (XML)" : "Стандартная (XML)",
            ".html" or ".htm" => _currentTheme == AppTheme.Dark ? "Темная (HTML)" : "Стандартная (HTML)",
            ".js" => "Стандартная (JavaScript)",
            ".java" => "Стандартная (Java)",
            ".css" => "Стандартная (CSS)",
            ".php" => "Стандартная (PHP)",
            ".ps1" => "Стандартная (PowerShell)",
            ".sql" => "Стандартная (SQL)",
            ".vb" => "Стандартная (VB)",
            ".patch" or ".diff" => "Стандартная (Patch)",
            _ => null
        };
    }

    private void ApplySyntaxHighlighting(string? selected)
    {
        if (CodeTextBox == null)
        {
            return;
        }

        var syntax = selected ?? SyntaxHighlightingComboBox?.SelectedItem?.ToString();

        if (syntax == "Темная C#")
        {
            CodeTextBox.SyntaxHighlighting = _darkCSharpHighlighting ?? HighlightingManager.Instance.GetDefinition("C#");
        }
        else if (syntax == "Темная C++")
        {
            CodeTextBox.SyntaxHighlighting = _darkCppHighlighting ?? HighlightingManager.Instance.GetDefinition("C++");
        }
        else if (syntax == "Стандартная (C++)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C++");
        }
        else if (syntax == "Темная (1C)")
        {
            CodeTextBox.SyntaxHighlighting = _dark1CHigh ?? HighlightingManager.Instance.GetDefinition("1C");
        }
        else if (syntax == "Темная (XML)")
        {
            CodeTextBox.SyntaxHighlighting = _darkXMLHigh ?? HighlightingManager.Instance.GetDefinition("XML");
        }
        else if (syntax == "Темная (HTML)")
        {
            CodeTextBox.SyntaxHighlighting = _darkHTMLHigh ?? HighlightingManager.Instance.GetDefinition("HTML Dark");
        }
        else if (syntax == "Стандартная (1C)")
        {
            CodeTextBox.SyntaxHighlighting = _standart1CHigh ?? HighlightingManager.Instance.GetDefinition("1C");
        }
        else if (syntax == "Стандартная (Java)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Java");
        }
        else if (syntax == "Стандартная (JavaScript)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        }
        else if (syntax == "Стандартная (HTML)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
        }
        else if (syntax == "Стандартная (XML)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        }
        else if (syntax == "Стандартная (CSS)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("CSS");
        }
        else if (syntax == "Стандартная (PHP)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PHP");
        }
        else if (syntax == "Стандартная (PowerShell)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PowerShell");
        }
        else if (syntax == "Стандартная (SQL)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("SQL");
        }
        else if (syntax == "Стандартная (VB)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
        }
        else if (syntax == "Стандартная (ASP/XHTML)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("ASP/XHTML");
        }
        else if (syntax == "Стандартная (Patch)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Patch");
        }
        else
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }

        UpdateCodeEditorColors(syntax);
    }

    private void MarkTabTextDirty()
    {
        if (_activeEditorTab != null)
        {
            _activeEditorTab.IsDirty = true;
        }
    }

    private void LoadWindowState()
    {
        // Проверяем, что настройки существуют
        if (Settings.Default.WindowTop >= 0 && Settings.Default.WindowLeft >= 0)
        {
            this.Top = Settings.Default.WindowTop;
            this.Left = Settings.Default.WindowLeft;
        }

        this.Width = Settings.Default.WindowWidth;
        this.Height = Settings.Default.WindowHeight;
        this.WindowState = ParseWindowState(Settings.Default.WindowState);

        // Проверяем, попадает ли окно на экран
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)this.Left, (int)this.Top));
        if (this.Left + this.Width < screen.WorkingArea.Left ||
            this.Left > screen.WorkingArea.Right ||
            this.Top + this.Height < screen.WorkingArea.Top ||
            this.Top > screen.WorkingArea.Bottom)
        {
            // Если окно за пределами экрана — показываем по центру
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    // Сохранение состояния перед закрытием
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Сохраняем только если окно не свернуто
        if (this.WindowState != WindowState.Minimized)
        {
            // Сохраняем позицию и размер
            Settings.Default.WindowTop = this.Top;
            Settings.Default.WindowLeft = this.Left;
            Settings.Default.WindowWidth = this.Width;
            Settings.Default.WindowHeight = this.Height;
            Settings.Default.WindowState = this.WindowState.ToString();

            Settings.Default.Save(); // Сохраняем в файл
        }

        base.OnClosing(e);
    }

    private WindowState ParseWindowState(string state)
    {
        return state switch
        {
            "Maximized" => WindowState.Maximized,
            "Minimized" => WindowState.Minimized,
            _ => WindowState.Normal
        };
    
    }

    private void ApplyStyleToSelection(string? backgroundColor = null, string? foregroundColor = null,
                                          bool? bold = null, bool? italic = null, bool? underline = null,
                                          string? fontFamily = null, double? fontSize = null)
    {
        var selection = CodeTextBox.TextArea.Selection;
        if (selection.IsEmpty)
        {
            _snackbar.Show(CodeTextBox, "Сначала выделите текст", NotificationType.Warning, 1.5);
            return;
        }

        // Получаем границы выделения
        var start = selection.Segments.First().StartOffset;
        var end = selection.Segments.Last().EndOffset;
        var length = end - start;

        // Проверяем, не перекрывается ли с существующими стилями
        var existingSegment = _textSegments.FirstOrDefault(s => s.StartOffset == start && s.Length == length);

        if (existingSegment != null)
        {
            // Обновляем существующий стиль
            if (backgroundColor != null) existingSegment.BackgroundColor = backgroundColor;
            if (foregroundColor != null) existingSegment.ForegroundColor = foregroundColor;
            if (bold.HasValue) existingSegment.IsBold = bold.Value;
            if (italic.HasValue) existingSegment.IsItalic = italic.Value;
            if (underline.HasValue) existingSegment.IsUnderline = underline.Value;
            if (fontFamily != null) existingSegment.FontFamily = fontFamily;
            if (fontSize.HasValue) existingSegment.FontSize = fontSize.Value;
        }
        else
        {



            // Создаём новый стиль
            var newSegment = new TextSegmentStyle
            {
                StartOffset = start,
                Length = length,
                BackgroundColor = backgroundColor,  // может быть null, строкой с цветом, или "transparent"
                ForegroundColor = foregroundColor,  // может быть null или строкой с цветом
                IsBold = bold ?? false,
                IsItalic = italic ?? false,
                IsUnderline = underline ?? false,
                FontFamily = fontFamily,  // может быть null или названием шрифта
                FontSize = fontSize       // может быть null или размером шрифта
            };
            _textSegments.Add(newSegment);
        }

        // Обновляем визуальное отображение
        CodeTextBox.TextArea.TextView.Redraw();
    }


    // 🔄 Обновить сегменты после изменения текста
    private void UpdateSegmentsAfterTextChange()
    {
        var currentLength = CodeTextBox.Document.TextLength;

        // Удаляем стили, которые вышли за пределы документа
        _textSegments.RemoveAll(s => s.StartOffset + s.Length > currentLength);

        // Можно добавить логику смещения позиций при вставке/удалении
        // (для простоты пока просто перерисовываем)
        CodeTextBox.TextArea.TextView.Redraw();
    }

    private void ChangeBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.ColorDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string colorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            ApplyStyleToSelection(backgroundColor: colorHex);
        }
    }

    private void ChangeForeground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.ColorDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string colorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            ApplyStyleToSelection(foregroundColor: colorHex);
        }
    }

    private void MakeBold_Click(object sender, RoutedEventArgs e)
    {
        ApplyStyleToSelection(bold: true);
    }

    private void MakeItalic_Click(object sender, RoutedEventArgs e)
    {
        ApplyStyleToSelection(italic: true);
    }

    private void MakeUnderline_Click(object sender, RoutedEventArgs e)
    {
        ApplyStyleToSelection(underline: true);
    }

    private void ClearStyle_Click(object sender, RoutedEventArgs e)
    {
        ClearStyleFromSelection();
    }


    // ❌ Очистить стили с выделенного текста
    private void ClearStyleFromSelection()
    {
        var selection = CodeTextBox.TextArea.Selection;
        if (selection.IsEmpty) return;

        var start = selection.Segments.First().StartOffset;
        var end = selection.Segments.Last().EndOffset;

        // Удаляем все стили, попадающие в выделение
        _textSegments.RemoveAll(s => s.StartOffset >= start && s.StartOffset + s.Length <= end);

        CodeTextBox.TextArea.TextView.Redraw();
    }


    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog();

        // Настройки диалога
        openFileDialog.Title = "Выберите текстовый файл";
        openFileDialog.Filter = "Текстовые файлы (*.txt;*.xshd;*.bsl;*.cs;*.xaml;*.json;*.xml)|*.txt;*.xshd;*.bsl;*cs;*.xaml;*.json;*.xml|Все файлы (*.*)|*.*";
        openFileDialog.FilterIndex = 1;
        openFileDialog.Multiselect = true;
        //openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    OpenEditorTab(fileName);
                }

                if (openFileDialog.FileNames.Length > 0)
                {
                    _snackbar.Show(CodeTextBox, $"Открыто файлов: {openFileDialog.FileNames.Length}", NotificationType.Success, 1.5);
                }

                return;

              
            }


            catch (Exception ex)
            {
                ShowAlert($"Ошибка при открытии файла: {ex.Message}", isError: true);
            }
        }
    }

  


    private void ShowAlert(string message, bool isError = false)
    {
        var popup = new Popup
        {
            PlacementTarget = CodeTextBox,
            Placement = PlacementMode.Mouse,
            AllowsTransparency = true,
            StaysOpen = false
        };

        var border = new Border
        {

            Background = new SolidColorBrush(isError ? Color.FromRgb(200, 50, 50) : Color.FromRgb(50, 50, 50)),   
            

            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 12
            }
        };

        popup.Child = border;
        popup.IsOpen = true;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        timer.Tick += (s, e) =>
        {
            popup.IsOpen = false;
            timer.Stop();
        };
        timer.Start();
    }


    // 💾 СОХРАНИТЬ (если путь уже есть, иначе Сохранить как...)
    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        var activeTab = GetActiveEditorTab();
        if (activeTab == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(activeTab.FilePath))
        {
            SaveAsFile_Click(sender, e);
            return;
        }

        try
        {
            if (!CodeTextBox.TextArea.Selection.IsEmpty)
            {
                using (var stream = new FileStream(activeTab.FilePath, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(CodeTextBox.SelectedText);
                }

                if (EditorTabs != null)
                {
                    EditorTabs.SelectedItem = activeTab;
                }

                _snackbar.Show(CodeTextBox, "Выделенный текст успешно сохранён", NotificationType.Success, 1.5);
            }
            else
            {
                using (var stream = new FileStream(activeTab.FilePath, FileMode.Create, FileAccess.Write))
                {
                    CodeTextBox.Save(stream);
                }

                if (EditorTabs != null)
                {
                    EditorTabs.SelectedItem = activeTab;
                }

                _snackbar.Show(CodeTextBox, "Файл успешно сохранён", NotificationType.Success, 1.5);
            }

            activeTab.MarkSaved();
            _currentFilePath = activeTab.FilePath;
            this.Title = $"{Path.GetFileName(activeTab.FilePath)}";
        }
        catch (Exception ex)
        {
            ShowAlert($"Ошибка при сохранении: {ex.Message}", isError: true);
        }

        return;

        SaveAsFile_Click(sender, e);

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAsFile_Click(sender, e);  // Если файл новый, вызываем "Сохранить как"
        }
        else
        {
            try
            {
                // Проверяем, есть ли выделенный текст
                var selection = CodeTextBox.TextArea.Selection;
                if (!selection.IsEmpty)
                {
                    // Сохраняем только выделенные строки
                    var selectedText = CodeTextBox.SelectedText;
                    using (var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.Write(selectedText);
                    }
                 
                    _snackbar.Show(CodeTextBox, "Выделенный текст успешно сохранён", NotificationType.Success, 1.5);
                  
                }
                else
                {
                    // Сохраняем весь файл
                    using (var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write))
                    {
                        CodeTextBox.Save(stream);
                    }
                    _snackbar.Show(CodeTextBox, "Файл успешно сохранён", NotificationType.Success, 1.5);
                  
                  }

                // Обновляем заголовок
                this.Title = $"{Path.GetFileName(_currentFilePath)} ";
            }
            catch (Exception ex)
            {
                ShowAlert($"Ошибка при сохранении: {ex.Message}", isError: true);
            }
        }

       
    }

    // 📝 СОХРАНИТЬ КАК (всегда спрашиваем путь)
    private void SaveAsFile_Click(object sender, RoutedEventArgs e)
    {
        var activeTab = GetActiveEditorTab();
        if (activeTab == null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog();

        // Настройки диалога
        saveFileDialog.Title = "Сохранить файл как";
        saveFileDialog.Filter = "Все файлы (*.*)|*.*|" +
                                "Текстовые файлы (*.txt)|*.txt|" +
                                "1c (*.bsl)|*.bsl|" +
                                "c# (*.cs)|*.cs|" +
                                "XSHD (*.xshd)|*.xshd";
        saveFileDialog.FilterIndex = 1;
        //saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Если файл уже был открыт, предлагаем его имя по умолчанию
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            saveFileDialog.FileName = Path.GetFileName(_currentFilePath);
        }

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                activeTab.FilePath = saveFileDialog.FileName;
                _currentFilePath = activeTab.FilePath;

                using (var stream = new FileStream(activeTab.FilePath, FileMode.Create, FileAccess.Write))
                {
                    CodeTextBox.Save(stream);
                }

                activeTab.MarkSaved();
                this.Title = $"{Path.GetFileName(activeTab.FilePath)}";

                _snackbar.Show(CodeTextBox, "Файл успешно сохранён", NotificationType.Success, 1.5);
            }


            catch (Exception ex)
            {
                ShowAlert($"Ошибка при сохранении: {ex.Message}", isError: true);
            }
        }
    }


    private void LoadCustomHighlighting()

    {
        try
        {
            var standart1CXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Standart1C.xshd");
            if (System.IO.File.Exists(standart1CXshdPath))
            {
                using (var reader = new XmlTextReader(standart1CXshdPath))
                {
                    _standart1CHigh = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
        catch
        {
            _standart1CHigh = null;
        }

        try
        {
            var dark1CXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dark1C.xshd");
            if (System.IO.File.Exists(dark1CXshdPath))
            {
                using (var reader = new XmlTextReader(dark1CXshdPath))
                {
                    _dark1CHigh = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
        catch
        {
            _dark1CHigh = null;
        }

        try
        {
            var darkXMLXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DarkXML.xshd");
            if (System.IO.File.Exists(darkXMLXshdPath))
            {
                using (var reader = new XmlTextReader(darkXMLXshdPath))
               
                {
                    var xshd = HighlightingLoader.LoadXshd(reader);
                    _darkXMLHigh = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting("XML Dark",
                      new[] { ".xml", ".xaml" }, _darkXMLHigh);
                }
                }
        }
        catch
        {
            _darkXMLHigh = null;
        }

        try
        {
            var darkHTMLXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DarkHTML.xshd");
            if (System.IO.File.Exists(darkHTMLXshdPath))
            {
                using (var reader = new XmlTextReader(darkHTMLXshdPath))
                {
                    var xshd = HighlightingLoader.LoadXshd(reader);
                    _darkHTMLHigh = HighlightingLoader.Load(xshd, HighlightingManager.Instance);

                    HighlightingManager.Instance.RegisterHighlighting("HTML  Dark",
                        new[] { ".html", ".htm" }, _darkHTMLHigh);
                }
            }
        }
        catch
        {
            _darkHTMLHigh = null;
        }


        try
        {
            var darkXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DarkCSharp.xshd");
            if (System.IO.File.Exists(darkXshdPath))
            {
                using (var reader = new XmlTextReader(darkXshdPath))
                {
                    _darkCSharpHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
        catch
        {
            _darkCSharpHighlighting = null;
        }

        try
        {
            var lightXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LightCSharp.xshd");
            if (System.IO.File.Exists(lightXshdPath))
            {
                using (var reader = new XmlTextReader(lightXshdPath))
                {
                    _lightCSharpHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
        catch
        {
            _lightCSharpHighlighting = null;
        }

        // Если не удалось загрузить кастомную светлую тему, используем стандартную
        if (_lightCSharpHighlighting == null)
        {
            _lightCSharpHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }

        try
        {
            var darkCppPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DarkCpp.xshd");
            if (System.IO.File.Exists(darkCppPath))
            {
                using (var reader = new XmlTextReader(darkCppPath))
                {
                    _darkCppHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
        catch
        {
            _darkCppHighlighting = null;
        }

        try
        {
            var lightCppPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LightCpp.xshd");
            if (System.IO.File.Exists(lightCppPath))
            {
                using (var reader = new XmlTextReader(lightCppPath))
                {
                    _lightCppHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
        catch
        {
            _lightCppHighlighting = null;

        }

        if (_lightCppHighlighting == null)
        {
            _lightCppHighlighting = HighlightingManager.Instance.GetDefinition("C++");
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Обновляем иконку кнопки развертывания
        if (WindowState == WindowState.Maximized)
        {
            MaximizeButton.Content = "❐";
        }
        else
        {
            MaximizeButton.Content = "□";
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Двойной клик - развернуть/свернуть
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }
        else
        {
            // Одиночный клик - перетаскивание
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
       {
      

        _data = await _dataService.LoadDataAsync();
        _appState = await _dataService.LoadStateAsync();


        DescriptionTextBox.Visibility = Visibility.Collapsed;
        DescriptionSplitter.Visibility = Visibility.Collapsed;
        DescriptionRow.Height = new GridLength(0);
        ToggleDescriptionButton.Content = " ▼ Развернуть ";
 
        RefreshEntriesList();
        _selectedCategoryPath = NormalizeCategoryPath(_appState.SelectedCategory);
        if (!string.IsNullOrWhiteSpace(_selectedCategoryPath))
        {
            CategoryComboBox.Text = _selectedCategoryPath;
        }

        // Восстанавливаем выбранную запись
        if (_appState.SelectedEntryId.HasValue)
        {
            SelectEntryInTree(_appState.SelectedEntryId.Value);
        }

        // Применяем сохраненную тему
        ThemeCheckBox.IsChecked = _appState.IsLightTheme;
        ApplyTheme(_appState.IsLightTheme ? AppTheme.Light : AppTheme.Dark);

      



        _isInitialized = true;
    }

    private void InitializeFontSettings()
    {
        // Популярные моноширинные шрифты для кода
        var monospaceFonts = new[] { "Consolas", "Courier New", "Lucida Console", "Cascadia Code", "Fira Code", "JetBrains Mono" };
        foreach (var font in monospaceFonts)
        {
            FontFamilyComboBox.Items.Add(font);
        }
        FontFamilyComboBox.SelectedIndex = 0;

        // Размеры шрифта
        var fontSizes = new[] { 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 22, 24 };
        foreach (var size in fontSizes)
        {
            FontSizeComboBox.Items.Add(size);
        }
        FontSizeComboBox.SelectedItem = 12;

        WordWrapCheckBox.IsChecked = false;
     
        SyntaxHighlightingComboBox.SelectedIndex = 0; // По умолчанию DarkCSharp
    }

    private void SyntaxHighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSyntaxSelectionChange || SyntaxHighlightingComboBox?.SelectedItem == null)
        {
            return;
        }

        var selectedSyntax = SyntaxHighlightingComboBox.SelectedItem.ToString();
        if (_activeEditorTab != null)
        {
            _activeEditorTab.SyntaxName = selectedSyntax;
            if (_activeEditorTab.Entry != null)
            {
                _activeEditorTab.Entry.Syntax = selectedSyntax ?? string.Empty;
                _activeEditorTab.IsDirty = true;
            }
        }

        ApplySyntaxHighlighting(selectedSyntax);
        return;

        var selected = SyntaxHighlightingComboBox.SelectedItem.ToString();

        if (selected == "Темная С#")
        {
            CodeTextBox.SyntaxHighlighting = _darkCSharpHighlighting ?? HighlightingManager.Instance.GetDefinition("C#");
        }
       
        else if (selected == "Темная C++")
        {
            CodeTextBox.SyntaxHighlighting = _darkCppHighlighting ?? HighlightingManager.Instance.GetDefinition("C++");
        }
       
        else if (selected == "Стандартная (C++)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C++");
        }
        
        else if (selected == "Темная (1C)")
        {
            CodeTextBox.SyntaxHighlighting = _dark1CHigh ?? HighlightingManager.Instance.GetDefinition("1C");
        }

        else if (selected == "Темная (XML)")
        {
            CodeTextBox.SyntaxHighlighting = _darkXMLHigh ?? HighlightingManager.Instance.GetDefinition("XML");
        }
        else if (selected == "Темная (HTML)")
        {
            CodeTextBox.SyntaxHighlighting = _darkHTMLHigh ?? HighlightingManager.Instance.GetDefinition("HTML Dark");
        }
        else if (selected == "Стандартная (1C)")
        {
            CodeTextBox.SyntaxHighlighting = _standart1CHigh ?? HighlightingManager.Instance.GetDefinition("1C");
        }

        else if (selected == "Стандартная (Java)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Java");
        }
        else if (selected == "Стандартная (JavaScript)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        }
        else if (selected == "Стандартная (HTML)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
        }
        else if (selected == "Стандартная (XML)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        }
        else if (selected == "Стандартная (CSS)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("CSS");
        }
        else if (selected == "Стандартная (PHP)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PHP");
        }

        else if (selected == "Стандартная (PowerShell)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PowerShell");
        }
        else if (selected == "Стандартная (SQL)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("SQL");
        }
        else if (selected == "Стандартная (VB)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
        }
        else if (selected == "Стандартная (ASP/XHTML)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("ASP/XHTML");
        }
        else if (selected == "Стандартная (Patch)")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Patch");
        }


        else
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }


        UpdateCodeEditorColors(selected);
    }

    private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontFamilyComboBox.SelectedItem != null)
        {
            var selectedFont = FontFamilyComboBox.SelectedItem.ToString()!;

            // Проверяем, есть ли выделенный текст
            var selection = CodeTextBox?.TextArea?.Selection;
            if (selection != null && !selection.IsEmpty)
            {
                // Применяем шрифт только к выделенному тексту
                ApplyStyleToSelection(fontFamily: selectedFont);
            }
            else
            {
                // Применяем шрифт ко всему редактору
                if (CodeTextBox != null)
                {
                    CodeTextBox.FontFamily = new System.Windows.Media.FontFamily(selectedFont);
                }
            }
        }
    }

    private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontSizeComboBox.SelectedItem != null)
        {
            var selectedSize = (int)FontSizeComboBox.SelectedItem;

            // Проверяем, есть ли выделенный текст
            var selection = CodeTextBox?.TextArea?.Selection;
            if (selection != null && !selection.IsEmpty)
            {
                // Применяем размер шрифта только к выделенному тексту
                ApplyStyleToSelection(fontSize: selectedSize);
            }
            else
            {
                // Применяем размер шрифта ко всему редактору
                if (CodeTextBox != null)
                {
                    CodeTextBox.FontSize = selectedSize;
                }
            }
        }
    }

    private void WordWrapCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        CodeTextBox.WordWrap = WordWrapCheckBox.IsChecked == true;
    }

    private void ThemeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        if (ThemeCheckBox.IsChecked == true)
        {
            ApplyTheme(AppTheme.Light);
        }
        else
        {
            ApplyTheme(AppTheme.Dark);
        }
    }

    private void ApplyTheme(AppTheme theme)
    {
        _currentTheme = theme;
        Theme.CurrentTheme = theme;

        if (theme == AppTheme.Dark)
        {
            // Обновляем динамические ресурсы
            this.Resources["DictSurface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202A38"));
            this.Resources["DictSurfaceStrong"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#273447"));
            this.Resources["DictBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#36465E"));
            this.Resources["DictCardBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202A38"));
            this.Resources["DictAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5BA5FF"));
            this.Resources["DictTextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F7"));
            this.Resources["DictTextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7B3C5"));

            // Основные фоны
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Background));
            ((Grid)this.Content).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Background));
            SidePanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.SidePanel));

            // Заголовок окна
            WindowTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));

            // Текст
           

            // Поля ввода
            SearchBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Input));
            SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));
            SearchBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));

            ThemeCheckBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));

            EntriesTreeView.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.SidePanel));
            EntriesTreeView.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));
            EntriesTreeView.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));

            TitleTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Input));
            TitleTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));
            TitleTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));

            DescriptionTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Input));
            DescriptionTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));
            DescriptionTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));

            TagsTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Input));
            TagsTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));
            TagsTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));

            WordWrapCheckBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));

            UpdateCodeEditorColors();
            SyntaxHighlightingComboBox?.Items.Clear();
            SyntaxHighlightingComboBox?.Items.Add("Темная (1C)");
            SyntaxHighlightingComboBox?.Items.Add("Темная С#");
            SyntaxHighlightingComboBox?.Items.Add("Темная C++");
            SyntaxHighlightingComboBox?.Items.Add("Темная (XML)");    
            SyntaxHighlightingComboBox?.Items.Add("Темная (HTML)");    

            // По умолчанию при темном режиме выбираем DarkCSharp или DarkCpp
            if (SyntaxHighlightingComboBox != null)
            {
                var selected = SyntaxHighlightingComboBox.SelectedItem?.ToString();
                if (selected == "LightCSharp" || selected == "Стандартная (C#)")
                {
                    SyntaxHighlightingComboBox.SelectedIndex = 0; // DarkCSharp
                }
                else if (selected == "LightCpp" || selected == "Стандартная (C++)")
                {
                    SyntaxHighlightingComboBox.SelectedIndex = 2; // DarkCpp
                }
                else if (string.IsNullOrEmpty(selected))
                {
                    SyntaxHighlightingComboBox.SelectedIndex = 0; // DarkCSharp
                }
            }
        }
        else
        {
            // Обновляем динамические ресурсы для светлой темы
            this.Resources["DictSurface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            this.Resources["DictSurfaceStrong"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
            this.Resources["DictBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D0D0"));
            this.Resources["DictCardBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            this.Resources["DictAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4"));
            this.Resources["DictTextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F1F"));
            this.Resources["DictTextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B6B6B"));

            // Основные фоны
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Background));
            ((Grid)this.Content).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Background));
            SidePanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.SidePanel));

            // Заголовок окна
            WindowTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));

            // Текст
           

            // Поля ввода
            SearchBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Input));
            SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));
            SearchBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));

            ThemeCheckBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));

            EntriesTreeView.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.SidePanel));
            EntriesTreeView.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));
            EntriesTreeView.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));

            TitleTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Input));
            TitleTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));
            TitleTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));

            DescriptionTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Input));
            DescriptionTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));
            DescriptionTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));

            TagsTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Input));
            TagsTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));
            TagsTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));

            WordWrapCheckBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));

             UpdateCodeEditorColors();

            SyntaxHighlightingComboBox?.Items.Clear();
           
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (1C)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (C#)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (C++)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (Java)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (JavaScript)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (HTML)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (XML)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (CSS)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (PHP)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (PowerShell)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (SQL)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (VB)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (ASP/XHTML)");
            SyntaxHighlightingComboBox?.Items.Add("Стандартная (Patch)");


            // По умолчанию при светлом режиме выбираем LightCSharp или LightCpp
            if (SyntaxHighlightingComboBox != null)
            {
                var selected = SyntaxHighlightingComboBox.SelectedItem?.ToString();
                if (selected == "DarkCSharp" || selected == "Стандартная (C#)")
                {
                    SyntaxHighlightingComboBox.SelectedIndex = 1; // LightCSharp
                }
                else if (selected == "DarkCpp" || selected == "Стандартная (C++)")
                {
                    SyntaxHighlightingComboBox.SelectedIndex = 3; // LightCpp
                }
                else if (string.IsNullOrEmpty(selected))
                {
                    SyntaxHighlightingComboBox.SelectedIndex = 1; // LightCSharp
                }
            }
        }
    }

    private void UpdateCodeEditorColors(string? selectedSyntax = null)
    {
        if (CodeTextBox == null) return;

        var syntax = selectedSyntax ?? SyntaxHighlightingComboBox?.SelectedItem?.ToString();

        if (syntax == "Стандартная (1C)")
        {
            // Для 1С фон всегда остается светлым
            CodeTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.CodeBackground));
            CodeTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.CodeForeground));
            CodeTextBox.LineNumbersForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextSecondary));
            CodeTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));
        }
        else
        {
            // Для остальных языков цвета зависят от темы
            if (_currentTheme == AppTheme.Dark)
            {
                CodeTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.CodeBackground));
                CodeTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.CodeForeground));
                CodeTextBox.LineNumbersForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextSecondary));
                CodeTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));
            }
            else
            {
                CodeTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.CodeBackground));
                CodeTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.CodeForeground));
                CodeTextBox.LineNumbersForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextSecondary));
                CodeTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));
            }
        }
    }

    private void RefreshCategoryFilter()
    {
        if (CategoryComboBox != null)
        {
            CategoryComboBox.ItemsSource = GetKnownCategoryPaths();
        }
    }

    private void RefreshEntriesList()
    {
        var searchText = SearchBox.Text.ToLower();

        // 1. Фильтруем записи по поисковому запросу
        _filteredEntries = _data.Entries
            .Where(e =>
            {
                return string.IsNullOrWhiteSpace(searchText) ||
                       searchText == "поиск..." ||
                       e.Title.ToLower().Contains(searchText) ||
                       e.Description.ToLower().Contains(searchText) ||
                       e.Code.ToLower().Contains(searchText) ||
                       e.Tags.Any(t => t.ToLower().Contains(searchText));
            })
            .OrderByDescending(e => e.ModifiedAt)
            .ToList();

        RefreshCategoryFilter();
        EntriesTreeView.ItemsSource = BuildCategoryTree(_filteredEntries);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitialized)
        {
            RefreshEntriesList();
        }
    }

    private void EntriesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_ignoreTreeSelectionChange)
        {
            _ignoreTreeSelectionChange = false;
            return;
        }

        if (e.NewValue is CodeEntryViewModel viewEntry)
        {
            return;
        }

        if (e.NewValue is CategoryNode categoryNode)
        {
            _selectedCategoryPath = categoryNode.FullPath;
            CategoryComboBox.Text = categoryNode.FullPath;
        }
    }

    private void EntriesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var treeViewItem = FindParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (treeViewItem?.DataContext is not CodeEntryViewModel viewEntry)
        {
            return;
        }

        LoadEntryToForm(viewEntry.Entry);
        e.Handled = true;
    }

    private void EntriesTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var treeViewItem = FindParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        _draggedTreeItem = treeViewItem?.DataContext;
        _dragStartPoint = e.GetPosition(null);
    }

    private void EntriesTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTreeItem == null)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        var delta = _dragStartPoint - currentPosition;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(EntriesTreeView, new DataObject("CodeDictionaryTreeItem", _draggedTreeItem), DragDropEffects.Move);
        _draggedTreeItem = null;
    }

    private void EntriesTreeView_DragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedItem(e, out var draggedItem))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var targetItem = GetItemUnderMouse(e.OriginalSource as DependencyObject);
        if (!TryGetDropTarget(targetItem, draggedItem, out _))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void EntriesTreeView_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedItem(e, out var draggedItem))
        {
            return;
        }

        var targetItem = GetItemUnderMouse(e.OriginalSource as DependencyObject);
        if (!TryGetDropTarget(targetItem, draggedItem, out var destinationPath))
        {
            return;
        }

        if (draggedItem is CategoryNode sourceCategory)
        {
            var destinationName = GetCategorySegmentName(sourceCategory.FullPath);
            await MoveCategoryAsync(sourceCategory.FullPath, destinationPath, destinationName);
            return;
        }

        if (draggedItem is CodeEntryViewModel sourceEntry)
        {
            await MoveEntryAsync(sourceEntry, destinationPath);
        }
    }

    private static bool TryGetDraggedItem(DragEventArgs e, out object? draggedItem)
    {
        draggedItem = null;
        if (!e.Data.GetDataPresent("CodeDictionaryTreeItem"))
        {
            return false;
        }

        draggedItem = e.Data.GetData("CodeDictionaryTreeItem");
        return draggedItem != null;
    }

    private object? GetItemUnderMouse(DependencyObject? origin)
    {
        var treeViewItem = FindParent<TreeViewItem>(origin);
        return treeViewItem?.DataContext;
    }

    private bool TryGetDropTarget(object? targetItem, object draggedItem, out string destinationPath)
    {
        destinationPath = string.Empty;

        if (targetItem is CategoryNode targetCategory)
        {
            destinationPath = targetCategory.FullPath;
        }
        else if (targetItem is CodeEntryViewModel targetEntry)
        {
            destinationPath = NormalizeCategoryPath(targetEntry.Entry.Category);
        }
        else
        {
            destinationPath = string.Empty;
        }

        if (draggedItem is CategoryNode draggedCategory)
        {
            var draggedPath = NormalizeCategoryPath(draggedCategory.FullPath);
            if (string.IsNullOrWhiteSpace(draggedPath))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(destinationPath) &&
                IsPathWithin(destinationPath, draggedPath))
            {
                return false;
            }
        }

        return true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void LoadEntryToForm(CodeEntry entry)
    {
        var entryTab = OpenEntryTab(entry);
        ActivateEditorTab(entryTab);
        _isUpdatingEditorContent = true;
        try
        {
            TitleTextBox.Text = entry.Title;
            DescriptionTextBox.Text = entry.Description;
            CategoryComboBox.Text = NormalizeCategoryPath(entry.Category);
            TagsTextBox.Text = string.Join(", ", entry.Tags);

            _selectedCategoryPath = NormalizeCategoryPath(entry.Category);
            RefreshCategoryFilter();

            if (!string.IsNullOrEmpty(entry.Syntax) && SyntaxHighlightingComboBox != null)
            {
                SyntaxHighlightingComboBox.SelectedItem = entry.Syntax;
            }

            ClearSearch();
        }
        finally
        {
            _isUpdatingEditorContent = false;
        }

        _currentEntry = entryTab.Entry;
        entryTab.MarkSaved();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        var newEntry = new CodeEntry();
        _data.Entries.Add(newEntry);
        _currentEntry = newEntry;
        LoadEntryToForm(newEntry);
        TitleTextBox.Focus();
    }

    private void ClearEditingForm()
    {
        ActivateEditorTab(_entryEditorTab);
        _isUpdatingEditorContent = true;
        try
        {
            TitleTextBox.Text = string.Empty;
            DescriptionTextBox.Text = string.Empty;
            CodeTextBox.Text = string.Empty;
            CategoryComboBox.Text = string.Empty;
            TagsTextBox.Text = string.Empty;
            _selectedCategoryPath = string.Empty;
        }
        finally
        {
            _isUpdatingEditorContent = false;
        }
    }

    private void NormalizeImportedData()
    {
        foreach (var entry in _data.Entries)
        {
            entry.Category = NormalizeCategoryPath(entry.Category);
        }

        var normalizedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in _data.Categories)
        {
            var normalizedCategory = NormalizeCategoryPath(category);
            if (string.IsNullOrWhiteSpace(normalizedCategory))
            {
                continue;
            }

            foreach (var ancestor in EnumerateCategoryAncestors(normalizedCategory))
            {
                normalizedCategories.Add(ancestor);
            }
        }

        foreach (var entry in _data.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Category))
            {
                foreach (var ancestor in EnumerateCategoryAncestors(entry.Category))
                {
                    normalizedCategories.Add(ancestor);
                }
            }
        }

        _data.Categories = normalizedCategories
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }


    private void SaveSegmentsToEntry()
    {
        // Создаем контейнер с данными
        var container = new FormattingContainer
        {
            Segments = _textSegments,
            Version = 1,
            SavedAt = DateTime.Now
        };

        // Настройки сериализации
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,  // Красивый формат JSON
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Поддержка Unicode
            DefaultIgnoreCondition = JsonIgnoreCondition.Never // Сохраняем все значения, включая null
        };

        // Сериализуем и сохраняем
        _currentEntry.FormattingData = JsonSerializer.Serialize(container, options);
    }

    // 📖 ЗАГРУЗКА СЕГМЕНТОВ ИЗ JSON
    private void LoadSegmentsFromEntry()
    {
        // Если данных нет - очищаем сегменты
        if (string.IsNullOrEmpty(_currentEntry.FormattingData))
        {
            _textSegments.Clear();
            return;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true  // Игнорируем регистр букв в JSON
            };

            // Десериализуем контейнер
            var container = JsonSerializer.Deserialize<FormattingContainer>(
                _currentEntry.FormattingData, options);

            if (container != null && container.Segments != null)
            {
                // Очищаем и добавляем элементы, чтобы сохранить ссылку на список
                _textSegments.Clear();
                _textSegments.AddRange(container.Segments);

                // Обновляем отображение в редакторе
                CodeTextBox.TextArea.TextView.Redraw();
            }
            else
            {
                _textSegments.Clear();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки форматирования: {ex.Message}");
            _textSegments.Clear();
        }
    }

    // Применение форматирования к редактору (если нужно)
    private void LoadSegmentsIntoTab(EditorTabModel tab, CodeEntry entry)
    {
        tab.Segments.Clear();

        if (string.IsNullOrEmpty(entry.FormattingData))
        {
            return;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var container = JsonSerializer.Deserialize<FormattingContainer>(entry.FormattingData, options);
            if (container?.Segments != null)
            {
                tab.Segments.AddRange(container.Segments);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки форматирования: {ex.Message}");
            tab.Segments.Clear();
        }
    }

    private void ApplySegmentsToEditor()
    {
        // Здесь ваша логика отрисовки сегментов
        // Например, перерисовка TextView
        CodeTextBox.TextArea.TextView.Redraw();
    }






private async void SaveEntry_Click(object sender, RoutedEventArgs e)
     {
         var activeTab = GetActiveEditorTab();

         // Если это вкладка файла без привязанной записи - создаем новую запись
         if (activeTab != null && activeTab.Entry == null && activeTab.IsFromFile)
         {
             var entry = new CodeEntry
             {
                 Title = Path.GetFileNameWithoutExtension(activeTab.FilePath),
                 Description = "",
                 Code = CodeTextBox.Text,
                 Category = NormalizeCategoryPath(CategoryComboBox.Text),
                 Tags = Array.Empty<string>().ToList(),
                 Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty
             };

             _data.Entries.Add(entry);
             activeTab.Entry = entry;
             activeTab.FilePath = null;
             activeTab.Title = entry.Title;
             activeTab.IsDirty = false;
             activeTab.NotifyHeaderChanged();
            TitleTextBox.Text = entry.Title;

            _currentEntry = entry;
             SaveSegmentsToEntry();
             await _dataService.SaveDataAsync(_data);
             RefreshEntriesList();
             _snackbar.Show(CodeTextBox, "Файл сохранен как запись", NotificationType.Success, 1.5);
             return;
         }

         var entryFromTab = activeTab?.Entry;

         if (entryFromTab == null)
         {
             ShowAlert("Выберите или создайте запись", isError: true);
             return;

         }

         if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
         {
             ShowAlert("Введите название", isError: true);
             return;
         }

       


         entryFromTab.Title = TitleTextBox.Text;
         entryFromTab.Description = DescriptionTextBox.Text;
         entryFromTab.Code = CodeTextBox.Text;
         entryFromTab.Category = NormalizeCategoryPath(CategoryComboBox.Text);
         entryFromTab.Tags = TagsTextBox.Text
             .Split(',')
             .Select(t => t.Trim())
             .Where(t => !string.IsNullOrWhiteSpace(t))
             .ToList();
         entryFromTab.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;
         entryFromTab.ModifiedAt = DateTime.Now;
         
         if (activeTab != null)
         {
             activeTab.Title = entryFromTab.Title;
             activeTab.SyntaxName = entryFromTab.Syntax;
             activeTab.IsDirty = false;
         }

         _currentEntry = entryFromTab;
         SaveSegmentsToEntry();

         EnsureCategoryPathExists(entryFromTab.Category);

         await _dataService.SaveDataAsync(_data);
         RefreshEntriesList();

         _snackbar.Show(CodeTextBox, "Запись сохранена", NotificationType.Success, 1.5);

      
    }




    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        var activeTab = GetActiveEditorTab();
        var entry = activeTab?.Entry;

        if (entry == null)
        {
            ShowAlert("Выберите запись для удаления", isError: true);
            return;
        }


            if (CustomMessageBox.ShowQuestion("Удалить запись?", "Подтверждение") )
            {
                _data.Entries.Remove(_currentEntry);
                await _dataService.SaveDataAsync(_data);
                _currentEntry = null;
                TitleTextBox.Text = "";
                DescriptionTextBox.Text = "";
                CodeTextBox.Text = "";
                CategoryComboBox.Text = "";
                TagsTextBox.Text = "";
                RefreshEntriesList();
                _snackbar.Show(CodeTextBox, "Запись удалена", NotificationType.Success, 1.5);
            }

        }
   

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Сохраняем текущее состояние
        _appState.SelectedCategory = !string.IsNullOrWhiteSpace(_selectedCategoryPath)
            ? _selectedCategoryPath
            : _currentEntry?.Category ?? "";
        _appState.SelectedEntryId = GetActiveEditorTab()?.Entry?.Id ?? _currentEntry?.Id;
        _appState.IsLightTheme = ThemeCheckBox.IsChecked == true;

        await _dataService.SaveStateAsync(_appState);
    }

    // Обработчики контекстного меню редактора кода
    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CodeTextBox.SelectedText))
        {
            Clipboard.SetText(CodeTextBox.SelectedText);
        }
    }

    private void CutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CodeTextBox.SelectedText))
        {
            Clipboard.SetText(CodeTextBox.SelectedText);
            CodeTextBox.Document.Remove(CodeTextBox.SelectionStart, CodeTextBox.SelectionLength);
        }
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (CodeTextBox.SelectionLength > 0)
            {
                CodeTextBox.Document.Replace(CodeTextBox.SelectionStart, CodeTextBox.SelectionLength, text);
            }
            else
            {
                CodeTextBox.Document.Insert(CodeTextBox.CaretOffset, text);
            }
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CodeTextBox.SelectionLength > 0)
        {
            CodeTextBox.Document.Remove(CodeTextBox.SelectionStart, CodeTextBox.SelectionLength);
        }
    }

    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CodeTextBox.SelectAll();
    }

    private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CodeTextBox.Undo();
    }

    private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CodeTextBox.Redo();
    }

    private void ToggleDescriptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DescriptionRow.Height.Value > 0)
        {
            DescriptionTextBox.Visibility = Visibility.Collapsed;
            DescriptionSplitter.Visibility = Visibility.Collapsed;
            DescriptionRow.Height = new GridLength(0);
            ToggleDescriptionButton.Content = " ▼ Развернуть ";
        }
        else
        {
            DescriptionTextBox.Visibility = Visibility.Visible;
            DescriptionSplitter.Visibility = Visibility.Visible;
            DescriptionRow.Height = new GridLength(150);
            ToggleDescriptionButton.Content = " ▲ Свернуть ";
        }
    }

    // Поиск в тексте
    private List<int> _searchResults = new List<int>();
    private int _currentSearchIndex = -1;

    private void SearchInTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PerformSearch();
    }

    private void SearchInTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift)
            {
                FindPrevious();
            }
            else
            {
                FindNext();
            }
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F3)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift)
            {
                FindPrevious();
            }
            else
            {
                FindNext();
            }
            e.Handled = true;
        }
    }

    private void SearchOptions_Changed(object sender, RoutedEventArgs e)
    {
        PerformSearch();
        if (_searchResults.Count > 0)
        {
            HighlightSearchResult();
        }
    }

    private void PerformSearch()
    {
        _searchResults.Clear();
        _currentSearchIndex = -1;

        string? searchText = SearchInTextBox?.Text;
        if (string.IsNullOrEmpty(searchText) || CodeTextBox == null)
        {
            if (SearchResultsTextBlock != null)
                SearchResultsTextBlock.Text = "";
            return;
        }

        string documentText = CodeTextBox.Text;
        bool matchCase = MatchCaseCheckBox?.IsChecked ?? false;

        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int index = 0;
        while ((index = documentText.IndexOf(searchText, index, comparison)) != -1)
        {
            _searchResults.Add(index);
            index += searchText.Length;
        }

        if (_searchResults.Count > 0)
        {
            _currentSearchIndex = 0;
            UpdateSearchResultsText();
        }
        else
        {
            if (SearchResultsTextBlock != null)
                SearchResultsTextBlock.Text = "Не найдено";
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        FindNext();
    }

    private void FindPrevious_Click(object sender, RoutedEventArgs e)
    {
        FindPrevious();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        ClearSearch();
    }

    private void SearchBox_TextChanged_Click(object sender, RoutedEventArgs e)
    {
        ClearSearchTextChanged();
    }

    private void FindNext()
    {
        if (_searchResults.Count == 0) return;

        _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.Count;
        HighlightSearchResult();
        UpdateSearchResultsText();
    }

    private void FindPrevious()
    {
        if (_searchResults.Count == 0) return;

        _currentSearchIndex--;
        if (_currentSearchIndex < 0)
            _currentSearchIndex = _searchResults.Count - 1;

        HighlightSearchResult();
        UpdateSearchResultsText();
    }

    private void HighlightSearchResult()
    {
        if (_currentSearchIndex < 0 || _currentSearchIndex >= _searchResults.Count)
            return;

        int offset = _searchResults[_currentSearchIndex];
        int length = SearchInTextBox.Text.Length;

        CodeTextBox.Select(offset, length);
        CodeTextBox.ScrollToLine(CodeTextBox.Document.GetLineByOffset(offset).LineNumber);
        CodeTextBox.Focus();
    }

    private void UpdateSearchResultsText()
    {
        if (SearchResultsTextBlock == null) return;

        if (_searchResults.Count > 0)
        {
            SearchResultsTextBlock.Text = $"{_currentSearchIndex + 1} из {_searchResults.Count}";
        }
        else
        {
            SearchResultsTextBlock.Text = "";
        }
    }

    private void ClearSearch()
    {
        if (SearchInTextBox != null)
            SearchInTextBox.Text = "";

        _searchResults.Clear();
        _currentSearchIndex = -1;

        if (SearchResultsTextBlock != null)
            SearchResultsTextBlock.Text = "";
    }

    private void ClearSearchTextChanged()
    {
        if (SearchBox != null)
            SearchBox.Text = "";
    }

    private void SelectEntryInTree(Guid entryId)
    {
        if (EntriesTreeView.ItemsSource is IEnumerable<CategoryNode> categories)
        {
            var viewEntry = FindEntryViewModel(categories, entryId);
            if (viewEntry != null)
            {
                _currentEntry = viewEntry.Entry;
                LoadEntryToForm(_currentEntry);
            }
        }
    }

    private async void ExportData_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Title = "Экспорт базы",
            Filter = "JSON (*.json)|*.json|XML (*.xml)|*.xml",
            FilterIndex = 1,
            //InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var exportPath = EnsureExportExtension(saveFileDialog.FileName, saveFileDialog.FilterIndex);
        try
        {
            await _dataService.ExportDataAsync(_data, exportPath);
            _snackbar.Show(CodeTextBox, $"База экспортирована в '{Path.GetFileName(exportPath)}'", NotificationType.Success, 1.5);
        }
        catch (Exception ex)
        {
            ShowAlert($"Ошибка экспорта: {ex.Message}", isError: true);
        }
    }

    private async void ImportData_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Импорт базы",
            Filter = "JSON (*.json)|*.json|XML (*.xml)|*.xml",
            FilterIndex = 1,
            //InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        if (CustomMessageBox.ShowQuestion("Импорт заменит текущую базу данных. Продолжить?", "Подтверждение импорта"))
        {
            try
            {
                var importedData = await _dataService.ImportDataAsync(openFileDialog.FileName);
                _data = importedData ?? new CodeDictionaryData();
                NormalizeImportedData();
                await _dataService.SaveDataAsync(_data);

                _currentEntry = null;
                _selectedCategoryPath = string.Empty;
                ClearEditingForm();
                RefreshEntriesList();
                _snackbar.Show(CodeTextBox, $"База импортирована из '{Path.GetFileName(openFileDialog.FileName)}'", NotificationType.Success, 1.5);
            }
            catch (Exception ex)
            {
                ShowAlert($"Ошибка импорта: {ex.Message}", isError: true);
            }

        }
        else 
        {
            return;
        }

    }

    private async void ExportCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not CategoryNode categoryNode)
        {
            return;
        }

        await ExportCategoryAsync(categoryNode.FullPath);
    }

    private async void ExportSelectedCategory_Click(object sender, RoutedEventArgs e)
    {
        var categoryPath = !string.IsNullOrWhiteSpace(_selectedCategoryPath)
            ? _selectedCategoryPath
            : NormalizeCategoryPath(_currentEntry?.Category);

        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            ShowAlert("Выберите категорию для экспорта", isError: true);
            return;
        }

        await ExportCategoryAsync(categoryPath);
    }

    private static string EnsureExportExtension(string fileName, int filterIndex)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return fileName;
        }

        return filterIndex == 2 ? $"{fileName}.xml" : $"{fileName}.json";
    }

    private async Task ExportCategoryAsync(string categoryPath)
    {
        var normalizedCategoryPath = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalizedCategoryPath))
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Title = $"Экспорт категории '{normalizedCategoryPath}'",
            Filter = "JSON (*.json)|*.json|XML (*.xml)|*.xml",
            FilterIndex = 1,
            //InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = SanitizeFileName(GetCategorySegmentName(normalizedCategoryPath))
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var exportPath = EnsureExportExtension(saveFileDialog.FileName, saveFileDialog.FilterIndex);
        var exportData = BuildCategoryExportData(normalizedCategoryPath);

        try
        {
            await _dataService.ExportDataAsync(exportData, exportPath);
            _snackbar.Show(CodeTextBox, $"Категория '{normalizedCategoryPath}' экспортирована", NotificationType.Success, 1.5);
        }
        catch (Exception ex)
        {
            ShowAlert($"Ошибка экспорта категории: {ex.Message}", isError: true);
        }
    }

    private CodeDictionaryData BuildCategoryExportData(string categoryPath)
    {
        var normalizedCategoryPath = NormalizeCategoryPath(categoryPath);
        var exportedEntries = _data.Entries
            .Where(entry => IsPathWithin(NormalizeCategoryPath(entry.Category), normalizedCategoryPath))
            .Select(CloneEntry)
            .ToList();

        var exportedCategories = _data.Categories
            .Select(NormalizeCategoryPath)
            .Where(path => IsPathWithin(path, normalizedCategoryPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entryCategory in exportedEntries.Select(entry => NormalizeCategoryPath(entry.Category)))
        {
            if (string.IsNullOrWhiteSpace(entryCategory))
            {
                continue;
            }

            foreach (var ancestor in EnumerateCategoryAncestors(entryCategory))
            {
                if (!exportedCategories.Any(path => string.Equals(path, ancestor, StringComparison.OrdinalIgnoreCase)))
                {
                    exportedCategories.Add(ancestor);
                }
            }
        }

        exportedCategories = exportedCategories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!exportedCategories.Any(path => string.Equals(path, normalizedCategoryPath, StringComparison.OrdinalIgnoreCase)))
        {
            exportedCategories.Insert(0, normalizedCategoryPath);
        }

        return new CodeDictionaryData
        {
            Entries = exportedEntries,
            Categories = exportedCategories
        };
    }

    private static CodeEntry CloneEntry(CodeEntry entry)
    {
        return new CodeEntry
        {
            Id = entry.Id,
            Title = entry.Title,
            Description = entry.Description,
            Code = entry.Code,
            Category = entry.Category,
            Tags = entry.Tags.ToList(),
            Syntax = entry.Syntax,
            CreatedAt = entry.CreatedAt,
            ModifiedAt = entry.ModifiedAt,
            FormattingData = entry.FormattingData
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "category" : sanitized;
    }

    private List<string> GetKnownCategoryPaths()
    {
        return _data.Categories
            .Concat(_data.Entries.Select(entry => entry.Category))
            .Select(NormalizeCategoryPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .SelectMany(EnumerateCategoryAncestors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void EnsureCategoryPathExists(string? categoryPath)
    {
        var normalizedPath = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        foreach (var ancestor in EnumerateCategoryAncestors(normalizedPath))
        {
            if (!_data.Categories.Any(existing => string.Equals(NormalizeCategoryPath(existing), ancestor, StringComparison.OrdinalIgnoreCase)))
            {
                _data.Categories.Add(ancestor);
            }
        }
    }

    private List<CategoryNode> BuildCategoryTree(IEnumerable<CodeEntry> entries)
    {
        var nodeLookup = new Dictionary<string, CategoryNode>(StringComparer.OrdinalIgnoreCase);

        CategoryNode GetOrCreateNode(string path)
        {
            var normalizedPath = NormalizeCategoryPath(path);
            if (nodeLookup.TryGetValue(normalizedPath, out var existingNode))
            {
                return existingNode;
            }

            var node = new CategoryNode
            {
                Name = string.IsNullOrWhiteSpace(normalizedPath)
                    ? UncategorizedCategoryName
                    : GetCategorySegmentName(normalizedPath),
                FullPath = normalizedPath
            };

            nodeLookup[normalizedPath] = node;

            var parentPath = GetParentCategoryPath(normalizedPath);
            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                GetOrCreateNode(parentPath).Children.Add(node);
            }

            return node;
        }

        foreach (var categoryPath in GetKnownCategoryPaths())
        {
            foreach (var ancestor in EnumerateCategoryAncestors(categoryPath))
            {
                GetOrCreateNode(ancestor);
            }
        }

        var uncategorizedNode = GetOrCreateNode(string.Empty);

        foreach (var entry in entries)
        {
            var normalizedCategory = NormalizeCategoryPath(entry.Category);
            var node = string.IsNullOrWhiteSpace(normalizedCategory)
                ? uncategorizedNode
                : GetOrCreateNode(normalizedCategory);

            node.Entries.Add(new CodeEntryViewModel(entry));
        }

        var roots = nodeLookup.Values
            .Where(node => string.IsNullOrWhiteSpace(GetParentCategoryPath(node.FullPath)))
            .OrderBy(node => node.FullPath == string.Empty ? 1 : 0)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SortCategoryNodes(roots);
        return roots;
    }

    private void SortCategoryNodes(IEnumerable<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.Children.Sort((left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            node.Entries.Sort((left, right) =>
                string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase));
            SortCategoryNodes(node.Children);
        }
    }

    private static IEnumerable<string> EnumerateCategoryAncestors(string categoryPath)
    {
        var normalized = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        var segments = normalized.Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;
        foreach (var segment in segments)
        {
            currentPath = string.IsNullOrWhiteSpace(currentPath)
                ? segment
                : $"{currentPath}{CategorySeparator}{segment}";
            yield return currentPath;
        }
    }

    private static string NormalizeCategoryPath(string? categoryPath)
    {
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            return string.Empty;
        }

        var segments = categoryPath
            .Replace('\\', CategorySeparator[0])
            .Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return string.Join(CategorySeparator, segments);
    }

    private static string GetCategorySegmentName(string categoryPath)
    {
        var normalized = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return UncategorizedCategoryName;
        }

        var lastSeparatorIndex = normalized.LastIndexOf(CategorySeparator, StringComparison.Ordinal);
        return lastSeparatorIndex >= 0
            ? normalized[(lastSeparatorIndex + 1)..]
            : normalized;
    }

    private static string GetParentCategoryPath(string categoryPath)
    {
        var normalized = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = normalized.LastIndexOf(CategorySeparator, StringComparison.Ordinal);
        return lastSeparatorIndex > 0
            ? normalized[..lastSeparatorIndex]
            : string.Empty;
    }

    private static CodeEntryViewModel? FindEntryViewModel(IEnumerable<CategoryNode> categories, Guid entryId)
    {
        foreach (var category in categories)
        {
            var directEntry = category.Entries.FirstOrDefault(entry => entry.Entry.Id == entryId);
            if (directEntry != null)
            {
                return directEntry;
            }

            var nestedEntry = FindEntryViewModel(category.Children, entryId);
            if (nestedEntry != null)
            {
                return nestedEntry;
            }
        }

        return null;
    }

    private async void AddCategoryRoot_Click(object sender, RoutedEventArgs e)
    {
        await AddCategoryAsync(string.Empty);
    }

    private async void AddCategoryChild_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not CategoryNode categoryNode)
        {
            return;
        }

        await AddCategoryAsync(categoryNode.FullPath);
    }

    private async Task AddCategoryAsync(string parentPath)
    {
        var dialog = new CategoryInputDialog(
            string.IsNullOrWhiteSpace(parentPath) ? "Новая категория" : $"Новая категория внутри '{parentPath}'",
            "Введите название категории:");
        dialog.Owner = this;

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var enteredName = NormalizeCategoryPath(dialog.CategoryName);
        if (string.IsNullOrWhiteSpace(enteredName))
        {
            return;
        }

        if (enteredName.Contains(CategorySeparator, StringComparison.Ordinal))
        {
            ShowAlert($"Имя категории не должно содержать '{CategorySeparator}'", isError: true);
            return;
        }

        var newCategoryPath = string.IsNullOrWhiteSpace(parentPath)
            ? enteredName
            : $"{NormalizeCategoryPath(parentPath)}{CategorySeparator}{enteredName}";

        if (_data.Categories.Any(existing => string.Equals(NormalizeCategoryPath(existing), newCategoryPath, StringComparison.OrdinalIgnoreCase)))
        {
            ShowAlert($"Категория '{newCategoryPath}' уже существует", isError: true);
            return;
        }

        EnsureCategoryPathExists(newCategoryPath);
        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();
        _snackbar.Show(CodeTextBox, $"Категория '{newCategoryPath}' добавлена", NotificationType.Success, 1.5);
    }

    private async void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not CategoryNode categoryNode)
        {
            return;
        }

        await RenameCategoryAsync(categoryNode);
    }

    private async Task RenameCategoryAsync(CategoryNode categoryNode)
    {
        var currentPath = NormalizeCategoryPath(categoryNode.FullPath);
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return;
        }

        var dialog = new CategoryInputDialog(
            "Переименовать категорию",
            $"Новое имя для '{currentPath}':",
            GetCategorySegmentName(currentPath));
        dialog.Owner = this;

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newName = NormalizeCategoryPath(dialog.CategoryName);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        if (newName.Contains(CategorySeparator, StringComparison.Ordinal))
        {
            ShowAlert($"Имя категории не должно содержать '{CategorySeparator}'", isError: true);
            return;
        }

        var parentPath = GetParentCategoryPath(currentPath);
        var newPath = string.IsNullOrWhiteSpace(parentPath)
            ? newName
            : $"{parentPath}{CategorySeparator}{newName}";

        await MoveCategoryAsync(currentPath, parentPath, newName);
        _snackbar.Show(CodeTextBox, $"Категория переименована в '{newPath}'", NotificationType.Success, 1.5);
    }

    private async Task MoveCategoryAsync(string sourcePath, string destinationParentPath, string newName)
    {
        var normalizedSourcePath = NormalizeCategoryPath(sourcePath);
        var normalizedDestinationParentPath = NormalizeCategoryPath(destinationParentPath);
        var normalizedNewName = NormalizeCategoryPath(newName);

        if (string.IsNullOrWhiteSpace(normalizedSourcePath) || string.IsNullOrWhiteSpace(normalizedNewName))
        {
            return;
        }

        var newPath = string.IsNullOrWhiteSpace(normalizedDestinationParentPath)
            ? normalizedNewName
            : $"{normalizedDestinationParentPath}{CategorySeparator}{normalizedNewName}";

        if (string.Equals(normalizedSourcePath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (newPath.StartsWith($"{normalizedSourcePath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
        {
            ShowAlert("Нельзя переместить категорию внутрь самой себя", isError: true);
            return;
        }

        if (IsCategoryPathTakenByAnotherNode(normalizedSourcePath, newPath))
        {
            ShowAlert($"Категория '{newPath}' уже существует", isError: true);
            return;
        }

        RemapCategoryPath(normalizedSourcePath, newPath);
        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();
        RestoreSelectionAfterCategoryMove(normalizedSourcePath, newPath);
    }

    private async Task MoveEntryAsync(CodeEntryViewModel entryViewModel, string destinationCategoryPath)
    {
        var normalizedDestination = NormalizeCategoryPath(destinationCategoryPath);
        var entry = entryViewModel.Entry;
        var normalizedSource = NormalizeCategoryPath(entry.Category);

        if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        entry.Category = normalizedDestination;
        EnsureCategoryPathExists(normalizedDestination);
        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();
        _currentEntry = entry;
        LoadEntryToForm(entry);
    }

    private static bool IsPathWithin(string path, string parentPath)
    {
        var normalizedPath = NormalizeCategoryPath(path);
        var normalizedParentPath = NormalizeCategoryPath(parentPath);

        if (string.IsNullOrWhiteSpace(normalizedParentPath))
        {
            return false;
        }

        return string.Equals(normalizedPath, normalizedParentPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith($"{normalizedParentPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCategoryPathTakenByAnotherNode(string sourcePath, string newPath)
    {
        return _data.Categories.Any(existing =>
        {
            var normalizedExisting = NormalizeCategoryPath(existing);
            if (string.IsNullOrWhiteSpace(normalizedExisting))
            {
                return false;
            }

            if (IsPathWithin(normalizedExisting, sourcePath))
            {
                return false;
            }

            return string.Equals(normalizedExisting, newPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void RemapCategoryPath(string sourcePath, string newPath)
    {
        var updatedCategoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in _data.Categories)
        {
            var normalizedExisting = NormalizeCategoryPath(existing);
            if (IsPathWithin(normalizedExisting, sourcePath))
            {
                updatedCategoryPaths.Add(ReplaceCategoryPrefix(normalizedExisting, sourcePath, newPath));
            }
            else if (!string.IsNullOrWhiteSpace(normalizedExisting))
            {
                updatedCategoryPaths.Add(normalizedExisting);
            }
        }

        foreach (var entry in _data.Entries)
        {
            var normalizedEntryCategory = NormalizeCategoryPath(entry.Category);
            if (IsPathWithin(normalizedEntryCategory, sourcePath))
            {
                entry.Category = ReplaceCategoryPrefix(normalizedEntryCategory, sourcePath, newPath);
            }
        }

        _data.Categories = updatedCategoryPaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_currentEntry != null)
        {
            _currentEntry.Category = IsPathWithin(_currentEntry.Category, sourcePath)
                ? ReplaceCategoryPrefix(_currentEntry.Category, sourcePath, newPath)
                : NormalizeCategoryPath(_currentEntry.Category);
        }

        if (IsPathWithin(_selectedCategoryPath, sourcePath))
        {
            _selectedCategoryPath = ReplaceCategoryPrefix(_selectedCategoryPath, sourcePath, newPath);
            CategoryComboBox.Text = _selectedCategoryPath;
        }
    }

    private static string ReplaceCategoryPrefix(string path, string sourcePath, string newPath)
    {
        var normalizedPath = NormalizeCategoryPath(path);
        var normalizedSource = NormalizeCategoryPath(sourcePath);
        var normalizedNew = NormalizeCategoryPath(newPath);

        if (string.IsNullOrWhiteSpace(normalizedSource))
        {
            return normalizedPath;
        }

        if (string.Equals(normalizedPath, normalizedSource, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedNew;
        }

        var suffix = normalizedPath.Substring(normalizedSource.Length);
        if (suffix.StartsWith(CategorySeparator, StringComparison.Ordinal))
        {
            suffix = suffix.Substring(CategorySeparator.Length);
        }

        return string.IsNullOrWhiteSpace(suffix)
            ? normalizedNew
            : $"{normalizedNew}{CategorySeparator}{suffix}";
    }

    private void RestoreSelectionAfterCategoryMove(string oldPath, string newPath)
    {
        if (string.Equals(_selectedCategoryPath, oldPath, StringComparison.OrdinalIgnoreCase) ||
            _selectedCategoryPath.StartsWith($"{oldPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
        {
            _selectedCategoryPath = ReplaceCategoryPrefix(_selectedCategoryPath, oldPath, newPath);
            CategoryComboBox.Text = _selectedCategoryPath;
        }

        if (_currentEntry != null && IsPathWithin(_currentEntry.Category, oldPath))
        {
            LoadEntryToForm(_currentEntry);
        }
    }

    private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not CategoryNode categoryNode)
        {
            return;
        }

        await DeleteCategoryAsync(categoryNode);
    }

    private async Task DeleteCategoryAsync(CategoryNode categoryNode)
    {
        var categoryPath = NormalizeCategoryPath(categoryNode.FullPath);
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            ShowAlert("Нельзя удалить корневой узел без категории", isError: true);
            return;
        }

        var entriesToDelete = _data.Entries
            .Where(entry => IsPathWithin(NormalizeCategoryPath(entry.Category), categoryPath))
            .ToList();

        var categoriesToDelete = _data.Categories
            .Where(category => IsPathWithin(NormalizeCategoryPath(category), categoryPath))
            .ToList();


        if (CustomMessageBox.ShowQuestion($"Удалить категорию '{categoryPath}' и все вложенные категории?\n\nБудет удалено категорий: {categoriesToDelete.Count}\nБудет удалено записей: {entriesToDelete.Count}", "Подтверждение"))

        {
            var deletedEntryIds = entriesToDelete.Select(entry => entry.Id).ToHashSet();
            var categoriesToDeleteSet = categoriesToDelete
                .Select(NormalizeCategoryPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _data.Categories.RemoveAll(existing => categoriesToDeleteSet.Contains(NormalizeCategoryPath(existing)));
            _data.Entries.RemoveAll(entry => deletedEntryIds.Contains(entry.Id));

            if (string.Equals(_selectedCategoryPath, categoryPath, StringComparison.OrdinalIgnoreCase) ||
                _selectedCategoryPath.StartsWith($"{categoryPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
            {
                _selectedCategoryPath = string.Empty;
                CategoryComboBox.Text = string.Empty;
            }

            if (_currentEntry != null && deletedEntryIds.Contains(_currentEntry.Id))
            {
                _currentEntry = null;
                ClearEditingForm();
            }
            else if (_currentEntry != null && IsPathWithin(_currentEntry.Category, categoryPath))
            {
                _currentEntry = null;
                ClearEditingForm();
            }

            await _dataService.SaveDataAsync(_data);
            RefreshEntriesList();
            _snackbar.Show(CodeTextBox, $"Категория '{categoryPath}' и её содержимое удалены", NotificationType.Success, 1.5);

        }
        else 
        {
            return;
        }
    }
}

public class CategoryNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public List<CategoryNode> Children { get; set; } = new();
    public List<CodeEntryViewModel> Entries { get; set; } = new();
    public IEnumerable<object> Items => Children.Cast<object>().Concat(Entries);
    public int TotalEntryCount => Entries.Count + Children.Sum(child => child.TotalEntryCount);
    public string CountString => $"({TotalEntryCount})";
    public bool CanDelete => !string.IsNullOrWhiteSpace(FullPath);
}

public class CodeEntryViewModel
{
    public CodeEntry Entry { get; }
    public string Title => Entry.Title;

    public CodeEntryViewModel(CodeEntry entry)
    {
        Entry = entry;
    }
}
