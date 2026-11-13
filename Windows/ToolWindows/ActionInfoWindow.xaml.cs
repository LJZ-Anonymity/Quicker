using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Quicker.Database.Core;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;

namespace Quicker.Windows.ToolWindows
{
    public partial class ActionInfoWindow : Window
    {
        private readonly ButtonDatabase db2 = new(); // 按钮数据库
        public int ButtonID { get; private set; } // 当前按钮ID
        public string TableName { get; private set; } // 表名

        public ActionInfoWindow(int buttonID, string tableName)
        {
            InitializeComponent();
            ButtonID = buttonID; // 设置当前按钮ID
            TableName = tableName; // 设置表名
            InitializeWindow(); // 初始化窗口
        }

        // 初始化信息窗口
        private void InitializeWindow()
        {
            ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            IDTextBlock.Text = buttonData.ButtonID.ToString(); // 初始化动作ID
            TitleLabel.Content = buttonData.Title; // 初始化动作名称
            UsageLabel.Content = buttonData.Description; // 初始化动作用途
            if(!string.IsNullOrEmpty(buttonData.ImagePath))
            {
                try
                {
                    Image.Source = new BitmapImage(new Uri(buttonData.ImagePath)); // 初始化动作图像
                }
                catch
                {
                    using var toast = new ToastManager(); // 消息提醒管理器
                    toast.Show($"图标加载失败：按钮{buttonData.Title}的图标被移动或删除", ToastType.Error); // 弹出消息提醒
                }
            }
            CreatTimeLabel.Content = buttonData.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"); // 初始化创建时间
            LatestEditTimeLabel.Content = buttonData.LatestEditTime.ToString("yyyy-MM-dd HH:mm:ss"); // 初始化最后编辑时间
            ActionSizeLabel.Content = $"{GetActionSize()} {GetActionSizeUnit()}"; // 初始化动作大小
        }

        /// <summary>
        /// 获取动作页大小
        /// </summary>
        /// <returns> 动作页大小 </returns>
        private int GetActionSize()
        {
            int actionPageSize = db2.GetActionSize(TableName, ButtonID); // 获取动作大小
            if (actionPageSize < 1024)
                return actionPageSize; // 字节
            else if (actionPageSize < 1024 * 1024)
                return actionPageSize / 1024; // 千字节
            else if (actionPageSize < 1024 * 1024 * 1024)
                return actionPageSize / (1024 * 1024); // 兆字节
            return actionPageSize / (1024 * 1024 * 1024); // 吉字节
        }

        /// <summary>
        /// 获取动作页大小单位
        /// </summary>
        /// <returns> 动作页大小单位 </returns>
        private string GetActionSizeUnit()
        {
            int actionPageSize = db2.GetActionSize(TableName, ButtonID); // 获取动作大小
            if (actionPageSize < 1024)
                return "B"; // 字节
            else if (actionPageSize < 1024 * 1024)
                return "KB"; // 千字节
            else if (actionPageSize < 1024 * 1024 * 1024)
                return "MB"; // 兆字节
            return "GB"; // 吉字节
        }

        // 获取动作ID
        private void IDTextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetText(IDTextBlock.Text); // 复制文本到剪贴板
            using var toast = new ToastManager(); // 消息提醒管理器
            toast.Show("动作ID已经写入剪贴板。", ToastType.Success); // 显示复制成功的通知
        }

        // 复制动作信息
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            string textToCopy = $"ID:{buttonData.ButtonID}\n" +
                                $"标题:{buttonData.Title}\n" +
                                $"说明:{buttonData.Description}\n" +
                                $"URI:quicker:runaction:{buttonData.Data1}{buttonData.Data2}{buttonData.Data3}"; // 复制的文本内容
            Clipboard.SetText(textToCopy); // 复制文本到剪贴板
            using var toast = new ToastManager(); // 消息提醒管理器
            toast.Show("已复制!", ToastType.Success); // 显示复制成功的通知
        }

        // 关闭动作信息窗口
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            WindowHelper.SetMainWindowFocused(); // 关闭窗口
            this.Close(); // 关闭窗口
        }

        // 关闭窗口前，释放资源
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e); // 调用基类的 OnClosed 方法
            Image.Source = null; // 释放图像资源

            // 清理事件处理器
            CopyButton.Click -= CopyButton_Click;
            CloseButton.Click -= CloseButton_Click;

            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}