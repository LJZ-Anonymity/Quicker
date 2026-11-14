using Quicker.Windows.ToolWindows;
using Quicker.Windows;
using System.Windows;
using Quicker.Models;

namespace Quicker.Managers
{
    public class ToastManager
    {
        private ToastWindow? toastWindow;

        /// <summary>
        /// 添加Toast消息
        /// </summary>
        /// <param name="message"> 消息内容 </param>
        /// <param name="toastType"> 消息类型 </param>
        public void Show(string message, ToastType toastType)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return; // 如果 Dispatcher 不可用或已关闭，直接返回

            dispatcher.BeginInvoke(() =>
            {
                toastWindow = Application.Current.Windows.OfType<ToastWindow>().FirstOrDefault();
                if (toastWindow == null)
                {
                    toastWindow = new ToastWindow(); // 创建新的ToastWindow实例
                    toastWindow.Show(); // 显示ToastWindow
                }
                toastWindow.AddToast(message, toastType); // 添加Toast消息
            });
        }
    }
}