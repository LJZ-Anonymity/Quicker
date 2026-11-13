using Quicker.Windows.MainWindows;
using System.Windows.Threading;
using System.ComponentModel;
using Quicker.Managers;
using System.Net.Http;
using System.Windows;
using Quicker.Models;
using System.Net;
using System.IO;

namespace Quicker.Windows.ToolWindows
{
    public partial class DownloadWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private string _downloadUrl, _downloadPath; // 下载地址和保存路径
        private DispatcherTimer _updateTimer; // 更新UI的定时器
        private long _lastDownloadedBytes; // 上一次速度更新时的下载字节数
        private DateTime _lastSpeedUpdate; // 上次速度更新时间
        private double _downloadSpeed; // 下载速度 (字节/秒)
        private long _downloadedBytes; // 已下载字节数
        private long _totalBytes; // 总字节数
        private bool _isClosed; // 标记窗口是否已经关闭

        public DownloadWindow(string downloadUrl, string downloadPath)
        {
            InitializeComponent();
            _isClosed = false; // 初始化标记
            _downloadUrl = downloadUrl; // 下载地址
            _downloadPath = downloadPath; // 保存路径
            this.Closed += OnWindowClosed; // 使用命名方法订阅事件
            InitializeUpdateTimer(); // 初始化定时器
        }

        // 定义一个命名方法来处理窗口关闭事件
        private void OnWindowClosed(object sender, EventArgs e) => _isClosed = true; // 标记窗口已关闭

        // 工厂方法，用于创建和管理DownloadWindow实例
        public static DownloadWindow Create(string downloadUrl, string downloadPath)
        {
            var existingWindow = Application.Current.Windows.OfType<DownloadWindow>().FirstOrDefault();
            if (existingWindow != null && !existingWindow._isClosed)
            {
                var toast = new ToastManager();
                toast.Show("下载中，请勿重复操作", ToastType.Common);
                existingWindow.EnableDownloadButton(false);
                return existingWindow;
            }
            return new DownloadWindow(downloadUrl, downloadPath);
        }

        // 初始化定时器
        private void InitializeUpdateTimer()
        {
            _updateTimer = new DispatcherTimer(); // 创建定时器
            _updateTimer.Interval = TimeSpan.FromMilliseconds(200); // 每200毫秒更新一次
            _updateTimer.Tick += UpdateTimer_Tick; // 定时器事件
            _updateTimer.Start(); // 启动定时器
        }

