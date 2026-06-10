using CodeDictionary.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CodeDictionary;
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly TranslationService _translationService;
        private string _text;
        private string _selectedText;
        private string _originalText;
        private bool _isTranslating;
        private string _status;

        public MainViewModel()
        {
            _translationService = new TranslationService();
            Text = string.Empty;
            Status = string.Empty;

            TranslateCommand = new RelayCommand(async () => await ExecuteTranslateAsync(), () => !IsTranslating && !string.IsNullOrWhiteSpace(SelectedText));
            TranslateToEnglishCommand = new RelayCommand(async () => await ExecuteTranslateToEnglishAsync(), () => !IsTranslating && !string.IsNullOrWhiteSpace(SelectedText));
            ClearCommand = new RelayCommand(() => ExecuteClear(), () => !IsTranslating && !string.IsNullOrEmpty(OriginalText));
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

        public string OriginalText
        {
            get => _originalText;
            set { _originalText = value; OnPropertyChanged(); }
        }

        public bool IsTranslating
        {
            get => _isTranslating;
            set { _isTranslating = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
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

        private void ExecuteClear()
        {
            if (!string.IsNullOrEmpty(OriginalText))
            {
                ReplaceSelectedText?.Invoke(OriginalText);
                OriginalText = null;
                Status = "Текст восстановлен";
            }
        }

        private async Task TranslateTextAsync(string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(SelectedText))
                return;

            IsTranslating = true;
            Status = "Перевод...";

            try
            {
                // Сохраняем оригинал перед первым переводом
                if (string.IsNullOrEmpty(OriginalText))
                {
                    OriginalText = SelectedText;
                }

                var translated = await _translationService.TranslateTextAsync(SelectedText, targetLanguage);

                if (translated.StartsWith("Ошибка:"))
                {
                    Status = translated;
                }
                else
                {
                    ReplaceSelectedText?.Invoke(translated);
                    Status = "Готово";
                }
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsTranslating = false;
            }
        }

        // Этот метод будет вызываться из code-behind для замены текста в AvalonEdit
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
            if (_executeAsync != null)
                await _executeAsync();
            else if (_executeSync != null)
                _executeSync();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

