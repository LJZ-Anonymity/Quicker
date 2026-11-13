using System.ComponentModel;
using Quicker.Database.Core;
using System.Windows.Media;
using Quicker.Models;

namespace Quicker.Windows.FloatingWindows.ViewModels
{
    public class FloatingActionWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        public int ButtonID { get; set; } // 当前按钮
        public string TableName { get; set; } // 表名

        // 外观属性
        private double _buttonSize;
        private string _actionButtonColor;
        private string _actionButtonMouseOverColor;
        private string _backgroundColor;
        private int _win11CornerRadius;
        private int _blur;

        // 按钮数据
        private ButtonData _buttonData;
        private readonly ButtonDatabase _buttonDatabase = new();
        private bool _disposed = false;

        public double ButtonSize
        {
            get => _buttonSize;
            set
            {
                if (_buttonSize != value)
                {
                    _buttonSize = value;
                    OnPropertyChanged(nameof(ButtonSize));
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

        public ButtonData ButtonData
        {
            get => _buttonData;
            set
            {
                if (_buttonData != value)
                {
                    _buttonData = value;
                    OnPropertyChanged(nameof(ButtonData));
                }
            }
        }

        public FloatingActionWindowViewModel(int buttonID, string tableName)
        {
            ButtonID = buttonID;
            TableName = tableName;
            
            // 加载外观设置
            LoadAppearanceSettings();
            
            // 加载按钮数据
            LoadButtonData();
        }

        /// <summary>
        /// 加载外观设置
        /// </summary>
        private void LoadAppearanceSettings()
        {
            var appearance = SettingDatabase.GetAppearanceSettings()?.FirstOrDefault();
            if (appearance != null)
            {
                ButtonSize = appearance.ButtonSize;
                ActionButtonColor = appearance.ActionButtonColor;
                ActionButtonMouseOverColor = appearance.ActionButtonMouseOverColor;
                BackgroundColor = appearance.BackgroundColor;
                Win11CornerRadius = appearance.Win11CornerRadius;
                Blur = appearance.Blur;
            }
        }

        /// <summary>
        /// 加载按钮数据
        /// </summary>
        private void LoadButtonData()
        {
            ButtonData = _buttonDatabase.GetButtonDataByID(ButtonID, TableName);
        }

        /// <summary>
        /// 增加动作使用次数
        /// </summary>
        public void IncreaseActionUsedTimes()
        {
            _buttonDatabase.IncreaseActionUsedTimes(ButtonID, TableName);
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
                    // ButtonDatabase没有实现IDisposable，不需要手动释放
                }
                // 释放非托管资源
                _disposed = true;
            }
        }
    }
}