using System.Windows;

namespace CodeDictionary;

public partial class CategoryInputDialog : Window
{
    public string CategoryName => CategoryNameTextBox.Text.Trim();

    public CategoryInputDialog()
    {
        InitializeComponent();
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
            MessageBox.Show("Введите название категории.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
