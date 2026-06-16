using ICSharpCode.AvalonEdit.CodeCompletion;
using Material.Icons;
using Material.Icons.WPF;
using Microsoft.CodeAnalysis.Completion;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeDictionary.Services
{
    public class RoslynCompletionData : ICompletionData
    {
        private readonly CompletionItem _item;
        private readonly RoslynCompletionService _service;
        private readonly int _position;
        private string? _description;
        private string? _signature;
        private System.Windows.Controls.TextBlock? _textBlock;

        public RoslynCompletionData(CompletionItem item, RoslynCompletionService service, int position)
        {
            _item = item;
            _service = service;
            _position = position;
        }

        public object Content
        {
            get
            {
                var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

                // Определяем цвета на основе темы
                var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;

                var icon = new System.Windows.Controls.Image
                {
                    Source = GetIconForTheme(foreground),
                    Width = 16,
                    Height = 16,
                    Margin = new System.Windows.Thickness(0, 0, 5, 0),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                _textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = _item.DisplayText + (_signature ?? ""),
                    Foreground = foreground,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                if (_signature == null && _item.Tags.Contains("Method"))
                {
                    _ = LoadSignatureAsync();
                }

                stack.Children.Add(icon);
                stack.Children.Add(_textBlock);
                return stack;
            }
        }

        private async Task LoadSignatureAsync()
        {
            _signature = await _service.GetMethodSignatureAsync(_item, _position);
            if (_textBlock != null)
            {
                _textBlock.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _textBlock.Text = _item.DisplayText + _signature;
                }));
            }
        }

        private System.Windows.Controls.TextBlock? _descriptionTextBlock;

        public object Description
        {
            get
            {
                if (_descriptionTextBlock == null)
                {
                    var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
                    _descriptionTextBlock = new System.Windows.Controls.TextBlock
                    {
                        Text = "Загрузка...",
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 400,
                        Foreground = foreground
                    };
                    _ = LoadDescriptionAsync();
                }
                return _descriptionTextBlock;
            }
        }

        private async Task LoadDescriptionAsync()
        {
            _description = await _service.GetDescriptionAsync(_item);
            if (_descriptionTextBlock != null)
            {
                _descriptionTextBlock.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _descriptionTextBlock.Text = _description;
                }));
            }
        }

        public System.Windows.Media.ImageSource? Image => null; // Убираем вторую иконку

        private ImageSource? GetIconForTheme(Brush foreground)
        {
            string type = "Help";
            if (_item.Tags.Contains("Method")) type = "Method";
            else if (_item.Tags.Contains("Property")) type = "Property";
            else if (_item.Tags.Contains("Field")) type = "Field";
            else if (_item.Tags.Contains("Class")) type = "Class";

            string iconName = type switch
            {
                "Method" => "FunctionVariant",
                "Property" => "CodeBraces",
                "Field" => "DatabaseOutline",
                "Class" => "CubeOutline",
                _ => "Help"
            };

            if (!Enum.TryParse<MaterialIconKind>(iconName, true, out var kind))
                return null;

            var materialIcon = new MaterialIcon
            {
                Kind = kind,
                Width = 16,
                Height = 16,
                Foreground = foreground // Используем цвет темы
            };

            var border = new System.Windows.Controls.Border
            {
                Child = materialIcon,
                Width = 16,
                Height = 16,
                Background = Brushes.Transparent
            };

            var size = new System.Windows.Size(16, 16);
            border.Measure(size);
            border.Arrange(new System.Windows.Rect(size));
            border.UpdateLayout();

            var renderTarget = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(border);
            renderTarget.Freeze();

            return renderTarget;
        }
        public double Priority => 0;
        public string Text => _item.DisplayText;

        public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var span = _item.Span;
            string textToInsert = _item.DisplayText;
            bool isMethod = _item.Tags.Contains("Method");

            if (isMethod)
            {
                textToInsert += "()";
            }

            // Корректируем смещение span с учетом OffsetShift в сервисе
            int start = Math.Max(0, span.Start - _service.OffsetShift);
            textArea.Document.Replace(start, Math.Min(span.Length, textArea.Document.TextLength - start), textToInsert);

            if (isMethod)
            {
                // Ставим курсор внутри скобок
                textArea.Caret.Offset = start + _item.DisplayText.Length + 1;
            }
            else
            {
                // Для не-методов ставим курсор в конец вставленного текста
                textArea.Caret.Offset = start + textToInsert.Length;
            }
        }
    }
}
