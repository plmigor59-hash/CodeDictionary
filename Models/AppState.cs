namespace CodeDictionary.Models;

public class AppState
{
    public string SelectedCategory { get; set; } = "Все категории";
    public Guid? SelectedEntryId { get; set; }
    public bool IsLightTheme { get; set; } = false;
}
