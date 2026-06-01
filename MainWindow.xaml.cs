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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace CodeDictionary;

public partial class MainWindow : Window
{
    private readonly DataService _dataService;
    private CodeDictionaryData _data;
    private List<CodeEntry> _filteredEntries;
    private CodeEntry? _currentEntry;
    private bool _isInitialized;
    private AppTheme _currentTheme;
    private AppState _appState;
    private IHighlightingDefinition? _darkCSharpHighlighting;
    private IHighlightingDefinition? _lightCSharpHighlighting;
    private IHighlightingDefinition? _darkCppHighlighting;
    private IHighlightingDefinition? _lightCppHighlighting;
    private IHighlightingDefinition? _standart1CHigh;
    private IHighlightingDefinition? _dark1CHigh;
    private string _currentFilePath = null;  // Хранит путь к текущему открытому файлу


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
            MessageBox.Show("Сначала выделите текст", "Нет выделения",
                          MessageBoxButton.OK, MessageBoxImage.Information);
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
        openFileDialog.Filter = "Текстовые файлы (*.txt;*.cs;*.xaml;*.json;*.xml)|*.txt;*.cs;*.xaml;*.json;*.xml|Все файлы (*.*)|*.*";
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
                MessageBox.Show($"Файл '{Path.GetFileName(_currentFilePath)}' успешно открыт",
                                "Открытие файла",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии файла: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }

    // 💾 СОХРАНИТЬ (если путь уже есть, иначе Сохранить как...)
    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
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

                    MessageBox.Show("Выделенный текст успешно сохранён",
                                    "Сохранение",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                else
                {
                    // Сохраняем весь файл
                    using (var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write))
                    {
                        CodeTextBox.Save(stream);
                    }

                    MessageBox.Show("Файл успешно сохранён",
                                    "Сохранение",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }

                // Обновляем заголовок
                this.Title = $"{Path.GetFileName(_currentFilePath)} - Мой редактор";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
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

                MessageBox.Show("Файл успешно сохранён",
                                "Сохранение",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
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
       DescriptionRow.Height = new GridLength(0);
       ToggleDescriptionButton.Content = " ▼ Развернуть ";
 
        RefreshCategoryFilter();

        // Восстанавливаем выбранную категорию
        if (!string.IsNullOrEmpty(_appState.SelectedCategory))
        {
            for (int i = 0; i < CategoryFilter.Items.Count; i++)
            {
                if (CategoryFilter.Items[i]?.ToString() == _appState.SelectedCategory)
                {
                    CategoryFilter.SelectedIndex = i;
                    break;
                }
            }
        }

        RefreshEntriesList();


      

        // Восстанавливаем выбранную запись
        if (_appState.SelectedEntryId.HasValue)
        {
            var entry = _filteredEntries.FirstOrDefault(e => e.Id == _appState.SelectedEntryId.Value);
            if (entry != null)
            {
                EntriesListBox.SelectedItem = entry;
            }
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

            EntriesListBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.SidePanel));
            EntriesListBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.TextPrimary));
            EntriesListBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Dark.Border));

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

            EntriesListBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.SidePanel));
            EntriesListBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.TextPrimary));
            EntriesListBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Light.Border));

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
        CategoryFilter.Items.Clear();
        CategoryFilter.Items.Add("Все категории");

        foreach (var category in _data.Categories.OrderBy(c => c))
        {
            CategoryFilter.Items.Add(category);
        }

        CategoryFilter.SelectedIndex = 0;
    }

    private void RefreshEntriesList()
    {
        var searchText = SearchBox.Text.ToLower();
        var selectedCategory = CategoryFilter.SelectedItem?.ToString();

        _filteredEntries = _data.Entries
            .Where(e =>
            {
                var matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                                  searchText == "поиск..." ||
                                  e.Title.ToLower().Contains(searchText) ||
                                  e.Description.ToLower().Contains(searchText) ||
                                  e.Code.ToLower().Contains(searchText) ||
                                  e.Tags.Any(t => t.ToLower().Contains(searchText));

                var matchesCategory = selectedCategory == "Все категории" ||
                                    string.IsNullOrEmpty(selectedCategory) ||
                                    e.Category == selectedCategory;

                return matchesSearch && matchesCategory;
            })
            .OrderByDescending(e => e.ModifiedAt)
            .ToList();

        EntriesListBox.ItemsSource = _filteredEntries;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitialized)
        {
            RefreshEntriesList();
        }
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized)
        {
            RefreshEntriesList();
        }
    }

    private void EntriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EntriesListBox.SelectedItem is CodeEntry entry)
        {
            _currentEntry = entry;
            LoadEntryToForm(entry);
        }
    }

    private void LoadEntryToForm(CodeEntry entry)
    {
        TitleTextBox.Text = entry.Title;
        DescriptionTextBox.Text = entry.Description;
        CodeTextBox.Text = entry.Code;
        CategoryComboBox.Text = entry.Category;
        TagsTextBox.Text = string.Join(", ", entry.Tags);

        CategoryComboBox.ItemsSource = _data.Categories;


        if (!string.IsNullOrEmpty(entry.Syntax) && SyntaxHighlightingComboBox != null)
        {
            SyntaxHighlightingComboBox.SelectedItem = entry.Syntax;
        }

        LoadSegmentsFromEntry();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        _currentEntry = new CodeEntry();
        _data.Entries.Add(_currentEntry);

        TitleTextBox.Text = "";
        DescriptionTextBox.Text = "";
        CodeTextBox.Text = "";
        CategoryComboBox.Text = "";
        TagsTextBox.Text = "";
        CategoryComboBox.ItemsSource = _data.Categories;

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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Не сохраняем null
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
            MessageBox.Show("Выберите или создайте запись", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            MessageBox.Show("Введите название", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _currentEntry.Title = TitleTextBox.Text;
        _currentEntry.Description = DescriptionTextBox.Text;
        _currentEntry.Code = CodeTextBox.Text;
        _currentEntry.Category = CategoryComboBox.Text;
        _currentEntry.Tags = TagsTextBox.Text
            .Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        _currentEntry.Syntax = SyntaxHighlightingComboBox.SelectedItem?.ToString() ?? string.Empty;
        _currentEntry.ModifiedAt = DateTime.Now;
        
        SaveSegmentsToEntry();

        if (!string.IsNullOrWhiteSpace(_currentEntry.Category) &&
            !_data.Categories.Contains(_currentEntry.Category))
        {
            _data.Categories.Add(_currentEntry.Category);
            RefreshCategoryFilter();
        }

        await _dataService.SaveDataAsync(_data);
        RefreshEntriesList();

        MessageBox.Show("Запись сохранена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
    }




    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_currentEntry == null)
        {
            MessageBox.Show("Выберите запись для удаления", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show("Запись удалена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Сохраняем текущее состояние
        _appState.SelectedCategory = CategoryFilter.SelectedItem?.ToString() ?? "Все категории";
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
            DescriptionRow.Height = new GridLength(0);
            ToggleDescriptionButton.Content = " ▼ Развернуть ";
        }
        else
        {
            DescriptionTextBox.Visibility = Visibility.Visible;
            DescriptionRow.Height = new GridLength(1, GridUnitType.Star);
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
            HighlightSearchResult();
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
}