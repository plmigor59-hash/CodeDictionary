using CodeDictionary.Analysis;
using CodeDictionary.Models;
using CodeDictionary.Properties;
using CodeDictionary.Services;
using CodeDictionary.SyntaxChecking;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
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
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Xml;




namespace CodeDictionary;

public partial class MainWindow : Window
{
    private const string CategorySeparator = "/";
    private const string UncategorizedCategoryName = "Без категории";
    private readonly DataService _dataService;
    private readonly MainViewModel _viewModel;
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
    private IHighlightingDefinition? _darkPythonHigh;
    private IHighlightingDefinition? _lightPythonHigh;
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
    private FoldingManager _foldingManager;
    private BraceFoldingStrategy _foldingStrategy;
    private BslFoldingStrategy _bslFoldingStrategy;
    private BookmarkBackgroundRenderer _bookmarkRenderer;
    private BookmarkMargin _bookmarkMargin;
    private bool _updateDescription;
    private bool _isNewRecordDescription;

    /// <summary>
    /// /Syntax
    private readonly IOneScriptAnalysisService _analyzer = new CodeAnalyzer();
    private ToolTip? _hoverToolTip;
    private CompletionWindow? _completionWindow;
    private CancellationTokenSource _parseCts = new CancellationTokenSource();
    private readonly DispatcherTimer _debounceTimer;
    private SyntaxErrorColorizer? _colorizer;
    private TextMarkerService? _markerService;
    /// 
    /// </summary>



    private readonly RoslynCompletionService _roslynCompletionService;

    private HashSet<string> _expandedCategories = new();

    private void SaveExpansionState()
    {
        _expandedCategories.Clear();
        if (EntriesTreeView.ItemsSource is IEnumerable<CategoryNode> nodes)
        {
            SaveExpansionStateRecursive(nodes);
        }
    }

