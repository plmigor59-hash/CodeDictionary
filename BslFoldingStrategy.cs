using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace CodeDictionary
{
    public class BslFoldingStrategy
    {
        private readonly string[] _blockStart = { "ПРОЦЕДУРА", "ФУНКЦИЯ", "ЕСЛИ", "ДЛЯ", "ПОКА", "ПОПЫТКА", "#ОБЛАСТЬ" };
        private readonly string[] _blockEnd = { "КОНЕЦПРОЦЕДУРЫ", "КОНЕЦФУНКЦИИ", "КОНЕЦЕСЛИ", "КОНЕЦЦИКЛА", "КОНЕЦПОПЫТКИ", "#КОНЕЦОБЛАСТИ" };

        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            int firstErrorOffset;
            IEnumerable<NewFolding> newFoldings = CreateNewFoldings(document, out firstErrorOffset);
            manager.UpdateFoldings(newFoldings, firstErrorOffset);
        }

        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            List<NewFolding> newFoldings = new List<NewFolding>();
            Stack<(int startOffset, string type, string header)> stack = new Stack<(int, string, string)>();

            for (int i = 1; i <= document.LineCount; i++)
            {
                var line = document.GetLineByNumber(i);
                var text = document.GetText(line).Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var upperText = text.ToUpper();

                // Check for start of block
                bool foundStart = false;
                foreach (var start in _blockStart)
                {
                    if (upperText.StartsWith(start))
                    {
                        // Special check for Если/Для/Пока to ensure they are at word boundary if possible, 
                        // but StartsWith is usually enough for BSL if trimmed
                        stack.Push((line.Offset, start, text));
                        foundStart = true;
                        break;
                    }
                }

                if (foundStart) continue;

                // Check for end of block
                foreach (var end in _blockEnd)
                {
                    if (upperText.StartsWith(end) && stack.Count > 0)
                    {
                        // Check if it matches the top of the stack (simplified)
                        var startItem = stack.Pop();

                        // Basic matching logic:
                        // Процедура -> КонецПроцедуры
                        // Функция -> КонецФункции
                        // Если -> КонецЕсли
                        // Для/Пока -> КонецЦикла
                        // Попытка -> КонецПопытки
                        // #Область -> #КонецОбласти

                        bool match = false;
                        if (startItem.type == "ПРОЦЕДУРА" && end == "КОНЕЦПРОЦЕДУРЫ") match = true;
                        else if (startItem.type == "ФУНКЦИЯ" && end == "КОНЕЦФУНКЦИИ") match = true;
                        else if (startItem.type == "ЕСЛИ" && end == "КОНЕЦЕСЛИ") match = true;
                        else if ((startItem.type == "ДЛЯ" || startItem.type == "ПОКА") && end == "КОНЕЦЦИКЛА") match = true;
                        else if (startItem.type == "ПОПЫТКА" && end == "КОНЕЦПОПЫТКИ") match = true;
                        else if (startItem.type == "#ОБЛАСТЬ" && end == "#КОНЕЦОБЛАСТИ") match = true;

                        if (match)
                        {
                            if (line.EndOffset > startItem.startOffset)
                            {
                                newFoldings.Add(new NewFolding(startItem.startOffset, line.EndOffset) { Name = startItem.header });
                            }
                        }
                        else
                        {
                            // If it doesn't match, we might have nested blocks that are not closed properly 
                            // or we are closing a different block. For simplicity, we just pop until we find a match or empty stack.
                            // But in BSL, it's usually strict.
                        }
                        break;
                    }
                }
            }

            newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return newFoldings;
        }
    }
}
