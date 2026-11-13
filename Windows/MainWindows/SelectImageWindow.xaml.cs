using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Diagnostics;
using Quicker.Helpers;
using System.Windows;
using System.IO;

namespace Quicker.Windows.MainWindows
{
    public partial class SelectImageWindow : Window
    {
        public ObservableCollection<ImageItem> ImageItems { get; set; } = new(); // 图片项集合
        public string SelectedImagePath { get; private set; } = string.Empty; // 选中的图片路径
        public event EventHandler<string> ImageConfirmed; // 图片确认事件
        private ScrollViewer listViewScrollViewer; // 图片列表的 ScrollViewer
        private int loadedImageCount = 0; // 已加载的图片数量
        private string[] allImageFiles; // 存储所有图片文件路径

        public SelectImageWindow()
        {
            InitializeComponent();
        }

        // 窗口加载事件
        private void SelectImageWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ImageListView.ItemsSource = ImageItems; // 设置图片列表的数据源
            listViewScrollViewer = FindScrollViewer(ImageListView); // 查找 ScrollViewer
            var itemsPanel = FindVisualChild<WrapPanel>(ImageListView); // 查找 WrapPanel
            listViewScrollViewer.Content = itemsPanel; // 设置 ScrollViewer 的内容为 WrapPanel
            LoadImagesFromFolder(); // 从文件夹加载图片
        }

        // 重新计算 WrapPanel 的高度
        private void UpdateWrapPanelHeight()
        {
            if (listViewScrollViewer == null) return; // 如果 ScrollViewer 为空，则返回
            var itemsPanel = FindVisualChild<WrapPanel>(ImageListView); // 查找 WrapPanel
            if (itemsPanel == null) return; // 如果 WrapPanel 为空，则返回

            int itemsPerRow = CalculateItemsPerRow(itemsPanel); // 计算每行显示的项数
            var elements = itemsPanel.Children.OfType<FrameworkElement>(); // 获取 WrapPanel 中的所有项
            if (elements.Any()) // 如果 WrapPanel 包含项
            {
                double rowHeight = elements.Max(e => e.DesiredSize.Height); // 获取每行的最大高度
                int totalRows = (int)Math.Ceiling((double)itemsPanel.Children.Count / itemsPerRow); // 计算总行数
                itemsPanel.Height = rowHeight * totalRows; // 设置 WrapPanel 的高度
            }
        }

        /// <summary>
        /// 计算每行显示的项数
        /// </summary>
        /// <param name="itemsPanel"> WrapPanel </param>
        /// <returns> 每行显示的项数 </returns>
        private int CalculateItemsPerRow(WrapPanel itemsPanel)
        {
            double panelWidth = itemsPanel.ActualWidth; // 获取 WrapPanel 的宽度
            double itemWidth = ((FrameworkElement)itemsPanel.Children[0]).ActualWidth; // 所有项的宽度相同
            return Math.Max(1, (int)(panelWidth / itemWidth)); // 每行显示的项数
        }

