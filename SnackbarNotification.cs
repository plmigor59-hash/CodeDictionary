using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public class SnackbarNotification
{
    private Border? _snackbarBorder;
    private DispatcherTimer? _timer;
    private Panel? _parentPanel;

    public void Show(UIElement parent, string message, NotificationType type = NotificationType.Info, double durationSeconds = 2)
    {
        Close();

        _parentPanel = FindParentPanel(parent);
        if (_parentPanel == null) return;

        // Выбираем цвет фона в зависимости от типа
        Brush backgroundBrush;
        Brush foregroundBrush = Brushes.White;
        string icon;

        switch (type)
        {
            case NotificationType.Success:
                backgroundBrush = Application.Current.TryFindResource("SuccessBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(40, 167, 69));
                icon = "✓ ";
                break;
            case NotificationType.Warning:
                backgroundBrush = Application.Current.TryFindResource("WarningBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(255, 193, 7));
                foregroundBrush = Brushes.Black;
                icon = "⚠️ ";
                break;
            case NotificationType.Error:
                backgroundBrush = Application.Current.TryFindResource("DangerBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(220, 53, 69));
                icon = "✕ ";
                break;
            default:
                backgroundBrush = Application.Current.TryFindResource("AccentBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(23, 162, 184));
                icon = "ℹ ";
                break;
        }

        // Создаем контейнер с иконкой и текстом
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        var iconText = new TextBlock
        {
            Text = icon,
            Foreground = foregroundBrush,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var messageText = new TextBlock
        {
            Text = message,
            Foreground = foregroundBrush,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        stackPanel.Children.Add(iconText);
        stackPanel.Children.Add(messageText);

        _snackbarBorder = new Border
        {
            Background = backgroundBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 0, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, Opacity = 0.2, ShadowDepth = 2 },
            Child = stackPanel
        };

        _parentPanel.Children.Add(_snackbarBorder);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(durationSeconds)
        };

        _timer.Tick += (s, e) => Close();
        _timer.Start();
    }

    public void Close()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }

        if (_snackbarBorder != null && _parentPanel != null)
        {
            _parentPanel.Children.Remove(_snackbarBorder);
            _snackbarBorder = null;
        }
    }

    private Panel? FindParentPanel(UIElement element)
    {
        // Проверяем, не является ли элемент сам панелью
        if (element is Panel panel)
            return panel;

        // Ищем родителя-панель
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is Panel p)
                return p;
            parent = VisualTreeHelper.GetParent(parent);
        }

        // Запасной вариант - главное окно
        if (Application.Current.MainWindow?.Content is Panel mainPanel)
            return mainPanel;

        return null;
    }
}