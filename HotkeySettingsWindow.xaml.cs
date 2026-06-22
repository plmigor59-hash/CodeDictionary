using CodeDictionary.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CodeDictionary;

public partial class HotkeySettingsWindow : Window
{
    private readonly HotkeyManager _hotkeyManager;
    private bool _isCapturing;
    private HotkeyAction? _capturingAction;

    public HotkeySettingsWindow(HotkeyManager hotkeyManager)
    {
        _hotkeyManager = hotkeyManager;
        InitializeComponent();

        HotkeyList.ItemsSource = _hotkeyManager.Actions;
    }

    private void HotkeyList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HotkeyList.SelectedItem is HotkeyAction action)
        {
            BeginCapture(action);
        }
    }

    private void BeginCapture(HotkeyAction action)
    {
        _capturingAction = action;
        _isCapturing = true;
        HotkeyList.IsEnabled = false;
        ResetButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        Title = $"Настройка горячих клавиш — нажмите новое сочетание для \"{action.DisplayName}\"";
    }

    private void EndCapture()
    {
        _isCapturing = false;
        _capturingAction = null;
        HotkeyList.IsEnabled = true;
        ResetButton.IsEnabled = true;
        CloseButton.IsEnabled = true;
        Title = "Настройка горячих клавиш";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_isCapturing && _capturingAction != null)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                EndCapture();
                e.Handled = true;
                return;
            }

            if (key == Key.None || key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            var modifiers = Keyboard.Modifiers;
            var gestureStr = modifiers == ModifierKeys.None
                ? key.ToString()
                : $"{modifiers}+{key}";

            // Validate it's not empty
            if (!string.IsNullOrWhiteSpace(gestureStr))
            {
                // Check for conflicts
                foreach (var other in _hotkeyManager.Actions)
                {
                    if (other.Id != _capturingAction.Id &&
                        string.Equals(other.CurrentGesture, gestureStr, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(gestureStr))
                    {
                        var confirm = MessageBox.Show(
                            $"Сочетание \"{gestureStr}\" уже используется для \"{other.DisplayName}\".\n\nПрименить всё равно?",
                            "Конфликт горячих клавиш",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        if (confirm != MessageBoxResult.Yes)
                        {
                            EndCapture();
                            e.Handled = true;
                            return;
                        }
                    }
                }

                _hotkeyManager.SetGesture(_capturingAction.Id, gestureStr);
                EndCapture();

                // Refresh list
                HotkeyList.ItemsSource = null;
                HotkeyList.ItemsSource = _hotkeyManager.Actions;
            }

            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Сбросить все горячие клавиши на значения по умолчанию?",
            "Сброс настроек",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            _hotkeyManager.ResetAll();
            HotkeyList.ItemsSource = null;
            HotkeyList.ItemsSource = _hotkeyManager.Actions;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
