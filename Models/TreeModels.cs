using CodeDictionary.Models;
using System.ComponentModel;

namespace CodeDictionary
{
    public class CategoryNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _isChecked;
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public List<CategoryNode> Children { get; set; } = new();
        public List<CodeEntryViewModel> Entries { get; set; } = new();
        public IEnumerable<object> Items => Children.Cast<object>().Concat(Entries);
        public int TotalEntryCount => Entries.Count + Children.Sum(child => child.TotalEntryCount);
        public string CountString => $"({TotalEntryCount})";
        public bool CanDelete => !string.IsNullOrWhiteSpace(FullPath);

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    foreach (var child in Children) child.IsChecked = value;
                    foreach (var entry in Entries) entry.IsChecked = value;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class CodeEntryViewModel : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isSelected;
        public CodeEntry Entry { get; }
        public string Title => Entry.Title;

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public CodeEntryViewModel(CodeEntry entry)
        {
            Entry = entry;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}