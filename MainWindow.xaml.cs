using CodeDictionary.Analysis;
using CodeDictionary.Models;
using CodeDictionary.Properties;
using CodeDictionary.Services;
using CodeDictionary.SyntaxChecking;
using CodeDictionary.SyntaxChecking.Providers;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;  // Для OpenFileDialog и SaveFileDialog
using ScriptEngine.Machine;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;




namespace CodeDictionary;

public partial class MainWindow : Window
{
    private const string CategorySeparator = "/";
    private const string UncategorizedCategoryName = "Без категории";
    private readonly DataService _dataService;
    private readonly CategoryService _categoryService;
    private readonly FormattingService _formattingService;
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
    private IHighlightingDefinition? _darkCppHighlighting;
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
    private bool _isDescriptionEditMode;
    private int _navigationSequence;
    private bool _descriptionWebView2Ready;
    private bool _isDraggingSplitter;
    private double _dragStartY;
    private double _dragStartHeight;
    private Process? _terminalProcess;
    private bool _isDraggingTerminalSplitter;
    private double _terminalDragStartY;
    private double _terminalDragStartHeight;
    private StringBuilder _terminalOutputBuffer = new();

    /// <summary>
    /// /Syntax
    private readonly ICodeAnalysisService _bslAnalyzer = new BslCodeAnalyzer();
    private readonly ICodeAnalysisService _csharpAnalyzer;
    private readonly ICodeAnalysisService _pythonAnalyzer = new PythonCodeAnalyzer();
    private ToolTip? _hoverToolTip;
    private CompletionWindow? _completionWindow;
    private bool _autoCompletionEnabled = true;
    private readonly DispatcherTimer _debounceTimer;
    private readonly DispatcherTimer _searchDebounceTimer; // Added timer
    private readonly DispatcherTimer _completionDebounceTimer;
    private readonly DispatcherTimer _htmlPositionDebounceTimer;
    private readonly DispatcherTimer _browserSelectionDebounceTimer;
    private string _lastBrowserSelectedText = "";
    private CancellationTokenSource? _completionCts;
    private SyntaxErrorColorizer? _colorizer;
    private TextMarkerService? _markerService;
    /// 
    /// </summary>



    private readonly RoslynCompletionService _roslynCompletionService;
    private readonly BslCompletionService _bslCompletionService = new();
    private readonly BslExecutionService _bslExecutionService = new();
    private readonly PythonExecutionService _pythonExecutionService = new();
    private readonly PythonCompletionService _pythonCompletionService = new();
    private readonly BslSignatureHelpService _bslSignatureHelpService = new();

    private readonly HashSet<int> _activeBreakpoints = new();
    private readonly DebugLineHighlighter _debugLineHighlighter = new();
    private BslDebugger? _currentDebugger;
    private bool _isDebugPaused;

    private void ContinueDebug()
    {
        if (_isDebugPaused && _currentDebugger != null)
        {
            _isDebugPaused = false;
            if (IsBslSyntax)
            {
                ContinueDebugButton.Visibility = Visibility.Collapsed;
                StepOverButton.Visibility = Visibility.Collapsed;
                WatchPanel.Visibility = Visibility.Collapsed;
            }
            ShowOutputPanel("Выполнение...");
            _currentDebugger.Resume();
        }
    }

    private void DebugStepOver()
    {
        if (_isDebugPaused && _currentDebugger != null)
        {
            _currentDebugger.StepOver();
            _isDebugPaused = false;
            if (IsBslSyntax)
            {
                ContinueDebugButton.Visibility = Visibility.Collapsed;
                StepOverButton.Visibility = Visibility.Collapsed;
                WatchPanel.Visibility = Visibility.Collapsed;
            }
            ShowOutputPanel("Выполнение...");
        }
    }

    private HashSet<string> _expandedCategories = new();

    private void SaveExpansionState()
    {
        _categoryService.SaveExpansionState(_expandedCategories,
            EntriesTreeView.ItemsSource as IEnumerable<CategoryNode> ?? Enumerable.Empty<CategoryNode>());
    }

    private void CodeTextBox_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (!_autoCompletionEnabled) return;

        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        var isBsl = syntax != null && syntax.Contains("1C");

        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (e.Text == "." && isBsl)
            {
                _completionWindow.Close();
            }
            else if (e.Text == " ")
            {
                // Space closes completion without inserting
                _completionWindow.Close();
            }
            else if (!char.IsLetterOrDigit(e.Text[0]))
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }

        if (e.Text == ".")
        {
            if (syntax != null && syntax.Contains("C#"))
            {
                _completionDebounceTimer.Stop();
                _completionDebounceTimer.Start();
            }
        }
        else if (e.Text == "(" || e.Text == ",")
        {
            if (syntax != null)
            {
                if (syntax.Contains("C#"))
                    Dispatcher.BeginInvoke(new Action(async () => await ShowSignatureHelp()));
                else if (syntax.Contains("1C"))
                    Dispatcher.BeginInvoke(new Action(() => ShowBslSignatureHelp()));
            }
        }
        else if (e.Text == ";")
        {
            CloseSignatureHelpToolTip();
        }
    }

    private async Task ShowSignatureHelp()
    {
        var position = CodeTextBox.CaretOffset;
        _roslynCompletionService.UpdateCode(CodeTextBox.Text);
        var signatureInfo = await _roslynCompletionService.GetSignatureInfoAsync(position, _completionCts?.Token ?? default);

        if (signatureInfo != null)
        {
            if (_hoverToolTip == null) _hoverToolTip = new ToolTip();
            _hoverToolTip.Content = signatureInfo.FullSignature;
            _hoverToolTip.PlacementTarget = CodeTextBox;
            _hoverToolTip.IsOpen = true;
        }
    }

    private void ShowBslSignatureHelp()
    {
        var symbols = _bslAnalyzer is BslCodeAnalyzer coreAnalyzer
            ? coreAnalyzer.Symbols
            : [];

        var sig = _bslSignatureHelpService.GetSignature(
            CodeTextBox.Text, CodeTextBox.CaretOffset, symbols);
        if (sig != null && sig.Found)
        {
            if (_hoverToolTip == null) _hoverToolTip = new ToolTip();

            var textView = CodeTextBox.TextArea.TextView;
            var caretPos = CodeTextBox.TextArea.Caret.Position;

            // Получаем позицию окончания слова
            int wordEnd = caretPos.Column - 1;

            // Получаем визуальную позицию
            var visualPos = textView.GetVisualPosition(
                new TextViewPosition(caretPos.Line, wordEnd + 1),
                VisualYPosition.LineBottom
            );

            // Преобразуем в экранные координаты
            var screenPos = textView.PointToScreen(visualPos - textView.ScrollOffset);

            // Настройка тултипа
            var bg = Application.Current.TryFindResource("WindowBackground") as Brush ?? Brushes.White;
            var fg = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
            var br = Application.Current.TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;

            _hoverToolTip.Background = bg;
            _hoverToolTip.Foreground = fg;
            _hoverToolTip.BorderBrush = br;
            _hoverToolTip.Content = sig.HighlightedSignature;

            // Позиционирование с использованием экранных координат
            _hoverToolTip.Placement = PlacementMode.Absolute;
            _hoverToolTip.HorizontalOffset = screenPos.X;
            _hoverToolTip.VerticalOffset = screenPos.Y + 5; // небольшой отступ
            _hoverToolTip.IsOpen = true;
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) =>
            {
                if (_hoverToolTip != null)
                {
                    _hoverToolTip.IsOpen = false;

                    timer.Stop();
                }

            };
            timer.Start();

        }
    }

    private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (!_autoCompletionEnabled) return;

            _completionDebounceTimer.Stop();
            var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
            if (syntax != null && syntax.Contains("C#"))
            {
                ShowCompletion();
                e.Handled = true;
            }

            if (syntax != null && syntax.Contains("1C"))
            {
                e.Handled = true;
                ShowCompletion1C(GetWordAtOffset(CodeTextBox.CaretOffset), true);
            }

            if (syntax != null && syntax.Contains("Python"))
            {
                e.Handled = true;
                ShowCompletionPython(GetWordAtOffset(CodeTextBox.CaretOffset), true);
            }
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            CloseSignatureHelpToolTip();
        }
    }

    private void CloseSignatureHelpToolTip()
    {
        if (_hoverToolTip == null) return;
        _hoverToolTip.IsOpen = false;
        _hoverToolTip = null;
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

    private string GetTextAfterDot(int offset)
    {
        var document = CodeTextBox.Document;
        if (document.TextLength == 0 || offset <= 0) return string.Empty;

        int pos = Math.Min(offset, document.TextLength) - 1;

        // Walk back to find the last dot
        int lastDot = -1;
        for (int i = pos; i >= 0; i--)
        {
            char c = document.GetCharAt(i);
            if (c == '.')
            {
                lastDot = i;
                break;
            }
            if (!char.IsLetterOrDigit(c) && c != '_')
                break;
        }

        if (lastDot < 0)
            return string.Empty;

        // Collect text after the last dot up to the cursor
        int end = lastDot + 1;
        while (end < document.TextLength && (char.IsLetterOrDigit(document.GetCharAt(end)) || document.GetCharAt(end) == '_'))
            end++;

        end = Math.Min(end, offset);
        int start = lastDot + 1;

        return start < end ? document.GetText(start, end - start) : string.Empty;
    }


    private void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (!_autoCompletionEnabled) return;

        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();

        if (e.Text == "." && syntax != null && syntax.Contains("1C"))
        {
            ShowCompletion1C("", false);
            return;
        }

        if (e.Text.Length > 0 && (char.IsLetter(e.Text[0]) || e.Text[0] == '_'))
        {
            if (syntax != null && (syntax.Contains("C#") || syntax.Contains("Python")))
            {
                _completionDebounceTimer.Stop();
                _completionDebounceTimer.Start();
            }
        }
    }


    private void ShowCompletion1C(string enteredText, bool controlSpace)
    {
        var context = BslSyntaxContext.Detect(CodeTextBox.Text, CodeTextBox.CaretOffset);

        if (context.Kind == BslContextKind.StringOrComment)
            return;

        IReadOnlyList<SymbolInfo> symbols;
        IReadOnlyDictionary<string, string> variableTypes;

        if (_bslAnalyzer is BslCodeAnalyzer coreAnalyzer)
        {
            symbols = coreAnalyzer.Symbols;
            variableTypes = coreAnalyzer.VariableTypes;

            if (context.Kind == BslContextKind.MemberAccess && context.HasLeftSide)
            {
                var varName = context.LeftSide.Split('.').First();
                if (!variableTypes.ContainsKey(varName))
                {
                    var pos = CodeTextBox.CaretOffset;
                    if (pos > 0 && CodeTextBox.Text[pos - 1] == '.')
                    {
                        var codeBeforeDot = CodeTextBox.Text.Substring(0, pos - 1);
                        coreAnalyzer.Analyze(codeBeforeDot);
                        variableTypes = coreAnalyzer.VariableTypes;
                    }
                }
            }
        }
        else
        {
            symbols = [];
            variableTypes = new Dictionary<string, string>();
        }

        var analysis = new BslAnalysisSnapshot
        {
            Symbols = symbols,
            VariableTypes = variableTypes
        };

        List<ICompletionData> allData;
        try
        {
            allData = _bslCompletionService.GetCompletions(context, analysis);
        }
        catch (Exception ex)
        {
            _snackbar.Show(CodeTextBox, $"Ошибка дополнения: {ex.Message}", NotificationType.Error, 3.0);
            return;
        }

        string filterWord;
        if (context.Kind == BslContextKind.MemberAccess)
            filterWord = GetTextAfterDot(CodeTextBox.CaretOffset);
        else
            filterWord = GetWordAtOffset(CodeTextBox.CaretOffset);

        var filteredList = !string.IsNullOrEmpty(filterWord)
            ? allData.Where(d => d.Text.StartsWith(filterWord, StringComparison.OrdinalIgnoreCase)).ToList()
            : allData.ToList();

        if (filteredList.Any())
        {
            _completionWindow?.Close();
            _completionWindow = new CompletionWindow(CodeTextBox.TextArea)
            {
                Width = 500,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                BorderThickness = new Thickness(1)
            };

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

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in filteredList.OrderBy(d => d.Text))
            {
                data.Add(item);
            }

            _completionWindow.Show();

            if (!string.IsNullOrEmpty(filterWord))
            {
                _completionWindow.CompletionList.SelectItem(filterWord);
            }
        }
        else
        {
            _completionWindow?.Close();
            _completionWindow = null;
        }
    }

    private void ShowCompletionPython(string enteredText, bool controlSpace)
    {
        var code = CodeTextBox.Text;
        var position = CodeTextBox.CaretOffset;

        if (IsCursorInsideStringOrComment(code, position))
            return;

        string filterWord = GetWordAtOffset(position);

        var allData = _pythonCompletionService.GetCompletions(filterWord);

        if (allData.Any())
        {
            _completionWindow?.Close();

            // Calculate start offset of the word to replace
            int wordStart = position;
            while (wordStart > 0 && (char.IsLetterOrDigit(code[wordStart - 1]) || code[wordStart - 1] == '_'))
                wordStart--;

            _completionWindow = new CompletionWindow(CodeTextBox.TextArea)
            {
                Width = 400,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                BorderThickness = new Thickness(1),
                StartOffset = wordStart
            };

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

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var item in allData.OrderBy(d => d.Text))
            {
                data.Add(item);
            }

            _completionWindow.Show();

            if (!string.IsNullOrEmpty(filterWord))
            {
                _completionWindow.CompletionList.SelectItem(filterWord);
            }
        }
        else
        {
            _completionWindow?.Close();
            _completionWindow = null;
        }
    }


    private void CompletionDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _completionDebounceTimer.Stop();
        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        if (syntax != null && syntax.Contains("C#"))
        {
            ShowCompletion();
        }
        else if (syntax != null && syntax.Contains("Python"))
        {
            ShowCompletionPython(GetWordAtOffset(CodeTextBox.CaretOffset), false);
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        _htmlPositionDebounceTimer.Stop();
        _htmlPositionDebounceTimer.Start();
    }

    private async void HtmlPositionDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _htmlPositionDebounceTimer.Stop();

        if (!_descriptionWebView2Ready || DescriptionPanel.Visibility != Visibility.Visible)
            return;

        var activeTab = GetActiveEditorTab();
        if (activeTab?.Entry == null || !activeTab.Entry.Extension.Contains(".html"))
            return;

        int line = CodeTextBox.TextArea.Caret.Line;
        var doc = CodeTextBox.Document;
        if (doc == null || line > doc.LineCount) return;

        var lineSegment = doc.GetLineByNumber(line);
        string lineText = doc.GetText(lineSegment.Offset, lineSegment.Length);
        string visibleText = Regex.Replace(lineText, @"<[^>]*>", "").Trim();
        if (string.IsNullOrWhiteSpace(visibleText) || visibleText.Length < 2) return;

        string escapedText = visibleText
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

        string js = $@"
