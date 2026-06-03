using CodeDictionary.Models;
using CodeDictionary.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;  // Для OpenFileDialog и SaveFileDialog
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Windows.Controls.Primitives;
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
    private string _currentFilePath = null;  // Хранит путь к текущему открытому файлу0
    private SnackbarNotification _snackbar = new SnackbarNotification();
    private List<TextSegmentStyle> _textSegments = new();


    public MainWindow()


    {
        // Загружаем кастомную тему подсветки для тёмного и светлого режимов
      

        InitializeComponent();
        _dataService = new DataService();
        _data = new CodeDictionaryData();
        _filteredEntries = new List<CodeEntry>();
        _isInitialized = false;
        _currentTheme = AppTheme.Dark;
        _appState = new AppState();

        LoadCustomHighlighting();


        // Инициализируем список шрифтов и настроек подсветки
        InitializeFontSettings();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        CodeTextBox.TextArea.TextView.LineTransformers.Add(new CustomColorTransformer(_textSegments));

        // Подписываемся на изменения текста
        CodeTextBox.TextChanged += (s, e) => UpdateSegmentsAfterTextChange();

    }

 
    private void ApplyStyleToSelection(string backgroundColor = null, string foregroundColor = null,
                                          bool? bold = null, bool? italic = null, bool? underline = null,
                                          string fontFamily = null, double? fontSize = null)
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
        openFileDialog.Filter = "Текстовые файлы (*.txt;*.bsl;*.cs;*.xaml;*.json;*.xml)|*.txt;*.bsl;cs;*.xaml;*.json;*.xml|Все файлы (*.*)|*.*";
        openFileDialog.FilterIndex = 1;
        openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                _currentFilePath = openFileDialog.FileName;

                // Загружаем файл с определением кодировки (автоматическая)
                using (var stream = new FileStream(_currentFilePath, FileMode.Open, FileAccess.Read))
                {
                    CodeTextBox.Load(stream);
                }

                // Обновляем заголовок окна или статус
                this.Title = $"{Path.GetFileName(_currentFilePath)} - Мой редактор";

                // Показываем уведомление об успехе
                _snackbar.Show(CodeTextBox, $"Файл '{Path.GetFileName(_currentFilePath)}' успешно открыт", NotificationType.Success, 1.5);
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
        SaveEntry_Click(sender, e);

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
                this.Title = $"{Path.GetFileName(_currentFilePath)} - Мой редактор";
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
        var saveFileDialog = new SaveFileDialog();

        // Настройки диалога
        saveFileDialog.Title = "Сохранить файл как";
        saveFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|1c (*.bsl)|*.bsl|c# (*.cs)|*.cs|Все файлы (*.*)|*.*";
        saveFileDialog.FilterIndex = 1;
        saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Если файл уже был открыт, предлагаем его имя по умолчанию
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            saveFileDialog.FileName = Path.GetFileName(_currentFilePath);
        }

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                _currentFilePath = saveFileDialog.FileName;

                using (var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write))
                {
                    CodeTextBox.Save(stream);
                }

                this.Title = $"{Path.GetFileName(_currentFilePath)} ";

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
        if (CodeTextBox == null || SyntaxHighlightingComboBox?.SelectedItem == null) return;

        var selected = SyntaxHighlightingComboBox.SelectedItem.ToString();

        if (selected == "Темная С#")
        {
            CodeTextBox.SyntaxHighlighting = _darkCSharpHighlighting ?? HighlightingManager.Instance.GetDefinition("C#");
        }
        else if (selected == "Светлая С#")
        {
            CodeTextBox.SyntaxHighlighting = _lightCSharpHighlighting ?? HighlightingManager.Instance.GetDefinition("C#");
        }
        else if (selected == "Темная C++")
        {
            CodeTextBox.SyntaxHighlighting = _darkCppHighlighting ?? HighlightingManager.Instance.GetDefinition("C++");
        }
        else if (selected == "Светлая C++")
        {
            CodeTextBox.SyntaxHighlighting = _lightCppHighlighting ?? HighlightingManager.Instance.GetDefinition("C++");
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
            HeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));

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
            HeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));

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
            SyntaxHighlightingComboBox?.Items.Add("Светлая С#");
            SyntaxHighlightingComboBox?.Items.Add("Светлая C++");
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
        if (e.NewValue is CodeEntryViewModel viewEntry)
        {
            _currentEntry = viewEntry.Entry;
            LoadEntryToForm(_currentEntry);
            return;
        }

        if (e.NewValue is CategoryNode categoryNode)
        {
            _selectedCategoryPath = categoryNode.FullPath;
            CategoryComboBox.Text = categoryNode.FullPath;
        }
    }

    private void LoadEntryToForm(CodeEntry entry)
    {
        TitleTextBox.Text = entry.Title;
        DescriptionTextBox.Text = entry.Description;
        CodeTextBox.Text = entry.Code;
        CategoryComboBox.Text = NormalizeCategoryPath(entry.Category);
        TagsTextBox.Text = string.Join(", ", entry.Tags);

        _selectedCategoryPath = NormalizeCategoryPath(entry.Category);
        RefreshCategoryFilter();


        if (!string.IsNullOrEmpty(entry.Syntax) && SyntaxHighlightingComboBox != null)
        {
            SyntaxHighlightingComboBox.SelectedItem = entry.Syntax;
        }

        LoadSegmentsFromEntry();
        ClearSearch();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        _currentEntry = new CodeEntry();
        _data.Entries.Add(_currentEntry);

        TitleTextBox.Text = "";
        DescriptionTextBox.Text = "";
        CodeTextBox.Text = "";
        CategoryComboBox.Text = _selectedCategoryPath;
        TagsTextBox.Text = "";
        RefreshCategoryFilter();

        ClearSearch();
        TitleTextBox.Focus();
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
    private void ApplySegmentsToEditor()
    {
        // Здесь ваша логика отрисовки сегментов
        // Например, перерисовка TextView
        CodeTextBox.TextArea.TextView.Redraw();
    }






    private async void SaveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_currentEntry == null)
        {
            ShowAlert("Выберите или создайте запись", isError: true);
            return;

        }

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            ShowAlert("Введите название", isError: true);
            return;
        }

        _currentEntry.Title = TitleTextBox.Text;
        _currentEntry.Description = DescriptionTextBox.Text;
        _currentEntry.Code = CodeTextBox.Text;
        _currentEntry.Category = NormalizeCategoryPath(CategoryComboBox.Text);
        _currentEntry.Tags = TagsTextBox.Text
            .Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        _currentEntry.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;
        _currentEntry.ModifiedAt = DateTime.Now;
        
        SaveSegmentsToEntry();

        EnsureCategoryPathExists(_currentEntry.Category);

        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();

        _snackbar.Show(CodeTextBox, "Запись сохранена", NotificationType.Success, 1.5);
    }




    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_currentEntry == null)
        {
            ShowAlert("Выберите запись для удаления", isError: true);
            return;
        }

        var result = MessageBox.Show(
            $"Удалить запись '{_currentEntry.Title}'?",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
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
        _appState.SelectedEntryId = _currentEntry?.Id;
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

        string searchText = SearchInTextBox?.Text;
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
        var dialog = new CategoryInputDialog
        {
            Owner = this,
            Title = string.IsNullOrWhiteSpace(parentPath)
                ? "Новая категория"
                : $"Новая категория внутри '{parentPath}'"
        };

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

        var result = MessageBox.Show(
            $"Удалить категорию '{categoryPath}' и все вложенные категории?\n\nЗаписи внутри будут переведены в 'Без категории'.",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _data.Categories.RemoveAll(existing =>
        {
            var normalizedExisting = NormalizeCategoryPath(existing);
            return string.Equals(normalizedExisting, categoryPath, StringComparison.OrdinalIgnoreCase) ||
                   normalizedExisting.StartsWith($"{categoryPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase);
        });

        foreach (var entry in _data.Entries)
        {
            var entryCategory = NormalizeCategoryPath(entry.Category);
            if (string.IsNullOrWhiteSpace(entryCategory))
            {
                continue;
            }

            if (string.Equals(entryCategory, categoryPath, StringComparison.OrdinalIgnoreCase) ||
                entryCategory.StartsWith($"{categoryPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
            {
                entry.Category = string.Empty;
            }
        }

        if (string.Equals(_selectedCategoryPath, categoryPath, StringComparison.OrdinalIgnoreCase) ||
            _selectedCategoryPath.StartsWith($"{categoryPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
        {
            _selectedCategoryPath = string.Empty;
            CategoryComboBox.Text = string.Empty;
        }

        if (_currentEntry != null)
        {
            var currentEntryCategory = NormalizeCategoryPath(_currentEntry.Category);
            if (string.Equals(currentEntryCategory, categoryPath, StringComparison.OrdinalIgnoreCase) ||
                currentEntryCategory.StartsWith($"{categoryPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase))
            {
                _currentEntry.Category = string.Empty;
                CategoryComboBox.Text = string.Empty;
            }
        }

        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();
        _snackbar.Show(CodeTextBox, $"Категория '{categoryPath}' удалена", NotificationType.Success, 1.5);
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
