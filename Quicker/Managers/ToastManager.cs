using Quicker.Windows.ToolWindows;
using Quicker.Windows;
using System.Windows;
using Quicker.Models;
using System.Linq;

namespace Quicker.Managers
{
    public class ToastManager
    {
        private ToastWindow? toastWindow;

        /// <summary>
        /// 添加Toast
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="toastType">消息类型</param>
        public void Show(string message, ToastType toastType)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return; // 如果 Dispatcher 不可用或已关闭，直接返回

            dispatcher.BeginInvoke(() =>
            {
                // 尝试获取已经存在（但可能被隐藏）的 ToastWindow 实例
                toastWindow = Application.Current.Windows.OfType<ToastWindow>().FirstOrDefault();

                if (toastWindow == null)
                {
                    // 如果确实没有，才创建新的
                    toastWindow = new ToastWindow();
                }

                // 如果窗口被隐藏了，重新显示它
                if (!toastWindow.IsVisible)
                {
                    toastWindow.Show();
                }

                toastWindow.AddToast(message, toastType); // 添加Toast消息
            });
        }
    }
}