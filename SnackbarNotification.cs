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
    private Border _snackbarBorder;
    private DispatcherTimer _timer;
    private Panel _parentPanel;

    public void Show(UIElement parent, string message, NotificationType type = NotificationType.Info, double durationSeconds = 2)
    {
        Close();

        _parentPanel = FindParentPanel(parent);
        if (_parentPanel == null) return;

        // Выбираем цвет фона в зависимости от типа
        Brush backgroundBrush;
        string icon;

        switch (type)
        {
            case NotificationType.Success:
                backgroundBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Зеленый
                icon = "✓ ";
                break;
            case NotificationType.Warning:
                backgroundBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Желтый
                icon = "⚠️ ";
                break;
            case NotificationType.Error:
                backgroundBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Красный
                icon = "❌ ";
                break;
            default:
                backgroundBrush = new SolidColorBrush(Color.FromRgb(23, 162, 184)); // Синий
                icon = "ℹ️ ";
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
            Foreground = type == NotificationType.Warning ? Brushes.Black : Brushes.White,
            FontSize = 14,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var messageText = new TextBlock
        {
            Text = message,
            Foreground = type == NotificationType.Warning ? Brushes.Black : Brushes.White,
            FontSize = 14
        };

        stackPanel.Children.Add(iconText);
        stackPanel.Children.Add(messageText);

        _snackbarBorder = new Border
        {
            Background = backgroundBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(15, 10, 15, 10),
            Margin = new Thickness(0, 0, 0, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
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

    private Panel FindParentPanel(UIElement element)
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