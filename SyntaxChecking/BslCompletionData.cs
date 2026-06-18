using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Material.Icons;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeDictionary.SyntaxChecking
{
    public class BslCompletionData : ICompletionData
    {
        private static readonly ConditionalWeakTable<string, ImageSource> IconCache = new();
        private static readonly Dictionary<string, (MaterialIconKind Kind, Color Color)> TypeToIconMap = new()
        {
            ["Функция"] = (MaterialIconKind.FunctionVariant, Colors.DodgerBlue),
            ["Процедура"] = (MaterialIconKind.PlayCircle, Colors.ForestGreen),
            ["Переменная"] = (MaterialIconKind.Variable, Colors.Orange),
            ["Локальная переменная"] = (MaterialIconKind.Variable, Colors.Orange),
            ["Автоматическая переменная"] = (MaterialIconKind.Variable, Colors.DarkOrange),
            ["Параметр"] = (MaterialIconKind.CodeBraces, Colors.Purple),
            ["Итератор цикла"] = (MaterialIconKind.Repeat, Colors.Teal),
            ["Ключевое слово"] = (MaterialIconKind.Key, Colors.Gray),
            ["Встроенная функция"] = (MaterialIconKind.Function, Colors.DodgerBlue),
            ["Тип"] = (MaterialIconKind.CubeOutline, Colors.DarkBlue),
        };

        public BslCompletionData(string text, string description, string type)
        {
            Text = text;
            Description = description;
            Type = type;
        }

        public string Text { get; }
        public string Type { get; }
        public object Description { get; }
        public double Priority => 0;

        public ImageSource? Image => null;

        public object Content
        {
            get
            {
                var panel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal
                };

                var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;

                var icon = new System.Windows.Controls.Image
                {
                    Source = GetCachedIcon(Type, foreground),
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = Text,
                    Foreground = foreground,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                panel.Children.Add(icon);
                panel.Children.Add(textBlock);
                return panel;
            }
        }

        private ImageSource? GetCachedIcon(string type, Brush foreground)
        {
            string lookupKey = TypeToIconMap.ContainsKey(type) ? type : "Ключевое слово";
            string cacheKey = $"{lookupKey}:{foreground?.ToString() ?? "default"}";

            return IconCache.GetValue(cacheKey, _ =>
            {
                var (kind, color) = TypeToIconMap.GetValueOrDefault(lookupKey, (MaterialIconKind.CodeBrackets, Colors.Gray));

                Color finalColor;
                if (foreground is SolidColorBrush solidBrush && solidBrush.Color != Colors.Black)
                {
                    finalColor = solidBrush.Color;
                }
                else
                {
                    finalColor = color;
                }

                var icon = new Material.Icons.WPF.MaterialIcon
                {
                    Kind = kind,
                    Width = 16,
                    Height = 16,
                    Foreground = new SolidColorBrush(finalColor)
                };

                var border = new System.Windows.Controls.Border
                {
                    Child = icon,
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
            });
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            int offset = completionSegment.Offset;
            var document = textArea.Document;

            int start = offset;
            while (start > 0 && IsIdentifierChar(document.GetCharAt(start - 1)))
                start--;

            int end = offset;
            while (end < document.TextLength && IsIdentifierChar(document.GetCharAt(end)))
                end++;

            string suffix = Type.Contains("Функция") || Type.Contains("Встроенная функция") ? "()" : "";
            textArea.Document.Replace(start, end - start, Text + suffix);

            if (suffix == "()")
            {
                textArea.Caret.Offset = start + Text.Length + 1;
            }
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
    }
}
