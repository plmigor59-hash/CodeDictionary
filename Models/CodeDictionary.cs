namespace CodeDictionary.Models;

public class CodeDictionaryData
{
    public List<CodeEntry> Entries { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}
