using System.Windows.Input;
using System.Windows;
using System;

namespace Quicker.Windows.ToolWindows
{
    public partial class MessageWindow : Window
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tytle"> 标题 </param>
        /// <param name="message"> 消息内容 </param>
        public MessageWindow(string tytle, string message)
        {
            InitializeComponent();
            TitileTextBlock.Text = tytle; // 设置标题
            MessageTextBlock.Text = message; // 设置消息
            if (tytle == "Quicker")
                MessageTextBlock.Margin = new Thickness(60, 68, 0, 0); // 调整消息内容的位置
        }

        // 点击按钮关闭窗口
        private void Button_Click(object sender, RoutedEventArgs e) => Close();

        // 允许拖动窗口
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e) => DragMove();

        protected override void OnClosed(EventArgs e)
        {
            TitileTextBlock.Text = null; // 清空标题
            MessageTextBlock.Text = null; // 清空消息
            base.OnClosed(e);
        }
    }
}