(function(){{
    var old = document.getElementById('__pos_marker');
    if (old) old.remove();
    var text = '{escapedText}';
    if (!text) return;
    var content = document.getElementById('__content');
    if (!content) return;
    var walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT, null, false);
    var n;
    while (n = walker.nextNode()) {{
        var idx = n.textContent.indexOf(text);
        if (idx >= 0) {{
            var r = document.createRange();
            r.setStart(n, idx);
            r.setEnd(n, idx + text.length);
            var rects = r.getClientRects();
            if (rects.length > 0) {{
                var rect = rects[0];
                var m = document.createElement('div');
                m.id = '__pos_marker';
                m.style.cssText = 'position:fixed;pointer-events:none;background:rgba(255,255,0,0.35);border:2px solid #FFD700;border-radius:2px;z-index:9998;';
                m.style.left = rect.left + 'px';
                m.style.top = rect.top + 'px';
                m.style.width = rect.width + 'px';
                m.style.height = rect.height + 'px';
                document.body.appendChild(m);
                m.scrollIntoView({{behavior:'smooth', block:'center'}});
            }}
            return;
        }}
    }}
}})();
";
        try
        {
            await DescriptionBrowser.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch { }
    }

    private async void BrowserSelectionDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _browserSelectionDebounceTimer.Stop();
        SyncBrowserSelectionToEditor(_lastBrowserSelectedText);
    }

    private void SyncBrowserSelectionToEditor(string selectedText)
    {
        try
        {
            if (string.IsNullOrEmpty(selectedText))
            {
                if (CodeTextBox.SelectionLength > 0)
                    CodeTextBox.SelectionLength = 0;
                return;
            }

            var activeTab = GetActiveEditorTab();
            if (activeTab?.Entry == null) return;

            string docText = CodeTextBox.Text;
            if (string.IsNullOrEmpty(docText)) return;

            string searchText = selectedText.Trim();
            if (searchText.Length < 2) return;

            string sourceToSearch = docText;
            bool isHtml = activeTab.Entry.Extension.Contains(".html");

            // Для HTML-записей удаляем теги для поиска по видимому тексту
            if (isHtml)
                sourceToSearch = Regex.Replace(docText, @"<[^>]*>", "");

            // Находим текст без учёта регистра
            int strippedStart = sourceToSearch.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            if (strippedStart < 0) return;

            int selStart, selLen;

            if (isHtml)
            {
                // Отображаем позицию из stripped-текста обратно в оригинальный HTML
                var (origStart, origEnd) = MapStrippedRangeToOriginal(docText, strippedStart, strippedStart + searchText.Length);
                selStart = origStart;
                selLen = origEnd - origStart;
            }
            else
            {
                selStart = strippedStart;
                selLen = searchText.Length;
            }

            CodeTextBox.Select(selStart, selLen);
            CodeTextBox.ScrollToLine(CodeTextBox.Document.GetLineByOffset(selStart).LineNumber);
        }
        catch { }
    }

    private static (int, int) MapStrippedRangeToOriginal(string originalText, int strippedStart, int strippedEnd)
    {
        int strippedPos = 0;
        int origStart = 0;
        int origEnd = 0;
        bool startFound = false;

        for (int i = 0; i < originalText.Length; i++)
        {
            if (originalText[i] == '<')
            {
                int closeTag = originalText.IndexOf('>', i);
                if (closeTag >= 0)
                    i = closeTag;
                continue;
            }

            if (!startFound && strippedPos == strippedStart)
            {
                origStart = i;
                startFound = true;
            }

            if (startFound && strippedPos == strippedEnd)
            {
                origEnd = i;
                break;
            }

            strippedPos++;
        }

        if (!startFound)
            origStart = strippedStart;

        if (origEnd <= origStart)
            origEnd = Math.Min(origStart + (strippedEnd - strippedStart), originalText.Length);

        return (origStart, origEnd);
    }

    private async void ShowCompletion()
    {
        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();
        var token = _completionCts.Token;

        var code = CodeTextBox.Text;
        var position = CodeTextBox.CaretOffset;

        // Проверяем, не находимся ли мы внутри строки или комментария
        if (IsCursorInsideStringOrComment(code, position))
            return;

        _roslynCompletionService.UpdateCode(code);

        if (token.IsCancellationRequested) return;

        IEnumerable<CompletionItem> items;
        try
        {
            items = await _roslynCompletionService.GetCompletionItemsAsync(position, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || !items.Any())
        {
            if (!items.Any()) _completionWindow?.Close();
            return;
        }

        // Вычисляем начало слова и текущий префикс для фильтрации
        int wordStart = position;
        while (wordStart > 0 && (char.IsLetterOrDigit(code[wordStart - 1]) || code[wordStart - 1] == '_'))
            wordStart--;

        string currentPrefix = code.Substring(wordStart, position - wordStart);
        if (!string.IsNullOrEmpty(currentPrefix))
        {
            var filteredItems = items.Where(i => i.DisplayText.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!filteredItems.Any())
            {
                _completionWindow?.Close();
                return;
            }
            items = filteredItems;
        }

        // Всегда пересоздаём окно, чтобы StartOffset гарантированно применился
        _completionWindow?.Close();
        _completionWindow = new CompletionWindow(CodeTextBox.TextArea)
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            BorderThickness = new Thickness(1),
            StartOffset = wordStart,
            Width = 500
        };

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

        _completionWindow.Closed += (s, e) => _completionWindow = null;

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var item in items)
        {
            data.Add(new RoslynCompletionData(item, _roslynCompletionService, position));
        }

        _completionWindow.Show();
    }

    private bool IsCursorInsideStringOrComment(string code, int offset)
    {
        if (string.IsNullOrEmpty(code) || offset <= 0 || offset > code.Length)
            return false;

        int adjustedOffset = Math.Min(offset - 1, code.Length - 1);

        bool inSingleComment = false;
        bool inMultiComment = false;
        bool inString = false;
        char stringChar = '"';

        for (int i = 0; i <= adjustedOffset; i++)
        {
            char c = code[i];

            if (inSingleComment)
            {
                if (c == '\n') inSingleComment = false;
                continue;
            }

            if (inMultiComment)
            {
                if (c == '*' && i + 1 < code.Length && code[i + 1] == '/')
                {
                    inMultiComment = false;
                    i++;
                }
                continue;
            }

            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == stringChar) inString = false;
                continue;
            }

            if (c == '/' && i + 1 < code.Length)
            {
                if (code[i + 1] == '/') { inSingleComment = true; i++; continue; }
                if (code[i + 1] == '*') { inMultiComment = true; i++; continue; }
            }

            if (c == '"' || c == '\'')
            {
                inString = true;
                stringChar = c;
            }
        }

        return inSingleComment || inMultiComment || inString;
    }

    private object? GetNeighborData(object item)
    {
        var roots = EntriesTreeView.ItemsSource as IEnumerable<CategoryNode> ?? Enumerable.Empty<CategoryNode>();
        return _categoryService.GetNeighborData(roots, item);
    }


    public MainWindow()


    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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
        _categoryService = new CategoryService();
        _formattingService = new FormattingService();
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
        _csharpAnalyzer = new CSharpCodeAnalyzer(_roslynCompletionService);

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

        // Инициализация компонента для поиска (Debouncing)
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

        _completionDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _completionDebounceTimer.Tick += CompletionDebounceTimer_Tick;

        _htmlPositionDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _htmlPositionDebounceTimer.Tick += HtmlPositionDebounceTimer_Tick;

        _browserSelectionDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _browserSelectionDebounceTimer.Tick += BrowserSelectionDebounceTimer_Tick;

        CodeTextBox.TextArea.TextView.MouseHover += OnTextViewMouseHover;
        CodeTextBox.TextArea.TextView.MouseHoverStopped += OnTextViewMouseHoverStopped;

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
        CodeTextBox.TextArea.Caret.PositionChanged += Caret_PositionChanged;


        // Debug line highlighter
        CodeTextBox.TextArea.TextView.BackgroundRenderers.Add(_debugLineHighlighter);

        // F9 toggle breakpoint, F5 continue
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(ToggleBreakpointAtCaret), new KeyGesture(Key.F9)));
        this.InputBindings.Add(new InputBinding(new RelayCommand(ContinueDebug), new KeyGesture(Key.F5)));
        this.InputBindings.Add(new InputBinding(new RelayCommand(DebugStepOver), new KeyGesture(Key.F10)));

        // Bookmark margin handler
        _bookmarkMargin = new BookmarkMargin(
            () => GetActiveEditorTab()?.Bookmarks ?? new HashSet<int>(),
            () => (Brush)Application.Current.TryFindResource("AccentBrush") ?? Brushes.Blue,
            () => _activeBreakpoints
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
        CodeTextBox.InputBindings.Add(new InputBinding(new RelayCommand(GoToDefinition), new KeyGesture(Key.F12)));
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

    private void NavigateToPosition(int line, int column)
    {
        CodeTextBox.ScrollToLine(line);
        CodeTextBox.TextArea.Caret.Line = line;
        CodeTextBox.TextArea.Caret.Column = Math.Max(1, column);
        CodeTextBox.TextArea.Caret.BringCaretToView();
        CodeTextBox.Focus();
    }

    private void GoToDefinition()
    {
        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(syntax))
            return;

        var offset = CodeTextBox.CaretOffset;
        var word = GetWordAtOffset(offset);
        if (string.IsNullOrEmpty(word))
            return;

        (int line, int column)? definition = null;

        if (syntax.Contains("1C"))
        {
            var symbols = _bslAnalyzer is BslCodeAnalyzer coreAnalyzer
                ? coreAnalyzer.Symbols
                : [];

            var caretLine = CodeTextBox.TextArea.Caret.Line;
            var caretCol = CodeTextBox.TextArea.Caret.Column;

            var match = symbols.FirstOrDefault(s =>
                s.Name.Equals(word, StringComparison.OrdinalIgnoreCase) &&
                !(s.Line == caretLine && Math.Abs(s.Column - caretCol) <= word.Length));
            if (match != null)
                definition = (match.Line, match.Column);
        }
        else if (syntax.Contains("Python"))
        {
            var code = CodeTextBox.Text;
            definition = FindPythonDefinition(code, word, CodeTextBox.TextArea.Caret.Line);
        }

        if (definition.HasValue)
        {
            NavigateToPosition(definition.Value.line, definition.Value.column);
        }
        else
        {
            _snackbar.Show(CodeTextBox, $"Определение для '{word}' не найдено", NotificationType.Warning, 1.5);
        }
    }

    private static (int line, int column)? FindPythonDefinition(string code, string word, int caretLine)
    {
        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();

            int col = lines[i].Length - trimmed.Length + 1;

            if (trimmed.StartsWith("def ") && trimmed.Length > 4)
            {
                var name = ExtractPythonDefName(trimmed, 4);
                if (name.Equals(word, StringComparison.Ordinal) && i + 1 != caretLine)
                    return (i + 1, col);
            }

            if (trimmed.StartsWith("class ") && trimmed.Length > 6)
            {
                var name = ExtractPythonDefName(trimmed, 6);
                if (name.Equals(word, StringComparison.Ordinal) && i + 1 != caretLine)
                    return (i + 1, col);
            }

            if (trimmed.Contains('=') && !trimmed.StartsWith("def ") && !trimmed.StartsWith("class ")
                && !trimmed.StartsWith("if ") && !trimmed.StartsWith("for ") && !trimmed.StartsWith("while ")
                && !trimmed.StartsWith("with ") && !trimmed.StartsWith("elif ") && !trimmed.StartsWith("except "))
            {
                var eqIdx = trimmed.IndexOf('=');
                var varName = trimmed[..eqIdx].Trim();
                if (varName.Equals(word, StringComparison.Ordinal) && i + 1 != caretLine)
                    return (i + 1, col);
            }
        }

        return null;
    }

    private static string ExtractPythonDefName(string line, int startIndex)
    {
        int end = startIndex;
        while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
            end++;
        return line[startIndex..end];
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

    private void ToggleBreakpointAtCaret()
    {
        if (!IsBslSyntax) return;
        int lineNumber = CodeTextBox.TextArea.Caret.Line;
        if (_activeBreakpoints.Contains(lineNumber))
            _activeBreakpoints.Remove(lineNumber);
        else
            _activeBreakpoints.Add(lineNumber);
        CodeTextBox.TextArea.TextView.Redraw();
        _bookmarkMargin?.Redraw();
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

        if (AnalyzeToggle.IsChecked == true)
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

        if (entry.Extension.Contains(".html"))
        {
            entry.Code = "";
            entry.Description = CodeTextBox.Text;
            if (DescriptionPanel.Visibility == Visibility.Visible)
            {
                SetDescriptionHtml(CodeTextBox.Text);
            }
        }
        else if (entry.Extension.Contains(".md"))
        {
            entry.Code = CodeTextBox.Text;
        }
        else
        {
            entry.Code = CodeTextBox.Text;
        }

        entry.Category = _categoryService.NormalizeCategoryPath(CategoryComboBox.Text);
        entry.Tags = TagsTextBox.Text
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        entry.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;

        if (entry.Extension.Contains(".html"))
        {
            entry.Description = CodeTextBox.Text;
            if (DescriptionPanel.Visibility == Visibility.Visible)
            {
                SetDescriptionHtml(CodeTextBox.Text);
            }
        }

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

        ClearDebugState();
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

        // Ensure code editor shows HTML content for HTML-only entry tabs
        if (tab.Entry != null && (tab.Entry.Extension.Contains(".html") || tab.Entry.Extension.Contains(".md")))
        {
            if (tab.Entry.Extension.Contains(".html") && string.IsNullOrEmpty(tab.Entry.Code) && !string.IsNullOrEmpty(tab.Entry.Description))
            {
                tab.Document.Text = tab.Entry.Description;
                tab.SyntaxName ??= "HTML";
            }

            if (tab.Entry.Extension.Contains(".md"))
            {
                tab.SyntaxName ??= "Markdown";
            }
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
                _isDescriptionEditMode = false;
                EditDescriptionButton.Content = "✏️ Ред.";
                SaveDescriptionButton.Visibility = Visibility.Collapsed;
                TitleTextBox.Text = tab.Entry.Title;
                SetDescriptionHtml(tab.Entry.Description);
                CategoryComboBox.Text = _categoryService.NormalizeCategoryPath(tab.Entry.Category);
                TagsTextBox.Text = string.Join(", ", tab.Entry.Tags);
                _currentEntry = tab.Entry;
                _selectedCategoryPath = _categoryService.NormalizeCategoryPath(tab.Entry.Category);
                if (tab.Entry.Extension.Contains(".html") || tab.Entry.Extension.Contains(".md"))
                {
                    ToggleDescription_Open();
                }
                else
                {
                    ToggleDescription_Close();
                }

                
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
        TitleTextBox.Text = Path.GetFileName(filePath);
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
            ".py" => _currentTheme == AppTheme.Dark ? "Темная Python" : "Стандартная Python",
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

        var start = selection.Segments.First().StartOffset;
        var end = selection.Segments.Last().EndOffset;
        var length = end - start;

        _formattingService.ApplyStyleToSelection(_textSegments, start, length,
            backgroundColor, foregroundColor, bold, italic, underline, fontFamily, fontSize);

        CodeTextBox.TextArea.TextView.Redraw();
    }


    private void UpdateSegmentsAfterTextChange()
    {
        _formattingService.UpdateSegmentsAfterTextChange(_textSegments, CodeTextBox.Document.TextLength);
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


    private void ClearStyleFromSelection()
    {
        var selection = CodeTextBox.TextArea.Selection;
        if (selection.IsEmpty) return;

        var start = selection.Segments.First().StartOffset;
        var end = selection.Segments.Last().EndOffset;

        _formattingService.ClearStyleFromSelection(_textSegments, start, end);
        CodeTextBox.TextArea.TextView.Redraw();
    }


    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog();

        // Настройки диалога
        openFileDialog.Title = "Выберите текстовый файл";
        openFileDialog.Filter = "Текстовые файлы (*.os;*.txt;*.xshd;*.bsl;*.html;*.cs;*.xaml;*.json;*.xml)|*.os;*.txt;*.xshd;*.bsl;*.html;*cs;*.xaml;*.json;*.xml|Все файлы (*.*)|*.*";
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


        DescriptionPanel.Visibility = Visibility.Collapsed;
        DescriptionSplitter.Visibility = Visibility.Collapsed;
        DescriptionRow.Height = new GridLength(0);
        ToggleDescriptionButton.Content = " ▼ Развернуть ";

        RefreshEntriesList();
        _selectedCategoryPath = _categoryService.NormalizeCategoryPath(_appState.SelectedCategory);
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
        var monospaceFonts = new[] { "Cascadia Mono", "Consolas", "Courier New", "Lucida Console", "Cascadia Code", "Fira Code", "JetBrains Mono" };
        foreach (var font in monospaceFonts)
        {
            FontFamilyComboBox.Items.Add(font);
        }
        FontFamilyComboBox.SelectedIndex = 0;

        // Размеры шрифта
        var fontSizes = new[] { 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24 };
        foreach (var size in fontSizes)
        {
            FontSizeComboBox.Items.Add(size);
        }
        FontSizeComboBox.SelectedItem = 14;

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
        else if (selected == "Стандартная HTML")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
        }
        else if (selected == "Стандартная XML")
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        }
        else
        {
            CodeTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }


        UpdateCodeEditorColors(selected);
        RunScriptButton.IsEnabled = selected != null && (selected.Contains("1C") || selected.Contains("Python"));
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
                SyntaxHighlightingComboBox?.Items.Add("Стандартная HTML");
                SyntaxHighlightingComboBox?.Items.Add("Стандартная XML");
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

        UpdateWebViewTheme();

    }

    private void UpdateWebViewTheme()
    {
        if (!_descriptionWebView2Ready) return;
        string bgColor = _currentTheme == AppTheme.Dark ? "#1E1E1E" : "#FFFFFF";
        string textColor = _currentTheme == AppTheme.Dark ? "#EDF2F7" : "#1F1F1F";
        string tbBgColor = _currentTheme == AppTheme.Dark ? "#2D2D2D" : "#F0F0F0";
        string tbBorderColor = _currentTheme == AppTheme.Dark ? "#555" : "#CCC";
        string js = $@"
document.body.style.backgroundColor='{bgColor}';
document.body.style.color='{textColor}';
var c=document.getElementById('__content');
if(c){{c.style.backgroundColor='{bgColor}';c.style.color='{textColor}';}}
var tb=document.getElementById('__fmt_toolbar');
if(tb){{tb.style.background='{tbBgColor}';tb.style.borderBottom='1px solid {tbBorderColor}';}}";
        _ = DescriptionBrowser.CoreWebView2.ExecuteScriptAsync(js);
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





    private void RefreshCategoryFilter()
    {
        if (CategoryComboBox != null)
        {
            CategoryComboBox.ItemsSource = _categoryService.GetKnownCategoryPaths(_data.Entries, _data.Categories);
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
        var tree = _categoryService.BuildCategoryTree(_filteredEntries, _data.Categories);

        if (string.IsNullOrWhiteSpace(searchText) || searchText == "поиск...")
        {
            _categoryService.ApplyExpansionState(_expandedCategories, tree);
        }

        EntriesTreeView.ItemsSource = tree;

        if (!string.IsNullOrWhiteSpace(searchText) && searchText != "поиск...")
        {
            foreach (var node in tree)
            {
                _categoryService.ExpandIfHasEntries(node);
            }
        }

        if (itemToSelect != null)
        {
            if (itemToSelect is CodeEntry entry)
            {
                var viewEntry = _categoryService.FindEntryViewModel(tree, entry.Id);
                if (viewEntry != null)
                {
                    _categoryService.ExpandAncestors(tree, _categoryService.NormalizeCategoryPath(entry.Category));
                    viewEntry.IsSelected = true;
                }
            }
            else if (itemToSelect is string categoryPath)
            {
                var targetNode = _categoryService.FindCategoryNode(tree, categoryPath);
                if (targetNode != null)
                {
                    _categoryService.ExpandAncestors(tree, categoryPath);
                    targetNode.IsSelected = true;
                }
            }
        }
    }

    private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        RefreshEntriesList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitialized)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
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
            var categoryPath = _categoryService.NormalizeCategoryPath(viewEntry.Entry.Category);
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

        HandleDragScroll(e);
    }

    private void HandleDragScroll(DragEventArgs e)
    {
        var pos = e.GetPosition(EntriesTreeView);
        const double margin = 40;
        const double speed = 10;

        var sv = FindScrollViewer(EntriesTreeView);
        if (sv == null) return;

        if (pos.Y < margin)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - speed);
        }
        else if (pos.Y > EntriesTreeView.ActualHeight - margin)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset + speed);
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject dep)
    {
        if (dep is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var result = FindScrollViewer(VisualTreeHelper.GetChild(dep, i));
            if (result != null) return result;
        }
        return null;
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
            var destinationName = _categoryService.GetCategorySegmentName(sourceCategory.FullPath);
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
            destinationPath = _categoryService.NormalizeCategoryPath(targetEntry.Entry.Category);
        }
        else
        {
            destinationPath = string.Empty;
        }

        if (draggedItem is CategoryNode draggedCategory)
        {
            var draggedPath = _categoryService.NormalizeCategoryPath(draggedCategory.FullPath);
            if (string.IsNullOrWhiteSpace(draggedPath))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(destinationPath) &&
                _categoryService.IsPathWithin(destinationPath, draggedPath))
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
        _updateDescription = entry.Extension.Contains(".html") || entry.Extension.Contains(".md");
       
        try
        {
            _isDescriptionEditMode = false;
            EditDescriptionButton.Content = "✏️ Ред.";
            SaveDescriptionButton.Visibility = Visibility.Collapsed;
            TitleTextBox.Text = entry.Title;
            SetDescriptionHtml(entry.Description);
            CategoryComboBox.Text = _categoryService.NormalizeCategoryPath(entry.Category);
            TagsTextBox.Text = string.Join(", ", entry.Tags);

            _selectedCategoryPath = _categoryService.NormalizeCategoryPath(entry.Category);
            RefreshCategoryFilter();

            if (!string.IsNullOrEmpty(entry.Code) || _updateDescription)
            {
                ToggleDescription_Close();
                var entryTab = OpenEntryTab(entry);
                ActivateEditorTab(entryTab);
                _currentEntry = entryTab.Entry;

                if (_updateDescription && string.IsNullOrEmpty(entry.Code))
                {
                    entryTab.Document.Text = entry.Description;
                    entryTab.SyntaxName = "HTML";
                    SyntaxHighlightingComboBox.SelectedItem = "Стандартная HTML";
                    ToggleDescription_Open();
                }
                else if (entry.Extension.Contains(".md"))
                {
                    ToggleDescription_Open();
                }

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
        var currentCategory = _categoryService.NormalizeCategoryPath(CategoryComboBox.Text);
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

    private void SaveSegmentsToEntry()
    {
        if (_currentEntry != null)
        {
            _formattingService.SaveSegmentsToEntry(_currentEntry, _textSegments);
        }
    }

    private void LoadSegmentsFromEntry()
    {
        _formattingService.LoadSegmentsFromEntry(_currentEntry, _textSegments);
        if (_currentEntry != null)
        {
            CodeTextBox.TextArea.TextView.Redraw();
        }
    }

    private void LoadSegmentsIntoTab(EditorTabModel tab, CodeEntry entry)
    {
        _formattingService.LoadSegmentsIntoTab(tab, entry);
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
                Category = _categoryService.NormalizeCategoryPath(CategoryComboBox.Text),
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

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            ShowAlert("Введите название", isError: true);
            return;
        }

        CodeEntry entryToUpdate;
        if (activeTab?.Entry != null)
        {
            entryToUpdate = activeTab.Entry;
        }
        else
        {
            var tabTitle = activeTab?.Title;
            if (string.IsNullOrWhiteSpace(tabTitle))
            {
                ShowAlert("Нет активной закладки", isError: true);
                return;
            }
            entryToUpdate = _data.Entries.FirstOrDefault(e => e.Title == tabTitle);
            if (entryToUpdate == null)
            {
                ShowAlert("Запись не найдена", isError: true);
                return;
            }
        }

        entryToUpdate.Title = TitleTextBox.Text;
        var isHtmlEntry = entryToUpdate.Extension.Contains(".html");
        var isMdEntry = entryToUpdate.Extension.Contains(".md") || entryToUpdate.Extension.Contains(".markdown");

        if (isHtmlEntry)
        {
            entryToUpdate.Code = "";
            entryToUpdate.Description = CodeTextBox.Text;
        }
        else if (isMdEntry)
        {
            entryToUpdate.Code = CodeTextBox.Text;
            entryToUpdate.Description = MarkdownConverter.ToHtml(CodeTextBox.Text);
        }
        else
        {
            entryToUpdate.Code = CodeTextBox.Text;
        }
        entryToUpdate.Category = _categoryService.NormalizeCategoryPath(CategoryComboBox.Text);
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

        _categoryService.EnsureCategoryPathExists(entryToUpdate.Category, _data.Categories);

        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList(entryToUpdate);

        _snackbar.Show(CodeTextBox, "Запись сохранена", NotificationType.Success, 1.5);


    }




    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        var checkedEntries = new List<CodeEntryViewModel>();
        var checkedCategories = new List<CategoryNode>();

        _categoryService.FindCheckedItems(EntriesTreeView.ItemsSource as IEnumerable<object>, checkedEntries, checkedCategories);

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
                var categoryPath = _categoryService.NormalizeCategoryPath(categoryNode.FullPath);
                var entriesToDelete = _data.Entries
                    .Where(entry => _categoryService.IsPathWithin(_categoryService.NormalizeCategoryPath(entry.Category), categoryPath))
                    .ToList();
                var categoriesToDelete = _data.Categories
                    .Where(category => _categoryService.IsPathWithin(_categoryService.NormalizeCategoryPath(category), categoryPath))
                    .ToList();

                var deletedEntryIds = entriesToDelete.Select(entry => entry.Id).ToHashSet();
                var categoriesToDeleteSet = categoriesToDelete
                    .Select(_categoryService.NormalizeCategoryPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _data.Categories.RemoveAll(existing => categoriesToDeleteSet.Contains(_categoryService.NormalizeCategoryPath(existing)));
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
        // Проверяем несохранённые изменения
        var dirtyTabs = _editorTabs.Where(t => t.IsDirty).ToList();
        if (dirtyTabs.Any())
        {
            ActivateEditorTab(dirtyTabs[0]);

            string message = dirtyTabs.Count == 1
                ? $"Вкладка \"{dirtyTabs[0].DisplayName}\" содержит несохранённые изменения. Закрыть без сохранения?"
                : $"{dirtyTabs.Count} вкладок содержат несохранённые изменения. Закрыть без сохранения?";

            if (!CustomMessageBox.ShowQuestion(message, "Несохранённые изменения"))
            {
                e.Cancel = true;
                return;
            }
        }

        StopTerminalProcess();

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
            ToggleDescription_Close();
        }
        else
        {
            ToggleDescription_Open();
            InitializeWebView2();
        }
    }



    private void ToggleDescription_Close()
    {
        _descriptionWebView2Ready = false;
        DescriptionPanel.Visibility = Visibility.Collapsed;
        DescriptionSplitter.Visibility = Visibility.Collapsed;
        DescriptionRow.Height = new GridLength(0);
        ToggleDescriptionButton.Content = " ▼ Развернуть ";
        _updateDescription = false;
    }


    private void ToggleDescription_Open()
    {
        DescriptionPanel.Visibility = Visibility.Visible;
        DescriptionSplitter.Visibility = Visibility.Visible;
        DescriptionRow.Height = new GridLength(350);
        ToggleDescriptionButton.Content = " ▲ Свернуть ";
        _updateDescription = false;

        // Загружаем актуальное описание из активного таба
        var activeTab = GetActiveEditorTab();
        if (activeTab != null)
        {
            string? html;
            if (activeTab.Entry != null)
                html = activeTab.Entry.Description;
            else if (activeTab.IsFromFile && activeTab.FilePath != null &&
                     Path.GetExtension(activeTab.FilePath).Equals(".html", StringComparison.OrdinalIgnoreCase))
                html = activeTab.Document.Text;
            else
                html = null;

            SetDescriptionHtml(html);
        }

        InitializeWebView2();
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
            var viewEntry = _categoryService.FindEntryViewModel(categories, entryId);
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

        var exportPath = CategoryService.EnsureExportExtension(saveFileDialog.FileName, saveFileDialog.FilterIndex);
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

    private async void InitializeWebView2()
    {
        if (_descriptionWebView2Ready) return;
        try
        {
            await DescriptionBrowser.EnsureCoreWebView2Async();
            DescriptionBrowser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            DescriptionBrowser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _descriptionWebView2Ready = true;
        }
        catch { }
    }

    private async System.Threading.Tasks.Task EnsureWebView2ReadyAsync()
    {
        if (_descriptionWebView2Ready) return;
        await DescriptionBrowser.EnsureCoreWebView2Async();
        DescriptionBrowser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        DescriptionBrowser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _descriptionWebView2Ready = true;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.TryGetWebMessageAsString();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            if (type == "pic")
            {
                var ofd = new OpenFileDialog
                {
                    Filter = "Изображения (*.png;*.jpg;*.jpeg;*.gif;*.svg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.svg;*.webp;*.bmp|Все файлы (*.*)|*.*",
                    Title = "Выберите изображение"
                };
                if (ofd.ShowDialog() == true)
                {
                    byte[] bytes = await System.IO.File.ReadAllBytesAsync(ofd.FileName);
                    string ext = Path.GetExtension(ofd.FileName).TrimStart('.').ToLower();
                    ext = ext switch
                    {
                        "jpg" => "jpeg",
                        "svg" => "svg+xml",
                        _ => ext
                    };
                    string b64 = Convert.ToBase64String(bytes);
                    string dataUri = $"data:image/{ext};base64,{b64}";
                    string js = $"document.execCommand('insertImage',false,'{dataUri.Replace("'", "\\'")}')";
                    await DescriptionBrowser.CoreWebView2.ExecuteScriptAsync(js);
                }
            }
            else if (type == "selection")
            {
                _lastBrowserSelectedText = root.GetProperty("text").GetString() ?? "";
                _browserSelectionDebounceTimer.Stop();
                _browserSelectionDebounceTimer.Start();
            }
        }
        catch { }
    }

    private async void SetDescriptionHtml(string? html)
    {
        try
        {
            await EnsureWebView2ReadyAsync();

            int seq = ++_navigationSequence;

            string bgColor = _currentTheme == AppTheme.Dark ? "#1E1E1E" : "#FFFFFF";
            string textColor = _currentTheme == AppTheme.Dark ? "#EDF2F7" : "#1F1F1F";

            string fullHtml;
            if (string.IsNullOrWhiteSpace(html))
            {
                fullHtml = $"<html><head><meta charset='utf-8'><style>body{{background:{bgColor};color:{textColor};font-family:'Segoe UI',sans-serif;font-size:12px;margin:0;padding:0;}}#__content{{min-height:150px;padding:5px;}}</style></head><body><div id='__content'></div></body></html>";
            }
            else
            {
                string style = $"<style>body{{background:{bgColor};color:{textColor};font-family:'Segoe UI',sans-serif;font-size:12px;margin:0;padding:0;}}#__content{{min-height:150px;padding:5px;}}</style>";
                fullHtml = $"<html><head><meta charset='utf-8'>{style}</head><body><div id='__content'>{html}</div></body></html>";
            }

            DescriptionBrowser.CoreWebView2.NavigationCompleted -= OnDescriptionNavigationCompleted;
            DescriptionBrowser.CoreWebView2.NavigationCompleted += OnDescriptionNavigationCompleted;
            DescriptionBrowser.NavigateToString(fullHtml);
        }
        catch { }
    }

    private async void OnDescriptionNavigationCompleted(object? sender, object? e)
    {
        try
        {
            int seq = _navigationSequence;
            DescriptionBrowser.CoreWebView2.NavigationCompleted -= OnDescriptionNavigationCompleted;
            if (seq != _navigationSequence) return;

            string js = GetFormatToolbarRemoveScript() + "var c=document.getElementById('__content');if(c)c.contentEditable=false;";
            if (_isDescriptionEditMode)
            {
                js += "var c=document.getElementById('__content');if(c){c.contentEditable=true;}" + GetFormatToolbarInjectScript();
            }
            await DescriptionBrowser.CoreWebView2.ExecuteScriptAsync(js);

            // Внедряем слушатель выделения текста в браузере для синхронизации с редактором
            string selJs = @"
(function(){
    if (window.__browserSelectionListener) return;
    window.__browserSelectionListener = true;
    document.addEventListener('selectionchange', function(){
        clearTimeout(window.__bselTimer);
        window.__bselTimer = setTimeout(function(){
            var s = window.getSelection().toString();
            window.chrome.webview.postMessage(JSON.stringify({type:'selection', text:s}));
        }, 150);
    });
})();
";
            await DescriptionBrowser.CoreWebView2.ExecuteScriptAsync(selJs);
        }
        catch { }
    }

    private void EditDescriptionButton_Click(object sender, RoutedEventArgs e)
    {
        _isDescriptionEditMode = !_isDescriptionEditMode;
        EditDescriptionButton.Content = _isDescriptionEditMode ? "✏️ Просмотр" : "✏️ Ред.";
        SaveDescriptionButton.Visibility = _isDescriptionEditMode ? Visibility.Visible : Visibility.Collapsed;

        if (_descriptionWebView2Ready)
        {
            string js = _isDescriptionEditMode
                ? $@"var c=document.getElementById('__content');if(c){{c.contentEditable=true;}}{GetFormatToolbarInjectScript()}"
                : $@"{GetFormatToolbarRemoveScript()}var c=document.getElementById('__content');if(c)c.contentEditable=false;";
            _ = DescriptionBrowser.CoreWebView2.ExecuteScriptAsync(js);
        }
        else
        {
            SetDescriptionHtml(GetCurrentEntryDescription());
        }
    }

    private string GetFormatToolbarInjectScript()
    {
        bool isDark = _currentTheme == AppTheme.Dark;
        string tbBg = isDark ? "#2D2D2D" : "#F0F0F0";
        string tbBorder = isDark ? "#555" : "#CCC";
        string btnBg = isDark ? "#3C3C3C" : "#FFF";
        string btnBorder = isDark ? "#666" : "#BBB";
        string btnText = isDark ? "#EDF2F7" : "#1F1F1F";
        string sepBg = isDark ? "#555" : "#CCC";

        return $@"
(function(){{
    if (document.getElementById('__fmt_toolbar')) return;
    var tb = document.createElement('div');
    tb.id = '__fmt_toolbar';
    tb.style.cssText = 'position:sticky;top:0;z-index:9999;background:{tbBg};border-bottom:1px solid {tbBorder};padding:3px 5px;font-family:Segoe UI,sans-serif;font-size:13px;display:flex;flex-wrap:wrap;gap:2px;align-items:center;user-select:none;';
    var bstyle = 'padding:2px 6px;border:1px solid {btnBorder};border-radius:2px;background:{btnBg};cursor:pointer;font-size:12px;line-height:1.2;color:{btnText};margin:0;';
    function addBtn(text, title, fn) {{
        var btn = document.createElement('button');
        btn.textContent = text;
        btn.title = title;
        btn.style.cssText = bstyle;
        btn.addEventListener('click', fn);
        tb.appendChild(btn);
    }}
    function addSep() {{
        var s = document.createElement('span');
        s.style.cssText = 'width:1px;height:18px;background:{sepBg};margin:0 2px;display:inline-block;';
        tb.appendChild(s);
    }}
    addBtn('B','Жирный',function(){{document.execCommand('bold');this.blur()}});
    addBtn('I','Курсив',function(){{document.execCommand('italic');this.blur()}});
    addBtn('U','Подчёркнутый',function(){{document.execCommand('underline');this.blur()}});
    addSep();
    addBtn('H1','Заголовок 1',function(){{document.execCommand('formatBlock',false,'<h1>')}});
    addBtn('H2','Заголовок 2',function(){{document.execCommand('formatBlock',false,'<h2>')}});
    addBtn('H3','Заголовок 3',function(){{document.execCommand('formatBlock',false,'<h3>')}});
    addSep();
    addBtn('•','Маркированный список',function(){{document.execCommand('insertUnorderedList');this.blur()}});
    addBtn('1.','Нумерованный список',function(){{document.execCommand('insertOrderedList');this.blur()}});
    addSep();
    addBtn('🔗','Ссылка',function(){{var u=prompt('URL:','https://');if(u)document.execCommand('createLink',false,u);this.blur()}});
    addBtn('🖼','Изображение',function(){{window.chrome.webview.postMessage(JSON.stringify({{type:'pic'}}))}});
    addSep();
    var paletteColors = ['#000000','#434343','#666666','#999999','#B7B7B7','#CCCCCC','#D9D9D9','#EFEFEF','#F3F3F3','#FFFFFF','#980000','#FF0000','#FF6D01','#FFFF00','#00FF00','#00B0F0','#0070C0','#002060','#7030A0','#A64D79','#E6B8AF','#F4CCCC','#FCE5CD','#FFF2CC','#D9EAD3','#D0E0E3','#C9DAF8','#CFE2F3','#D9D2E9','#EAD1DC','#DD7A6B','#EA9999','#F9CB9C','#FFE599','#B6D7A8','#A2C4C9','#A4C2F4','#9FC5E8','#B4A7D6','#D5A6BD','#CC4125','#E06666','#F6B26B','#FFD966','#93C47D','#76A5AF','#6D9EEB','#6FA8DC','#8E7CC3','#C27BA0','#A61C00','#CC0000','#E69138','#F1C232','#6AA84F','#45818E','#3C78D8','#3D85C6','#674EA7','#A64D79','#85200C','#990000','#B45F06','#BF9000','#38761D','#134F5C','#1155CC','#0B5394','#351C75','#741B47'];
    var palette = document.createElement('div');
    palette.id = '__color_palette';
    palette.style.cssText = 'position:fixed;top:40px;right:20px;z-index:99999;display:none;background:{tbBg};border:1px solid {tbBorder};border-radius:4px;padding:4px;box-shadow:0 4px 12px rgba(0,0,0,.3);';
    var paletteGrid = document.createElement('div');
    paletteGrid.style.cssText = 'display:grid;grid-template-columns:repeat(10,18px);gap:2px;';
    function applyColor(color){{
        var sel=window.getSelection();
        sel.removeAllRanges();
        if(window.__savedRange){{sel.addRange(window.__savedRange);}}
        document.execCommand(window.__colorTarget||'foreColor',false,color);
        window.__savedRange=null;
        palette.style.display='none';
    }}
    paletteColors.forEach(function(c){{
        var sw = document.createElement('div');
        sw.style.cssText = 'width:18px;height:18px;background:'+c+';border:1px solid {btnBorder};border-radius:2px;cursor:pointer;';
        sw.addEventListener('click',function(){{applyColor(c);}});
        paletteGrid.appendChild(sw);
    }});
    palette.appendChild(paletteGrid);
    var cp = document.createElement('input');
    cp.type = 'color'; cp.style.cssText = 'width:100%;margin-top:4px;border:none;padding:0;height:20px;cursor:pointer;';
    cp.addEventListener('change',function(){{applyColor(this.value);}});
    palette.appendChild(cp);
    document.body.appendChild(palette);
    document.addEventListener('click',function(e){{
        if(!e.target.closest('#__color_palette')&&!e.target.closest('#__fmt_toolbar')) palette.style.display='none';
    }});
    function showPalette(btn,target){{
        var sel=window.getSelection();
        window.__savedRange=sel.rangeCount>0?sel.getRangeAt(0):null;
        window.__colorTarget=target;
        var r=btn.getBoundingClientRect();
        palette.style.top=(r.bottom+4)+'px';palette.style.left=r.left+'px';palette.style.display='block';
        btn.blur();
    }}
    addBtn('A','Цвет текста',function(){{showPalette(this,'foreColor');}});
    addBtn('▨','Цвет фона',function(){{showPalette(this,'hiliteColor');}});
    var content = document.getElementById('__content'); if(content) content.parentNode.insertBefore(tb, content);
}})();
";
    }

    private static string GetFormatToolbarRemoveScript()
    {
        return @"var el=document.getElementById('__fmt_toolbar');if(el)el.remove();";
    }

    private async void SaveDescriptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_descriptionWebView2Ready) return;

        string? html = null;
        try
        {
            string? result = await DescriptionBrowser.CoreWebView2.ExecuteScriptAsync("document.getElementById('__content').innerHTML");
            if (!string.IsNullOrEmpty(result))
            {
                html = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                html = html?.Trim();
            }
        }
        catch { }

        if (string.IsNullOrEmpty(html)) return;

        var activeTab = GetActiveEditorTab();
        if (activeTab == null) return;

        if (activeTab.Entry != null)
        {
            activeTab.Entry.Description = html;
        }

        bool isHtmlTab = (activeTab.IsFromFile && activeTab.FilePath != null &&
            Path.GetExtension(activeTab.FilePath).Equals(".html", StringComparison.OrdinalIgnoreCase))
            || (activeTab.Entry != null && activeTab.Entry.Extension.Contains(".html"));

        if (isHtmlTab)
        {
            activeTab.Document.Text = html;
        }

        activeTab.IsDirty = true;

        await _dataService.SaveDataAsync(_data);

        _isDescriptionEditMode = false;
        EditDescriptionButton.Content = "✏️ Ред.";
        SaveDescriptionButton.Visibility = Visibility.Collapsed;

        SetDescriptionHtml(html);

        _snackbar.Show(CodeTextBox, "Описание сохранено", NotificationType.Success, 1.5);
    }

    private string? GetCurrentEntryDescription()
    {
        return GetActiveEditorTab()?.Entry?.Description;
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
        string baseCategory = _categoryService.NormalizeCategoryPath(_selectedCategoryPath);

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

            category = _categoryService.NormalizeCategoryPath(category);
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

            category = _categoryService.NormalizeCategoryPath(category);
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

        _categoryService.NormalizeImportedData(_data);
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
                _categoryService.NormalizeImportedData(_data);
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
            : _categoryService.NormalizeCategoryPath(_currentEntry?.Category);

        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            ShowAlert("Выберите категорию для экспорта", isError: true);
            return;
        }

        await ExportCategoryAsync(categoryPath);
    }

    private async Task ExportCategoryAsync(string categoryPath)
    {
        var normalizedCategoryPath = _categoryService.NormalizeCategoryPath(categoryPath);
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
            FileName = CategoryService.SanitizeFileName(_categoryService.GetCategorySegmentName(normalizedCategoryPath))
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var exportPath = CategoryService.EnsureExportExtension(saveFileDialog.FileName, saveFileDialog.FilterIndex);
        var exportData = _categoryService.BuildCategoryExportData(normalizedCategoryPath, _data);

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

    private async void ImportCategory_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Импорт категории",
            Filter = "JSON (*.json)|*.json|XML (*.xml)|*.xml",
            FilterIndex = 1,
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var importedData = await _dataService.ImportDataAsync(openFileDialog.FileName);
            if (importedData == null || (importedData.Entries.Count == 0 && importedData.Categories.Count == 0))
            {
                ShowAlert("Файл не содержит данных категории", isError: true);
                return;
            }

            _categoryService.NormalizeImportedData(importedData);

            var existingIds = new HashSet<Guid>(_data.Entries.Select(e => e.Id));
            foreach (var entry in importedData.Entries)
            {
                if (!existingIds.Contains(entry.Id))
                {
                    _data.Entries.Add(entry);
                }
            }

            foreach (var category in importedData.Categories)
            {
                _categoryService.EnsureCategoryPathExists(category, _data.Categories);
            }

            _categoryService.NormalizeImportedData(_data);
            await _dataService.SaveDataAsync(_data);

            _currentEntry = null;
            _selectedCategoryPath = string.Empty;
            ClearEditingForm();
            RefreshEntriesList();
            _snackbar.Show(CodeTextBox, $"Категория импортирована из '{Path.GetFileName(openFileDialog.FileName)}'", NotificationType.Success, 1.5);
        }
        catch (Exception ex)
        {
            ShowAlert($"Ошибка импорта категории: {ex.Message}", isError: true);
        }
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

        var enteredName = _categoryService.NormalizeCategoryPath(dialog.CategoryName);
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
            : $"{_categoryService.NormalizeCategoryPath(parentPath)}{CategorySeparator}{enteredName}";

        if (_data.Categories.Any(existing => string.Equals(_categoryService.NormalizeCategoryPath(existing), newCategoryPath, StringComparison.CurrentCultureIgnoreCase)))
        {
            ShowAlert($"Категория '{newCategoryPath}' уже существует", isError: true);
            return;
        }

        _categoryService.EnsureCategoryPathExists(newCategoryPath, _data.Categories);

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
            var targetNode = _categoryService.FindCategoryNode(categories, categoryPath);
            if (targetNode != null)
            {
                _categoryService.ExpandAncestors(categories, categoryPath);
                targetNode.IsSelected = true;
                targetNode.IsExpanded = true;
            }
        }
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
        var currentPath = _categoryService.NormalizeCategoryPath(categoryNode.FullPath);
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return;
        }

        var dialog = new CategoryInputDialog(
            "Переименовать категорию",
            $"Новое имя для '{currentPath}':",
            _categoryService.GetCategorySegmentName(currentPath));
        dialog.Owner = this;

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newName = _categoryService.NormalizeCategoryPath(dialog.CategoryName);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        if (newName.Contains(CategorySeparator, StringComparison.Ordinal))
        {
            ShowAlert($"Имя категории не должно содержать '{CategorySeparator}'", isError: true);
            return;
        }

        var parentPath = _categoryService.GetParentCategoryPath(currentPath);
        var newPath = string.IsNullOrWhiteSpace(parentPath)
            ? newName
            : $"{parentPath}{CategorySeparator}{newName}";

        await MoveCategoryAsync(currentPath, parentPath, newName);
        _snackbar.Show(CodeTextBox, $"Категория переименована в '{newPath}'", NotificationType.Success, 1.5);
    }

    private async Task MoveCategoryAsync(string sourcePath, string destinationParentPath, string newName)
    {
        var normalizedSourcePath = _categoryService.NormalizeCategoryPath(sourcePath);
        var normalizedDestinationParentPath = _categoryService.NormalizeCategoryPath(destinationParentPath);
        var normalizedNewName = _categoryService.NormalizeCategoryPath(newName);

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

        if (_categoryService.IsCategoryPathTakenByAnotherNode(normalizedSourcePath, newPath, _data.Categories))
        {
            ShowAlert($"Категория '{newPath}' уже существует", isError: true);
            return;
        }

        _categoryService.RemapCategoryPath(normalizedSourcePath, newPath, _data);
        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();
        RestoreSelectionAfterCategoryMove(normalizedSourcePath, newPath);
    }

    private async Task MoveEntryAsync(CodeEntryViewModel entryViewModel, string destinationCategoryPath)
    {
        var normalizedDestination = _categoryService.NormalizeCategoryPath(destinationCategoryPath);
        var entry = entryViewModel.Entry;
        var normalizedSource = _categoryService.NormalizeCategoryPath(entry.Category);

        if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        entry.Category = normalizedDestination;
        _categoryService.EnsureCategoryPathExists(normalizedDestination, _data.Categories);
        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();
        _currentEntry = entry;
        LoadEntryToForm(entry);
    }

    private void RestoreSelectionAfterCategoryMove(string oldPath, string newPath)
    {
        if (string.Equals(_selectedCategoryPath, oldPath, StringComparison.OrdinalIgnoreCase) ||
            _selectedCategoryPath.StartsWith($"{oldPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
        {
            _selectedCategoryPath = _categoryService.ReplaceCategoryPrefix(_selectedCategoryPath, oldPath, newPath);
            CategoryComboBox.Text = _selectedCategoryPath;
        }

        if (_currentEntry != null && _categoryService.IsPathWithin(_currentEntry.Category, oldPath))
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

    private async void ExportCategoryToDisk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not CategoryNode categoryNode)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для выгрузки категории"
        };

        if (dialog.ShowDialog() != true)
            return;

        string categoryName = !string.IsNullOrWhiteSpace(categoryNode.Name)
            ? categoryNode.Name
            : "Корневая категория";

        string targetPath = Path.Combine(dialog.FolderName, SanitizeFileName(categoryName));
        Directory.CreateDirectory(targetPath);

        await ExportCategoryRecursiveAsync(categoryNode, targetPath);

        _snackbar.Show(CodeTextBox, $"Категория выгружена: {CategoryNodeCountString(categoryNode)} записей в '{targetPath}'", NotificationType.Success, 3);
    }

    private async Task ExportCategoryRecursiveAsync(CategoryNode node, string folderPath)
    {
        foreach (var entryVm in node.Entries)
        {
            var entry = entryVm.Entry;
            string fileName = SanitizeFileName(entry.Title);
            string extension = !string.IsNullOrWhiteSpace(entry.Extension)
                ? entry.Extension
                : ".txt";
            if (!extension.StartsWith('.'))
                extension = "." + extension;

            if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(entry.Code) && !string.IsNullOrEmpty(entry.Description))
            {
                fileName += extension;
                var filePath = Path.Combine(folderPath, fileName);
                await File.WriteAllTextAsync(filePath, entry.Description);
            }
            else if ((extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)) &&
                     !string.IsNullOrEmpty(entry.Code))
            {
                fileName += extension;
                var filePath = Path.Combine(folderPath, fileName);
                await File.WriteAllTextAsync(filePath, entry.Code);
            }
            else
            {
                fileName += extension;
                var filePath = Path.Combine(folderPath, fileName);
                await File.WriteAllTextAsync(filePath, entry.Code ?? string.Empty);
            }
        }

        foreach (var child in node.Children)
        {
            string childFolderName = SanitizeFileName(child.Name);
            string childPath = Path.Combine(folderPath, childFolderName);
            Directory.CreateDirectory(childPath);
            await ExportCategoryRecursiveAsync(child, childPath);
        }
    }

    private async void ImportCategoryFromDisk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not CategoryNode categoryNode)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для загрузки категории"
        };

        if (dialog.ShowDialog() != true)
            return;

        string baseCategory = !string.IsNullOrWhiteSpace(categoryNode.FullPath)
            ? categoryNode.FullPath
            : string.Empty;

        int imported = 0;
        ImportCategoryFromDiskRecursive(dialog.FolderName, baseCategory, ref imported);

        if (imported > 0)
        {
            _categoryService.NormalizeImportedData(_data);
            await _dataService.SaveDataAsync(_data);
            RefreshEntriesList();
            _snackbar.Show(CodeTextBox, $"Загружено {imported} записей в категорию '{categoryNode.Name}'", NotificationType.Success, 3);
        }
        else
        {
            ShowAlert("Файлы для импорта не найдены", isError: true);
        }
    }

    private void ImportCategoryFromDiskRecursive(string folderPath, string parentCategory, ref int imported)
    {
        var files = Directory.GetFiles(folderPath);
        foreach (var filePath in files)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string title = Path.GetFileNameWithoutExtension(filePath);

            if (string.IsNullOrWhiteSpace(title))
                continue;

            string content = File.ReadAllText(filePath);
            string category = parentCategory;

            var entry = new CodeEntry
            {
                Title = title,
                Extension = extension,
                Category = category,
                Syntax = DetectSyntax(extension),
                Tags = new List<string>(),
                Bookmarks = new List<int>(),
                FormattingData = string.Empty
            };

            if (extension == ".html")
            {
                entry.Code = "";
                entry.Description = content;
            }
            else if (extension == ".md" || extension == ".markdown")
            {
                entry.Code = content;
                entry.Description = MarkdownConverter.ToHtml(content);
            }
            else
            {
                entry.Code = content;
                entry.Description = "";
            }

            _data.Entries.Add(entry);
            imported++;
        }

        var dirs = Directory.GetDirectories(folderPath);
        foreach (var dir in dirs)
        {
            string dirName = Path.GetFileName(dir);
            string subCategory = string.IsNullOrWhiteSpace(parentCategory)
                ? dirName
                : $"{parentCategory}/{dirName}";

            _categoryService.EnsureCategoryPathExists(subCategory, _data.Categories);
            ImportCategoryFromDiskRecursive(dir, subCategory, ref imported);
        }
    }

    private static string DetectSyntax(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".bsl" or ".os" => "Стандартная 1C",
            ".html" or ".htm" => "Стандартная HTML",
            ".xml" => "Стандартная XML",
            ".py" => "Стандартная Python",
            ".js" => "JavaScript",
            ".css" => "CSS",
            ".json" => "JSON",
            ".sql" => "SQL",
            ".md" => "Markdown",
            _ => ""
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private static string CategoryNodeCountString(CategoryNode node)
    {
        int count = node.Entries.Count;
        foreach (var child in node.Children)
        {
            count += child.Entries.Count;
        }
        return count.ToString();
    }

    private async Task DeleteCategoryAsync(CategoryNode categoryNode)
    {
        var categoryPath = _categoryService.NormalizeCategoryPath(categoryNode.FullPath);
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            ShowAlert("Нельзя удалить корневой узел без категории", isError: true);
            return;
        }

        var entriesToDelete = _data.Entries
            .Where(entry => _categoryService.IsPathWithin(_categoryService.NormalizeCategoryPath(entry.Category), categoryPath))
            .ToList();

        var categoriesToDelete = _data.Categories
            .Where(category => _categoryService.IsPathWithin(_categoryService.NormalizeCategoryPath(category), categoryPath))
            .ToList();


        if (CustomMessageBox.ShowQuestion($"Удалить категорию '{categoryPath}' и все вложенные категории?\n\nБудет удалено категорий: {categoriesToDelete.Count}\nБудет удалено записей: {entriesToDelete.Count}", "Подтверждение"))

        {
            // Находим соседа ПЕРЕД удалением
            object? neighborData = GetNeighborData(categoryNode);

            var deletedEntryIds = entriesToDelete.Select(entry => entry.Id).ToHashSet();
            var categoriesToDeleteSet = categoriesToDelete
                .Select(p => _categoryService.NormalizeCategoryPath(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _data.Categories.RemoveAll(existing => categoriesToDeleteSet.Contains(_categoryService.NormalizeCategoryPath(existing)));
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
            else if (_currentEntry != null && _categoryService.IsPathWithin(_currentEntry.Category, categoryPath))
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

    private async void FormatCode_Click(object sender, RoutedEventArgs e)
    {
        var selectedSyntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        if (selectedSyntax == null) return;

        if (selectedSyntax.Contains("C#"))
        {
            try
            {
                _roslynCompletionService.UpdateCode(CodeTextBox.Text);
                var formattedText = await _roslynCompletionService.FormatCodeAsync();

                if (!string.IsNullOrEmpty(formattedText))
                {
                    CodeTextBox.Document.Replace(0, CodeTextBox.Document.TextLength, formattedText);
                    _snackbar.Show(CodeTextBox, "Код C# успешно отформатирован", NotificationType.Success, 1.5);
                }
            }
            catch (Exception ex)
            {
                _snackbar.Show(CodeTextBox, $"Ошибка форматирования: {ex.Message}", NotificationType.Error, 3);
            }
        }
        else if (selectedSyntax.Contains("1C"))
        {
            Format1CCode();
        }
        else if (selectedSyntax.Contains("Python"))
        {
            FormatPythonCode();
        }
        else
        {
            _snackbar.Show(CodeTextBox, "Форматирование для данного синтаксиса не поддерживается", NotificationType.Warning, 2);
        }
    }

    private static readonly Dictionary<string, (string? Line, string? BlockStart, string? BlockEnd)> CommentRules = new(StringComparer.OrdinalIgnoreCase)
    {
        { "C#", ("//", "/*", "*/") },
        { "C++", ("//", "/*", "*/") },
        { "Java", ("//", "/*", "*/") },
        { "JavaScript", ("//", "/*", "*/") },
        { "PHP", ("//", "/*", "*/") },
        { "Python", ("#", null, null) },
        { "1C", ("//", null, null) },
        { "HTML", (null, "<!--", "-->") },
        { "XML", (null, "<!--", "-->") },
        { "CSS", (null, "/*", "*/") },
        { "PowerShell", ("#", "<#", "#>") },
        { "SQL", ("--", "/*", "*/") },
        { "VB", ("'", null, null) },
        { "ASP/XHTML", (null, "<!--", "-->") },
    };

    private static (string? Line, string? BlockStart, string? BlockEnd) GetCommentRule(string? syntax)
    {
        if (string.IsNullOrWhiteSpace(syntax)) return (null, null, null);

        foreach (var kvp in CommentRules)
        {
            if (syntax.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return (null, null, null);
    }

    private void CommentSelection_Click(object sender, RoutedEventArgs e)
    {
        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        var rule = GetCommentRule(syntax);

        if (rule.Line != null)
        {
            CommentWithLinePrefix(rule.Line);
        }
        else if (rule.BlockStart != null && rule.BlockEnd != null)
        {
            CommentWithBlock(rule.BlockStart, rule.BlockEnd);
        }
        else
        {
            _snackbar.Show(CodeTextBox, "Комментирование для данного синтаксиса не поддерживается", NotificationType.Warning, 2);
        }
    }

    private void UncommentSelection_Click(object sender, RoutedEventArgs e)
    {
        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        var rule = GetCommentRule(syntax);

        if (rule.Line != null)
        {
            UncommentWithLinePrefix(rule.Line);
        }
        else if (rule.BlockStart != null && rule.BlockEnd != null)
        {
            UncommentWithBlock(rule.BlockStart, rule.BlockEnd);
        }
        else
        {
            _snackbar.Show(CodeTextBox, "Раскомментирование для данного синтаксиса не поддерживается", NotificationType.Warning, 2);
        }
    }

    private void CommentWithLinePrefix(string lineComment)
    {
        var doc = CodeTextBox.Document;
        var start = CodeTextBox.SelectionStart;
        var length = CodeTextBox.SelectionLength;
        var text = CodeTextBox.Text;

        int selStartLine = text[..start].Count(c => c == '\n');
        int selEndLine = text[..(start + length)].Count(c => c == '\n');

        var lines = new List<(int offset, string newText)>();

        for (int line = selStartLine; line <= selEndLine; line++)
        {
            var lineOffset = doc.Lines[line].Offset;
            var lineText = doc.GetText(lineOffset, doc.Lines[line].Length);
            var trimmed = lineText.TrimStart();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed.StartsWith(lineComment, StringComparison.Ordinal)) continue;
            lines.Add((lineOffset, lineComment));
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            doc.Insert(lines[i].offset, lines[i].newText);
        }
    }

    private void UncommentWithLinePrefix(string lineComment)
    {
        var doc = CodeTextBox.Document;
        var start = CodeTextBox.SelectionStart;
        var length = CodeTextBox.SelectionLength;
        var text = CodeTextBox.Text;

        int selStartLine = text[..start].Count(c => c == '\n');
        int selEndLine = text[..(start + length)].Count(c => c == '\n');

        var lines = new List<(int offset, int removeLength)>();

        for (int line = selStartLine; line <= selEndLine; line++)
        {
            var lineOffset = doc.Lines[line].Offset;
            var lineText = doc.GetText(lineOffset, doc.Lines[line].Length);
            var trimmed = lineText.TrimStart();
            if (trimmed.StartsWith(lineComment, StringComparison.Ordinal))
            {
                var prefixLen = lineText.Length - lineText.TrimStart().Length;
                lines.Add((lineOffset + prefixLen, lineComment.Length));
            }
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            doc.Remove(lines[i].offset, lines[i].removeLength);
        }
    }

    private void CommentWithBlock(string blockStart, string blockEnd)
    {
        var doc = CodeTextBox.Document;
        var selStart = CodeTextBox.SelectionStart;
        var selLength = CodeTextBox.SelectionLength;
        doc.Insert(selStart + selLength, blockEnd);
        doc.Insert(selStart, blockStart);
    }

    private void UncommentWithBlock(string blockStart, string blockEnd)
    {
        var doc = CodeTextBox.Document;
        var selStart = CodeTextBox.SelectionStart;
        var selLength = CodeTextBox.SelectionLength;
        var text = doc.GetText(selStart, selLength);

        if (text.StartsWith(blockStart, StringComparison.Ordinal) && text.EndsWith(blockEnd, StringComparison.Ordinal))
        {
            doc.Replace(selStart, selLength, text[blockStart.Length..^blockEnd.Length]);
        }
    }

    private void FormatPythonCode()
    {
        var text = CodeTextBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var newLines = new List<string>();
        int indent = 0;
        bool prevLineIsBlank = false;

        var blockKeywords = new[]
        {
            "def", "class", "if", "elif", "else", "for", "while", "try",
            "except", "finally", "with", "async def"
        };
        var deindentKeywords = new[] { "elif", "else", "except", "finally" };

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (!prevLineIsBlank)
                {
                    newLines.Add("");
                    prevLineIsBlank = true;
                }
                continue;
            }

            prevLineIsBlank = false;

            var lowerTrimmed = trimmed.ToLower();

            bool startsWithDeindent = deindentKeywords.Any(k =>
                lowerTrimmed.StartsWith(k) &&
                (lowerTrimmed.Length == k.Length || !char.IsLetterOrDigit(lowerTrimmed[k.Length])));

            bool startsWithBlockKeyword = blockKeywords.Any(k =>
                lowerTrimmed.StartsWith(k) &&
                (lowerTrimmed.Length == k.Length || !char.IsLetterOrDigit(lowerTrimmed[k.Length])));

            bool isSimpleElseOrElif = lowerTrimmed == "else" || lowerTrimmed.StartsWith("elif");

            if (startsWithDeindent)
            {
                indent = Math.Max(0, indent - 1);
            }

            newLines.Add(new string(' ', indent * 4) + trimmed);

            if (trimmed.EndsWith(":") && !isCommentOrStringLine(trimmed))
            {
                indent++;
            }
            else if (lowerTrimmed.StartsWith("pass") && indent > 0)
            {
                // pass doesn't increase indent
            }
        }

        CodeTextBox.Text = string.Join(Environment.NewLine, newLines);
        _snackbar.Show(CodeTextBox, "Код Python отформатирован", NotificationType.Success, 1.5);
    }

    private static bool isCommentOrStringLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("#") || trimmed.StartsWith("\"\"\"") || trimmed.StartsWith("'''");
    }

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        var syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        if (syntax == null || (!syntax.Contains("1C") && !syntax.Contains("Python")))
        {
            _snackbar.Show(CodeTextBox, "Запуск доступен только для BSL/1C и Python", NotificationType.Warning, 2);
            return;
        }

        var code = CodeTextBox.Text;
        if (string.IsNullOrWhiteSpace(code)) return;

        _debugLineHighlighter.CurrentLine = null;
        _currentDebugger = null;
        _isDebugPaused = false;

        if (syntax.Contains("1C") && _activeBreakpoints.Count > 0)
        {
            ShowOutputPanel("Выполнение (debug)...", showContinue: false);
        }
        else
        {
            ShowOutputPanel("Выполнение...");
        }

        try
        {
            if (syntax.Contains("1C"))
            {
                var args = ParseArguments(ScriptArgsTextBox.Text);
                _currentDebugger = null;
                var debuggerForCleanup = _currentDebugger;
                var result = await _bslExecutionService.ExecuteAsync(
                    code, args,
                    _activeBreakpoints.Count > 0 ? _activeBreakpoints : null,
                    debugger =>
                    {
                        _currentDebugger = debugger;
                        debugger.BreakpointHit += OnDebugBreakpointHit;
                        debugger.ExecutionFinished += OnDebugExecutionFinished;
                    });

                _debugLineHighlighter.CurrentLine = null;
                CodeTextBox.TextArea.TextView.Redraw();

                if (debuggerForCleanup != null)
                {
                    debuggerForCleanup.BreakpointHit -= OnDebugBreakpointHit;
                    debuggerForCleanup.ExecutionFinished -= OnDebugExecutionFinished;
                }
                _currentDebugger = null;
                _isDebugPaused = false;

                if (result.Success)
                {
                    if (string.IsNullOrEmpty(result.Output))
                        ShowOutputPanel("Скрипт выполнен успешно (нет вывода)");
                    else
                        ShowOutputPanel(result.Output);
                }
                else
                {
                    ShowOutputPanel($"Ошибка: {result.Error}");
                }
            }
            else
            {
                var pyArgs = ParseArguments(ScriptArgsTextBox.Text);
                var result = await _pythonExecutionService.ExecuteAsync(code, pyArgs);

                if (result.Success)
                {
                    if (string.IsNullOrEmpty(result.Output))
                        ShowOutputPanel("Скрипт выполнен успешно (нет вывода)");
                    else
                        ShowOutputPanel(result.Output);
                }
                else
                {
                    ShowOutputPanel($"Ошибка: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            _debugLineHighlighter.CurrentLine = null;
            CodeTextBox.TextArea.TextView.Redraw();
            if (_currentDebugger != null)
            {
                _currentDebugger.BreakpointHit -= OnDebugBreakpointHit;
                _currentDebugger.ExecutionFinished -= OnDebugExecutionFinished;
                _currentDebugger = null;
            }
            _isDebugPaused = false;
            ShowOutputPanel($"Ошибка: {ex.Message}");
        }
    }

    private bool IsBslSyntax => SyntaxHighlightingComboBox.SelectedItem?.ToString()?.Contains("1C") == true;

    private void OnDebugBreakpointHit(int threadId, MachineStopReason reason, string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            var line = _currentDebugger?.StoppedLineNumber ?? -1;
            if (line > 0)
            {
                _debugLineHighlighter.CurrentLine = line;
                CodeTextBox.TextArea.TextView.Redraw();
                CodeTextBox.TextArea.Caret.Line = line;
                CodeTextBox.TextArea.Caret.BringCaretToView();
            }
            _isDebugPaused = true;
            ShowOutputPanel($"Останов на строке {line}", showContinue: IsBslSyntax);
            if (IsBslSyntax)
            {
                StepOverButton.Visibility = Visibility.Visible;
                WatchPanel.Visibility = Visibility.Visible;
                WatchTextBox.Focus();
            }
        });
    }

    private void OnDebugExecutionFinished()
    {
        Dispatcher.Invoke(() =>
        {
            _debugLineHighlighter.CurrentLine = null;
            CodeTextBox.TextArea.TextView.Redraw();
            if (IsBslSyntax)
                WatchPanel.Visibility = Visibility.Collapsed;
        });
    }

    private void ClearArgsButton_Click(object sender, RoutedEventArgs e)
    {
        ScriptArgsTextBox.Text = string.Empty;
    }

    private void ScriptArgsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = (TextBox)sender;
        var placeholder = tb.Template.FindName("PlaceholderText", tb) as TextBlock;
        if (placeholder != null)
            placeholder.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string[] ParseArguments(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
    }

    private void ClearOutputPanelButton_Click(object sender, RoutedEventArgs e)
    {
        OutputTextBox.Text = string.Empty;
    }

    private void CloseOutputPanelButton_Click(object sender, RoutedEventArgs e)
    {
        OutputPanelHeader.Visibility = Visibility.Collapsed;
        OutputSplitter.Visibility = Visibility.Collapsed;
        OutputPanelContent.Visibility = Visibility.Collapsed;

        var parentGrid = OutputPanelHeader.Parent as Grid;
        if (parentGrid != null && parentGrid.RowDefinitions.Count > 7)
        {
            parentGrid.RowDefinitions[5].Height = new GridLength(0);
            parentGrid.RowDefinitions[6].Height = new GridLength(0);
            parentGrid.RowDefinitions[7].Height = new GridLength(0);
        }
    }

    private void ClearDebugState()
    {
        _activeBreakpoints.Clear();
        _currentDebugger = null;
        _isDebugPaused = false;
        _debugLineHighlighter.CurrentLine = null;
        ContinueDebugButton.Visibility = Visibility.Collapsed;
        StepOverButton.Visibility = Visibility.Collapsed;
        WatchPanel.Visibility = Visibility.Collapsed;
        CodeTextBox.TextArea.TextView.Redraw();
        _bookmarkMargin?.Redraw();
    }

    private void ContinueDebugButton_Click(object sender, RoutedEventArgs e) => ContinueDebug();

    private void StepOverButton_Click(object sender, RoutedEventArgs e) => DebugStepOver();

    private void WatchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            EvaluateWatch();
    }

    private void WatchEvalButton_Click(object sender, RoutedEventArgs e) => EvaluateWatch();

    private void EvaluateWatch()
    {
        var expr = WatchTextBox.Text.Trim();
        if (string.IsNullOrEmpty(expr) || _currentDebugger == null) return;

        var value = _currentDebugger.Evaluate(expr);
        if (value != null)
            OutputTextBox.Text += $"\n{expr} = {value}";
    }

    private void ShowOutputPanel(string text, bool showContinue = false)
    {
        OutputTextBox.Text = text;
        ContinueDebugButton.Visibility = showContinue ? Visibility.Visible : Visibility.Collapsed;
        OutputPanelHeader.Visibility = Visibility.Visible;
        OutputSplitter.Visibility = Visibility.Visible;
        OutputPanelContent.Visibility = Visibility.Visible;

        var parentGrid = OutputPanelHeader.Parent as Grid;
        if (parentGrid != null && parentGrid.RowDefinitions.Count > 7)
        {
            parentGrid.RowDefinitions[5].Height = GridLength.Auto;
            parentGrid.RowDefinitions[6].Height = new GridLength(5);
            parentGrid.RowDefinitions[7].Height = new GridLength(200);
        }
    }

    private void OutputSplitter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSplitter = true;
        _dragStartY = e.GetPosition(this).Y;
        var parentGrid = OutputPanelHeader.Parent as Grid;
        if (parentGrid?.RowDefinitions.Count > 7)
        {
            _dragStartHeight = parentGrid.RowDefinitions[7].Height.Value;
        }
        OutputSplitter.CaptureMouse();
        e.Handled = true;
    }

    private void OutputSplitter_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSplitter) return;
        var parentGrid = OutputPanelHeader.Parent as Grid;
        if (parentGrid?.RowDefinitions.Count > 7)
        {
            double delta = e.GetPosition(this).Y - _dragStartY;
            double newHeight = Math.Max(50, _dragStartHeight - delta);
            parentGrid.RowDefinitions[7].Height = new GridLength(newHeight);
        }
        e.Handled = true;
    }

    private void OutputSplitter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingSplitter) return;
        _isDraggingSplitter = false;
        OutputSplitter.ReleaseMouseCapture();
        e.Handled = true;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern uint GetOEMCP();

    private void StartTerminalProcess()
    {
        if (_terminalProcess != null && !_terminalProcess.HasExited)
            return;

        var enc = Encoding.GetEncoding((int)GetOEMCP());

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = enc,
            StandardErrorEncoding = enc
        };

        _terminalProcess = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        _terminalProcess.OutputDataReceived += OnTerminalOutput;
        _terminalProcess.ErrorDataReceived += OnTerminalOutput;
        _terminalProcess.Exited += OnTerminalExited;

        try
        {
            _terminalProcess.Start();
            _terminalProcess.BeginOutputReadLine();
            _terminalProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            TerminalOutputTextBox.AppendText("Ошибка запуска cmd.exe: " + ex.Message + Environment.NewLine);
            TerminalOutputTextBox.ScrollToEnd();
            _terminalProcess?.Dispose();
            _terminalProcess = null;
        }
    }

    private void OnTerminalOutput(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;

        Dispatcher.BeginInvoke(() =>
        {
            TerminalOutputTextBox.AppendText(e.Data + Environment.NewLine);
            TerminalOutputTextBox.ScrollToEnd();
        });
    }

    private void OnTerminalExited(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            TerminalOutputTextBox.AppendText(Environment.NewLine + "--- Процесс завершён ---" + Environment.NewLine);
            TerminalOutputTextBox.ScrollToEnd();
            _terminalProcess?.Dispose();
            _terminalProcess = null;
        });
    }

    private void StopTerminalProcess()
    {
        if (_terminalProcess == null || _terminalProcess.HasExited)
        {
            _terminalProcess?.Dispose();
            _terminalProcess = null;
            return;
        }

        try
        {
            _terminalProcess.Kill();
            _terminalProcess.WaitForExit(3000);
        }
        catch { }
        _terminalProcess.Dispose();
        _terminalProcess = null;
    }

    private void TerminalToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (TerminalPanelContent.Visibility == Visibility.Visible)
        {
            HideTerminalPanel();
        }
        else
        {
            ShowTerminalPanel();
            Activate();
            TerminalPanelContent.UpdateLayout();
            TerminalInputTextBox.Focus();
            Keyboard.Focus(TerminalInputTextBox);
        }
    }

    private void ShowTerminalPanel()
    {
        TerminalPanelHeader.Visibility = Visibility.Visible;
        TerminalSplitter.Visibility = Visibility.Visible;
        TerminalPanelContent.Visibility = Visibility.Visible;
        TerminalToggleButton.IsChecked = true;

        var parentGrid = TerminalPanelHeader.Parent as Grid;
        if (parentGrid?.RowDefinitions.Count > 10)
        {
            parentGrid.RowDefinitions[8].Height = GridLength.Auto;
            parentGrid.RowDefinitions[9].Height = new GridLength(5);
            parentGrid.RowDefinitions[10].Height = new GridLength(200);
        }


        StartTerminalProcess();
    }

    private void HideTerminalPanel()
    {
        TerminalPanelHeader.Visibility = Visibility.Collapsed;
        TerminalSplitter.Visibility = Visibility.Collapsed;
        TerminalPanelContent.Visibility = Visibility.Collapsed;
        TerminalToggleButton.IsChecked = false;

        var parentGrid = TerminalPanelHeader.Parent as Grid;
        if (parentGrid?.RowDefinitions.Count > 10)
        {
            parentGrid.RowDefinitions[8].Height = new GridLength(0);
            parentGrid.RowDefinitions[9].Height = new GridLength(0);
            parentGrid.RowDefinitions[10].Height = new GridLength(0);
        }
    }

    private void CloseTerminalButton_Click(object sender, RoutedEventArgs e)
    {
        HideTerminalPanel();
    }

    private void ClearTerminalButton_Click(object sender, RoutedEventArgs e)
    {
        TerminalOutputTextBox.Clear();
        _terminalOutputBuffer.Clear();
    }

    private void TerminalPanelContent_MouseDown(object sender, MouseButtonEventArgs e)
    {
        TerminalInputTextBox.Focus();
        Keyboard.Focus(TerminalInputTextBox);
    }

    private void TerminalInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var command = TerminalInputTextBox.Text;
            TerminalInputTextBox.Clear();

            if (_terminalProcess == null || _terminalProcess.HasExited)
            {
                TerminalOutputTextBox.AppendText("--- Терминал не запущен. Нажмите кнопку 'Терминал' для запуска ---" + Environment.NewLine);
                TerminalOutputTextBox.ScrollToEnd();
                return;
            }

            TerminalOutputTextBox.AppendText("❯ " + command + Environment.NewLine);
            TerminalOutputTextBox.ScrollToEnd();

            try
            {
                _terminalProcess.StandardInput.WriteLine(command);
                _terminalProcess.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                TerminalOutputTextBox.AppendText("Ошибка: " + ex.Message + Environment.NewLine);
                TerminalOutputTextBox.ScrollToEnd();
            }

            e.Handled = true;
        }
    }

    private void TerminalSplitter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTerminalSplitter = true;
        _terminalDragStartY = e.GetPosition(this).Y;
        var parentGrid = TerminalPanelHeader.Parent as Grid;
        if (parentGrid?.RowDefinitions.Count > 10)
        {
            _terminalDragStartHeight = parentGrid.RowDefinitions[10].Height.Value;
        }
        TerminalSplitter.CaptureMouse();
        e.Handled = true;
    }

    private void TerminalSplitter_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingTerminalSplitter) return;
        var parentGrid = TerminalPanelHeader.Parent as Grid;
        if (parentGrid?.RowDefinitions.Count > 10)
        {
            double delta = e.GetPosition(this).Y - _terminalDragStartY;
            double newHeight = Math.Max(50, _terminalDragStartHeight - delta);
            parentGrid.RowDefinitions[10].Height = new GridLength(newHeight);
        }
        e.Handled = true;
    }

    private void TerminalSplitter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingTerminalSplitter) return;
        _isDraggingTerminalSplitter = false;
        TerminalSplitter.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Format1CCode()
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

            var upperLine = trimmed.ToUpper();
            if (blockEnd.Any(b => upperLine.StartsWith(b.ToUpper())))
            {
                indent = Math.Max(0, indent - 1);
            }

            newLines.Add(new string(' ', indent * 4) + trimmed);

            if (blockStart.Any(b => upperLine.StartsWith(b.ToUpper())))
            {
                indent++;
            }
        }

        CodeTextBox.Text = string.Join(Environment.NewLine, newLines);
        _snackbar.Show(CodeTextBox, "Код 1С отформатирован", NotificationType.Success, 1.5);
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();

        var sourceCode = CodeTextBox.Text;
        if (string.IsNullOrWhiteSpace(sourceCode)) return;

        var selectedSyntax = SyntaxHighlightingComboBox.SelectedItem?.ToString();
        if (selectedSyntax == null) return;

        ICodeAnalysisService? analyzer = null;
        if (selectedSyntax.Contains("1C")) analyzer = _bslAnalyzer;
        else if (selectedSyntax.Contains("C#")) analyzer = _csharpAnalyzer;
        else if (selectedSyntax.Contains("Python")) analyzer = _pythonAnalyzer;

        if (analyzer == null) return;

        try
        {
            var result = await analyzer.AnalyzeAsync(sourceCode);
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
        bool shouldShow = AnalyzeToggle.IsChecked == true && hasErrors;

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
        bool isVisible = visibility == Visibility.Visible;
        var parentGrid = ErrorListGrid.Parent as Grid;

        AnalyzeToggle.IsChecked = isVisible;

        if (parentGrid == null || parentGrid.RowDefinitions.Count < 5)
            return;

        if (isVisible)
        {
            // Показываем элемент
            ErrorListGrid.Visibility = Visibility.Visible;
            ErrorListHeader.Visibility = Visibility.Visible;

            // Настраиваем строки
            parentGrid.RowDefinitions[2].Height = GridLength.Auto;
            parentGrid.RowDefinitions[3].Height = new GridLength(4);

            // Анимируем появление
            AnimateHeight(parentGrid.RowDefinitions[4], 0, 140, TimeSpan.FromMilliseconds(300));
        }
        else
        {
            // Анимируем скрытие
            double currentHeight = parentGrid.RowDefinitions[4].Height.Value;
            if (currentHeight <= 0) currentHeight = 140;

            AnimateHeight(parentGrid.RowDefinitions[4], currentHeight, 0, TimeSpan.FromMilliseconds(300), () =>
            {
                ErrorListGrid.Visibility = Visibility.Collapsed;
                ErrorListHeader.Visibility = Visibility.Collapsed;
                parentGrid.RowDefinitions[4].Height = new GridLength(0);
            });
        }
    }

    private void AnimateHeight(RowDefinition row, double from, double to, TimeSpan duration, Action? onComplete = null)
    {
        var startTime = DateTime.Now;
        var startHeight = from;
        var endHeight = to;

        // Используем CompositionTarget для синхронизации с частотой обновления экрана
        EventHandler renderingHandler = null;
        renderingHandler = (sender, e) =>
        {
            var elapsed = DateTime.Now - startTime;
            var progress = Math.Min(1.0, elapsed.TotalMilliseconds / duration.TotalMilliseconds);

            // Плавная функция EaseInOut
            double easedProgress;
            if (progress < 0.5)
                easedProgress = 4 * progress * progress * progress;
            else
                easedProgress = 1 - Math.Pow(1 - progress, 3) / 2;

            var currentHeight = startHeight + (endHeight - startHeight) * easedProgress;
            row.Height = new GridLength(currentHeight);

            if (progress >= 1.0)
            {
                row.Height = new GridLength(endHeight);
                CompositionTarget.Rendering -= renderingHandler;
                onComplete?.Invoke();
            }
        };

        CompositionTarget.Rendering += renderingHandler;
    }

    private void CloseErrorListButton_Click(object sender, RoutedEventArgs e)
    {
        SetErrorPanelVisibility(Visibility.Collapsed);
    }

    private void ErrorListGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ErrorListGrid.SelectedItem is CodeSyntaxError error)
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
        if (AnalyzeToggle.IsChecked != true) return;

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

    private void AnalyzeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (AnalyzeToggle.IsChecked == true)
        {
            _debounceTimer.Start();
            // Сразу запускаем проверку
            DebounceTimer_Tick(null, EventArgs.Empty);
        }
        else
        {
            _debounceTimer.Stop();

            // Очистка ошибок при выключении
            var emptyResult = new AnalysisResult(new List<CodeSyntaxError>(), new List<CodeDictionary.Analysis.SymbolInfo>());
            UpdateUiWithResult(emptyResult);
        }
    }

    private void AutoCompletionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (AutoCompletionToggle.IsChecked == true)
        {
            _autoCompletionEnabled = true;

            // Restore syntax highlighting
            var selected = SyntaxHighlightingComboBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selected))
                SelectSyntax(selected);

            if (AnalyzeToggle.IsChecked == true)
            {
                _debounceTimer.Start();
                DebounceTimer_Tick(null, EventArgs.Empty);
            }
        }
        else
        {
            _autoCompletionEnabled = false;

            // Close windows
            _completionWindow?.Close();
            _completionWindow = null;
            CloseSignatureHelpToolTip();

            // Stop timers
            _debounceTimer.Stop();
            _completionDebounceTimer.Stop();

            // Disable syntax highlighting (plain text)
            CodeTextBox.SyntaxHighlighting = null;

            // Clear errors
            var emptyResult = new AnalysisResult(new List<CodeSyntaxError>(), new List<CodeDictionary.Analysis.SymbolInfo>());
            UpdateUiWithResult(emptyResult);
        }
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
