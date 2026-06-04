using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CodeDictionary
{
    public enum CustomMessageBoxType
    {
        Info,
        Warning,
        Error,
        Question
    }

    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox()
        {
            InitializeComponent();
            ApplyTheme(Theme.CurrentTheme);
        }

        public static void Show(string message, string title = "Сообщение", CustomMessageBoxType type = CustomMessageBoxType.Info)
        {
            var msgBox = Create(message, title, type);
            msgBox.ShowDialog();
        }

        public static bool ShowQuestion(string message, string title = "Вопрос")
        {
            var msgBox = Create(message, title, CustomMessageBoxType.Question);
            return msgBox.ShowDialog() == true;
        }

        private static CustomMessageBox Create(string message, string title, CustomMessageBoxType type)
        {
            var msgBox = new CustomMessageBox
            {
                Owner = Application.Current?.MainWindow
            };

            msgBox.Title = title;
            msgBox.TitleText.Text = title;
            msgBox.MessageText.Text = message;
            msgBox.CancelButton.Visibility = Visibility.Collapsed;
            msgBox.OkButton.Content = "OK";

            switch (type)
            {
                case CustomMessageBoxType.Warning:
                    msgBox.CaptionText.Text = "Предупреждение";
                    msgBox.IconText.Text = "!";
                    msgBox.SetSeverityBrushes("DictWarning", "DictWarningBackground");
                    break;

                case CustomMessageBoxType.Error:
                    msgBox.CaptionText.Text = "Произошла ошибка";
                    msgBox.IconText.Text = "!";
                    msgBox.SetSeverityBrushes("DictError", "DictErrorBackground");
                    break;

                case CustomMessageBoxType.Question:
                    msgBox.CaptionText.Text = "Подтвердите действие";
                    msgBox.OkButton.Content = "Да";
                    msgBox.CancelButton.Content = "Нет";
                    msgBox.CancelButton.Visibility = Visibility.Visible;
                    msgBox.IconText.Text = "?";
                    msgBox.SetSeverityBrushes("DictChat", "DictQuestionBackground");
                    break;

                default:
                    msgBox.CaptionText.Text = "Системное сообщение";
                    msgBox.IconText.Text = "!";
                    msgBox.SetSeverityBrushes("DictAccent", "DictInfoBackground");
                    break;
            }

            return msgBox;
        }

        private void ApplyTheme(AppTheme theme)
        {
            var isDark = theme == AppTheme.Dark;

            SetBrush("DictSurface", isDark ? Theme.Dark.Background : Theme.Light.Background);
            SetBrush("DictSurfaceStrong", isDark ? "#273447" : "#F5F5F5");
            SetBrush("DictBorder", isDark ? Theme.Dark.Border : Theme.Light.Border);
            SetBrush("DictCardBackground", isDark ? "#202A38" : "#FFFFFF");
            SetBrush("DictTitleBackground", isDark ? "#A51A2534" : "#EDEDED");
            SetBrush("DictStatusBackground", isDark ? "#132f4b" : "#EAF3FD");
            SetBrush("DictAccent", isDark ? Theme.Dark.ButtonPrimary : Theme.Light.ButtonPrimary);
            SetBrush("DictTextPrimary", isDark ? Theme.Dark.TextPrimary : Theme.Light.TextPrimary);
            SetBrush("DictTextSecondary", isDark ? Theme.Dark.TextSecondary : Theme.Light.TextSecondary);
            SetBrush("DictSuccess", isDark ? "#4ADE80" : "#16A34A");
            SetBrush("DictChat", isDark ? "#FFD700" : "#B45309");
            SetBrush("DarkBackground", isDark ? "#1E1E1E" : "#FFFFFF");
            SetBrush("DictIconBackground", isDark ? "#233652" : "#EAF3FD");
            SetBrush("DictInfoBackground", isDark ? "#233652" : "#EAF3FD");
            SetBrush("DictQuestionBackground", isDark ? "#3A2F0B" : "#FFF4CC");
            SetBrush("DictWarning", isDark ? "#F59E0B" : "#B45309");
            SetBrush("DictWarningBackground", isDark ? "#3A2F0B" : "#FFF4CC");
            SetBrush("DictError", isDark ? "#F87171" : "#C62828");
            SetBrush("DictErrorBackground", isDark ? "#4A1C1C" : "#FDECEC");
        }

        private void SetSeverityBrushes(string accentKey, string backgroundKey)
        {
            var accentBrush = (Brush)FindResource(accentKey);
            var backgroundBrush = (Brush)FindResource(backgroundKey);

            IconText.Foreground = accentBrush;
            HeaderDot.Foreground = accentBrush;
            IconBackground.Background = backgroundBrush;
        }

        private void SetBrush(string resourceKey, string color)
        {
            Resources[resourceKey] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
