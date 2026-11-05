using Quicker.Windows.FloatingWindows.ViewModels;
using System.Threading.Tasks;
using Quicker.Windows.Menus;
using System.Windows.Input;
using System.Windows.Media;
using Quicker.Managers;
using System.Windows;
using Quicker.Models;

namespace Quicker.Windows.FloatingWindows.Windows
{
    public partial class FloatingActionWindow : Window
    {
        private readonly FloatingActionWindowViewModel viewModel; // ViewModel
        private readonly ButtonManager buttonManager = new(); // 按钮管理器
        private SolidColorBrush _normalBrush; // 按钮初始背景色
        private SolidColorBrush _hoverBrush; // 按钮悬停背景色
        private bool _isDragging = false; // 是否正在拖动
        private Point _mouseDownPoint; // 鼠标按下时的位置

        /// <summary>
        /// 悬浮动作窗体
        /// </summary>
        /// <param name="buttonID">按钮ID</param>
        /// <param name="tableName">表名</param>
        public FloatingActionWindow(int buttonID, string tableName)
        {
            InitializeComponent();

            // 创建并设置ViewModel
            viewModel = new FloatingActionWindowViewModel(buttonID, tableName);
            this.DataContext = viewModel;
        }

        // 加载窗体
        private void FloatingActionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetWindowPositionAndTopmost(); // 设置窗口位置和置顶
            SetButtonAppearance(); // 设置按钮外观
            LoadButtonData(); // 加载按钮数据并刷新显示
        }

        /// <summary>
        /// 设置窗口位置和置顶
        /// </summary>
        private void SetWindowPositionAndTopmost()
        {
            using var windowManager = new WindowManager(); // 创建窗口管理器
            windowManager.SetWindowPositionNearMouse(this); // 设置窗口位置
            windowManager.SetWindowTopmost(this); // 设置窗口置顶
        }

        /// <summary>
        /// 设置按钮外观
        /// </summary>
        private void SetButtonAppearance()
        {
            _normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(viewModel.ActionButtonColor)); // 设置按钮初始背景色
            _hoverBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(viewModel.ActionButtonMouseOverColor)); // 设置按钮悬停背景色
            Button.Background = _normalBrush; // 设置按钮初始背景色
        }

        /// <summary>
        /// 加载按钮数据并刷新显示
        /// </summary>
        private void LoadButtonData()
        {
            if (viewModel.ButtonData != null)
            {
                buttonManager.RefreshButtonDisplay(Button, viewModel.ButtonData, viewModel.ButtonSize);
            }
        }

        /// <summary>
        /// 鼠标左键按下时记录初始位置，准备判断是否为拖动
        /// </summary>
        private void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPoint = e.GetPosition(this); // 记录初始位置
            _isDragging = false; // 重置拖动状态
        }

        /// <summary>
        /// 鼠标移动时判断是否达到拖动阈值，若达到则拖动窗口
        /// </summary>
        private void Button_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) // 判断是否左键按下
            {
                Point currentPoint = e.GetPosition(this); // 记录当前位置
                if (!_isDragging && (Math.Abs(currentPoint.X - _mouseDownPoint.X) > 5 ||
                    Math.Abs(currentPoint.Y - _mouseDownPoint.Y) > 5)) // 判断是否达到拖动阈值
                {
                    _isDragging = true; // 开始拖动
                    DragMove(); // 开始拖动窗口
                }
            }
        }

        /// <summary>
        /// 鼠标左键释放时重置拖动状态
        /// </summary>
        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false; // 重置拖动状态
        }

        /// <summary>
        /// 按钮点击事件，未拖动时执行按钮动作
        /// </summary>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDragging && viewModel.ButtonData != null)
            {
                using (var actionManager = new ActionManager())
                {
                    await actionManager.DoActionAsync(viewModel.ButtonData, viewModel.TableName); // 执行动作
                }
                viewModel.IncreaseActionUsedTimes(); // 增加动作使用次数
            }
        }

        /// <summary>
        /// 鼠标移入按钮时切换为悬停背景色
        /// </summary>
        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button.Background = _hoverBrush; // 设置悬停背景色
        }

        /// <summary>
        /// 鼠标移出按钮时恢复为普通背景色
        /// </summary>
        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Button.Background = _normalBrush; // 设置按钮初始背景色
        }

        /// <summary>
        /// 右键点击按钮时显示操作菜单
        /// </summary>
        private void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            OperationMenu menu = new OperationMenu(viewModel.ButtonID, viewModel.TableName, this); // 创建操作菜单
            menu.Show(); // 显示菜单
        }

        /// <summary>
        /// 关闭窗口释放资源
        /// </summary>
        private void FloatingActionWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            // 释放引用
            Button.Tag = null;
            _normalBrush = null;
            _hoverBrush = null;

            buttonManager?.Dispose(); // 释放管理器
            viewModel?.Dispose(); // 清理ViewModel
            this.DataContext = null; // 清理数据上下文
        }
    }
}