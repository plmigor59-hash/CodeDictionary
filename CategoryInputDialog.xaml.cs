using System.Windows;

namespace CodeDictionary;

public partial class CategoryInputDialog : Window
{
    public string CategoryName => CategoryNameTextBox.Text.Trim();

    public CategoryInputDialog(string? title = null, string? prompt = null, string? initialValue = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            PromptTextBlock.Text = prompt;
        }

        if (!string.IsNullOrWhiteSpace(initialValue))
        {
            CategoryNameTextBox.Text = initialValue;
        }

        Loaded += CategoryInputDialog_Loaded;
    }

    private void CategoryInputDialog_Loaded(object sender, RoutedEventArgs e)
    {
        CategoryNameTextBox.Focus();
        CategoryNameTextBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
        {
            CustomMessageBox.Show("Введите название категории.", "Проверка", CustomMessageBoxType.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
