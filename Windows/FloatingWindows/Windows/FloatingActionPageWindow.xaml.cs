using Quicker.Windows.FloatingWindows.ViewModels;
using System.Windows.Controls;
using System.Threading.Tasks;
using Quicker.Windows.Menus;
using Quicker.Database.Core;
using System.Windows.Input;
using Quicker.Managers;
using Quicker.Models;
using System.Windows;

namespace Quicker.Windows.FloatingWindows.Windows
{
    public partial class FloatingActionPageWindow : Window
    {
        public int ActionPageIndex { get; private set; } // 当前按钮所在的页面索引
        public string TableName { get; private set; } // 表名

        private readonly ActionPageDatabase db3 = new(); // 动作页面数据库
        private readonly ButtonManager buttonManager = new(); // 按钮管理器
        private readonly FloatingActionPageWindowViewModel viewModel; // ViewModel

        public FloatingActionPageWindow(int actionPageIndex, string tableName)
        {
            InitializeComponent();
            ActionPageIndex = actionPageIndex;
            TableName = tableName;

            // 创建并设置ViewModel
            viewModel = new FloatingActionPageWindowViewModel(actionPageIndex, tableName);
            this.DataContext = viewModel;
        }

        private void FloatingActionPageWindow_Loaded(object sender, RoutedEventArgs e)
        {
            using var windowManager = new WindowManager(); // 创建窗口管理器
            windowManager.SetWindowPositionNearMouse(this); // 设置窗口位置
            windowManager.SetWindowTopmost(this); // 设置窗口置顶

            // 设置标题
            var actionPageData = db3.GetActionPageData(TableName, 0); // 从数据库中获取通用动作页面数据
            TitleTextBlock.Text = actionPageData.ActionPageName; // 设置通用标签内容
            TitleTextBlock.ToolTip = actionPageData.ActionPageName; // 设置通用标签提示

            LoadButtonData(); // 加载按钮数据
        }

        /// <summary>
        /// 加载按钮数据并绑定到按钮
        /// </summary>
        public void LoadButtonData()
        {
            viewModel.LoadButtonData(); // 让ViewModel刷新数据集合
            var buttons = ActionButtonGrid.Children.OfType<Button>().ToArray(); // 通过遍历ActionButtonGrid获取所有按钮
            // 为每个按钮加载数据
            int buttonIndex = 0;
            foreach (var button in buttons)
            {
                int row = buttonIndex / 4;
                int col = buttonIndex % 4;

                // 获取按钮数据
                var buttonData = viewModel.GetButtonData(row, col);
                if (buttonData != null) // 有数据的情况
                {
                    button.Tag = buttonData;
                    buttonManager.RefreshButtonDisplay(button, buttonData, viewModel.ButtonSize);
                }
                else // 没有数据的情况
                {
                    button.Tag = null;
                    button.Content = null;
                }
                buttonIndex++;
            }
        }

        /// <summary>
        /// ActionButton点击事件处理
        /// </summary>
        private async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ButtonData buttonData)
            {
                // 执行按钮动作
                using var actionManager = new ActionManager();
                await actionManager.DoActionAsync(buttonData, TableName);
            }
        }

        /// <summary>
        /// 标题栏鼠标左键按下事件处理
        /// </summary>
        private void TitleGrid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove(); // 允许拖动窗口
        }

        /// <summary>
        /// 固定到桌面按钮点击事件处理
        /// </summary>
        private void PinToDesktop(object sender, RoutedEventArgs e)
        {
            viewModel.IsPinned = !viewModel.IsPinned; // 切换固定状态
        }

        /// <summary>
        /// 关闭窗口按钮点击事件处理
        /// </summary>
        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.Close(); // 关闭窗口
        }

        /// <summary>
        /// 右键菜单点击事件处理
        /// </summary>
        private void TitleGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            OperationMenu menu = new OperationMenu(0, TableName, this);
            menu.Show();
        }

        /// <summary>
        /// 按钮右键菜单点击事件处理
        /// </summary>
        private void ActionButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button button = (Button)sender;
            OperationMenu menu = new OperationMenu(button.Tag is ButtonData buttonData ? buttonData.ButtonID : 0, TableName, this);
            menu.Show();
        }

        // 关闭窗口时释放资源
        private void FloatingActionPageWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            // 清理所有按钮的Tag
            var buttons = ActionButtonGrid.Children.OfType<Button>().ToArray();
            foreach (var button in buttons)
            {
                button.Tag = null;
                button.Content = null;
            }

            buttonManager?.Dispose(); // 释放管理器
            viewModel?.Dispose(); // 清理ViewModel
            this.DataContext = null; // 清理数据上下文
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            ViewBorder.Visibility = Visibility.Visible;
            Border.Visibility = Visibility.Collapsed;
        }

        private void ViewBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            // 只有在未固定状态下才隐藏
            if (!viewModel.IsPinned)
            {
                ViewBorder.Visibility = Visibility.Collapsed;
                Border.Visibility = Visibility.Visible;
            }
        }
    }
}