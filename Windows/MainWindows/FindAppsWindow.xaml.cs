using Quicker.UserControls.AddWindow;
using System.Collections.ObjectModel;
using Quicker.Windows.ToolWindows;
using Quicker.Windows.AddWindows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Data;
using Quicker.Database;
using Quicker.Managers;
using Quicker.Models;
using System.Windows;
using System.IO;

namespace Quicker.Windows.MainWindows
{
    public partial class FindAppsWindow : Window
    {
        public delegate void ApplicationSelectedEventHandler(object sender, ApplicationSelectedEventArgs e); // 选中应用事件
        private static WeakReference<FindAppsWindow> _findAppsWindowRef = new(null); // 避免内存泄漏
        private CancellationTokenSource _cancellationTokenSource = new(); // 取消令牌源,管理异步任务
        public event ApplicationSelectedEventHandler ApplicationSelected; // 选中应用事件
        private ObservableCollection<AppInfo> _allApplications = new(); // 所有应用
        private SelectWindowWindow selectWindowWindow; // SelectWindowWindow 的实例引用
        private List<AppInfo> _searchResults = new(); // 搜索结果
        private ICollectionView _applicationView; // ICollectionView接口
        private ScrollViewer scrollViewer; // 滚动条

        private static T FindAncestor<T>(DependencyObject dependencyObject) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(dependencyObject);
            if (parent == null) return null;
            if (parent is T t) return t;
            return FindAncestor<T>(parent);
        } // 辅助方法：查找祖先元素
        public static FindAppsWindow findAppsWindow
        {
            get
            {
                if (_findAppsWindowRef.TryGetTarget(out var window))
                    return window; // 如果弱引用有效，返回窗口
                return null; // 弱引用无效，返回null
            }
            set
            {
                _findAppsWindowRef = new WeakReference<FindAppsWindow>(value); // 设置弱引用
            }
        } // 避免内存泄漏
        public class ApplicationSelectedEventArgs : EventArgs
        {
            public AppInfo SelectedApp { get; set; }
        } // 传递选中的应用
        public AppInfo SelectedApp { get; set; } // 选中的应用

        public FindAppsWindow()
        {
            InitializeComponent(); // 初始化窗口的UI组件
            SearchTextBox.Focus(); // 让搜索框获得焦点
            _applicationView = CollectionViewSource.GetDefaultView(_allApplications); // 创建视图
            ApplicationsListView.ItemsSource = _applicationView; // 绑定 ItemsSource
        }

        // UI加载完成后加载应用
        private void LoadApplications(object sender, EventArgs e)
        {
            LoadApplications(); // 调用加载应用的方法
        }

        // 加载应用
        private async void LoadApplications()
        {
            _allApplications.Clear(); // 清空原始数据源
            LoadingWindow loadingWindow = new(); // 显示加载窗口
            loadingWindow.Show(); // 显示加载窗口

            try
            {
                // 使用AppManager加载应用
                AppManager appManager = new();
                var apps = await appManager.LoadAllApplicationsAsync(); // 加载所有应用并排序
                foreach (var app in apps) // 将排序后的应用添加到集合中
                {
                    _allApplications.Add(app);
                }
                // 刷新视图
                _applicationView.Refresh();
            }
            catch { }
            finally
            {
                InitializeScrollBar(); // 初始化滚动条
                loadingWindow?.Close(); // 关闭加载窗口
            }
        }

        // 初始化滚动条
        private void InitializeScrollBar()
        {
            scrollViewer = GetScrollViewer(ApplicationsListView); // 获取ListView的ScrollViewer

            // 初始化纵向滚动条
            VerticalScrollBar.Maximum = scrollViewer.ScrollableHeight; // 设置最大值
            VerticalScrollBar.ViewportSize = scrollViewer.ViewportHeight; // 设置视口大小
            VerticalScrollBar.Value = scrollViewer.VerticalOffset; // 设置当前值

            // 初始化横向滚动条
            HorizontalScrollBar.Maximum = scrollViewer.ScrollableWidth; // 设置最大值
            HorizontalScrollBar.ViewportSize = scrollViewer.ViewportWidth; // 设置视口大小
            HorizontalScrollBar.Value = scrollViewer.HorizontalOffset; // 设置当前值
        }

