using Quicker.Windows.ToolWindows;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Windows.Forms;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;
using System.IO;

namespace Quicker.Windows.MainWindows
{
    /// <summary>
    /// UpdateWindow 窗口用于检查和下载软件更新
    /// </summary>
    public partial class UpdateWindow : Window
    {
        #region 字段

        private const string _releaseUrl = "https://github.com/LJZ-Anonymity/Quicker/releases/"; // 发布页面地址
        private const string _downLoadPath = "C:\\Users\\LENOVO\\Downloads"; // 下载路径
        private AppUpdateManager _updateManager; // 更新管理器
        private string _downloadUrlWithNet; // 下载地址
        private string _downloadUrl; // 下载地址

        #endregion

        #region 构造函数

        public UpdateWindow()
        {
            InitializeComponent();
            _updateManager = new AppUpdateManager(); // 创建更新管理器
            CheckNewVersion(); // 检查新版本
            Activate(); // 激活窗口
        }

        #endregion

        #region 事件处理

        // 窗口加载时获取更新信息
        private void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            VersionTextBlock.Text = $"当前版本：{VersionHelper.CurrentVersion}"; // 显示当前版本号
        }

        // 点击按钮关闭窗口
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close(); // 关闭窗口
        }

        // 下载按钮点击事件 - 显示下载选项Popup
        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有可用的下载地址
            bool hasNormalDownload = !string.IsNullOrWhiteSpace(_downloadUrl);
            bool hasNetDownload = !string.IsNullOrWhiteSpace(_downloadUrlWithNet);

            // 更新按钮状态
            NormalDownloadButton.IsEnabled = hasNormalDownload;
            NetDownloadButton.IsEnabled = hasNetDownload;

            // 显示Popup
            DownloadPopup.IsOpen = true;
        }

        // 普通版本下载按钮点击事件
        private void NormalDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadPopup.IsOpen = false; // 关闭Popup
            var fileName = Path.GetFileName(new Uri(_downloadUrl).AbsolutePath); // 获取文件名
            string destinationPath = Path.Combine(_downLoadPath, fileName); // 构建保存路径

            var downloadWindow = DownloadWindow.Create(_downloadUrl, destinationPath); // 创建下载窗口
            downloadWindow.Show(); // 显示下载窗口
        }

        // 内置.NET框架版本下载按钮点击事件
        private void NetDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadPopup.IsOpen = false; // 关闭Popup
            var fileName = Path.GetFileName(new Uri(_downloadUrlWithNet).AbsolutePath); // 获取文件名
            string destinationPath = Path.Combine(_downLoadPath, fileName); // 构建保存路径

            var downloadWindow = DownloadWindow.Create(_downloadUrlWithNet, destinationPath); // 创建下载窗口
            downloadWindow.Show(); // 显示下载窗口
        }

        // 前往下载地址查看详细信息
        private void LatestVersionTextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            using var actionManager = new ActionManager(); // 创建动作管理器
            actionManager.OpenUrl(_releaseUrl + LatestVersionTextBlock1.Text); // 查看详细信息
        }

        // 窗口关闭清理资源
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e); // 调用基类方法
            CleanupResources();
        }

        #endregion

        #region 业务逻辑

        // 检查新版本
        private void CheckNewVersion()
        {
            if (AppStateManager.HasNewVersion)
            {
                LoadLatestVersionInfo();
            }
            else
            {
                DisplayNoUpdateAvailable();
            }
        }

        // 加载最新版本信息
        private void LoadLatestVersionInfo()
        {
            _updateManager.ReadJsonFromUrl(); // 读取更新信息
            if (_updateManager.Versions.Count > 0)
            {
                var latestVersion = _updateManager.GetLatestVersion(); // 获取最新版本
                if (latestVersion != null)
                {
                    LoadUpdateInfo(latestVersion); // 加载更新信息
                    LoadVersionHistory(); // 加载版本历史
                }
                else
                {
                    ShowUpdateFailedMessage();
                }
            }
            else
            {
                ShowUpdateFailedMessage();
            }
        }

        // 显示更新失败消息
        private void ShowUpdateFailedMessage()
        {
            var toast = new ToastManager(); // 创建Toast提示
            toast.Show("获取更新失败！", ToastType.Common); // 显示Toast提示
        }

        // 显示无更新可用信息
        private void DisplayNoUpdateAvailable()
        {
            // 更新UI显示
            TitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7D4D")); // 设置标题颜色
            LatestVersionTextBlock1.Text = VersionHelper.CurrentVersion; // 显示当前版本号
            LatestVersionTextBlock1.FontWeight = FontWeights.Normal; // 设置字体粗细
            DownloadButton.Visibility = Visibility.Collapsed; // 隐藏下载按钮
            TitleTextBlock.Text = "暂无新版本。"; // 更新标题文本
            LineRectangle.Width = 350; // 调整分界线宽度
            
            // 调整窗口大小和位置
            Height = 266; // 设置窗口高度
            Width = 400; // 设置窗口宽度
            LocateCenter(); // 窗口居中
        }

        /// <summary>
        /// 加载更新信息
        /// </summary>
        /// <param name="updateInfo">更新信息</param>
        private void LoadUpdateInfo(UpdateInfo updateInfo)
        {
            _downloadUrl = updateInfo.DownloadUrl; // 保存下载地址
            _downloadUrlWithNet = updateInfo.DownloadUrlWithNet; // 保存下载地址
            string newVersion = updateInfo.Version; // 获取最新版本号

            // 更新UI显示
            LatestVersionTextBlock1.Text = newVersion; // 显示最新版本号
            VersionChangeTextBlock.Text = $"{VersionHelper.CurrentVersion} -- {newVersion}"; // 显示版本号变更

            // 检查下载地址可用性并更新按钮状态
            UpdateDownloadButtonsState();
        }

        /// <summary>
        /// 更新下载按钮状态
        /// </summary>
        private void UpdateDownloadButtonsState()
        {
            // 检查是否有可用的下载地址
            bool hasNormalDownload = !string.IsNullOrWhiteSpace(_downloadUrl);
            bool hasNetDownload = !string.IsNullOrWhiteSpace(_downloadUrlWithNet);
            bool hasAnyDownload = hasNormalDownload || hasNetDownload; // 是否有任何下载地址

            // 更新主下载按钮状态
            DownloadButton.IsEnabled = hasAnyDownload;
        }

        /// <summary>
        /// 加载版本历史记录
        /// </summary>
        private void LoadVersionHistory()
        {
            var allVersions = _updateManager.GetVersionHistory(); // 获取所有版本
            if (allVersions.Count > 0)
            {
                // 获取当前版本和最新版本
                var currentVersion = new Version(VersionHelper.CurrentVersion);
                var latestVersion = allVersions.FirstOrDefault(v => v.IsLatest);
                if (latestVersion != null)
                {
                    // 筛选当前版本到最新版本之间的版本（不包含当前版本，只显示更新的版本）
                    var relevantVersions = allVersions.Where(v => 
                    {
                        if (Version.TryParse(v.Version, out Version version))
                        {
                            return version > currentVersion; // 只显示比当前版本更新的版本
                        }
                        return false;
                    }).OrderByDescending(v => v.Version).ToList();

                    // 为每个版本添加LatestVersionText属性
                    var versionHistoryWithText = relevantVersions.Select(v => new
                    {
                        v.Version,
                        v.DownloadUrl,
                        v.DownloadUrlWithNet,
                        v.Changelog,
                        v.ReleaseDate,
                        v.IsLatest,
                        LatestVersionText = v.IsLatest ? "最新版本" : ""
                    }).ToList();

                    // 设置版本历史列表的数据源
                    VersionHistoryItemsControl.ItemsSource = versionHistoryWithText;
                }
            }
        }

        #endregion

        #region 辅助方法

        // 窗口居中
        private void LocateCenter()
        {
            double screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight; // 获取屏幕高度
            double screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth; // 获取屏幕宽度
            double windowHeight = Height; // 获取窗口高度
            double windowWidth = Width; // 获取窗口宽度
            
            Left = (screenWidth / 2) - (windowWidth / 2); // 计算水平居中位置
            Top = (screenHeight / 2) - (windowHeight / 2); // 计算垂直居中位置
        }

        // 清理资源
        private void CleanupResources()
        {
            // 清理字段
            _downloadUrl = null;
            _downloadUrlWithNet = null;
            _updateManager?.Dispose(); // 释放更新管理器

            // 清理UI元素引用
            VersionTextBlock.Text = null;
            LatestVersionTextBlock1.Text = null;
            VersionChangeTextBlock.Text = null;
            
            // 清理版本历史列表
            VersionHistoryItemsControl.ItemsSource = null;

            // 清理数据绑定
            Content = null;
            DataContext = null;
        }

        #endregion
    }
}