using System.ComponentModel;
using System.Windows.Media;
using Quicker.Helpers;

namespace Quicker.Models
{
    /// <summary>
    /// 应用信息模型：承载搜索结果展示与启动所需的所有信息。
    /// </summary>
    public class AppInfo : INotifyPropertyChanged
    {
        // ==================== 展示信息 ====================
        public string Name { get; set; }
        public string Location { get; set; }
        public ImageSource Icon { get; set; }
        public string Tag { get; set; }

        // ==================== 搜索支持 ====================
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged(nameof(NameAndSearchText));
                OnPropertyChanged(nameof(TagAndSearchText));
            }
        }

        public HighlightTextData NameAndSearchText =>
            new HighlightTextData { Text = Name, Keyword = SearchText };

        public HighlightTextData TagAndSearchText =>
            new HighlightTextData { Text = Tag, Keyword = SearchText };

        // ==================== INotifyPropertyChanged ====================
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}