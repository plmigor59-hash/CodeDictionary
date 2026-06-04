using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

namespace XshdEditor
{
    public partial class XshdEditorWindow : Window
    {
        private readonly SyntaxDefinitionViewModel _viewModel;
        private string _filePath;

        public XshdEditorWindow() : this(null)
        {
        }

        public XshdEditorWindow(string? filePath)
        {
            InitializeComponent();

            _filePath = filePath ?? string.Empty;
            _viewModel = string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath)
                ? CreateDefaultViewModel()
                : SyntaxDefinitionViewModel.Load(filePath);
            DataContext = _viewModel;
        }

        private static SyntaxDefinitionViewModel CreateDefaultViewModel()
        {
            return new SyntaxDefinitionViewModel
            {
                Name = "NewDefinition",
                Extensions = ".xshd"
            };
        }

        private void PreviewColor_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var colorRule = button?.Tag as XshdColorRule;

            if (colorRule != null)
            {
                ShowColorPreview(colorRule);
            }
        }

        private void DeleteColor_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var colorRule = button?.Tag as XshdColorRule;

            if (colorRule != null)
            {
                _viewModel.Colors.Remove(colorRule);
            }
        }

        private void ShowColorPreview(XshdColorRule colorRule)
        {
            var previewWindow = new Window
            {
                Title = $"Preview: {colorRule.Name}",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(colorRule.BackgroundColor),
                    Padding = new Thickness(20),
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(colorRule.ExampleText)
                            ? "Пример текста"
                            : colorRule.ExampleText,
                        Foreground = new System.Windows.Media.SolidColorBrush(colorRule.ForegroundColor),
                        FontSize = 16,
                        FontWeight = colorRule.FontWeight == "Bold" ? FontWeights.Bold : FontWeights.Normal,
                        FontStyle = colorRule.FontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };

            previewWindow.ShowDialog();
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var tempFile = System.IO.Path.GetTempFileName() + ".xshd";
            _viewModel.Save(tempFile);
            ShowPreview(tempFile);
        }

        private void ShowPreview(string xshdFile)
        {
            var previewWindow = new Window
            {
                Title = "Предпросмотр подсветки синтаксиса",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var infoText = new TextBlock
            {
                Text = $"Пример текста для {_viewModel.Name}",
                Margin = new Thickness(10),
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(infoText, 0);

            var editor = new ICSharpCode.AvalonEdit.TextEditor();
            editor.SyntaxHighlighting = LoadHighlightingDefinition(xshdFile);
            editor.Text = GetSampleText();
            editor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            editor.FontSize = 14;
            Grid.SetRow(editor, 1);

            grid.Children.Add(infoText);
            grid.Children.Add(editor);
            previewWindow.Content = grid;

            previewWindow.ShowDialog();
        }

        private string GetSampleText()
        {
            return @"<?xml version=""1.0""?>
<root>
    <!-- Это комментарий -->
    <element attribute=""value"">
        Пример текста с <child>вложенным</child> тегом
    </element>
</root>";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var targetPath = _filePath;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "Сохранить XSHD файл",
                        Filter = "XSHD files (*.xshd)|*.xshd|All files (*.*)|*.*",
                        DefaultExt = ".xshd",
                        FileName = string.IsNullOrWhiteSpace(_viewModel.Name) ? "NewDefinition.xshd" : $"{_viewModel.Name}.xshd"
                    };

                    if (saveDialog.ShowDialog() != true)
                    {
                        return;
                    }

                    targetPath = saveDialog.FileName;
                }

                _viewModel.Save(targetPath);
                _filePath = targetPath;

                var def = LoadHighlightingDefinition(targetPath);
                if (def != null)
                {
                    HighlightingManager.Instance.RegisterHighlighting(
                        _viewModel.Name,
                        _viewModel.Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                        def
                    );
                }
                else
                {
                    MessageBox.Show("Не удалось загрузить созданную подсветку. Файл сохранилcя, проверьте XSHD.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                MessageBox.Show("Файл успешно сохранен!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static IHighlightingDefinition? LoadHighlightingDefinition(string filePath)
        {
            using var reader = XmlReader.Create(filePath);
            var xshd = HighlightingLoader.LoadXshd(reader);

            // Basic validations: ensure there are color definitions and a Main RuleSet
            var problems = new System.Text.StringBuilder();
            var colorNames = xshd.Elements.OfType<XshdColor>().Select(c => c.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
            var ruleSets = xshd.Elements.OfType<XshdRuleSet>().ToList();

            if (!colorNames.Any())
            {
                problems.AppendLine("No <Color> definitions found in XSHD.");
            }

            if (!ruleSets.Any(rs => string.Equals(rs.Name, "Main", StringComparison.OrdinalIgnoreCase)))
            {
                problems.AppendLine("Missing RuleSet with name 'Main'.");
            }

            if (problems.Length > 0)
            {
                try
                {
                    var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetFileName(filePath));
                    System.IO.File.Copy(filePath, temp, true);
                    MessageBox.Show($"Проблемы в XSHD:\n{problems}\nФайл сохранён для отладки: {temp}", "Ошибка подсветки", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    MessageBox.Show($"Проблемы в XSHD:\n{problems}", "Ошибка подсветки", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return null;
            }

            try
            {
                return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            }
            catch (ICSharpCode.AvalonEdit.Highlighting.HighlightingDefinitionInvalidException ex)
            {
                // Save a copy for inspection and show details to the user
                try
                {
                    var temp = Path.Combine(Path.GetTempPath(), Path.GetFileName(filePath));
                    File.Copy(filePath, temp, true);
                    MessageBox.Show($"Ошибка загрузки подсветки: {ex.Message}\nФайл сохранён: {temp}", "Ошибка подсветки", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    MessageBox.Show($"Ошибка загрузки подсветки: {ex.Message}", "Ошибка подсветки", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                // Save full exception stack to temp file for debugging
                try
                {
                    var logPath = Path.Combine(Path.GetTempPath(), $"xshd_error_{System.Guid.NewGuid()}.txt");
                    File.WriteAllText(logPath, ex.ToString());
                    MessageBox.Show($"Ошибка при загрузке подсветки: {ex.Message}\nПолный стек сохранён: {logPath}", "Ошибка подсветки", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    MessageBox.Show($"Ошибка при загрузке подсветки: {ex.Message}", "Ошибка подсветки", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return null;
            }
        }
    }
}
