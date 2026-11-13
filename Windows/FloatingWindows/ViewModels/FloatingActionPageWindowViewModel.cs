using System.Collections.ObjectModel;
using System.ComponentModel;
using Quicker.Database.Core;
using System.Windows.Media;
using Quicker.Converters;
using Quicker.Models;
using System.Windows;

namespace Quicker.Windows.FloatingWindows.ViewModels
{
    public class FloatingActionPageWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        public int ActionPageIndex { get; set; } // 当前按钮所在的页面索引
        public string TableName { get; set; } // 表名

        // 数据绑定属性
        private double _buttonSize;
        private double _buttonGap;
        private double _gridHeight;
        private int _buttonRows; // 按钮行数

        // 外观属性
        private double _borderWidth;
        private double _buttonCornerRadius;
        private string _actionButtonColor;
        private string _actionButtonMouseOverColor;
        private string _blankButtonColor;
        private string _blankButtonMouseOverColor;
        private string _buttonTextColor;
        private string _borderColor;
        private string _toolbarColor;
        private string _toolbarIconColor;
        private string _backgroundColor;
        private double _fontSize;
        private FontWeight _fontWeight;
        private bool _isPinned; // 是否固定窗口
        private int _win11CornerRadius; // Win11圆角模式
        private int _blur; // 模糊模式

        // 按钮数据集合
        private readonly ObservableCollection<ButtonData> _buttonDataCollection = new();
        private readonly ButtonDatabase _buttonDatabase = new();
        private bool _disposed = false;

        public ObservableCollection<ButtonData> ButtonDataCollection
        {
            get => _buttonDataCollection;
        }

        public double ButtonSize
        {
            get => _buttonSize;
            set
            {
                if (_buttonSize != value)
                {
                    _buttonSize = value;
                    OnPropertyChanged(nameof(ButtonSize));
                    CalculateGridHeight(); // 重新计算高度
                }
            }
        }

        public double ButtonGap
        {
            get => _buttonGap;
            set
            {
                if (_buttonGap != value)
                {
                    _buttonGap = value;
                    OnPropertyChanged(nameof(ButtonGap));
                    CalculateGridHeight(); // 重新计算高度
                }
            }
        }

        public double GridHeight
        {
            get => _gridHeight;
            set
            {
                if (_gridHeight != value)
                {
                    _gridHeight = value;
                    OnPropertyChanged(nameof(GridHeight));
                }
            }
        }

        public int ButtonRows
        {
            get => _buttonRows;
            set
            {
                if (_buttonRows != value)
                {
                    _buttonRows = value;
                    OnPropertyChanged(nameof(ButtonRows));
                }
            }
        }

        public double BorderWidth
        {
            get => _borderWidth;
            set
            {
                if (_borderWidth != value)
                {
                    _borderWidth = value;
                    OnPropertyChanged(nameof(BorderWidth));
                }
            }
        }

        public double ButtonCornerRadius
        {
            get => _buttonCornerRadius;
            set
            {
                if (_buttonCornerRadius != value)
                {
                    _buttonCornerRadius = value;
                    OnPropertyChanged(nameof(ButtonCornerRadius));
                }
            }
        }

        public string ActionButtonColor
        {
            get => _actionButtonColor;
            set
            {
                if (_actionButtonColor != value)
                {
                    _actionButtonColor = value;
                    OnPropertyChanged(nameof(ActionButtonColor));
                }
            }
        }

        public string ActionButtonMouseOverColor
        {
            get => _actionButtonMouseOverColor;
            set
            {
                if (_actionButtonMouseOverColor != value)
                {
                    _actionButtonMouseOverColor = value;
                    OnPropertyChanged(nameof(ActionButtonMouseOverColor));
                }
            }
        }

        public string BlankButtonColor
        {
            get => _blankButtonColor;
            set
            {
                if (_blankButtonColor != value)
                {
                    _blankButtonColor = value;
                    OnPropertyChanged(nameof(BlankButtonColor));
                }
            }
        }

        public string BlankButtonMouseOverColor
        {
            get => _blankButtonMouseOverColor;
            set
            {
                if (_blankButtonMouseOverColor != value)
                {
                    _blankButtonMouseOverColor = value;
                    OnPropertyChanged(nameof(BlankButtonMouseOverColor));
                }
            }
        }

        public string ButtonTextColor
        {
            get => _buttonTextColor;
            set
            {
                if (_buttonTextColor != value)
                {
                    _buttonTextColor = value;
                    OnPropertyChanged(nameof(ButtonTextColor));
                }
            }
        }

        public string BorderColor
        {
            get => _borderColor;
            set
            {
                if (_borderColor != value)
                {
                    _borderColor = value;
                    OnPropertyChanged(nameof(BorderColor));
                }
            }
        }

