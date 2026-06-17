using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.CodeAnalysis.Completion;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodeDictionary.Services
{
    public class RoslynCompletionData : ICompletionData
    {
        private static readonly ConditionalWeakTable<string, ImageSource> IconCache = new();
        private static readonly Dictionary<string, string> TagToIconMap = new()
        {
            ["Method"] = "FunctionVariant",
            ["Property"] = "CodeBraces",
            ["Field"] = "DatabaseOutline",
            ["Class"] = "CubeOutline",
            ["Struct"] = "CubeOutline",
            ["Interface"] = "CodeBraces",
            ["Enum"] = "FormatListBullets",
            ["Keyword"] = "CodeBrackets",
            ["Local"] = "CodeBraces",
            ["Parameter"] = "CodeBraces",
            ["Namespace"] = "FolderOutline",
            ["Event"] = "CodeBraces",
            ["Delegate"] = "CodeBraces",
            ["Operator"] = "CodeBraces",
            ["ExtensionMethod"] = "CodeBraces",
        };

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
                var foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;

                var iconMargin = new System.Windows.Thickness(0, 0, 6, 0);
                var icon = new System.Windows.Controls.Image
                {
                    Source = GetCachedIcon(foreground),
                    Width = 16,
                    Height = 16,
                    Margin = iconMargin,
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

        public ImageSource? Image => null;

        private ImageSource? GetCachedIcon(Brush foreground)
        {
            string tag = _item.Tags.FirstOrDefault(t => TagToIconMap.ContainsKey(t)) ?? "Help";
            string cacheKey = $"{tag}:{foreground?.ToString() ?? "default"}";

            return IconCache.GetValue(cacheKey, _ =>
            {
                string iconName = TagToIconMap.GetValueOrDefault(tag, "Help");
                if (!Enum.TryParse<Material.Icons.MaterialIconKind>(iconName, true, out var kind))
                    return null;

                var icon = new Material.Icons.WPF.MaterialIcon
                {
                    Kind = kind,
                    Width = 16,
                    Height = 16,
                    Foreground = foreground
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

        public double Priority => 0;
        public string Text => _item.DisplayText;

        public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var doc = textArea.Document;
            int offset = Math.Max(0, Math.Min(completionSegment.Offset, doc.TextLength));
            int length = Math.Max(0, Math.Min(completionSegment.Length, doc.TextLength - offset));

            string textToInsert = _item.DisplayText;
            bool isMethod = _item.Tags.Contains("Method");

            if (isMethod)
            {
                textToInsert += "()";
            }

            doc.Replace(offset, length, textToInsert);

            int newCaretOffset = isMethod
                ? offset + _item.DisplayText.Length + 1
                : offset + textToInsert.Length;

            textArea.Caret.Offset = Math.Max(0, Math.Min(newCaretOffset, doc.TextLength));
        }
    }
}
