namespace CodeDictionary.Models;

public class CodeEntry
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Syntax { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string FormattingData { get; set; } = string.Empty; // JSON строка с форматированием

    public CodeEntry()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.Now;
        ModifiedAt = DateTime.Now;
    }

}
