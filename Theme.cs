namespace CodeDictionary;

public enum AppTheme
{
    Dark,
    Light
}

public static class Theme
{
    public static class Dark
    {
        public const string Background = "#060606";
        public const string SidePanel = "#252526";
        public const string Input = "#3C3C3C";
        public const string TextPrimary = "#CCCCCC";
        public const string TextSecondary = "#858585";
        public const string Border = "#555555";
        public const string CodeBackground = "#1E1E1E";
        public const string CodeForeground = "#D4D4D4";
        public const string ButtonPrimary = "#0E639C";
        public const string ButtonDanger = "#C72E2E";
    }

    public static class Light
    {
        public const string Background = "#FFFFFF";
        public const string SidePanel = "#F3F3F3";
        public const string Input = "#FFFFFF";
        public const string TextPrimary = "#000000";
        public const string TextSecondary = "#666666";
        public const string Border = "#CCCCCC";
        public const string CodeBackground = "#FFFFFF";
        public const string CodeForeground = "#000000";
        public const string ButtonPrimary = "#0078D4";
        public const string ButtonDanger = "#D13438";
    }
}
