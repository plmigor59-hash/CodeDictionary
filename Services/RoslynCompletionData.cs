using ICSharpCode.AvalonEdit.CodeCompletion;
using Material.Icons;
using Material.Icons.WPF;
using Microsoft.CodeAnalysis.Completion;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeDictionary.Services
{
    public class RoslynCompletionData : ICompletionData
    {
        private readonly CompletionItem _item;
        private readonly RoslynCompletionService _service;
        private readonly string _code;
        private string? _description;

        public RoslynCompletionData(CompletionItem item, RoslynCompletionService service, string code)
        {
            _item = item;
            _service = service;
            _code = code;
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
                var text = new System.Windows.Controls.TextBlock
                {
                    Text = _item.DisplayText,
                    Foreground = foreground,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                stack.Children.Add(icon);
                stack.Children.Add(text);
                return stack;
            }
        }
        public object Description
        {
            get
            {
                if (_description == null)
                {
                    // Асинхронное получение описания
                    _ = LoadDescriptionAsync();
                    return "Загрузка...";
                }
                return _description;
            }
        }

        private async Task LoadDescriptionAsync()
        {
            _description = await _service.GetDescriptionAsync(_item, _code);
            // Уведомляем интерфейс об обновлении описания (если AvalonEdit это поддерживает)
            // В простом варианте может потребоваться принудительное обновление UI
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
            textArea.Document.Replace(span.Start, span.Length, _item.DisplayText);
        }
    }
}