        /// <summary>
        /// 获取Visual的ScrollViewer
        /// </summary>
        /// <param name="visual"> 要查找的Visual </param>
        /// <returns> ScrollViewer</returns>
        private ScrollViewer GetScrollViewer(Visual visual)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                Visual child = VisualTreeHelper.GetChild(visual, i) as Visual; // 获取子元素
                if (child != null) // 如果子元素不为空
                {
                    ScrollViewer scrollViewer = child as ScrollViewer; // 尝试转换为ScrollViewer
                    if (scrollViewer != null) // 如果是ScrollViewer
                        return scrollViewer; // 返回ScrollViewer
                    scrollViewer = GetScrollViewer(child); // 递归查找
                    if (scrollViewer != null) // 如果找到ScrollViewer
                        return scrollViewer; // 返回ScrollViewer
                }
            }
            return null; // 未找到返回空
        }

        // 文本框内容改变时，进行查找
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchApps(); // 调用搜索方法
        }

        // 通过应用名称在ListView中查找应用
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchApps(); // 调用搜索方法
        }

        /// <summary>
        /// 根据搜索框输入内容，过滤应用列表，并同步高亮关键字。
        /// </summary>
        private void SearchApps()
        {
            string searchText = SearchTextBox.Text.Trim(); // 获取搜索框输入的文本，并去除首尾空格
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 遍历所有应用，将当前搜索关键字同步到每个AppInfo（用于高亮显示）
                foreach (var app in _allApplications)
                {
                    app.SearchText = searchText;
                }
                // 设置集合视图的过滤器，只显示名称中包含搜索关键字的应用
                _applicationView.Filter = item =>
                {
                    if (string.IsNullOrEmpty(searchText))
                        return true; // 搜索内容为空时显示全部
                    AppInfo app = item as AppInfo;
                    // 仅当应用名称包含关键字时显示
                    return app != null && app.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                };
            }); // 在UI线程中执行过滤和高亮同步
        }

        // 刷新ListView
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            AppManager appManager = new();
            appManager.ClearCache(); // 清空缓存

            LoadApplications(); // 重新加载应用
        }

        // 在文件管理器中定位应用
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            AddActionWindow addWindow = Application.Current.Windows.OfType<AddActionWindow>().FirstOrDefault(); // 获取AddWindow实例
            OpenFile openFile = addWindow.ActionInfoGrid.Children[0] as OpenFile;
            openFile.ChooseProcess(null, null); // 调用选择进程的方法
        }
        
        // 选择窗口按钮点击事件
        private void SelectWindowButton_Click(object sender, RoutedEventArgs e)
        {
            selectWindowWindow = new(); // 创建 SelectWindowWindow 实例
            selectWindowWindow.WindowSelected += OnWindowSelected; // 订阅 WindowSelected 事件
            selectWindowWindow.StartSelecting(this); // 开始选择窗口
        }

        // 处理选中的窗口
        private void OnWindowSelected(object sender, SelectWindowWindow.WindowSelectedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.ProcessPath) || e.WindowHandle != IntPtr.Zero)
            {
                try
                {
                    AppInfo selectedApp;
                    if (e.ProcessPath.StartsWith("hwnd:", StringComparison.OrdinalIgnoreCase))
                    {
                        string windowName = !string.IsNullOrEmpty(e.WindowTitle)
                            ? e.WindowTitle
                            : "系统窗口";

                        selectedApp = new AppInfo
                        {
                            Name = windowName,
                            Location = e.ProcessPath,
                            Tag = e.ProcessPath
                        };
                    }
                    else // 普通应用程序窗口：使用进程路径
                    {
                        selectedApp = new AppInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(e.ProcessPath),
                            Location = e.ProcessPath,
                            Tag = e.ProcessPath
                        };
                    }

                    if (e.ProcessIcon != null)
                        selectedApp.Icon = e.ProcessIcon;

                    SelectedApp = selectedApp;
                    ApplicationSelected?.Invoke(this, new ApplicationSelectedEventArgs { SelectedApp = selectedApp });
                    this.Close();
                }
                catch { }
                finally
                {
                    selectWindowWindow?.Close();
                }
            }
        }

        // 鼠标移入ListView显示滚动条
        private void ShowScrollBar(object sender, System.Windows.Input.MouseEventArgs e)
        {
            VerticalScrollBar.Visibility = Visibility.Visible; // 显示纵向滚动条
            HorizontalScrollBar.Visibility = Visibility.Visible; // 显示横向滚动条
        }

        // 鼠标移出ListView隐藏滚动条
        private void HideScrollBar(object sender, System.Windows.Input.MouseEventArgs e)
        {
            VerticalScrollBar.Visibility = Visibility.Collapsed; // 隐藏纵向滚动条
            HorizontalScrollBar.Visibility = Visibility.Collapsed; // 隐藏横向滚动条
        }

        // 递归查找VisualChild
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i); // 获取子元素
                if (child is T t)
                {
                    return t; // 返回匹配的子元素
                }

                T foundChild = FindVisualChild<T>(child); // 递归查找
                if (foundChild != null)
                {
                    return foundChild; // 返回匹配的子元素
                }
            }
            return null; // 未找到返回空
        }

        // 选中软件后启用保存Button
        private void ApplicationsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selectedItem = ApplicationsListView.SelectedItem as AppInfo;
            SelectedApp = selectedItem; // 选中的应用
            SaveButton.IsEnabled = true; // 启用保存按钮
        }

        // 双击选中应用直接保存
        private void SaveApplication(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ApplicationsListView.Items.Count > 0)
            {
                SaveApp(sender, e); // 保存程序
            }
        }

        // 保存程序
        private void SaveApp(object sender, RoutedEventArgs e)
        {
            ApplicationSelected?.Invoke(this, new ApplicationSelectedEventArgs { SelectedApp = SelectedApp }); // 触发事件，通知其他窗口应用被选中
            this.Close(); // 关闭窗口
        }

        // 关闭FindAppsWindow
        private void CloseThisWindow(object sender, RoutedEventArgs e)
        {
            this.Close(); // 关闭窗口
        }

        // 复制应用地址
        private void CopyLocation(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource); // 获取右键点击的 ListViewItem
            if (listViewItem != null)
            {
                AppInfo selectedApp = listViewItem.DataContext as AppInfo; // 获取选中的应用
                if (selectedApp != null && !string.IsNullOrEmpty(selectedApp.Location))
                {                    
                    string directoryPath = Path.GetDirectoryName(selectedApp.Location); // 提取文件所在目录路径
                    if (!string.IsNullOrEmpty(directoryPath))
                    {                        
                        Clipboard.SetText(directoryPath); // 复制目录路径到剪贴板
                        using var toast = new ToastManager(); // 消息提醒管理器
                        toast.Show("文件夹路径已复制到剪贴板！", ToastType.Common); // 弹出消息提醒
                    }
                }
            }
        }

        // 同步滚动条数据
        private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            scrollViewer.ScrollToVerticalOffset(VerticalScrollBar.Value);
        }
        private void HorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            scrollViewer.ScrollToHorizontalOffset(HorizontalScrollBar.Value);
        }
        private void ListViewScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (scrollViewer == null) return;
            // 更新纵向滚动条
            VerticalScrollBar.Maximum = scrollViewer.ScrollableHeight; // 设置最大值
            VerticalScrollBar.ViewportSize = scrollViewer.ViewportHeight; // 设置视口大小
            VerticalScrollBar.Value = scrollViewer.VerticalOffset; // 设置当前值

            // 更新横向滚动条
            HorizontalScrollBar.Maximum = scrollViewer.ScrollableWidth; // 设置最大值
            HorizontalScrollBar.ViewportSize = scrollViewer.ViewportWidth; // 设置视口大小
            HorizontalScrollBar.Value = scrollViewer.HorizontalOffset; // 设置当前值
        }

        // 关闭窗口前，释放资源
        protected override void OnClosed(EventArgs e)
        {
            // 取消事件订阅
            if (selectWindowWindow != null)
            {
                selectWindowWindow.WindowSelected -= OnWindowSelected;
                selectWindowWindow.Close();
                selectWindowWindow = null;
            }
            
            ApplicationSelected = null; // 清理事件
            _applicationView = null; // 清理视图

            // 清理应用列表
            _allApplications?.Clear(); // 清空应用列表
            _allApplications = null; // 清理应用列表

            // 清理搜索结果
            _searchResults?.Clear(); // 清空搜索结果
            _searchResults = null; // 清理搜索结果

            // 取消所有异步操作
            _cancellationTokenSource?.Cancel(); // 取消所有异步操作
            _cancellationTokenSource?.Dispose(); // 释放取消令牌源
            _cancellationTokenSource = null; // 清理取消令牌源

            // 清理静态引用
            findAppsWindow = null; // 清理静态引用

            // 调用基类的 OnClosed 方法
            base.OnClosed(e); // 调用基类的 OnClosed 方法
            GC.Collect(); // 强制垃圾回收
            GC.WaitForPendingFinalizers(); // 等待垃圾回收完成
            GC.Collect(); // 强制垃圾回收
        }
    }
}