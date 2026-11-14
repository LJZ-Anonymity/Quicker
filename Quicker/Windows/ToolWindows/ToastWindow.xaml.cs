using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using Quicker.Managers;
using System.Windows;
using Quicker.Models;

namespace Quicker.Windows.ToolWindows
{
    public partial class ToastWindow : Window
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new(); // 取消令牌源
        private readonly Dictionary<Border, DispatcherTimer> timerDictionary = new(); // 用于存储计时器
        private readonly HashSet<Border> animatingBorders = new(); // 正在动画中的 Border 集合
        private readonly Queue<Message> messageQueue = new(); // 创建消息队列
        private readonly SemaphoreSlim _queueSignal = new(0); // 创建一个信号量，初始计数为0
        private readonly Storyboard _scaleOutStoryboard; // 缩小动画
        private readonly Storyboard _scaleInStoryboard; // 放大动画
        private readonly Task _queueProcessingTask; // 队列处理任务

        public ToastWindow()
        {
            InitializeComponent();
            _scaleOutStoryboard = (Storyboard)FindResource("ScaleOutAnimation");
            _scaleInStoryboard = (Storyboard)FindResource("ScaleInAnimation");
            double screenHeight = SystemParameters.WorkArea.Height; // 获取屏幕高度
            Height = screenHeight; // 设置窗口高度为屏幕高度
            _queueProcessingTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token)); // 启动后台任务处理消息队列
        }

        /// <summary>
        /// 异步处理队列中的消息
        /// </summary>
        /// <param name="cancellationToken">取消令牌，用于请求取消操作</param>
        /// <returns>一个表示异步操作的任务</returns>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested) // 循环处理，直到收到取消请求
            {
                await _queueSignal.WaitAsync(cancellationToken); // 等待信号量，直到有新的消息加入队列
                await Dispatcher.InvokeAsync(() => // 使用UI线程调用检查并显示通知的方法
                {
                    CheckAndDisplayToast(); // 检查并显示消息
                }, DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// 添加消息
        /// </summary>
        /// <param name="message"> 消息内容 </param>
        /// <param name="toastType"> 消息类型 </param>
        public void AddToast(string message, ToastType toastType)
        {
            var msg = new Message { Content = message, ToastType = toastType }; // 创建消息对象
            messageQueue.Enqueue(msg); // 将消息对象添加到队列中
            _queueSignal.Release(); // 释放信号量，通知队列处理任务有新的消息加入
        }

        // 检查并显示消息
        // 检查并显示消息
        private void CheckAndDisplayToast()
        {
            // 如果队列有消息且当前显示数量未达到上限，显示新消息
            if (messageQueue.Count > 0 && ToastStackPanel.Children.Count < 5)
            {
                ShowToast(messageQueue.Dequeue());
            }
            // 如果队列为空且没有显示的消息，只隐藏窗口，不销毁
            else if (messageQueue.Count == 0 && ToastStackPanel.Children.Count == 0)
            {
                Hide();
            }
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        /// <param name="msg"> 消息内容 </param>
        private void ShowToast(Message msg)
        {
            var border = InitializeBorder(msg.ToastType); // 初始化边框
            var textblock = InitializeToast(msg.Content); // 初始化消息
            var grid = new Grid();
            border.Child = grid; // 将消息添加到边框中
            grid.Children.Add(textblock); // 将消息添加到网格中
            border.MouseRightButtonDown += Border_MouseRightButtonDown;

            // 初始化关闭按钮，并传入边框对象
            var closeToastButton = InitializeCloseButton(border);
            closeToastButton.Click += (s, e) => DeleteToast(border); // 关闭按钮点击事件
            grid.Children.Add(closeToastButton); // 将关闭按钮添加到网格中

            InitializeAnimation(border); // 初始化动画

            // 为每个消息创建一个独立的计时器
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3), // 设置计时器时间为3秒
                Tag = border // 将边框对象存储在计时器的Tag属性中
            };
            timer.Tick += Timer_Tick; // 计时器事件

            timerDictionary[border] = timer; // 将计时器存储到字典中
            timer.Start(); // 启动计时器
        }

        /// <summary>
        /// 右键报错边框复制错误信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Border_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && 
                border.Tag is ToastType toastType && 
                toastType == ToastType.Error &&
                border.Child is Grid grid &&
                grid.Children.Count > 0 &&
                grid.Children[0] is TextBlock textblock)
            {
                Clipboard.SetText(textblock.Text);
            }
        }

        /// <summary>
        /// 初始化边框
        /// </summary>
        /// <param name="toastType"> 消息类型 </param>
        private Border InitializeBorder(ToastType toastType)
        {
            Border border = new()
            {
                Style = (Style)FindResource("ToastBorderStyle"), // 设置边框样式
                RenderTransform = new ScaleTransform(0.1, 0.1), // 初始大小设置为 0.1
                RenderTransformOrigin = new Point(0.5, 1.0), // 设置缩放中心为底部中心
                CornerRadius = new CornerRadius(5), // 设置边框圆角
                Margin = new Thickness(0, 5, 0, 5), // 设置边距
                Tag = toastType, // 设置消息类型
                Width = 400 // 设置边框宽度
            }; // 创建消息边框

            ToastStackPanel.Children.Add(border); // 添加消息边框到消息面板
            return border; // 返回消息边框
        }

        /// <summary>
        /// 初始化关闭按钮
        /// </summary>
        /// <param name="border"> 消息边框 </param>
        private Button InitializeCloseButton(Border border)
        {
            var button = new Button { Style = (Style)FindResource("CloseToastButton") }; // 设置按钮样式

            // 创建命名事件处理器，以便后续可以解绑
            // 将 border 和 button 存储在闭包中，但使用命名方法以便解绑
            MouseEventHandler mouseEnterHandler = (s, e) => CloseButton_MouseEnter(s, e, border, button);
            MouseEventHandler mouseLeaveHandler = (s, e) => CloseButton_MouseLeave(s, e, button);

            // 将事件处理器存储在按钮的 Tag 中，以便后续解绑
            button.Tag = new ButtonEventHandlers { MouseEnterHandler = mouseEnterHandler, MouseLeaveHandler = mouseLeaveHandler };
            button.MouseEnter += mouseEnterHandler; // 鼠标进入事件
            button.MouseLeave += mouseLeaveHandler; // 鼠标离开事件

            return button; // 返回关闭按钮
        }

        /// <summary>
        /// 关闭按钮鼠标进入事件
        /// </summary>
        private void CloseButton_MouseEnter(object sender, MouseEventArgs e, Border border, Button button)
        {
            if (border.Background is SolidColorBrush borderBrush)
            {
                var darkerColor = Color.FromRgb(
                    (byte)(borderBrush.Color.R * 0.9),
                    (byte)(borderBrush.Color.G * 0.9),
                    (byte)(borderBrush.Color.B * 0.9)
                );
                button.Background = new SolidColorBrush(darkerColor);
            }
        }

        /// <summary>
        /// 关闭按钮鼠标离开事件
        /// </summary>
        private void CloseButton_MouseLeave(object sender, MouseEventArgs e, Button button)
        {
            button.Background = new SolidColorBrush(Colors.Transparent); // 恢复按钮的背景色
        }

        /// <summary>
        /// 按钮事件处理器存储类
        /// </summary>
        private class ButtonEventHandlers
        {
            public MouseEventHandler MouseEnterHandler { get; set; }
            public MouseEventHandler MouseLeaveHandler { get; set; }
        }

        /// <summary>
        /// 初始化消息
        /// </summary>
        /// <param name="message"> 消息内容 </param>
        private TextBlock InitializeToast(string message)
        {
            return new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20),
                Text = message,
                FontSize = 16
            };
        }

        // 初始化动画
        private void InitializeAnimation(Border border)
        {
            _scaleInStoryboard.Begin(border);
        }

        // 计时器事件
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (sender is not DispatcherTimer timer || timer.Tag is not Border border)
                return;

            StopTimer(timer);
            AnimateToastOut(border);
        }

        /// <summary>
        /// 停止计时器
        /// </summary>
        /// <param name="timer">计时器</param>
        private void StopTimer(DispatcherTimer timer)
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
        }

        /// <summary>
        /// 解绑按钮的所有事件
        /// </summary>
        /// <param name="button">按钮</param>
        /// <param name="handlers">事件处理器</param>
        private void UnbindButtonEvents(Button button, ButtonEventHandlers handlers)
        {
            if (handlers.MouseEnterHandler != null)
            {
                button.MouseEnter -= handlers.MouseEnterHandler;
            }
            if (handlers.MouseLeaveHandler != null)
            {
                button.MouseLeave -= handlers.MouseLeaveHandler;
            }
            button.Tag = null;
        }

        /// <summary>
        /// 解绑 Border 及其子元素的所有事件
        /// </summary>
        /// <param name="border">消息边框</param>
        private void UnbindBorderEvents(Border border)
        {
            border.MouseRightButtonDown -= Border_MouseRightButtonDown;
            if (border.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is Button button && button.Tag is ButtonEventHandlers handlers)
                    {
                        UnbindButtonEvents(button, handlers);
                    }
                }
            }
        }

        /// <summary>
        /// 核心删除消息逻辑
        /// </summary>
        /// <param name="border"> 消息边框 </param>
        private void RemoveToastCore(Border border)
        {
            // 使用 TryGetValue 避免两次字典查找，提高性能
            if (!timerDictionary.TryGetValue(border, out var timer))
                return;

            UnbindBorderEvents(border);
            ToastStackPanel.Children.Remove(border);
            timerDictionary.Remove(border);
            StopTimer(timer);
        }

        /// <summary>
        /// 播放关闭动画并删除消息
        /// </summary>
        /// <param name="border">消息边框</param>
        private void AnimateToastOut(Border border)
        {
            // 如果已经在动画中，直接返回，避免重复动画
            if (animatingBorders.Contains(border))
                return;

            // 如果 Border 不在字典中，说明已经被删除，直接返回
            if (!timerDictionary.TryGetValue(border, out var timer))
                return;

            StopTimer(timer);
            animatingBorders.Add(border);

            // 创建一次性事件处理器，避免内存泄漏
            EventHandler completedHandler = null;
            completedHandler = (s, arg) =>
            {
                _scaleOutStoryboard.Completed -= completedHandler;
                animatingBorders.Remove(border);
                RemoveToastCore(border);
                CheckAndDisplayToast();
            };
            _scaleOutStoryboard.Completed += completedHandler;
            _scaleOutStoryboard.Begin(border);
        }

        /// <summary>
        /// 删除消息（带动画效果）
        /// </summary>
        /// <param name="border"> 消息边框 </param>
        private void DeleteToast(Border border)
        {
            AnimateToastOut(border);
        }

        // 关闭窗口释放资源
        protected override async void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _cancellationTokenSource.Cancel(); // 取消后台任务
            try
            {
                await _queueProcessingTask;
            }
            catch { }

            messageQueue.Clear();
            animatingBorders.Clear();

            // 解绑所有 Border 的事件并清理计时器
            foreach (var (border, timer) in timerDictionary)
            {
                UnbindBorderEvents(border);
                StopTimer(timer);
            }
            timerDictionary.Clear();
        }
    }

    // 消息类
    public class Message
    {
        public string Content { get; set; } // 消息内容
        public ToastType ToastType { get; set; } // 消息类型
    }

    // 用于将 ToastType 转换为 SolidColorBrush
    public class ToastTypeToBrushConverter : IValueConverter
    {
        // 颜色字典
        private static readonly Dictionary<ToastType, Color> ToastColors = new Dictionary<ToastType, Color>
        {
            [ToastType.Common] = Color.FromRgb(0x14, 0x7E, 0xC9),  // 示例通用颜色，蓝色
            [ToastType.Error] = Color.FromRgb(0xF5, 0xA3, 0x00),   // 示例错误颜色，橙色
            [ToastType.Warning] = Color.FromRgb(0xFF, 0x00, 0x00), // 示例警告颜色，红色
            [ToastType.Success] = Color.FromRgb(0x11, 0xAD, 0x45)  // 示例成功颜色，绿色
        };

        /// <summary>
        /// 将 ToastType 转换为 SolidColorBrush
        /// </summary>
        /// <param name="value"> ToastType </param>
        /// <param name="targetType"> 目标类型 </param>
        /// <param name="parameter"> 参数 </param>
        /// <param name="culture"> 文化信息 </param>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ToastType toastType && ToastColors.TryGetValue(toastType, out var color))
            {
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush(ToastColors[ToastType.Common]); // 默认颜色
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}