        // 窗口大小变化时，重新计算 WrapPanel 的高度
        private void SelectImageWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWrapPanelHeight(); // 重新计算 WrapPanel 的高度
        }

        // 更新滚动条的值
        private void ListViewScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            VerticalScrollBar.Maximum = e.ExtentHeight - e.ViewportHeight; // 更新滚动条的最大值
            VerticalScrollBar.ViewportSize = e.ViewportHeight; // 更新滚动条的视窗大小
            if (listViewScrollViewer != null)
                VerticalScrollBar.Value = listViewScrollViewer.VerticalOffset; // 更新滚动条的值
        }

        // 外部滚动条的值变化事件
        private void ExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            listViewScrollViewer.ScrollToVerticalOffset(e.NewValue); // 更新 ScrollViewer 的位置
        }

        // 查找 ScrollViewer
        private ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) // 遍历子元素
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i); // 获取子元素
                if (child is ScrollViewer)
                    return (ScrollViewer)child; // 返回 ScrollViewer
                else
                {
                    ScrollViewer scrollViewer = FindScrollViewer(child); // 递归查找
                    if (scrollViewer != null) // 如果找到 ScrollViewer
                        return scrollViewer; // 返回 ScrollViewer
                }
            }
            return null; // 未找到返回空
        }

        // 从文件夹加载图片
        private void LoadImagesFromFolder()
        {
            string folderPath = AppPathHelper.LocalIconsFolder; // 获取文件夹路径
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath); // 创建文件夹
            }

            allImageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => IsImageFile(file))
                .ToArray(); // 获取所有图片文件

            LoadInitialImages(); // 加载初始图片
        }

        /// <summary>
        /// 检查文件是否为图片文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        private bool IsImageFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".svg" }.Contains(extension);
        }

        // 加载初始图片
        private void LoadInitialImages()
        {
            LoadImages(27); // 初始加载27张图片
            LoadMoreButton.IsEnabled = allImageFiles.Length > 27; // 如果还有更多图片，启用加载更多按钮
        }

        /// <summary>
        /// 加载图片
        /// </summary>
        /// <param name="maxImagesToLoad">一次加载的最大图片数量</param>
        private void LoadImages(int maxImagesToLoad)
        {
            if (allImageFiles == null || loadedImageCount >= allImageFiles.Length) // 如果已加载所有图片，则返回
            {
                LoadMoreButton.IsEnabled = false;
                return;
            }

            int remainingImages = allImageFiles.Length - loadedImageCount; // 计算剩余图片数量
            int imagesToLoad = Math.Min(maxImagesToLoad, remainingImages); // 计算本次加载的图片数量
            var iconManager = new Quicker.Managers.IconManager(); // 创建 IconManager 对象
            for (int i = 0; i < imagesToLoad; i++)
            {
                string imagePath = allImageFiles[loadedImageCount + i]; // 获取图片路径
                FileInfo fileInfo = new(imagePath); // 获取文件信息
                var imageControl = new Image
                {
                    Stretch = Stretch.Uniform
                };

                // 关键：支持 GIF
                iconManager.SetImageWithGifSupport(imageControl, imagePath); // 设置图片源
                var imageItem = new ImageItem
                {
                    FilePath = imagePath,
                    FileName = fileInfo.Name,
                    ImageControl = imageControl
                }; // 创建图片项
                ImageItems.Add(imageItem); // 添加到图片项集合
            }
            loadedImageCount += imagesToLoad; // 更新已加载的图片数量
        }

        /// <summary>
        /// 加载图片
        /// </summary>
        /// <param name="filePath"> 图片文件路径 </param>
        private BitmapImage LoadImage(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null; // 如果文件不存在，返回空
                BitmapImage bi = new BitmapImage(); // 创建 BitmapImage 对象
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    bi.BeginInit(); // 开始初始化图片
                    bi.CacheOption = BitmapCacheOption.OnLoad; // 设置缓存选项
                    bi.StreamSource = stream; // 设置图片流
                    bi.EndInit(); // 加载图片
                }
                return bi; // 返回图片
            }
            catch{ return null; } // 加载失败返回空
        }

        // 关闭窗口
        private void CancelSelect(object sender, RoutedEventArgs e) => Close(); // 关闭窗口

        // 选择本地图标
        private void SelectLocalIcons(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter =
                "所有支持的文件 (*.png;*.ico;*.svg;*.exe;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.ico;*.svg;*.exe;*.jpg;*.jpeg;*.bmp;*.gif|" +
                "PNG 图片 (*.png)|*.png|" +
                "Icon 文件 (*.ico)|*.ico|" +
                "Exe 文件 (*.exe)|*.exe|" +
                "Svg 图标 (*.svg)|*.svg|" +
                "Jpg 文件 (*.jpg)|*.jpg|" +
                "Jpeg 文件 (*.jpeg)|*.jpeg|" +
                "Bmp 文件 (*.bmp)|*.bmp|" +
                "Gif 文件 (*.gif)|*.gif",
                Multiselect = false, // 不允许多选
                Title = "选择图片文件"
            }; // 创建文件选择对话框
            if (openFileDialog.ShowDialog() == true)
            {
                if (openFileDialog.FileNames.Length != 0)
                {
                    SelectedImagePath = openFileDialog.FileName; // 设置选中的图片路径
                    AddIcon(sender, e); // 触发图片确认事件
                }
            }
        }

        // 按下键盘事件
        private void SelectImageWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.S)
                AddIcon(sender, e); // 按下 S 键添加图标
        }

        // 添加图标
        private void AddIcon(object sender, RoutedEventArgs e)
        {
            ImageConfirmed?.Invoke(this, SelectedImagePath); // 触发图片确认事件
            this.Close(); // 关闭窗口
        }

        // 双击确认图片
        private void ConfirmImage(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedImage = ImageListView.SelectedItem as ImageItem; // 获取选中的图片
            if (selectedImage != null)
            {
                SelectedImagePath = selectedImage.FilePath; // 设置选中的图片路径
                AddIcon(sender, e); // 触发图片确认事件
            }
        }

        // 选择图片
        private void SelectImage(object sender, SelectionChangedEventArgs e)
        {
            var selectedImage = ImageListView.SelectedItem as ImageItem; // 获取选中的图片
            if (selectedImage != null)
            {
                SelectedImagePath = selectedImage.FilePath; // 设置选中的图片路径
                ConfirmButton.IsEnabled = true; // 启用确认按钮
            }
        }

        // 递归查找VisualChild
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i); // 获取子元素
                if (child is T t)
                    return t; // 返回匹配的子元素

                T foundChild = FindVisualChild<T>(child); // 递归查找
                if (foundChild != null)
                    return foundChild; // 返回匹配的子元素
            }
            return null; // 未找到返回空
        }

        // 管理本地图标
        private void ManageLocalIcons(object sender, RoutedEventArgs e)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.LocalIconsFolder); // 确保目录存在
            Process.Start(new ProcessStartInfo(AppPathHelper.LocalIconsFolder) { UseShellExecute = true }); // 打开文件夹
        }

        // 继续加载图片
        private void LoadImages(object sender, RoutedEventArgs e)
        {
            LoadImages(27); // 加载图片
            UpdateWrapPanelHeight(); // 重新计算 WrapPanel 的高度
        }

        // 关闭窗口前，释放资源
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e); // 调用基类的 OnClosed 方法

            foreach (var imageItem in ImageItems) // 遍历图片项集合
                imageItem.ImageControl = null; // 释放图片资源
            ImageItems.Clear(); // 清空图片项集合

            // 清理事件订阅
            ImageConfirmed = null; // 清空事件

            // 释放其他资源
            ImageListView.ItemsSource = null; // 清空 ListView 数据源
            SelectedImagePath = null; // 清理选中图片路径
            listViewScrollViewer = null; // 清理 ScrollViewer 引用
        }
    }

    // 图片项的数据模型
    public class ImageItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public Image ImageControl { get; set; }  // 用 Image 控件承载，支持 GIF 动图
    }
}