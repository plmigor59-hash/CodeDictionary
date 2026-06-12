using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CodeDictionary;

public partial class GoToLineWindow : Window
{
    private readonly int _maxLine;
    private readonly Action<int> _onGoToLine;

    public GoToLineWindow(int currentLine, int maxLine, Action<int> onGoToLine)
    {
        _maxLine = maxLine;
        _onGoToLine = onGoToLine;
        
        InitializeComponent();
        LineNumberTextBox.Text = currentLine.ToString();
        LineNumberTextBox.SelectAll();
        LineNumberTextBox.Focus();
    }

    private TextBox LineNumberTextBox;

    private void InitializeComponent()
    {
        this.Title = "Перейти к строке";
        this.Width = 250;
        this.Height = 130;
        this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.ResizeMode = ResizeMode.NoResize;
        this.WindowStyle = WindowStyle.ToolWindow;
        
        this.SetResourceReference(Window.BackgroundProperty, "WindowBackground");
        
        var grid = new Grid();
        grid.Margin = new Thickness(15);
        this.Content = grid;

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock 
        { 
            Text = $"Номер строки (1-{_maxLine}):",
            Margin = new Thickness(0, 0, 0, 8)
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        LineNumberTextBox = new TextBox
        {
            Height = 26,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5, 0, 5, 0)
        };
        LineNumberTextBox.SetResourceReference(TextBox.BackgroundProperty, "SurfaceBackground");
        LineNumberTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
        LineNumberTextBox.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
        
        LineNumberTextBox.KeyDown += (s, e) => {
            if (e.Key == Key.Enter) Confirm();
            if (e.Key == Key.Escape) this.Close();
        };
        
        Grid.SetRow(LineNumberTextBox, 1);
        grid.Children.Add(LineNumberTextBox);
        
        var buttonPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        // We don't have direct access to ModernButton style here easily without XAML, 
        // but we can make it look decent.
        var okButton = new Button { Content = "Перейти", Width = 75, Height = 26, IsDefault = true };
        okButton.Click += (s, e) => Confirm();
        
        buttonPanel.Children.Add(okButton);
        // Note: In a real app we'd use XAML for proper styling, but this works for a quick functional addition.
    }

    private void Confirm()
    {
        if (int.TryParse(LineNumberTextBox.Text, out int line))
        {
            if (line >= 1 && line <= _maxLine)
            {
                _onGoToLine(line);
                this.Close();
            }
            else
            {
                MessageBox.Show($"Введите число от 1 до {_maxLine}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
