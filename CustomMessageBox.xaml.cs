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
        private static readonly Brush DefaultIconBackground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#233652"));
        
        public CustomMessageBox()
        {
            InitializeComponent();
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
            msgBox.IconBackground.Background = DefaultIconBackground;

            switch (type)
            {
                case CustomMessageBoxType.Warning:
                    msgBox.CaptionText.Text = "Предупреждение";
                    msgBox.IconText.Text = "!";
                    msgBox.IconText.Foreground = (Brush)msgBox.FindResource("DictWarning");
                    msgBox.IconBackground.Background = (Brush)msgBox.FindResource("DictWarningBackground");
                    msgBox.HeaderDot.Foreground = (Brush)msgBox.FindResource("DictWarning");
                    break;

                case CustomMessageBoxType.Error:
                    msgBox.CaptionText.Text = "Произошла ошибка";
                    msgBox.IconText.Text = "!";
                    msgBox.IconText.Foreground = (Brush)msgBox.FindResource("DictError");
                    msgBox.IconBackground.Background = (Brush)msgBox.FindResource("DictErrorBackground");
                    msgBox.HeaderDot.Foreground = (Brush)msgBox.FindResource("DictError");
                    break;

                case CustomMessageBoxType.Question:
                    msgBox.CaptionText.Text = "Подтвердите действие";
                    msgBox.OkButton.Content = "Да";
                    msgBox.CancelButton.Content = "Нет";
                    msgBox.CancelButton.Visibility = Visibility.Visible;
                    msgBox.IconText.Text = "?";
                    msgBox.IconText.Foreground = (Brush)msgBox.FindResource("DictChat");
                    msgBox.HeaderDot.Foreground = (Brush)msgBox.FindResource("DictChat");
                    break;

                default:
                    msgBox.CaptionText.Text = "Системное сообщение";
                    msgBox.IconText.Text = "!";
                    msgBox.IconText.Foreground = (Brush)msgBox.FindResource("DictAccent");
                    msgBox.HeaderDot.Foreground = (Brush)msgBox.FindResource("DictAccent");
                    break;
            }

            return msgBox;
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
