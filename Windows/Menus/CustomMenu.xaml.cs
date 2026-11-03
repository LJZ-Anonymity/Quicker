using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Diagnostics;
using Quicker.Managers;
using System.Windows;
using System.IO;

namespace Quicker.Windows.Menus
{
    public partial class CustomMenu : BaseMenuWindow
    {
        private readonly App app = (App.Current as App); // App实例

        public CustomMenu()
        {
            InitializeComponent();
        }

        // 重写基类的窗口加载方法
        protected override void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            base.OnWindowLoaded(sender, e); // 调用基类方法处理动画
            base.SetWindowBottomLeftNearMouse(); // 使用基类方法设置窗口左下角在鼠标位置附近
            SetRunModeText(); // 设置运行模式文本
        }

        // 设置运行模式文本
        private void SetRunModeText()
        {
            var text = AppStateManager.Pause ? "恢复" : "暂停"; // 消息提醒
            PauseQuickerTextBlock.Text = text; // 更新菜单栏文本
        }

        // 弹出面板窗口
        private void ShowMainWindow(object sender, RoutedEventArgs e)
        {
            app.ShowMainWindow(sender, e); // 显示主面板
            base.Visibility = Visibility.Hidden; // 隐藏当前菜单窗口
        }

        // 弹出设置窗口
        private void OpenSettingWindow(object sender, RoutedEventArgs e)
        {
            using var windowManager = new WindowManager(); // 创建窗口管理器
            windowManager.OpenTargetWindow("SettingWindow"); // 打开设置窗口
        }

        // 打开动作管理窗口
        private void OpenActionPageManageWindow(object sender, RoutedEventArgs e)
        {
            using var windowManager = new WindowManager(); // 创建窗口管理器
            windowManager.OpenTargetWindow("ActionPageManageWindow"); // 打开动作管理窗口
        }

        // 暂停Quicker
        private void PauseQuicker(object sender, RoutedEventArgs e)
        {
            base.Dispatcher.Invoke(new Action(() =>
            {
                app.PauseQuicker(sender, e); // 调用App类中的PauseQuicker方法 
            }));
            base.Visibility = Visibility.Hidden; // 隐藏当前窗口
        }

        // 重启应用
        public void Restart(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏当前窗口
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location; // 获取当前应用程序的完整路径
            string directory = Path.GetDirectoryName(exePath); // 获取可执行文件的目录
            string exeName = Path.GetFileNameWithoutExtension(exePath) + ".exe"; // 获取可执行文件的名称
            SingleInstanceManager.ReleaseMutex(); // 释放互斥锁
            app.Shutdown(); // 通过App类正确关闭当前应用程序
            Process.Start(new ProcessStartInfo // 启动新进程
            {
                FileName = Path.Combine(directory, exeName), // 指定可执行文件的路径
                UseShellExecute = true // 使用ShellExecute来启动程序
            });
        }

        // 退出应用
        private void Exit(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏当前窗口
            app.Shutdown(); // 通过App类正确关闭应用，确保OnExit方法被调用
        }

        // 重写基类的失焦处理方法
        protected override void HandleDeactivated()
        {
            CloseWithAnimation(); // 使用动画关闭窗口，真正释放资源
            base.HandleDeactivated(); // 调用基类方法以触发ClosingOrHiding事件
        }

        // 关闭窗口前释放资源
        protected override void OnClosed(EventArgs e)
        {            
            // 强制垃圾回收，确保资源及时释放
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            base.OnClosed(e); // 调用基类的 OnClosed 方法
        }
    }
}