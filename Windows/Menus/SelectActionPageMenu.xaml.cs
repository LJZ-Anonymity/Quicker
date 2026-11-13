using Quicker.Windows.MainWindows.MainWindow;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Quicker.Database.Core;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;

namespace Quicker.Windows.Menus
{
    public partial class SelectActionPageMenu : BaseMenuWindow
    {
        private ActionPageDatabase db3 = new(); // 动作页数据库

        public SelectActionPageMenu()
        {
            InitializeComponent();
        }

        // 重写基类的窗口加载方法
        protected override void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            base.OnWindowLoaded(sender, e); // 调用基类方法处理动画
            base.SetWindowPositionNearMouse(); // 设置窗口位置

            var sceneData = db3.GetAllSceneData(); // 获取所有场景数据
            foreach (var data in sceneData) // 遍历所有场景
            {
                if (data.SceneCount == 0 || data.SceneTag == "_global") continue; // 跳过没有动作页的场景和全局场景
                IncreaseMenuSize(); // 增加菜单大小
                Button button = GenerateButton(data); // 生成切换动作页按钮
                Grid grid = new() { Width = MainGrid.Width }; // 创建按钮容器
                button.Content = grid; // 设置按钮内容
                Image image = GenerateImage(data); // 生成按钮图片
                grid.Children.Add(image);
                TextBlock textBlock = GenerateTextBlock(data); // 生成按钮文字
                grid.Children.Add(textBlock); // 添加文字到按钮容器
                ChangeSceneButtonStackPanel.Children.Add(button); // 添加按钮到按钮堆栈
                button.Click += ChangeActionPage; // 绑定切换动作页事件
            }
        }

        // 增加菜单大小
        private void IncreaseMenuSize()
        {
            MainGrid.Height += 25;
            base.Height += 25;
        }

        /// <summary>
        /// 生成切换动作页按钮
        /// </summary>
        /// <param name="data"> 场景数据 </param>
        /// <returns> 切换动作页按钮 </returns>
        private Button GenerateButton(SceneData data)
        {
            Button button = new()
            {
                Style = (Style)base.FindResource("MenuButton"), // 加载按钮样式
                ToolTip = data.SceneTag, // 设置按钮提示
                Name = data.SceneTag, // 设置按钮名称
                Tag = data.SceneName // 设置按钮标签
            }; // 生成切换动作页按钮
            return button; // 返回按钮
        }

        /// <summary>
        /// 生成按钮图标
        /// </summary>
        /// <param name="data"> 场景数据 </param>
        /// <returns> 按钮图标 </returns>
        private Image GenerateImage(SceneData data)
        {
            Image image = new() { Style = (Style)base.FindResource("SelectButtonImage") }; // 生成按钮图标
            try
            {
                image.Source = new BitmapImage(new Uri(data.SceneIconPath, UriKind.RelativeOrAbsolute)); // 设置按钮图标
            }
            catch (System.IO.IOException)
            {
                using var toast = new ToastManager(); // 创建Toast管理器
                toast.Show("加载场景" + data.SceneName + " 图标失败。", ToastType.Error); // 显示Toast提示
                image.Source = new BitmapImage(new Uri("/Resources/Images/Quicker_Enabled.png", UriKind.Relative)); // 设置默认图标
            }
            return image; // 返回图标
        }

        /// <summary>
        /// 生成按钮文字
        /// </summary>
        /// <param name="data"> 场景数据 </param>
        /// <returns> 按钮文字 </returns>
        private TextBlock GenerateTextBlock(SceneData data)
        {
            TextBlock textBlock = new()
            {
                Style = (Style)base.FindResource("MenuButtonTextBlock"), // 加载按钮文字样式
                Text = db3.GetSceneTitle(data), // 设置按钮文字
            }; // 生成按钮文字
            return textBlock; // 返回文字
        }

        // 切换动作页
        private void ChangeActionPage(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button; // 获取按钮
            MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(); // 获取主窗口
            if (mainWindow != null) // 如果主窗口存在
                mainWindow.OnCommonStyleChanged(button.Name); // 切换动作页时更新样式
            base.Close(); // 关闭菜单
        }

        // 重写基类的失焦处理方法
        protected override void HandleDeactivated()
        {
            WindowHelper.SetMainWindowFocused(); // 关闭窗口
            base.HandleDeactivated(); // 调用基类方法以触发ClosingOrHiding事件
        }

        // 关闭窗口前释放资源
        protected override void OnClosed(EventArgs e)
        {
            GC.Collect(); // 强制垃圾回收
            base.OnClosed(e); // 调用基类的 OnClosed 方法
        }
    }
}
