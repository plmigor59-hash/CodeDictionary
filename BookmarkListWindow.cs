using CodeDictionary.Models;
using System.Windows;
using System.Windows.Controls;

namespace CodeDictionary;

public partial class BookmarkListWindow : Window
{
    private readonly EditorTabModel _tab;
    private readonly Action<int> _navigateToLine;

    public BookmarkListWindow(EditorTabModel tab, Action<int> navigateToLine)
    {
        _tab = tab;
        _navigateToLine = navigateToLine;

        InitializeComponent();
        LoadBookmarks();
    }

    private void InitializeComponent()
    {
        this.Title = "Закладки";
        this.WindowStyle = WindowStyle.ToolWindow;
        this.Width = 500;
        this.Height = 500;
        this.BorderThickness = new Thickness(1);
        this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Use resources from MainWindow/App
        this.SetResourceReference(Window.BackgroundProperty, "WindowBackground");

        var grid = new Grid();
        this.Content = grid;

        var listbox = new ListBox();
        listbox.SetResourceReference(ListBox.BackgroundProperty, "SurfaceBackground");
        listbox.SetResourceReference(ListBox.ForegroundProperty, "TextPrimaryBrush");
        listbox.BorderThickness = new Thickness(0);
        listbox.Margin = new Thickness(8);

        listbox.MouseDoubleClick += (s, e) =>
        {
            if (listbox.SelectedItem is BookmarkItem item)
            {
                _navigateToLine(item.LineNumber);
                this.Close();
            }
        };

        // Item Template for better look
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DisplayText"));
        listbox.ItemTemplate = new DataTemplate { VisualTree = factory };

        grid.Children.Add(listbox);
    }

    private void LoadBookmarks()
    {
        var listbox = (ListBox)((Grid)this.Content).Children[0];
        var bookmarks = _tab.Bookmarks.OrderBy(b => b).Select(b =>
        {
            var line = _tab.Document.GetLineByNumber(b);
            var text = _tab.Document.GetText(line.Offset, line.Length).Trim();
            if (text.Length > 50) text = text.Substring(0, 47) + "...";
            return new BookmarkItem { LineNumber = b, DisplayText = $"стр. {b}: {text}" };
        });
        listbox.ItemsSource = bookmarks;
    }

    private class BookmarkItem
    {
        public int LineNumber { get; set; }
        public string DisplayText { get; set; }
    }
}
