using CodeDictionary.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CodeDictionary.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly TranslationService _translationService;
        private string _text;
        private string _selectedText;
        private bool _isTranslating;

        public MainViewModel()
        {
            _translationService = new TranslationService();
            Text = string.Empty;

            TranslateCommand = new RelayCommand(async () => await ExecuteTranslateAsync(), () => !IsTranslating);
            TranslateToEnglishCommand = new RelayCommand(async () => await ExecuteTranslateToEnglishAsync(), () => !IsTranslating);
            ClearCommand = new RelayCommand(() => Text = string.Empty);
        }

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        public string SelectedText
        {
            get => _selectedText;
            set { _selectedText = value; OnPropertyChanged(); }
        }

        public bool IsTranslating
        {
            get => _isTranslating;
            set { _isTranslating = value; OnPropertyChanged(); }
        }

        public ICommand TranslateCommand { get; }
        public ICommand TranslateToEnglishCommand { get; }
        public ICommand ClearCommand { get; }

        private async Task ExecuteTranslateAsync()
        {
            await TranslateTextAsync("ru");
        }

        private async Task ExecuteTranslateToEnglishAsync()
        {
            await TranslateTextAsync("en");
        }

        private async Task TranslateTextAsync(string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(SelectedText))
                return;

            IsTranslating = true;

            try
            {
                var translated = await _translationService.TranslateTextAsync(SelectedText, targetLanguage);

                // Опционально: заменяем выделенный текст на перевод
                // В реальном приложении нужно получить ссылку на TextEditor
                ReplaceSelectedText(translated);
            }
            finally
            {
                IsTranslating = false;
            }
        }

        // Этот метод будет вызываться из code-behind
        public Action<string> ReplaceSelectedText { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action _executeSync;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        // ✅ Для синхронных команд (добавить этот конструктор)
        public RelayCommand(Action executeSync, Func<bool> canExecute = null)
        {
            _executeSync = executeSync;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public async void Execute(object parameter)
        {
            await _executeAsync();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