        public string ToolbarColor
        {
            get => _toolbarColor;
            set
            {
                if (_toolbarColor != value)
                {
                    _toolbarColor = value;
                    OnPropertyChanged(nameof(ToolbarColor));
                }
            }
        }

        public string ToolbarIconColor
        {
            get => _toolbarIconColor;
            set
            {
                if (_toolbarIconColor != value)
                {
                    _toolbarIconColor = value;
                    OnPropertyChanged(nameof(ToolbarIconColor));
                }
            }
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    OnPropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged(nameof(FontSize));
                }
            }
        }

        public FontWeight FontWeight
        {
            get => _fontWeight;
            set
            {
                if (_fontWeight != value)
                {
                    _fontWeight = value;
                    OnPropertyChanged(nameof(FontWeight));
                }
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged(nameof(IsPinned));
                }
            }
        }

        public int Win11CornerRadius
        {
            get => _win11CornerRadius;
            set
            {
                if (_win11CornerRadius != value)
                {
                    _win11CornerRadius = value;
                    OnPropertyChanged(nameof(Win11CornerRadius));
                }
            }
        }

        public int Blur
        {
            get => _blur;
            set
            {
                if (_blur != value)
                {
                    _blur = value;
                    OnPropertyChanged(nameof(Blur));
                }
            }
        }

        public FloatingActionPageWindowViewModel(int actionPageIndex, string tableName)
        {
            ActionPageIndex = actionPageIndex;
            TableName = tableName;
            LoadAppearanceSettings(); // 加载外观设置
            LoadButtonData(); // 加载按钮数据
        }

        private void LoadAppearanceSettings()
        {
            var appearance = SettingDatabase.GetAppearanceSettings()?.FirstOrDefault();
            if (appearance != null)
            {
                ButtonSize = appearance.ButtonSize;
                ButtonGap = appearance.ButtonGap;
                BorderWidth = appearance.BorderWidth;
                ButtonCornerRadius = appearance.ButtonCornerRadius;
                ActionButtonColor = appearance.ActionButtonColor;
                ActionButtonMouseOverColor = appearance.ActionButtonMouseOverColor;
                BlankButtonColor = appearance.BlankButtonColor;
                BlankButtonMouseOverColor = appearance.BlankButtonMouseOverColor;
                ButtonTextColor = appearance.ButtonTextColor;
                BorderColor = appearance.BorderColor;
                ToolbarColor = appearance.ToolbarColor;
                ToolbarIconColor = appearance.ToolbarIconColor;
                BackgroundColor = appearance.BackgroundColor;
                Win11CornerRadius = appearance.Win11CornerRadius;
                Blur = appearance.Blur;
                FontSize = appearance.FontSize;
                FontWeight = Converters.FontWeightConverter.IndexToFontWeight(appearance.FontWeight);
            }
        }

        /// <summary>
        /// 加载按钮数据
        /// </summary>
        public void LoadButtonData()
        {
            ButtonDataCollection.Clear(); // 清空现有数据
            // 获取当前页面的按钮数据
            var pageButtons = _buttonDatabase.GetPagesOfButtons(TableName, ActionPageIndex);
            foreach (var buttonData in pageButtons) // 将按钮数据添加到集合中
            {
                ButtonDataCollection.Add(buttonData);
            }
        }

        /// <summary>
        /// 获取指定位置的按钮数据
        /// </summary>
        /// <param name="row">行索引 (0-3)</param>
        /// <param name="col">列索引 (0-3)</param>
        /// <returns>按钮数据，如果没有数据则返回null</returns>
        public ButtonData GetButtonData(int row, int col)
        {
            int buttonId = ActionPageIndex * 100 + (row + 1) * 10 + (col + 1); // 计算按钮ID：ActionPageIndex * 100 + (row + 1) * 10 + (col + 1)
            return ButtonDataCollection.FirstOrDefault(b => b.ButtonID == buttonId); // 在集合中查找对应的按钮数据
        }

        /// <summary>
        /// 动态计算Grid高度
        /// </summary>
        private void CalculateGridHeight()
        {
            const double titleHeight = 31; // 标题栏高度
            if (TableName == "_global") // _global模式：3行按钮，通过调整高度隐藏第4行
            {
                ButtonRows = 3;
                GridHeight = titleHeight + ButtonSize * 3 + ButtonGap * 2;
            }
            else // 其他模式：4行按钮
            {
                ButtonRows = 4;
                GridHeight = titleHeight + ButtonSize * 4 + ButtonGap * 3;
            }
        }

        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    _buttonDataCollection.Clear();
                }
            }
            _disposed = true;
        }
    }
}