    private void CodeTextBox_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (!char.IsLetterOrDigit(e.Text[0]))
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }

        if (e.Text == ".")
        {
            var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
            if (syntax != null && syntax.Contains("C#"))
            {
                // Задержка необходима, чтобы символ '.' был добавлен в документ
                // перед тем, как Roslyn проанализирует контекст
                Dispatcher.BeginInvoke(new Action(() => ShowCompletion()));
            }
        }
    }

    private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
            if (syntax != null && syntax.Contains("C#"))
            {
                ShowCompletion();
                e.Handled = true;
            }

            if (syntax != null && syntax.Contains("1C"))
            {
                e.Handled = true;
                ShowCompletion1C(string.Empty, true);
            }


        }
    }

    private void AddStandardSymbols(System.Collections.Generic.IList<ICompletionData> data)
    {
        string[] keywords = { 
                // Русский вариант
                "Процедура", "Функция", "КонецПроцедуры", "КонецФункции",
                "Если", "Тогда", "Иначе", "ИначеЕсли", "КонецЕсли",
                "Для", "Каждого", "Из", "По", "Цикл", "КонецЦикла",
                "Пока", "Прервать", "Продолжить", "Возврат",
                "Попытка", "Исключение", "КонецПопытки", "ВызватьИсключение",
                "Перем", "Знач", "Экспорт", "Истина", "Ложь", "Неопределено", "Null",
                "Новый", "Перейти", "КонецПротокола", "Выполнить",

                // Английский вариант (синонимы)
                "Procedure", "Function", "EndProcedure", "EndFunction",
                "If", "Then", "Else", "ElsIf", "EndIf",
                "For", "Each", "In", "To", "Do", "EndDo",
                "While", "Break", "Continue", "Return",
                "Try", "Except", "EndTry", "Raise",
                "Var", "Val", "Export", "True", "False", "Undefined",
                "New", "And", "Or", "Not"
            };

        foreach (var kw in keywords)
        {
            if (!data.Any(d => d.Text.Equals(kw, StringComparison.OrdinalIgnoreCase)))
            {
                data.Add(new BslCompletionData(kw, "Ключевое слово", "Keyword"));
            }
        }
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '.';
    }

    private string GetWordAtOffset(int offset)
    {
        var document = CodeTextBox.Document;
        if (document.TextLength == 0) return string.Empty;

        if (offset >= document.TextLength)
            offset = document.TextLength - 1;

        if (!IsIdentifierChar(document.GetCharAt(offset)) && offset > 0 && IsIdentifierChar(document.GetCharAt(offset - 1)))
            offset--;

        if (!IsIdentifierChar(document.GetCharAt(offset)))
            return string.Empty;

        int start = offset;
        while (start > 0 && IsIdentifierChar(document.GetCharAt(start - 1)))
            start--;

        int end = offset;
        while (end < document.TextLength - 1 && IsIdentifierChar(document.GetCharAt(end + 1)))
            end++;

        return document.GetText(start, end - start + 1);
    }


    private void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
     {
         // Добавьте эту строку для отладки
         System.Diagnostics.Debug.WriteLine($"OnTextEntered: {e.Text}");
    
         if (e.Text.Length > 0 && (char.IsLetter(e.Text[0]) || e.Text[0] == '_' || e.Text[0] == '.'))
         {
            ShowCompletion1C(e.Text, false);
         }
    }


    private void ShowCompletion1C(string enteredText, bool controlSpace)
    {
        // Сначала собираем все возможные данные
        var allData = new List<ICompletionData>();

        // Добавляем символы из анализатора
        if (_analyzer is CodeAnalyzer coreAnalyzer)
        {
            foreach (var symbol in coreAnalyzer.Symbols)
            {
                if (!allData.Any(d => d.Text == symbol.Name))
                {
                    allData.Add(new BslCompletionData(symbol.Name, symbol.Name, symbol.Type));
                }
            }
        }

        // Добавляем стандартные ключевые слова BSL
        AddStandardSymbols(allData);

        // Если это автоматический вызов (не через Ctrl+Space), фильтруем список
        var filteredList = allData.ToList();
        if (!controlSpace && !string.IsNullOrEmpty(enteredText))
        {
            // Получаем слово целиком до курсора для более точной фильтрации
            string currentWord = GetWordAtOffset(CodeTextBox.CaretOffset);
            if (!string.IsNullOrEmpty(currentWord))
            {
                filteredList = allData.Where(d => d.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        if (filteredList.Any())
        {
            if (_completionWindow == null)
            {
                _completionWindow = new CompletionWindow(CodeTextBox.TextArea);
                
                // Применяем цвета темы при создании окна
                var background = Application.Current.TryFindResource("WindowBackground") as Brush ?? Brushes.White;
                var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
                var border = Application.Current.TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;
                
                _completionWindow.Background = background;
                _completionWindow.Foreground = foreground;
                _completionWindow.BorderBrush = border;
                
                if (_completionWindow.CompletionList != null)
                {
                    _completionWindow.CompletionList.Background = background;
                    _completionWindow.CompletionList.Foreground = foreground;
                }

                _completionWindow.Closed += delegate { _completionWindow = null; };
            }
            
            var data = _completionWindow.CompletionList.CompletionData;

            // Очищаем существующие данные, так как мы будем добавлять новые
            data.Clear();

            foreach (var item in filteredList.OrderBy(d => d.Text))
            {
                data.Add(item);
            }

            if (_completionWindow.Visibility != Visibility.Visible)
            {
                _completionWindow.Show();
            }

            // В AvalonEdit CompletionList сам подсветит лучшее совпадение при вводе, 
            // но для надежности укажем текущий префикс
            if (!controlSpace && !string.IsNullOrEmpty(enteredText))
            {
                _completionWindow.CompletionList.SelectItem(enteredText);
            }
        }
        else
        {
            // Если совпадений нет, закрываем окно
            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
            }
        }
    }


    private async void ShowCompletion()
    {
        var code = CodeTextBox.Text;
        var position = CodeTextBox.CaretOffset;
        var items = await _roslynCompletionService.GetCompletionItemsAsync(code, position);

        if (items.Any())
        {
            _completionWindow = new CompletionWindow(CodeTextBox.TextArea);

            // Применяем цвета темы к окну автодополнения
            var background = Application.Current.TryFindResource("WindowBackground") as Brush ?? Brushes.White;
            var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
            var border = Application.Current.TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;

            _completionWindow.Background = background;
            _completionWindow.Foreground = foreground;
            _completionWindow.BorderBrush = border;

            // Установка цветов для самого списка внутри окна
            if (_completionWindow.CompletionList != null)
            {
                _completionWindow.CompletionList.Background = background;
                _completionWindow.CompletionList.Foreground = foreground;
            }

            foreach (var item in items)
            {
                _completionWindow?.CompletionList?.CompletionData.Add(new RoslynCompletionData(item, _roslynCompletionService, code));
            }
            _completionWindow?.Closed += (s, e) => _completionWindow = null;
            _completionWindow?.Show();
        }
    }

    private void SaveExpansionStateRecursive(IEnumerable<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded)
            {
                _expandedCategories.Add(node.FullPath);
                SaveExpansionStateRecursive(node.Children);
            }
        }
    }

    private void ApplyExpansionState(IEnumerable<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (_expandedCategories.Contains(node.FullPath))
            {
                node.IsExpanded = true;
            }
            ApplyExpansionState(node.Children);
        }
    }

    private object? GetNeighborData(object item)
    {
        if (item == null) return null;

        IEnumerable<CategoryNode> roots = EntriesTreeView.ItemsSource as IEnumerable<CategoryNode> ?? new List<CategoryNode>();

        // Helper to find parent and index
        (object? parent, int index, IList<object>? siblings) FindInNodes(IEnumerable<object> nodes, object target)
        {
            var list = nodes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return (null, i, list);

                if (list[i] is CategoryNode cat)
                {
                    var result = FindInNodes(cat.Items, target);
                    if (result.siblings != null)
                    {
                        return (result.parent ?? cat, result.index, result.siblings);
                    }
                }
            }
            return (null, -1, null);
        }

        var (parent, index, siblings) = FindInNodes(roots, item);
        if (siblings == null) return null;

        object? neighbor = null;
        if (siblings.Count > 1)
        {
            if (index + 1 < siblings.Count) neighbor = siblings[index + 1];
            else if (index - 1 >= 0) neighbor = siblings[index - 1];
        }

        if (neighbor == null) neighbor = parent;

        if (neighbor is CodeEntryViewModel evm) return evm.Entry;
        if (neighbor is CategoryNode cn) return cn.FullPath;
        return neighbor;
    }


    public MainWindow()


    {
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.ReplaceSelectedText = (translatedText) =>
        {
            if (CodeTextBox.SelectionLength > 0)
            {
                int start = CodeTextBox.SelectionStart;
                CodeTextBox.Document.Replace(start, CodeTextBox.SelectionLength, translatedText);
                CodeTextBox.Select(start, translatedText.Length);
            }
        };


        // Загружаем кастомную тему подсветки для тёмного и светлого режимов

        _dataService = new DataService();
        _appState = _dataService.LoadState();

        InitializeComponent();
        LoadWindowState();

        // Применяем тему СРАЗУ после создания компонентов, до отображения окна
        ApplyTheme(_appState.IsLightTheme ? AppTheme.Light : AppTheme.Dark);
        ThemeCheckBox.IsChecked = _appState.IsLightTheme;

        _data = new CodeDictionaryData();
        _filteredEntries = new List<CodeEntry>();
        _isInitialized = false;

        _currentTheme = _appState.IsLightTheme ? AppTheme.Light : AppTheme.Dark;
        _isNewRecordDescription = false;


        _roslynCompletionService = new RoslynCompletionService();

        LoadCustomHighlighting();

        CodeTextBox.TextArea.TextView.LineTransformers.Add(new CustomColorTransformer(() => _textSegments));
        

        _foldingManager = FoldingManager.Install(CodeTextBox.TextArea);
        _foldingStrategy = new BraceFoldingStrategy();
        _bslFoldingStrategy = new BslFoldingStrategy();

        InitializeEditorTabs();

        // Инициализация компонентов для 1С (SyntaxChecking)
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;
        

        CodeTextBox.TextArea.TextView.MouseHover += OnTextViewMouseHover;
        CodeTextBox.TextArea.TextView.MouseHoverStopped += OnTextViewMouseHoverStopped;
        CodeTextBox.TextArea.TextEntered += OnTextEntered;

        // Инициализируем список шрифтов и настроек подсветки
        InitializeFontSettings();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        // Подписываемся на изменения текста
        CodeTextBox.TextChanged += CodeTextBox_TextChanged;
        CodeTextBox.TextArea.SelectionChanged += (s, e) =>
        {
            _viewModel.SelectedText = CodeTextBox.SelectedText;
            // Если выделение изменилось, сбрасываем оригинальный текст, 
            // чтобы не восстановить старый оригинал в новое место
            if (_viewModel.IsTranslating == false)
            {
                _viewModel.OriginalText = null;
            }
        };
        TitleTextBox.TextChanged += EntryField_TextChanged;
        TagsTextBox.TextChanged += EntryField_TextChanged;

        CodeTextBox.TextArea.TextEntering += CodeTextBox_TextEntering;
        CodeTextBox.TextArea.TextEntered += OnTextEntered; // Subscribe to TextEntered
        CodeTextBox.TextArea.KeyDown += CodeTextBox_KeyDown;
      

        // Bookmark margin handler
        _bookmarkMargin = new BookmarkMargin(
            () => GetActiveEditorTab()?.Bookmarks ?? new HashSet<int>(),
            () => (Brush)Application.Current.TryFindResource("AccentBrush") ?? Brushes.Blue
        );
        CodeTextBox.TextArea.LeftMargins.Insert(0, _bookmarkMargin);
        _bookmarkMargin.MouseDown += Margin_MouseDown;

        CodeTextBox.TextArea.Loaded += (s, e) =>
        {
            var margin = CodeTextBox.TextArea.LeftMargins.OfType<ICSharpCode.AvalonEdit.Editing.LineNumberMargin>().FirstOrDefault();
            if (margin != null)
            {
                margin.MouseDown += Margin_MouseDown;
            }
        };

        // Add hotkeys
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(ToggleBookmarkAtCaret), new KeyGesture(Key.F2, ModifierKeys.Alt)));
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(GoToNextBookmark), new KeyGesture(Key.Down, ModifierKeys.Control | ModifierKeys.Alt)));
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(GoToPreviousBookmark), new KeyGesture(Key.Up, ModifierKeys.Control | ModifierKeys.Alt)));
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(ShowBookmarkList), new KeyGesture(Key.F3)));

        // Help and Global shortcuts
        this.InputBindings.Add(new InputBinding(new RelayCommand(ToggleHelp), new KeyGesture(Key.F1)));
        this.InputBindings.Add(new InputBinding(new RelayCommand(CloseHelp), new KeyGesture(Key.Escape)));
        this.InputBindings.Add(new InputBinding(new RelayCommand(() => SaveFile_Click(null, null)), new KeyGesture(Key.S, ModifierKeys.Control)));
        this.InputBindings.Add(new InputBinding(new RelayCommand(() => OpenFile_Click(null, null)), new KeyGesture(Key.O, ModifierKeys.Control)));
        this.InputBindings.Add(new InputBinding(new RelayCommand(() => AddEntry_Click(null, null)), new KeyGesture(Key.N, ModifierKeys.Control)));
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(ShowGoToLineWindow), new KeyGesture(Key.G, ModifierKeys.Control)));
    }

    private void ShowGoToLineWindow()
    {
        var dialog = new GoToLineWindow(CodeTextBox.TextArea.Caret.Line, CodeTextBox.Document.LineCount, NavigateToLine);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void ToggleHelp()
    {
        if (HelpPanel.Visibility == Visibility.Collapsed)
        {
            HelpColumn.Width = new GridLength(300);
            HelpSplitter.Visibility = Visibility.Visible;
            HelpPanel.Visibility = Visibility.Visible;
        }
        else
        {
            CloseHelp();
        }
    }

    private void CloseHelp()
    {
        HelpColumn.Width = new GridLength(0);
        HelpSplitter.Visibility = Visibility.Collapsed;
        HelpPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowBookmarkList()
    {
        var tab = GetActiveEditorTab();
        if (tab == null || tab.Bookmarks.Count == 0) return;

        var window = new BookmarkListWindow(tab, NavigateToLine);
        window.Owner = this;
        window.ShowDialog();
    }

    private void NavigateToLine(int lineNumber)
    {
        CodeTextBox.ScrollToLine(lineNumber);
        CodeTextBox.TextArea.Caret.Line = lineNumber;
        CodeTextBox.TextArea.Caret.BringCaretToView();
        CodeTextBox.Focus();
    }

    private void GoToNextBookmark()
    {
        var tab = GetActiveEditorTab();
        if (tab == null || tab.Bookmarks.Count == 0) return;

        int currentLine = CodeTextBox.TextArea.Caret.Line;
        var nextLine = tab.Bookmarks
            .Where(b => b > currentLine)
            .OrderBy(b => b)
            .FirstOrDefault();

        if (nextLine == 0) // Wrap around
            nextLine = tab.Bookmarks.OrderBy(b => b).FirstOrDefault();

        if (nextLine != 0)
        {
            CodeTextBox.ScrollToLine(nextLine);
            CodeTextBox.TextArea.Caret.Line = nextLine;
            CodeTextBox.TextArea.Caret.BringCaretToView();
        }
    }

    private void GoToPreviousBookmark()
    {
        var tab = GetActiveEditorTab();
        if (tab == null || tab.Bookmarks.Count == 0) return;

        int currentLine = CodeTextBox.TextArea.Caret.Line;
        var prevLine = tab.Bookmarks
            .Where(b => b < currentLine)
            .OrderByDescending(b => b)
            .FirstOrDefault();

        if (prevLine == 0) // Wrap around
            prevLine = tab.Bookmarks.OrderByDescending(b => b).FirstOrDefault();

        if (prevLine != 0)
        {
            CodeTextBox.ScrollToLine(prevLine);
            CodeTextBox.TextArea.Caret.Line = prevLine;
            CodeTextBox.TextArea.Caret.BringCaretToView();
        }
    }

    private void ToggleBookmarkAtCaret()
    {
        // Ensure we get the correct document line number from the caret
        int lineNumber = CodeTextBox.TextArea.Caret.Line;
        ToggleBookmark(lineNumber);
    }

    private void ToggleBookmark(int lineNumber)
    {
        var tab = GetActiveEditorTab();
        if (tab != null)
        {
            if (tab.Bookmarks.Contains(lineNumber))
            {
                tab.Bookmarks.Remove(lineNumber);
            }
            else
            {
                tab.Bookmarks.Add(lineNumber);
            }
            CodeTextBox.TextArea.TextView.Redraw();
            _bookmarkMargin?.Redraw();
        }
    }

    private void Margin_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ICSharpCode.AvalonEdit.Editing.AbstractMargin margin) return;
        var textView = margin.TextView;
        if (textView == null) return;

        // Get the position relative to the TextView to account for scrolling
        var pos = e.GetPosition(textView);

        // Adjust Y position by the vertical offset to get the correct visual line
        var visualLine = textView.GetVisualLineFromVisualTop(pos.Y + textView.VerticalOffset);

        if (visualLine != null)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;

            if (e.ChangedButton == MouseButton.Left)
            {
                ToggleBookmark(lineNumber);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                var contextMenu = new ContextMenu();
                var tab = GetActiveEditorTab();
                bool isBookmarked = tab?.Bookmarks.Contains(lineNumber) ?? false;

                var menuItem = new MenuItem { Header = isBookmarked ? "Удалить закладку" : "Добавить закладку" };
                menuItem.Click += (s, args) => ToggleBookmark(lineNumber);
                contextMenu.Items.Add(menuItem);

                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }
    }



    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        OpenEditorTab(file);
                    }
                }
            }
        }
    }

    private void DescriptionBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        // Отменяем переход по любой ссылке
        e.Cancel = true;
    }



    private void UpdateFoldings()
    {
        if (_foldingManager != null && CodeTextBox.Document != null)
        {
            var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
            if (syntax != null && (syntax.Contains("1C") || syntax.Contains("BSL")))
            {
                _bslFoldingStrategy?.UpdateFoldings(_foldingManager, CodeTextBox.Document);
            }
            else if (_foldingStrategy != null)
            {
                _foldingStrategy.UpdateFoldings(_foldingManager, CodeTextBox.Document);
            }
        }
    }

    private void InitializeEditorTabs()
    {
        _editorTabs.Clear();



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


        MarkTabTextDirty();
        SyncActiveEntryTabFromForm();
        UpdateSegmentsAfterTextChange();
        UpdateFoldings();

        if (Analysis1CToggle.IsChecked == true)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
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
        // Описание теперь только отображается через WebBrowser и не редактируется напрямую в UI
        entry.Code = CodeTextBox.Text;
        entry.Category = NormalizeCategoryPath(CategoryComboBox.Text);
        entry.Tags = TagsTextBox.Text
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        entry.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;

        // Sync bookmarks
        entry.Bookmarks = activeTab.Bookmarks.ToList();

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
        _isNewRecordDescription = false;

        ActivateEditorTab(tab, selectInTabControl: false);
    }

    private EditorTabModel? GetActiveEditorTab()
    {
        //FindAndFocusItem(_activeEditorTab?.Title ?? string.Empty);
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
            ClearEditingForm();
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
        ToggleDescription_Close();
        _isSwitchingEditorTab = true;
        _isUpdatingEditorContent = true;
        try
        {
            _activeEditorTab = tab;
            _textSegments = tab.Segments;

            if (CodeTextBox.Document != tab.Document)
            {
                if (_foldingManager != null)
                {
                    FoldingManager.Uninstall(_foldingManager);
                }

                // Remove old renderer
                if (_bookmarkRenderer != null)
                {
                    CodeTextBox.TextArea.TextView.BackgroundRenderers.Remove(_bookmarkRenderer);
                }

                CodeTextBox.Document = tab.Document;
                _foldingManager = FoldingManager.Install(CodeTextBox.TextArea);

                // Add new renderer
                _bookmarkRenderer = new BookmarkBackgroundRenderer(CodeTextBox.TextArea.TextView, () => tab.Bookmarks.ToList());
                CodeTextBox.TextArea.TextView.BackgroundRenderers.Add(_bookmarkRenderer);

                // Update syntax checking services for the new document
                InitializeSyntaxServices();
            }

            CodeTextBox.TextArea.TextView.Redraw();
            _currentFilePath = tab.FilePath;
            this.Title = !string.IsNullOrWhiteSpace(tab.FilePath)
                ? $"{Path.GetFileName(tab.FilePath)} "
                : $"{tab.Title} ";

            _suppressSyntaxSelectionChange = true;

            if (tab.Entry != null)
            {
                TitleTextBox.Text = tab.Entry.Title;
                SetDescriptionHtml(tab.Entry.Description);
                CategoryComboBox.Text = NormalizeCategoryPath(tab.Entry.Category);
                TagsTextBox.Text = string.Join(", ", tab.Entry.Tags);
                _currentEntry = tab.Entry;
                _selectedCategoryPath = NormalizeCategoryPath(tab.Entry.Category);
                ToggleDescription_Close();
            }
            else
            {
                TitleTextBox.Text = tab.Title;
                if (tab.IsFromFile && tab.FilePath != null && Path.GetExtension(tab.FilePath).ToLower() == ".html")
                {
                    SetDescriptionHtml(tab.Document.Text);
                    ToggleDescription_Open();
                }
                else
                {
                    SetDescriptionHtml(string.Empty);
                }
                CategoryComboBox.Text = string.Empty;
                TagsTextBox.Text = string.Empty;
                _currentEntry = null;
                _selectedCategoryPath = string.Empty;
            }

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

            if (selectInTabControl && EditorTabs != null && !ReferenceEquals(EditorTabs.SelectedItem, tab))
            {
                EditorTabs.SelectedItem = tab;
            }

            UpdateFoldings();
        }
        finally
        {
            _isUpdatingEditorContent = false;
            _isSwitchingEditorTab = false;
        }
    }



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
        //TitleTextBox.Text = Path.GetFileName(filePath);
        ActivateEditorTab(tab);
        return tab;
    }

    private string? GetSyntaxSelectionForFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".cs" => _currentTheme == AppTheme.Dark ? "Темная C#" : "Стандартная C#",
            ".cpp" or ".cxx" or ".cc" or ".hpp" or ".h" => _currentTheme == AppTheme.Dark ? "Темная C++" : "Стандартная C++",
            ".bsl" or ".os" => _currentTheme == AppTheme.Dark ? "Темная 1C" : "Стандартная 1C",
            ".xml" or ".xsd" or ".xaml" => _currentTheme == AppTheme.Dark ? "Темная XML" : "Стандартная XML",
            ".html" or ".htm" => _currentTheme == AppTheme.Dark ? "Темная HTML" : "Стандартная HTML",
            ".js" => "Стандартная JavaScript",
            ".java" => "Стандартная Java",
            ".css" => "Стандартная CSS",
            ".php" => "Стандартная PHP",
            ".ps1" => "Стандартная PowerShell",
            ".py" => _currentTheme == AppTheme.Dark ? "Темная Python" : "Стандартная Python",
            ".sql" => "Стандартная SQL",
            ".vb" => "Стандартная VB",
            ".patch" or ".diff" => "Стандартная Patch",
            _ => null
        };
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
            var standart1CXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "Standart1C.xshd");
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
            var dark1CXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "Dark1C.xshd");
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
            var darkXMLXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "DarkXML.xshd");
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
            var darkHTMLXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "DarkHTML.xshd");
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
            var darkXshdPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "DarkCSharp.xshd");
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
            var darkPythonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "DarkPython.xshd");
            if (System.IO.File.Exists(darkPythonPath))
            {
                using (var reader = new XmlTextReader(darkPythonPath))
                {
                    _darkPythonHigh = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting("Python Dark", new[] { ".py" }, _darkPythonHigh);
                }
            }
        }
        catch { _darkPythonHigh = null; }

        try
        {
            var lightPythonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "LightPython.xshd");
            if (System.IO.File.Exists(lightPythonPath))
            {
                using (var reader = new XmlTextReader(lightPythonPath))
                {
                    _lightPythonHigh = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting("Python Light", new[] { ".py" }, _lightPythonHigh);
                }
            }
        }
        catch { _lightPythonHigh = null; }

        // Если не удалось загрузить кастомную светлую тему, используем стандартную
        if (_lightCSharpHighlighting == null)
        {
            _lightCSharpHighlighting = HighlightingManager.Instance.GetDefinition("1C");
        }

        try
        {
            var darkCppPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xshd", "DarkCpp.xshd");
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

            {
                WindowState = WindowState.Normal;
                Width = 1200;
                Height = 800;
            }

            else
                WindowState = WindowState.Maximized;
        }
        else
        {
            // Одиночный клик - перетаскивание
            //DragMove();
        }


        if (e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
        {
            // Если окно развернуто на весь экран
            if (WindowState == WindowState.Maximized)
            {
                // Сохраняем позицию курсора относительно окна
                var mouseX = e.GetPosition(this).X;

                // Вычисляем процентное смещение курсора от ширины окна
                double percent = mouseX / ActualWidth;

                // Переводим окно в нормальное состояние
                WindowState = WindowState.Normal;
                Width = 1200;
                Height = 800;

                // Перемещаем окно так, чтобы курсор остался примерно на том же месте
                Left = System.Windows.Forms.Cursor.Position.X - (RestoreBounds.Width * percent);
                Top = System.Windows.Forms.Cursor.Position.Y - 10; // чуть ниже верхней границы
            }

            // Запускаем стандартное перемещение окна
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

        var workingArea = SystemParameters.WorkArea;
        this.MaxWidth = workingArea.Width;
        this.MaxHeight = workingArea.Height;
        this.Left = workingArea.Left + (workingArea.Width - this.Width) / 2;
        this.Top = workingArea.Top + (workingArea.Height - this.Height) / 2;


        _data = await _dataService.LoadDataAsync();
        _appState = await _dataService.LoadStateAsync();


        DescriptionBrowser.Visibility = Visibility.Collapsed;
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

        // Применяем сохраненную тему (уже применено в конструкторе, но синхронизируем UI)
        ThemeCheckBox.IsChecked = _appState.IsLightTheme;


        _isInitialized = true;

        // Обработка аргументов командной строки для открытия файлов
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            // Первый аргумент - это путь к самому приложению
            for (int i = 1; i < args.Length; i++)
            {
                string filePath = args[i];
                if (File.Exists(filePath))
                {
                    OpenEditorTab(filePath);
                }
            }
        }
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

        SyntaxHighlightingComboBox.SelectedIndex = 0; // По умолчанию 1C или C#, будет определяться при открытии файла
    }

    private void SelectSyntax(string selected)
    {
        if (selected == "Темная C#")
        {
            CodeTextBox.SyntaxHighlighting = _darkCSharpHighlighting ?? HighlightingManager.Instance.GetDefinition("C#");
        }

        else if (selected == "Темная C++")
        {
            CodeTextBox.SyntaxHighlighting = _darkCppHighlighting ?? HighlightingManager.Instance.GetDefinition("C++");
        }

        else if (selected == "Стандартная C++")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C++");
        }

        else if (selected == "Темная 1C")
        {
            CodeTextBox.SyntaxHighlighting = _dark1CHigh ?? HighlightingManager.Instance.GetDefinition("1C");
        }

        else if (selected == "Темная XML")
        {
            CodeTextBox.SyntaxHighlighting = _darkXMLHigh ?? HighlightingManager.Instance.GetDefinition("XML");
        }
        else if (selected == "Темная HTML")
        {
            CodeTextBox.SyntaxHighlighting = _darkHTMLHigh ?? HighlightingManager.Instance.GetDefinition("HTML Dark");
        }
        else if (selected == "Темная Python")
        {
            CodeTextBox.SyntaxHighlighting = _darkPythonHigh ?? HighlightingManager.Instance.GetDefinition("Python Dark");
        }
        else if (selected == "Стандартная 1C")
        {
            CodeTextBox.SyntaxHighlighting = _standart1CHigh ?? HighlightingManager.Instance.GetDefinition("1C");
        }
        else if (selected == "Стандартная Python")
        {
            CodeTextBox.SyntaxHighlighting = _lightPythonHigh ?? HighlightingManager.Instance.GetDefinition("Python Light");
        }
        else if (selected == "Стандартная Java")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Java");
        }
        else if (selected == "Стандартная JavaScript")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        }
        else if (selected == "Стандартная HTML")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
        }
        else if (selected == "Стандартная XML")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        }
        else if (selected == "Стандартная CSS")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("CSS");
        }
        else if (selected == "Стандартная PHP")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PHP");
        }

        else if (selected == "Стандартная PowerShell")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PowerShell");
        }
        else if (selected == "Стандартная SQL")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("SQL");
        }
        else if (selected == "Стандартная (VB")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
        }
        else if (selected == "Стандартная ASP/XHTML")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("ASP/XHTML");
        }
        else if (selected == "Стандартная Patch")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Patch");
        }


        else
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }


        UpdateCodeEditorColors(selected);
    }

    private void SyntaxHighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSyntaxSelectionChange || SyntaxHighlightingComboBox?.SelectedItem == null)
        {
            return;
        }


        var selected = SyntaxHighlightingComboBox.SelectedItem.ToString();
        if (selected != null)
        {
            SelectSyntax(selected);
        }
        else { }


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

        try
        {
            var dict = new ResourceDictionary();
            dict.Source = theme == AppTheme.Dark
                ? new Uri("Themes/Dark.xaml", UriKind.Relative)
                : new Uri("Themes/Light.xaml", UriKind.Relative);

            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            var themeDict = mergedDicts.FirstOrDefault(d => d.Source != null &&
                (d.Source.OriginalString.Contains("Dark.xaml") || d.Source.OriginalString.Contains("Light.xaml")));

            if (themeDict != null)
            {
                mergedDicts.Remove(themeDict);
            }
            mergedDicts.Add(dict);

            UpdateCodeEditorColors();

            _suppressSyntaxSelectionChange = true;
            var currentSyntax = SyntaxHighlightingComboBox?.SelectedItem?.ToString();

            SyntaxHighlightingComboBox?.Items.Clear();
            if (theme == AppTheme.Dark)
            {
                SyntaxHighlightingComboBox?.Items.Add("Темная 1C");
                SyntaxHighlightingComboBox?.Items.Add("Темная C#");
                SyntaxHighlightingComboBox?.Items.Add("Темная C++");
                SyntaxHighlightingComboBox?.Items.Add("Темная XML");
                SyntaxHighlightingComboBox?.Items.Add("Темная HTML");
                SyntaxHighlightingComboBox?.Items.Add("Темная Python");
            }
            else
            {
                SyntaxHighlightingComboBox?.Items.Add("Стандартная 1C");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная C#");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная C++");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная Python");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная Java");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная JavaScript");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная HTML");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная XML");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная CSS");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная PHP");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная PowerShell");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная SQL");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная VB");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная ASP/XHTML");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная Patch");
            }

            // Restore selection or set default
            if (!string.IsNullOrEmpty(currentSyntax) && SyntaxHighlightingComboBox != null)
            {
                var newSyntaxName = theme == AppTheme.Dark
                    ? currentSyntax.Replace("Стандартная", "Темная").Replace("Standart", "Dark")
                    : currentSyntax.Replace("Темная", "Стандартная").Replace("Dark", "Standart");

                foreach (var item in SyntaxHighlightingComboBox.Items)
                {
                    if (item?.ToString() == newSyntaxName || item?.ToString() == currentSyntax)
                    {
                        SyntaxHighlightingComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            if (SyntaxHighlightingComboBox != null && SyntaxHighlightingComboBox.SelectedItem == null && SyntaxHighlightingComboBox.Items.Count > 1)
            {
                SyntaxHighlightingComboBox.SelectedIndex = 1;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error applying theme: {ex.Message}");
        }
        finally
        {
            _suppressSyntaxSelectionChange = false;
        }


        var selected = SyntaxHighlightingComboBox.SelectedItem.ToString();
        SelectSyntax(selected!);

    }

    private void UpdateCodeEditorColors(string? selectedSyntax = null)
    {
        if (CodeTextBox == null) return;

        var background = Application.Current.TryFindResource("WindowBackground") as Brush ?? Brushes.White;
        var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
        var lineNumbers = Application.Current.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        var border = Application.Current.TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;

        CodeTextBox.Background = background;
        CodeTextBox.Foreground = foreground;
        CodeTextBox.LineNumbersForeground = lineNumbers;
        CodeTextBox.BorderBrush = border;
    }

    private void FormatCSharpCode_Click(object sender, RoutedEventArgs e)
    {
        var selectedSyntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        if (selectedSyntax == null || !selectedSyntax.Contains("C#"))
        {
            _snackbar.Show(CodeTextBox, "Форматирование поддерживается только для C#", NotificationType.Warning, 2);
            return;
        }

        try
        {
            var code = CodeTextBox.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            using (var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace())
            {
                var formattedNode = Microsoft.CodeAnalysis.Formatting.Formatter.Format(root, workspace);
                var formattedText = formattedNode.ToFullString();
                CodeTextBox.Document.Replace(0, CodeTextBox.Document.TextLength, formattedText);
            }

            _snackbar.Show(CodeTextBox, "Код C# успешно отформатирован", NotificationType.Success, 1.5);
        }
        catch (Exception ex)
        {
            _snackbar.Show(CodeTextBox, $"Ошибка форматирования: {ex.Message}", NotificationType.Error, 3);
        }
    }



    private void RefreshCategoryFilter()
    {
        if (CategoryComboBox != null)
        {
            CategoryComboBox.ItemsSource = GetKnownCategoryPaths();
        }
    }

    private void RefreshEntriesList(object? itemToSelect = null)
    {
        var searchText = SearchBox.Text.ToLower();

        if (string.IsNullOrWhiteSpace(searchText) || searchText == "поиск...")
        {
            SaveExpansionState();
        }

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
        var tree = BuildCategoryTree(_filteredEntries);

        if (string.IsNullOrWhiteSpace(searchText) || searchText == "поиск...")
        {
            ApplyExpansionState(tree);
        }

        EntriesTreeView.ItemsSource = tree;

        if (!string.IsNullOrWhiteSpace(searchText) && searchText != "поиск...")
        {
            foreach (var node in tree)
            {
                ExpandIfHasEntries(node);
            }
        }

        if (itemToSelect != null)
        {
            if (itemToSelect is CodeEntry entry)
            {
                var viewEntry = FindEntryViewModel(tree, entry.Id);
                if (viewEntry != null)
                {
                    ExpandAncestors(tree, NormalizeCategoryPath(entry.Category));
                    viewEntry.IsSelected = true;
                }
            }
            else if (itemToSelect is string categoryPath)
            {
                var targetNode = FindCategoryNode(tree, categoryPath);
                if (targetNode != null)
                {
                    ExpandAncestors(tree, categoryPath);
                    targetNode.IsSelected = true;
                }
            }
        }
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
            var categoryPath = NormalizeCategoryPath(viewEntry.Entry.Category);
            _selectedCategoryPath = categoryPath;
            CategoryComboBox.Text = categoryPath;
        }
        else if (e.NewValue is CategoryNode categoryNode)
        {
            _selectedCategoryPath = categoryNode.FullPath;
            CategoryComboBox.Text = categoryNode.FullPath;
        }
    }

    private void EntriesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var treeViewItem = FindParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        _updateDescription = false; // Разрешаем обновление описания при двойном клике, так как пользователь явно взаимодействует с элементом
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

    private static bool TryGetDraggedItem(DragEventArgs e, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out object? draggedItem)
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
        _isUpdatingEditorContent = true;
        _updateDescription = entry.Extension.Contains(".html");
        try
        {
            TitleTextBox.Text = entry.Title;
            SetDescriptionHtml(entry.Description);
            CategoryComboBox.Text = NormalizeCategoryPath(entry.Category);
            TagsTextBox.Text = string.Join(", ", entry.Tags);

            _selectedCategoryPath = NormalizeCategoryPath(entry.Category);
            RefreshCategoryFilter();

            if (!string.IsNullOrEmpty(entry.Code))

            {
                ToggleDescription_Close();
                var entryTab = OpenEntryTab(entry);
                ActivateEditorTab(entryTab);
                _currentEntry = entryTab.Entry;

                if (!string.IsNullOrEmpty(entry.Syntax) && SyntaxHighlightingComboBox != null)
                {

                    SyntaxHighlightingComboBox.SelectedItem = entry.Syntax;
                }


            }
            else
            {
                // Если кода нет (только документация/HTML), не создаем закладку
                _activeEditorTab = null;
                if (_updateDescription)

                {
                    if (_currentTheme == AppTheme.Dark)
                        SyntaxHighlightingComboBox.SelectedIndex = 6;
                    else SyntaxHighlightingComboBox.SelectedIndex = 0;


                }
                if (!_isNewRecordDescription)
                {
                    ToggleDescription_Open();
                }
            }

            ClearSearch();
        }

        finally
        {
            _isUpdatingEditorContent = false;
        }
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        _isNewRecordDescription = true;
        var currentCategory = NormalizeCategoryPath(CategoryComboBox.Text);
        var newEntry = new CodeEntry
        {
            Category = currentCategory
        };
        _data.Entries.Add(newEntry);
        _currentEntry = newEntry;
        _entryEditorTab = OpenEntryTab(newEntry);
        _isUpdatingEditorContent = true;
        LoadEntryToForm(newEntry);

        RefreshEntriesList(newEntry);
        TitleTextBox.Focus();
    }

    private void ClearEditingForm()
    {
        ActivateEditorTab(_entryEditorTab);
        _isUpdatingEditorContent = true;
        try
        {
            TitleTextBox.Text = string.Empty;
            SetDescriptionHtml(string.Empty);
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
        if (_currentEntry == null)
            return;

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
        if (_currentEntry == null)
        {
            _textSegments.Clear();
            return;
        }

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

        if (activeTab != null && activeTab.Entry == null && activeTab.IsFromFile)
        {
            string filePath = activeTab.FilePath ?? string.Empty;
            bool isHtml = Path.GetExtension(filePath).ToLower() == ".html";

            var entry = new CodeEntry
            {
                Title = Path.GetFileNameWithoutExtension(filePath) ?? "Без названия",
                Extension = Path.GetExtension(filePath) ?? string.Empty,
                Description = isHtml ? CodeTextBox.Text : "",
                Code = isHtml ? "" : CodeTextBox.Text,
                Category = NormalizeCategoryPath(CategoryComboBox.Text),
                Tags = Array.Empty<string>().ToList(),
                Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty
            };

            _data.Entries.Add(entry);
            activeTab.Entry = entry;
            activeTab.FilePath = null;
            activeTab.Title = entry.Title ?? string.Empty;
            activeTab.IsDirty = false;
            activeTab.NotifyHeaderChanged();


            _currentEntry = entry;
            SaveSegmentsToEntry();
            await _dataService.SaveDataAsync(_data);
            RefreshEntriesList(entry);
            _snackbar.Show(CodeTextBox, "Файл сохранен как запись", NotificationType.Success, 1.5);
            return;
        }

        var tabTitle = activeTab?.Title;

        if (string.IsNullOrWhiteSpace(tabTitle))
        {
            ShowAlert("Нет активной закладки", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            ShowAlert("Введите название", isError: true);
            return;
        }

        var entryToUpdate = _data.Entries.FirstOrDefault(e => e.Title == tabTitle);
        if (entryToUpdate == null)
        {
            ShowAlert("Запись не найдена", isError: true);
            return;
        }

        entryToUpdate.Title = TitleTextBox.Text;
        // Описание теперь отображается через WebBrowser и не редактируется напрямую
        entryToUpdate.Code = CodeTextBox.Text;
        entryToUpdate.Category = NormalizeCategoryPath(CategoryComboBox.Text);
        entryToUpdate.Tags = TagsTextBox.Text
            .Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        entryToUpdate.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;
        entryToUpdate.ModifiedAt = DateTime.Now;

        if (activeTab != null)
        {
            activeTab.Entry = entryToUpdate;
            activeTab.Title = entryToUpdate.Title;
            activeTab.SyntaxName = entryToUpdate.Syntax;
            activeTab.IsDirty = false;
        }

        _currentEntry = entryToUpdate;
        SaveSegmentsToEntry();

        EnsureCategoryPathExists(entryToUpdate.Category);

        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList(entryToUpdate);

        _snackbar.Show(CodeTextBox, "Запись сохранена", NotificationType.Success, 1.5);


    }




    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        var checkedEntries = new List<CodeEntryViewModel>();
        var checkedCategories = new List<CategoryNode>();

        FindCheckedItems(EntriesTreeView.ItemsSource as IEnumerable<object>, checkedEntries, checkedCategories);

        if (checkedEntries.Count == 0 && checkedCategories.Count == 0)
        {
            ShowAlert("Не выбрано ни одной записи или категории для удаления", isError: true);
            return;
        }

        if (CustomMessageBox.ShowQuestion($"Удалить отмеченные элементы?\n\nЗаписей: {checkedEntries.Count}\nКатегорий: {checkedCategories.Count}", "Подтверждение удаления"))
        {
            foreach (var entryViewModel in checkedEntries)
            {
                var entryToDelete = entryViewModel.Entry;
                _data.Entries.Remove(entryToDelete);

                var tab = FindEntryTab(entryToDelete.Id);
                if (tab != null) _editorTabs.Remove(tab);
            }

            foreach (var categoryNode in checkedCategories)
            {
                var categoryPath = NormalizeCategoryPath(categoryNode.FullPath);
                var entriesToDelete = _data.Entries
                    .Where(entry => IsPathWithin(NormalizeCategoryPath(entry.Category), categoryPath))
                    .ToList();
                var categoriesToDelete = _data.Categories
                    .Where(category => IsPathWithin(NormalizeCategoryPath(category), categoryPath))
                    .ToList();

                var deletedEntryIds = entriesToDelete.Select(entry => entry.Id).ToHashSet();
                var categoriesToDeleteSet = categoriesToDelete
                    .Select(NormalizeCategoryPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _data.Categories.RemoveAll(existing => categoriesToDeleteSet.Contains(NormalizeCategoryPath(existing)));
                _data.Entries.RemoveAll(entry => deletedEntryIds.Contains(entry.Id));

                foreach (var entryId in deletedEntryIds)
                {
                    var tab = FindEntryTab(entryId);
                    if (tab != null) _editorTabs.Remove(tab);
                }
            }

            if (_editorTabs.Count == 0)
            {
                InitializeEditorTabs();
                ClearEditingForm();
            }
            else if (_activeEditorTab != null && !_editorTabs.Contains(_activeEditorTab))
            {
                ActivateEditorTab(_editorTabs[0]);
            }

            await _dataService.SaveDataAsync(_data);
            RefreshEntriesList();
            _snackbar.Show(CodeTextBox, "Отмеченные элементы удалены", NotificationType.Success, 1.5);
        }
    }

    private async void DeleteSingleEntry_Click(object sender, RoutedEventArgs e)
    {
        CodeEntry? entryToDelete = null;
        object? selectedItem = EntriesTreeView.SelectedItem;

        if (selectedItem is CodeEntryViewModel viewEntry)
        {
            entryToDelete = viewEntry.Entry;
        }
        else if (_currentEntry != null)
        {
            entryToDelete = _currentEntry;
        }

        if (entryToDelete == null)
        {
            ShowAlert("Выберите запись для удаления", isError: true);
            return;
        }

        if (CustomMessageBox.ShowQuestion($"Удалить запись '{entryToDelete.Title}'?", "Подтверждение"))
        {
            // Находим соседа ПЕРЕД удалением
            object? neighborData = GetNeighborData(selectedItem);

            var deletedEntryId = entryToDelete.Id;
            _data.Entries.Remove(entryToDelete);
            await _dataService.SaveDataAsync(_data);

            var deletedTab = FindEntryTab(deletedEntryId);
            if (deletedTab != null)
            {
                _editorTabs.Remove(deletedTab);
            }

            if (_currentEntry?.Id == deletedEntryId)
            {
                _currentEntry = null;
                ClearEditingForm();
            }

            if (_editorTabs.Count == 0)
            {
                InitializeEditorTabs();
            }

            RefreshEntriesList(neighborData);
            _snackbar.Show(CodeTextBox, "Запись удалена", NotificationType.Success, 1.5);
        }
    }

    private void FindCheckedItems(IEnumerable<object>? items, List<CodeEntryViewModel> entries, List<CategoryNode> categories)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item is CategoryNode cat)
            {
                if (cat.IsChecked) categories.Add(cat);
                FindCheckedItems(cat.Items, entries, categories);
            }
            else if (item is CodeEntryViewModel entry)
            {
                if (entry.IsChecked) entries.Add(entry);
            }
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
            DescriptionBrowser.Visibility = Visibility.Collapsed;
            DescriptionSplitter.Visibility = Visibility.Collapsed;


            DescriptionRow.Height = new GridLength(0);
            ToggleDescriptionButton.Content = " ▼ Развернуть ";
        }
        else
        {
            DescriptionBrowser.Visibility = Visibility.Visible;
            DescriptionSplitter.Visibility = Visibility.Visible;

            DescriptionRow.Height = new GridLength(150);
            ToggleDescriptionButton.Content = " ▲ Свернуть ";
        }
    }



    private void ToggleDescription_Close()
    {
        DescriptionBrowser.Visibility = Visibility.Collapsed;
        DescriptionSplitter.Visibility = Visibility.Collapsed;
        DescriptionRow.Height = new GridLength(0);
        ToggleDescriptionButton.Content = " ▼ Развернуть ";
        _updateDescription = false;
    }


    private void ToggleDescription_Open()
    {
        DescriptionBrowser.Visibility = Visibility.Visible;
        DescriptionSplitter.Visibility = Visibility.Visible;
        DescriptionRow.Height = new GridLength(350);
        ToggleDescriptionButton.Content = " ▲ Свернуть ";
        _updateDescription = false;
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

    private void SetDescriptionHtml(string? html)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                DescriptionBrowser.NavigateToString("<html><head><meta charset='utf-8'></head><body style='background-color:#1E1E1E;'></body></html>");
            }
            else
            {
                string bgColor = _currentTheme == AppTheme.Dark ? "#1E1E1E" : "#FFFFFF";
                string textColor = _currentTheme == AppTheme.Dark ? "#EDF2F7" : "#1F1F1F";
                string style = $"<style>body {{ background-color: {bgColor}; color: {textColor}; font-family: 'Segoe UI', sans-serif; font-size: 12px; margin: 5px; }} a {{ pointer-events: none; cursor: default; color: inherit; text-decoration: none; }}</style>";

                // JavaScript to ensure links are disabled
                string script = "<script>window.onload = function() { var links = document.getElementsByTagName('a'); for (var i = 0; i < links.length; i++) { links[i].onclick = function(e) { e.preventDefault(); return false; }; } };</script>";

                DescriptionBrowser.NavigateToString($"<html><head><meta charset='utf-8'>{style}{script}</head><body>{html}</body></html>");
            }
        }
        catch { /* Ignore browser errors */ }
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для импорта (*.bsl, *.html)",
            InitialDirectory = @"G:\Dictionary\Dictionary\Test"
        };

        if (dialog.ShowDialog() == true)
        {
            string selectedPath = dialog.FolderName;
            await ImportFromFolderAsync(selectedPath);
            RefreshEntriesList();
            _snackbar.Show(CodeTextBox, "Импорт из папки завершен", NotificationType.Success, 2.0);
        }
    }

    private async Task ImportFromFolderAsync(string rootPath)
    {
        // Список папок, которые не должны становиться категориями
        var foldersToIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ext", "Forms", "Help" };

        var allFiles = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !Path.GetDirectoryName(f)!.Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith("_")))
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();

        var bslFiles = allFiles.Where(f => f.EndsWith(".bsl", StringComparison.OrdinalIgnoreCase)).ToList();

        // Базовая категория для импорта - текущая выбранная
        string baseCategory = NormalizeCategoryPath(_selectedCategoryPath);

        foreach (var bslFile in bslFiles)
        {
            string relativePath = Path.GetDirectoryName(Path.GetRelativePath(rootPath, bslFile)) ?? "";

            // Фильтруем части пути: если часть пути в списке исключений, пропускаем ее
            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(p => !foldersToIgnore.Contains(p));

            string folderCategory = string.Join("/", pathParts);

            string category = string.IsNullOrWhiteSpace(baseCategory)
                ? folderCategory
                : string.IsNullOrWhiteSpace(folderCategory)
                    ? baseCategory
                    : $"{baseCategory}/{folderCategory}";

            category = NormalizeCategoryPath(category);
            if (string.IsNullOrEmpty(category)) category = UncategorizedCategoryName;

            string title = Path.GetFileNameWithoutExtension(bslFile);
            string code = await File.ReadAllTextAsync(bslFile);

            // Ищем соответствующий html файл для описания
            string htmlFile = Path.ChangeExtension(bslFile, ".html");
            string description = "";
            if (File.Exists(htmlFile))
            {
                description = await File.ReadAllTextAsync(htmlFile);
            }
            else
            {
                // Если нет файла с таким же именем, ищем index.html в той же папке
                string indexHtml = Path.Combine(Path.GetDirectoryName(bslFile) ?? "", "index.html");
                if (File.Exists(indexHtml))
                {
                    description = await File.ReadAllTextAsync(indexHtml);
                }
            }

            var entry = new CodeEntry
            {
                Id = Guid.NewGuid(),
                Title = title,
                Extension = Path.GetExtension(bslFile),
                Code = code,
                Category = category,
                Description = description,
                Syntax = _currentTheme == AppTheme.Dark ? "Темная 1C" : "Стандартная 1C",
                Tags = new List<string> { "Imported" }
            };
            _data.Entries.Add(entry);
        }

        // Также импортируем HTML файлы, для которых нет BSL (как отдельные записи)
        var htmlFiles = allFiles.Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var htmlFile in htmlFiles)
        {
            string bslEquiv = Path.ChangeExtension(htmlFile, ".bsl");
            if (File.Exists(bslEquiv)) continue; // Уже импортировано с BSL
            if (Path.GetFileName(htmlFile).Equals("index.html", StringComparison.OrdinalIgnoreCase)) continue;

            string relativePath = Path.GetDirectoryName(Path.GetRelativePath(rootPath, htmlFile)) ?? "";

            // Фильтруем части пути
            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(p => !foldersToIgnore.Contains(p));

            string folderCategory = string.Join("/", pathParts);

            string category = string.IsNullOrWhiteSpace(baseCategory)
                ? folderCategory
                : string.IsNullOrWhiteSpace(folderCategory)
                    ? baseCategory
                    : $"{baseCategory}/{folderCategory}";

            category = NormalizeCategoryPath(category);
            if (string.IsNullOrEmpty(category)) category = UncategorizedCategoryName;

            string title = Path.GetFileNameWithoutExtension(htmlFile);
            string description = await File.ReadAllTextAsync(htmlFile);

            var entry = new CodeEntry
            {
                Id = Guid.NewGuid(),
                Title = title,
                Extension = Path.GetExtension(htmlFile),
                Code = "",
                Category = category,
                Description = description,
                Syntax = "Темная HTML",
                Tags = new List<string> { "Imported", "Doc" }
            };
            _data.Entries.Add(entry);
        }

        NormalizeImportedData();
        await _dataService.SaveDataAsync(_data);
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

        if (_data.Categories.Any(existing => string.Equals(NormalizeCategoryPath(existing), newCategoryPath, StringComparison.CurrentCultureIgnoreCase)))
        {
            ShowAlert($"Категория '{newCategoryPath}' уже существует", isError: true);
            return;
        }

        EnsureCategoryPathExists(newCategoryPath);

        // Сортируем категории для сохранения порядка
        _data.Categories = _data.Categories
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();

        _selectedCategoryPath = newCategoryPath;
        CategoryComboBox.Text = newCategoryPath;

        // Автоматически раскрываем и выделяем новую категорию
        SelectCategoryInTree(newCategoryPath);

        _snackbar.Show(CodeTextBox, $"Категория '{newCategoryPath}' добавлена", NotificationType.Success, 1.5);
    }

    private void SelectCategoryInTree(string categoryPath)
    {
        if (EntriesTreeView.ItemsSource is IEnumerable<CategoryNode> categories)
        {
            var targetNode = FindCategoryNode(categories, categoryPath);
            if (targetNode != null)
            {
                ExpandAncestors(categories, categoryPath);
                targetNode.IsSelected = true;
                targetNode.IsExpanded = true;
            }
        }
    }

    private CategoryNode? FindCategoryNode(IEnumerable<CategoryNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.CurrentCultureIgnoreCase))
                return node;

            var found = FindCategoryNode(node.Children, path);
            if (found != null) return found;
        }
        return null;
    }

    private void ExpandAncestors(IEnumerable<CategoryNode> nodes, string path)
    {
        var ancestors = EnumerateCategoryAncestors(path).ToList();
        foreach (var ancestor in ancestors)
        {
            var node = FindCategoryNode(nodes, ancestor);
            if (node != null) node.IsExpanded = true;
        }
    }

    private bool ExpandIfHasEntries(CategoryNode node)
    {
        bool hasMatchingEntries = node.Entries.Count > 0;
        bool hasMatchingChildren = false;

        foreach (var child in node.Children)
        {
            if (ExpandIfHasEntries(child))
            {
                hasMatchingChildren = true;
            }
        }

        if (hasMatchingEntries || hasMatchingChildren)
        {
            node.IsExpanded = true;
            return true;
        }

        return false;
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
            // Находим соседа ПЕРЕД удалением
            object? neighborData = GetNeighborData(categoryNode);

            var deletedEntryIds = entriesToDelete.Select(entry => entry.Id).ToHashSet();
            var categoriesToDeleteSet = categoriesToDelete
                .Select(NormalizeCategoryPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _data.Categories.RemoveAll(existing => categoriesToDeleteSet.Contains(NormalizeCategoryPath(existing)));
            _data.Entries.RemoveAll(entry => deletedEntryIds.Contains(entry.Id));

            foreach (var entryId in deletedEntryIds)
            {
                var tab = FindEntryTab(entryId);
                if (tab != null)
                {
                    _editorTabs.Remove(tab);
                }
            }

            if (_editorTabs.Count == 0)
            {
                InitializeEditorTabs();
                ClearEditingForm();
            }
            else if (_activeEditorTab != null && deletedEntryIds.Contains(_activeEditorTab.Entry?.Id ?? Guid.Empty))
            {
                ActivateEditorTab(_editorTabs[0]);
            }

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
            RefreshEntriesList(neighborData);
            _snackbar.Show(CodeTextBox, $"Категория '{categoryPath}' и её содержимое удалены", NotificationType.Success, 1.5);

        }
        else
        {
            return;
        }
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();

        var sourceCode = CodeTextBox.Text;
        if (string.IsNullOrWhiteSpace(sourceCode)) return;

        try
        {
            // Запускаем анализ в фоне
            var result = await _analyzer.AnalyzeAsync(sourceCode);

            // Обновляем UI
            UpdateUiWithResult(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка анализа: {ex.Message}");
        }
    }

    private void UpdateUiWithResult(AnalysisResult result)
    {
        _colorizer?.UpdateErrors(result.Errors.ToList());
        _markerService?.UpdateMarkers(result.Errors);
        CodeTextBox.TextArea.TextView.Redraw();

        // Обновляем таблицу ошибок
        bool hasErrors = result.Errors != null && result.Errors.Count > 0;
        bool shouldShow = Analysis1CToggle.IsChecked == true && hasErrors;

        // Если панель была закрыта вручную, мы её не открываем автоматически 
        // до следующего сеанса анализа или изменения состояния (опционально)
        // Но здесь мы просто следуем логике: если есть ошибки и анализ включен - показываем.

        ErrorListGrid.ItemsSource = shouldShow ? result.Errors : null;

        // Если ошибок нет - всегда скрываем
        if (!shouldShow)
        {
            SetErrorPanelVisibility(Visibility.Collapsed);
        }
        else if (ErrorListGrid.Visibility != Visibility.Visible)
        {
            // Если ошибки появились и анализ включен - показываем
            SetErrorPanelVisibility(Visibility.Visible);
        }
    }

    private void SetErrorPanelVisibility(Visibility visibility)
    {
        ErrorListGrid.Visibility = visibility;
        ErrorListHeader.Visibility = visibility;
        ErrorListSplitter.Visibility = visibility;

        var parentGrid = ErrorListGrid.Parent as Grid;
        if (parentGrid != null && parentGrid.RowDefinitions.Count >= 5)
        {
            bool isVisible = visibility == Visibility.Visible;
            parentGrid.RowDefinitions[2].Height = isVisible ? new GridLength(28) : new GridLength(0);
            parentGrid.RowDefinitions[3].Height = isVisible ? new GridLength(4) : new GridLength(0);
            parentGrid.RowDefinitions[4].Height = isVisible ? new GridLength(100, GridUnitType.Pixel) : new GridLength(0);
        }
    }

    private void CloseErrorListButton_Click(object sender, RoutedEventArgs e)
    {
        SetErrorPanelVisibility(Visibility.Collapsed);
    }

    private void ErrorListGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ErrorListGrid.SelectedItem is BslSyntaxError error)
        {
            try
            {
                // AvalonEdit uses 1-based indexing for lines and columns
                int line = error.Line;
                int column = error.Column;

                if (line < 1) line = 1;
                if (line > CodeTextBox.Document.LineCount) line = CodeTextBox.Document.LineCount;

                var lineSegment = CodeTextBox.Document.GetLineByNumber(line);
                if (column < 1) column = 1;
                if (column > lineSegment.Length + 1) column = lineSegment.Length + 1;

                CodeTextBox.ScrollTo(line, column);
                CodeTextBox.CaretOffset = CodeTextBox.Document.GetOffset(line, column);
                CodeTextBox.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка перехода к строке {error.Line}: {ex.Message}");
            }
        }
    }

    private void OnTextViewMouseHover(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        if (Analysis1CToggle.IsChecked != true) return;

        var pos = CodeTextBox.GetPositionFromPoint(e.GetPosition(CodeTextBox));
        if (pos == null) return;

        int line = pos.Value.Line;
        int column = pos.Value.Column;

        var errors = _colorizer?.GetErrorsAtLine(line);
        if (errors == null || errors.Count == 0) return;

        // Ищем ошибку, попадающую под курсор
        var error = errors.FirstOrDefault(err => column >= err.Column && column <= err.Column + Math.Max(1, err.Length));

        if (error != null)
        {
            if (_hoverToolTip == null)
                _hoverToolTip = new ToolTip();

            _hoverToolTip.Content = new TextBlock
            {
                Text = error.Message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };
            _hoverToolTip.PlacementTarget = CodeTextBox;
            _hoverToolTip.IsOpen = true;
            e.Handled = true;
        }
    }

    private void OnTextViewMouseHoverStopped(object? sender, EventArgs e)
    {
        if (_hoverToolTip == null) return;
        _hoverToolTip.IsOpen = false;
        _hoverToolTip = null;
    }

    private void Analysis1CToggle_Click(object sender, RoutedEventArgs e)
    {
        if (Analysis1CToggle.IsChecked == true)
        {
            _debounceTimer.Start();
            // Сразу запускаем проверку
            DebounceTimer_Tick(null, EventArgs.Empty);
        }
        else
        {
            _debounceTimer.Stop();

            // Очистка ошибок при выключении
            var emptyResult = new AnalysisResult(new List<BslSyntaxError>(), new List<SymbolInfo>());
            UpdateUiWithResult(emptyResult);
        }
    }

    private void Format1CCode_Click(object sender, RoutedEventArgs e)
    {
        var text = CodeTextBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var newLines = new List<string>();
        int indent = 0;

        var blockStart = new[] { "Процедура", "Функция", "Если", "Для", "Пока", "Попытка", "Цикл", "Тогда" };
        var blockEnd = new[] { "КонецПроцедуры", "КонецФункции", "КонецЕсли", "КонецЦикла", "КонецПопытки", "Исключение" };

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                newLines.Add("");
                continue;
            }

            // Уменьшаем отступ, если строка начинается с ключевого слова конца блока
            var upperLine = trimmed.ToUpper();
            if (blockEnd.Any(b => upperLine.StartsWith(b.ToUpper())))
            {
                indent = Math.Max(0, indent - 1);
            }

            newLines.Add(new string(' ', indent * 4) + trimmed);

            // Увеличиваем отступ, если строка начинается с ключевого слова начала блока
            if (blockStart.Any(b => upperLine.StartsWith(b.ToUpper())))
            {
                // Для "Тогда" и "Цикл" обычно отступ увеличивается после них, но они часто в той же строке что и Если/Для
                // Но если они на отдельной строке, то тоже увеличиваем.
                // В простейшем случае:
                indent++;
            }
        }

        CodeTextBox.Text = string.Join(Environment.NewLine, newLines);
        _snackbar.Show(CodeTextBox, "Код 1С отформатирован", NotificationType.Success, 1.5);
    }

    private void InitializeSyntaxServices()
    {
        // Remove old services if they exist
        if (_colorizer != null)
        {
            CodeTextBox.TextArea.TextView.LineTransformers.Remove(_colorizer);
        }
        if (_markerService != null)
        {
            // TextMarkerService probably has a way to disconnect or we just stop using it
            // It was added to BackgroundRenderers in AddToTextView
            CodeTextBox.TextArea.TextView.BackgroundRenderers.Remove(_markerService);
        }

        // Initialize new services for the current document
        _colorizer = new SyntaxErrorColorizer(CodeTextBox.Document);
        CodeTextBox.TextArea.TextView.LineTransformers.Add(_colorizer);

        _markerService = new TextMarkerService(CodeTextBox.Document);
        _markerService.AddToTextView(CodeTextBox.TextArea.TextView);
    }
}