        // 每200毫秒更新一次UI
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateUI(); // 更新UI
        }

        // 更新UI
        private void UpdateUI()
        {
            if (_totalBytes > 0)
            {
                double progress = (double)_downloadedBytes / _totalBytes * 100; // 计算进度百分比
                DownloadProgressBar.Value = progress; // 更新进度条
                DownloadSpeed.Text = FormatDownloadSpeed(_downloadSpeed); // 更新下载速度
                DownloadSize.Text = $"{_downloadedBytes / (1024.0 * 1024.0):F2} MB/{_totalBytes / (1024.0 * 1024.0):F2} MB";
                DownloadSize.Margin = new Thickness(DownloadSpeed.ActualWidth + 20, 66, 0, 0); // 调整下载大小的Margin
            }
        }

        /// <summary>
        /// 格式化下载速度
        /// </summary>
        /// <param name="speedInBytesPerSecond"> 下载速度（字节/秒） </param>
        private string FormatDownloadSpeed(double speedInBytesPerSecond)
        {
            if (speedInBytesPerSecond >= 1024 * 1024) // 大于等于1MB/s
                return $"{speedInBytesPerSecond / (1024.0 * 1024.0):F2} MB/S";
            else if (speedInBytesPerSecond >= 1024) // 大于等于1KB/s
                return $"{speedInBytesPerSecond / 1024.0:F2} KB/S";
            else // 小于1KB/s
                return $"{speedInBytesPerSecond:F2} B/S";
        }

        // 窗口加载完成后设置窗口位置
        private void DownloadWindow_Loaded(object sender, RoutedEventArgs e)
        {
            double screenWidth = SystemParameters.WorkArea.Width; // 屏幕宽度
            double screenHeight = SystemParameters.WorkArea.Height; // 屏幕高度
            this.Left = screenWidth - 300; // 窗口距离屏幕右侧300像素
            this.Top = screenHeight - 93.8; // 窗口距离屏幕下侧93.8像素
            LoadDownloadInfo(); // 加载下载信息
            StartDownload(); // 开始下载
        }

        // 开始下载
        private async void StartDownload()
        {
            await StartDownloadAsync(); // 异步开始下载
        }

        // 鼠标进入窗口还原透明度
        private void DownloadWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => this.Opacity = 1; // 还原透明度
        // 鼠标离开窗口降低透明度
        private void DownloadWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => this.Opacity = 0.8; // 降低透明度

        private void LoadDownloadInfo()
        {
            DownloadFileName.Text = Path.GetFileName(new Uri(_downloadUrl).AbsolutePath); // 获取文件名
        }

        // 异步开始下载
        private async Task StartDownloadAsync()
        {
            try
            {
                InitializeDownloadState(); // 初始化下载状态
                _lastDownloadedBytes = 0; // 初始化基准下载字节数
                _lastSpeedUpdate = DateTime.Now; // 初始化基准时间
                using (var webClient = new WebClient()) // 创建WebClient
                {
                    webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(OnDownloadProgressChanged); // 下载进度事件
                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(OnDownloadCompleted); // 下载完成事件
                    webClient.DownloadFileAsync(new Uri(_downloadUrl), _downloadPath); // 异步下载文件
                }
            }
            catch (Exception ex)
            {
                HandleDownloadError(ex); // 处理下载错误
            }
        }

        // 下载进度事件
        private void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _downloadedBytes = e.BytesReceived; // 已下载字节数
            _totalBytes = e.TotalBytesToReceive; // 总字节数
            UpdateUI(); // 更新UI
            UpdateDownloadSpeed(); // 更新下载速度
        }

        // 下载完成事件
        private void OnDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
                HandleCancellation(); // 处理取消操作
            else if (e.Error != null)
                HandleDownloadError(e.Error); // 处理下载错误
            else
                FinalizeDownload(); // 完成下载后的处理
        }

        // 初始化下载状态
        private void InitializeDownloadState()
        {
            EnableDownloadButton(false); // 禁用下载按钮
            var toast = new ToastManager(); // 创建Toast提示
            var fileName = Path.GetFileName(new Uri(_downloadUrl).AbsolutePath); // 获取文件名
            _cancellationTokenSource = new CancellationTokenSource(); // 创建取消令牌源
        }

        // 完成下载后的处理
        private void FinalizeDownload()
        {
            var toast = new ToastManager(); // 创建Toast提示
            var fileName = Path.GetFileName(new Uri(_downloadUrl).AbsolutePath); // 获取文件名
            toast.Show($"{fileName} 下载完成", ToastType.Success); // 显示Toast提示
            EnableDownloadButton(true); // 启用下载按钮
            var downloadedFileInfo = new FileInfo(_downloadPath); // 获取下载文件信息
            System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{downloadedFileInfo.FullName}\""); // 打开下载目录并选中文件
            this.Close(); // 关闭窗口
        }

        // 处理取消操作
        private void HandleCancellation()
        {
            var toast = new ToastManager(); // 创建Toast提示
            toast.Show("下载已取消", ToastType.Common); // 显示Toast提示
        }

        // 处理下载错误
        private void HandleDownloadError(Exception ex)
        {
            var toast = new ToastManager(); // 创建Toast提示
            if (ex is WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError) // 协议错误
                {
                    var response = webEx.Response as HttpWebResponse; // 获取HttpWebResponse
                    toast.Show($"服务器返回错误：{(int)response.StatusCode} {response.StatusDescription}", ToastType.Error); // 服务器返回错误
                }
                else
                    toast.Show($"网络错误：{webEx.Message}", ToastType.Error); // 网络错误
            }
            else
                toast.Show($"系统错误：{ex.Message}", ToastType.Error); // 其他错误
            Close(); // 关闭下载窗口
            EnableDownloadButton(true); // 启用下载按钮
        }

        // 更新下载速度
        private void UpdateDownloadSpeed()
        {
            var currentTime = DateTime.Now; // 当前时间
            var timeDiff = (currentTime - _lastSpeedUpdate).TotalSeconds; // 时间差（秒）

            // 计算增量字节数
            long currentBytes = _downloadedBytes; // 当前字节数
            long incrementalBytes = currentBytes - _lastDownloadedBytes; // 增量字节数
            _downloadSpeed = incrementalBytes / timeDiff; // 真实的下载速度
            _lastDownloadedBytes = currentBytes; // 更新基准下载字节数
            _lastSpeedUpdate = currentTime; // 更新基准时间
        }

        /// <summary>
        /// 启用或禁用下载按钮
        /// </summary>
        private void EnableDownloadButton(bool enable)
        {
            var updateWindows = Application.Current.Windows.OfType<UpdateWindow>(); // 获取所有UpdateWindow
            if (updateWindows.Count() > 0) // 如果有UpdateWindow，则禁用下载按钮
                foreach (var window in updateWindows)
                    window.DownloadButton.IsEnabled = enable; // 禁用下载按钮
        }

        // 取消下载，保留已下载文件
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel(); // 取消下载
                this.Close(); // 关闭窗口
            }
        }

        // 取消下载，删除已下载文件
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel(); // 取消下载
                try
                {
                    File.Delete(_downloadPath); // 删除已下载文件
                }
                catch (Exception ex)
                {
                    var toast = new ToastManager(); // 创建Toast提示
                    toast.Show($"删除文件失败：{ex.Message}", ToastType.Error); // 显示Toast提示
                }
                this.Close(); // 关闭窗口
            }
        }

        // 窗口关闭时释放资源
        protected override void OnClosed(EventArgs e)
        {
            _totalBytes = 0; // 释放总字节数
            _downloadedBytes = 0; // 释放已下载字节数
            _downloadSpeed = 0; // 释放下载速度
            _lastSpeedUpdate = DateTime.Now; // 释放上次速度更新时间
            _updateTimer.Stop(); // 停止更新UI的定时器
            _updateTimer = null; // 释放更新UI的定时器
            _downloadPath = null; // 释放下载路径
            _downloadUrl = null; // 释放下载地址
            _isClosed = true; // 标记窗口已关闭
            _lastDownloadedBytes = 0; // 释放上一次速度更新时的下载字节数
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Dispose(); // 清理取消令牌源
            this.Closed -= OnWindowClosed; // 解绑事件
            base.OnClosed(e);
        }
    }
}