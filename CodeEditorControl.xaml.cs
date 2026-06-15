using CodeDictionary.Analysis;
using CodeDictionary.SyntaxChecking;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;

namespace CodeDictionary
{
    public partial class CodeEditorControl : UserControl, INotifyPropertyChanged
    {
        private readonly TextEditor _textEditor;
        private readonly IOneScriptAnalysisService _analyzer = new CodeAnalyzer();
        private ToolTip? _hoverToolTip;
        private CompletionWindow? _completionWindow;
        private CancellationTokenSource _parseCts = new CancellationTokenSource();
        private readonly DispatcherTimer _debounceTimer;
        private SyntaxErrorColorizer? _colorizer;
        private TextMarkerService? _markerService;
        private FoldingManager? _foldingManager;

        public event EventHandler<AnalysisResult>? AnalysisCompleted;

        public CodeEditorControl()
        {
            //InitializeComponent();

            _textEditor = new TextEditor();
            _textEditor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            _textEditor.FontSize = 14;
            _textEditor.ShowLineNumbers = true;

            //EditorHost.Content = _textEditor;

            LoadHighlighting();

            // Таймер для задержки проверки
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

            InitializeErrorHighlighting();

            // Настройка сворачивания
            _foldingManager = FoldingManager.Install(_textEditor.TextArea);

            var foldingUpdateTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (s, e) =>
            {
                UpdateFoldings();
            }, Dispatcher.CurrentDispatcher);

            // Подписка на изменение текста и подсказки
            _textEditor.TextChanged += OnTextChanged;
            _textEditor.TextArea.TextEntered += OnTextEntered;
            _textEditor.TextArea.KeyDown += OnKeyDown;
            _textEditor.TextArea.TextView.MouseHover += OnTextViewMouseHover;
            _textEditor.TextArea.TextView.MouseHoverStopped += OnTextViewMouseHoverStopped;
        }

        private void InitializeErrorHighlighting()
        {
            _colorizer = new SyntaxErrorColorizer(_textEditor.Document);
            _textEditor.TextArea.TextView.LineTransformers.Add(_colorizer);

            _markerService = new TextMarkerService(_textEditor.Document);
            _markerService.AddToTextView(_textEditor.TextArea.TextView);
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space && (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                ShowCompletion(string.Empty, true);
            }
        }

