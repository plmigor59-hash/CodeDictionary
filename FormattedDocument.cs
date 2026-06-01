using System;
using System.Collections.Generic;
using System.Text;




namespace CodeDictionary
{
    // Модель для хранения форматированного текста
    // Класс-контейнер для сохранения всех сегментов
    public class FormattingContainer
    {
        public List<TextSegmentStyle> Segments { get; set; } = new();
        public int Version { get; set; } = 1;
        public DateTime SavedAt { get; set; } = DateTime.Now;
    }

    // Стиль для выделенного отрезка
    public class TextSegmentStyle
    {
        public int StartOffset { get; set; }      // Начальная позиция
        public int Length { get; set; }           // Длина выделения

        // Цвета (сохраняем как строки HTML)
        public string? BackgroundColor { get; set; }  // Фон
        public string? ForegroundColor { get; set; }  // Цвет текста

        public string? ForegroundColorResourceKey { get; set; }  // "DictTextPrimary"
        public string? BackgroundColorResourceKey { get; set; }  // null = прозрачный


        // Дополнительные стили
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }

        // Шрифт и размер
        public string? FontFamily { get; set; }   // Название шрифта (например, "Consolas")
        public double? FontSize { get; set; }     // Размер шрифта (например, 14.0)

        public TextSegmentStyle()
        {
            BackgroundColor = "#00000000";  // Прозрачный по умолчанию
            ForegroundColor = "#000000";
            IsBold = false;
            IsItalic = false;
            IsUnderline = false;
            FontFamily = null;  // null = использовать шрифт по умолчанию
            FontSize = null;    // null = использовать размер по умолчанию
        }
    }
}
