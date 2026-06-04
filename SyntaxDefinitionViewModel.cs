using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace XshdEditor
{
    public class SyntaxDefinitionViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _extensions = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Extensions
        {
            get => _extensions;
            set { _extensions = value; OnPropertyChanged(); }
        }

        public ObservableCollection<XshdColorRule> Colors { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static SyntaxDefinitionViewModel Load(string filePath)
        {
            var viewModel = new SyntaxDefinitionViewModel();

            using (var reader = XmlReader.Create(filePath))
            {
                var xshd = HighlightingLoader.LoadXshd(reader);

                viewModel.Name = xshd.Name ?? "Unknown";

                if (xshd.Extensions != null && xshd.Extensions.Count > 0)
                {
                    viewModel.Extensions = string.Join(", ", xshd.Extensions);
                }

                foreach (var element in xshd.Elements)
                {
                    if (element is XshdColor color)
                    {
                        viewModel.Colors.Add(new XshdColorRule
                        {
                            Name = color.Name,
                            Foreground = GetBrushColorHex(color.Foreground, "#000000"),
                            Background = GetBrushColorHex(color.Background, "#00000000"),
                            ExampleText = color.ExampleText ?? string.Empty,
                            FontWeight = color.FontWeight?.ToString() ?? "Normal",
                            FontStyle = color.FontStyle?.ToString() ?? "Normal"
                        });
                    }
                }
            }

            return viewModel;
        }

        public void Save(string filePath)
        {
            var xshd = new XshdSyntaxDefinition
            {
                Name = this.Name
            };

            if (!string.IsNullOrEmpty(Extensions))
            {
                foreach (var ext in Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    xshd.Extensions.Add(ext.Trim());
                }
            }

            foreach (var colorRule in Colors)
            {
                var xshdColor = new XshdColor
                {
                    Name = colorRule.Name,
                    ExampleText = colorRule.ExampleText
                };

                if (!string.IsNullOrEmpty(colorRule.Foreground) && colorRule.Foreground != "#00000000")
                {
                    xshdColor.Foreground = CreateHighlightingBrush(colorRule.Foreground);
                }

                if (!string.IsNullOrEmpty(colorRule.Background) && colorRule.Background != "#00000000")
                {
                    xshdColor.Background = CreateHighlightingBrush(colorRule.Background);
                }

                if (!string.IsNullOrEmpty(colorRule.FontWeight) && colorRule.FontWeight != "Normal")
                {
                    xshdColor.FontWeight = ConvertToFontWeight(colorRule.FontWeight);
                }

                if (!string.IsNullOrEmpty(colorRule.FontStyle) && colorRule.FontStyle != "Normal")
                {
                    xshdColor.FontStyle = ConvertToFontStyle(colorRule.FontStyle);
                }

                xshd.Elements.Add(xshdColor);
            }

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = System.Text.Encoding.UTF8
            };

            using (var writer = XmlWriter.Create(filePath, settings))
            {
                var visitor = new SaveXshdVisitor(writer);
                visitor.WriteDefinition(xshd);
            }
        }

        private HighlightingBrush CreateHighlightingBrush(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex) || colorHex == "#00000000")
                return null;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                return new SimpleHighlightingBrush(color);
            }
            catch
            {
                return null;
            }
        }

        private static string GetBrushColorHex(HighlightingBrush? brush, string fallback)
        {
            if (brush == null)
            {
                return fallback;
            }

            var colorProperty = brush.GetType().GetProperty("Color");
            if (colorProperty?.GetValue(brush) is Color color)
            {
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            var text = brush.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    var converted = ColorConverter.ConvertFromString(text);
                    if (converted is Color parsedColor)
                    {
                        return $"#{parsedColor.A:X2}{parsedColor.R:X2}{parsedColor.G:X2}{parsedColor.B:X2}";
                    }
                }
                catch
                {
                }
            }

            return fallback;
        }

        private FontWeight? ConvertToFontWeight(string fontWeight)
        {
            return fontWeight?.ToLower() switch
            {
                "bold" => FontWeights.Bold,
                "normal" => FontWeights.Normal,
                "light" => FontWeights.Light,
                "ultrabold" => FontWeights.UltraBold,
                "semibold" => FontWeights.SemiBold,
                _ => FontWeights.Normal
            };
        }

        private FontStyle? ConvertToFontStyle(string fontStyle)
        {
            return fontStyle?.ToLower() switch
            {
                "italic" => FontStyles.Italic,
                "oblique" => FontStyles.Oblique,
                "normal" => FontStyles.Normal,
                _ => FontStyles.Normal
            };
        }
    }
}