        private void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnTextEntered: {e.Text}");
            if (e.Text.Length > 0 && (char.IsLetter(e.Text[0]) || e.Text[0] == '_' || e.Text[0] == '.'))
            {
                ShowCompletion(e.Text, false);
            }
        }

        private void ShowCompletion(string enteredText, bool controlSpace)
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
                string currentWord = GetWordAtOffset(_textEditor.CaretOffset);
                if (!string.IsNullOrEmpty(currentWord))
                {
                    filteredList = allData.Where(d => d.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            if (filteredList.Any())
            {
                if (_completionWindow == null)
                {
                    _completionWindow = new CompletionWindow(_textEditor.TextArea);
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

        private void OnTextViewMouseHover(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = _textEditor.GetPositionFromPoint(e.GetPosition(_textEditor));
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
                _hoverToolTip.PlacementTarget = _textEditor;
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

        private string GetWordAtOffset(int offset)
        {
            var document = _textEditor.Document;
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

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.';
        }

        public string Text
        {
            get => _textEditor.Text;
            set => _textEditor.Text = value;
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            // Перезапускаем таймер
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();

            var sourceCode = Text;

            // Запускаем анализ в фоне
            var result = await _analyzer.AnalyzeAsync(sourceCode);

            // Обновляем UI
            UpdateUiWithResult(result);
        }

        private void UpdateUiWithResult(AnalysisResult result)
        {
            _colorizer?.UpdateErrors(result.Errors.ToList());
            _markerService?.UpdateMarkers(result.Errors);
            _textEditor.TextArea.TextView.Redraw();
            StatusMessage = $"Ошибок: {result.Errors.Count}, Символов: {result.Symbols.Count}";

            System.Diagnostics.Debug.WriteLine($"CodeEditor: Invoking AnalysisCompleted. Symbols: {result.Symbols.Count}, Errors: {result.Errors.Count}");
            AnalysisCompleted?.Invoke(this, result);
        }


        public void ScrollTo(int line, int column)
        {
            if (_textEditor.Document == null) return;

            // AvalonEdit uses 1-based indexing for lines and columns
            if (line < 1) line = 1;
            if (line > _textEditor.Document.LineCount) line = _textEditor.Document.LineCount;

            var lineSegment = _textEditor.Document.GetLineByNumber(line);
            if (column < 1) column = 1;
            if (column > lineSegment.Length + 1) column = lineSegment.Length + 1;

            try
            {
                _textEditor.ScrollTo(line, column);
                _textEditor.CaretOffset = _textEditor.Document.GetOffset(line, column);
                _textEditor.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scrolling to {line}:{column}: {ex.Message}");
            }
        }

        private string _statusMessage = "Готов";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Метод для очистки ресурсов
        public void Cleanup()
        {
            _parseCts?.Cancel();
            _debounceTimer.Stop();
        }

        private void AnalysisToggle_Click(object sender, RoutedEventArgs e)
        {
            //if (AnalysisToggle.IsChecked == true)
            //{
            //    AnalysisToggle.Content = "Анализ включен";
            //    _debounceTimer.Start();
            //}
            //else
            //{
            //    AnalysisToggle.Content = "Анализ выключен";
            //    _debounceTimer.Stop();

            //    // Очистка ошибок и символов при выключении
            //    var emptyResult = new AnalysisResult(new List<BslSyntaxError>(), new List<SymbolInfo>());
            //    UpdateUiWithResult(emptyResult);
            //}
        }

        private void FormatCode_Click(object sender, RoutedEventArgs e)
        {
            var text = _textEditor.Text;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var newLines = new List<string>();
            int indent = 0;

            var blockStart = new[] { "Процедура", "Функция", "Если", "Для", "Пока", "Попытка" };
            var blockEnd = new[] { "КонецПроцедуры", "КонецФункции", "КонецЕсли", "КонецЦикла", "КонецПопытки", "Исключение" };

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

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
                    indent++;
                }
            }

            _textEditor.Text = string.Join(Environment.NewLine, newLines);
        }

        private void LoadHighlighting()
        {
            try
            {
                using (var s = File.OpenRead("Standart1C.xshd"))
                {
                    using (var reader = XmlReader.Create(s))
                    {
                        _textEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки подсветки: {ex.Message}");
            }
        }

        private void UpdateFoldings()
        {
            var document = _textEditor.Document;
            var foldings = new List<NewFolding>();
            var stack = new Stack<(int startOffset, string type, string header)>();

            var blockStart = new[] { "Процедура", "Функция", "Если", "Для", "Пока", "Попытка" };
            var blockEnd = new[] { "КонецПроцедуры", "КонецФункции", "КонецЕсли", "КонецЦикла", "КонецПопытки", "Исключение" };

            for (int i = 0; i < document.LineCount; i++)
            {
                var line = document.GetLineByNumber(i + 1);
                var text = document.GetText(line).Trim();
                var upperText = text.ToUpper();

                foreach (var start in blockStart)
                {
                    if (upperText.StartsWith(start.ToUpper()))
                    {
                        // Пытаемся захватить имя процедуры/функции, если есть
                        string header = text;
                        stack.Push((line.Offset, start, header));
                        break;
                    }
                }

                foreach (var end in blockEnd)
                {
                    if (upperText.StartsWith(end.ToUpper()) && stack.Count > 0)
                    {
                        var startItem = stack.Pop();
                        foldings.Add(new NewFolding(startItem.startOffset, line.EndOffset) { Name = startItem.header });
                        break;
                    }
                }
            }
            _foldingManager?.UpdateFoldings(foldings.OrderBy(f => f.StartOffset), -1);
        }
    }
}
