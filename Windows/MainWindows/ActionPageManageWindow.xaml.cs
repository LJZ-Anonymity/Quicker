using VisualTreeHelper = Quicker.Helpers.VisualTreeHelper;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using Quicker.Windows.EditWindows;
using Quicker.Windows.ToolWindows;
using Quicker.Windows.AddWindows;
using System.Windows.Controls;
using Quicker.Database.Core;
using Quicker.Windows.Menus;
using System.Windows.Input;
using System.Windows.Media;
using Quicker.Internal;
using Quicker.Managers;
using Quicker.Helpers;
using Quicker.Models;
using System.Windows;
using System.IO;

namespace Quicker.Windows.MainWindows
{
    public partial class ActionPageManageWindow : Window
    {
        private bool isDarkModle = false, showUsedTimes = false; // 是否为暗黑模式，是否显示使用次数
        private readonly ButtonManager buttonManager = new(); // 按钮管理器
        private readonly ActionPageDatabase db3 = new(); // 动作页数据库
        private readonly ButtonDatabase db2 = new(); // 按钮数据库
        private string type; // 场景类型

        private static SolidColorBrush actionDarkModleButtonBrush; // 动作按钮暗黑模式背景色
        private static SolidColorBrush actionButtonBrush; // 动作按钮背景色
        private static SolidColorBrush actionButtonMouseOverBrush; // 动作按钮鼠标移入背景色
        private static SolidColorBrush blankButtonBrush; // 空白按钮背景色
        private static SolidColorBrush blankButtonMouseOverBrush; // 空白按钮鼠标移入背景色

        #region 脏位刷新

        private readonly HashSet<string> _dirty = new();
        private readonly object _dirtyLock = new();
        private bool _refreshScheduled;
        private bool _isSelfTriggered;

        private void SubscribeStateEvents()
        {
            AppStateManager.AppearanceChanged += OnAppearanceChanged;
            AppStateManager.ConventionsChanged += OnConventionsChanged;
            AppStateManager.ActionPageChanged += OnActionPageChanged;
            AppStateManager.SceneListChanged += OnSceneListChanged;
        }

        private void UnsubscribeStateEvents()
        {
            AppStateManager.AppearanceChanged -= OnAppearanceChanged;
            AppStateManager.ConventionsChanged -= OnConventionsChanged;
            AppStateManager.ActionPageChanged -= OnActionPageChanged;
            AppStateManager.SceneListChanged -= OnSceneListChanged;
        }

        private void OnAppearanceChanged(Quicker.Models.Settings.Appearance appearance)
        {
            if (_isSelfTriggered) return;
            MarkDirty("appearance");
            ScheduleRefresh();
        }

        private void OnConventionsChanged()
        {
            if (_isSelfTriggered) return;
            MarkDirty("conventions");
            ScheduleRefresh();
        }

        private void OnActionPageChanged(string table)
        {
            if (_isSelfTriggered) return;
            MarkDirty($"page:{table}");
            ScheduleRefresh();
        }

        private void OnSceneListChanged(string table)
        {
            if (_isSelfTriggered) return;
            MarkDirty($"scenes:{table}");
            ScheduleRefresh();
        }

        private void MarkDirty(string key)
        {
            lock (_dirtyLock) { _dirty.Add(key); }
        }

        private bool ConsumeDirty(string key)
        {
            lock (_dirtyLock) { return _dirty.Remove(key); }
        }

        private List<string> ConsumeDirtyByPrefix(string prefix)
        {
            lock (_dirtyLock)
            {
                var keys = _dirty
                    .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                    .ToList();
                foreach (var k in keys) _dirty.Remove(k);
                return keys.Select(k => k.Substring(prefix.Length)).ToList();
            }
        }

        private void ScheduleRefresh()
        {
            if (Dispatcher.HasShutdownStarted) return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ScheduleRefresh));
                return;
            }

            if (_refreshScheduled) return;
            _refreshScheduled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _refreshScheduled = false;
                RefreshOnShow();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 按脏位增量刷新 UI（在 UI 线程调用）
        /// </summary>
        private void RefreshOnShow()
        {
            if (type == null) return; // 窗口正在关闭

            // 外观变更：重新加载颜色 + 刷新所有按钮背景 + 场景按钮背景
            if (ConsumeDirty("appearance"))
            {
                InitAppearanceBrushes();
                RefreshAllButtonBackgrounds();
                SetButtonBackground();
            }

            // 行为设置变更
            if (ConsumeDirty("conventions"))
            {
                LoadSettings();
            }

            // 页面数据变更（按钮内容）
            foreach (var table in ConsumeDirtyByPrefix("page:"))
            {
                if (string.Equals(table, type, StringComparison.Ordinal))
                    RefreshCurrentPageButtons();
            }

            // 场景页数变更（动作页数量变化）
            foreach (var table in ConsumeDirtyByPrefix("scenes:"))
            {
                if (string.Equals(table, type, StringComparison.Ordinal))
                    LoadCanvas(type);
            }
        }

        /// <summary>
        /// 刷新所有画布中动作按钮的背景色（不重建画布）
        /// </summary>
        private void RefreshAllButtonBackgrounds()
        {
            foreach (var item in MainListView.Items)
            {
                if (item is not Canvas canvas) continue;
                var grid = canvas.Children.Cast<UIElement>().OfType<UniformGrid>().FirstOrDefault();
                if (grid == null) continue;
                foreach (var child in grid.Children)
                {
                    if (child is not Button button) continue;
                    button.Background = isDarkModle
                        ? (button.Tag is ButtonData ? actionDarkModleButtonBrush : blankButtonBrush)
                        : (button.Tag is ButtonData ? actionButtonBrush : blankButtonBrush);
                }
            }
        }

        /// <summary>
        /// 刷新当前画布中所有按钮的显示内容
        /// </summary>
        private void RefreshCurrentPageButtons()
        {
            foreach (var item in MainListView.Items)
            {
                if (item is not Canvas canvas) continue;
                var grid = canvas.Children.Cast<UIElement>().OfType<UniformGrid>().FirstOrDefault();
                if (grid == null) continue;
                foreach (var child in grid.Children)
                {
                    if (child is not Button button) continue;
                    int buttonId = int.Parse(button.Name.Replace(type, ""));
                    var data = db2.GetButtonDataByID(buttonId, type);
                    buttonManager.RefreshButtonDisplay(button, data, 60, false);
                    button.Background = isDarkModle
                        ? (data != null ? actionDarkModleButtonBrush : blankButtonBrush)
                        : (data != null ? actionButtonBrush : blankButtonBrush);
                }
            }
        }

        #endregion

        public ActionPageManageWindow(string type = "common")
        {
            InitAppearanceBrushes(); // 加载 Appearance 颜色
            InitializeComponent(); // 初始化窗口
            SubscribeStateEvents();
            GenerateSceneButtons(); // 加载按钮前缀
            TypeChanged(type); // 默认加载通用场景
        }

        // 加载 Appearance 颜色
        private void InitAppearanceBrushes()
        {
            var appearance = Quicker.Database.Core.SettingDatabase.GetAppearanceSettings()?.FirstOrDefault();
            if (appearance != null)
            {
                actionDarkModleButtonBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("DarkGray"));
                actionButtonBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.ActionButtonColor));
                actionButtonMouseOverBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.ActionButtonMouseOverColor));
                blankButtonBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.BlankButtonColor));
                blankButtonMouseOverBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.BlankButtonMouseOverColor));
            }
        }

        /// <summary>
        /// 场景类型改变事件
        /// </summary>
        /// <param name="targetType"> 目标类型 </param>
        private void TypeChanged(string targetType)
        {
            if (type == targetType) return; // 如果目标类型与当前类型相同，直接返回
            type = targetType; // 设置类型
            LoadCanvas(type); // 加载动作页画布
            LoadSettings(); // 加载设置
            SetButtonBackground(); // 设置场景按钮背景色
            SetSceneTitle(); // 设置场景标题
        }

        // 加载设置
        private void LoadSettings()
        {
            var autoReturn = db3.GetAutoReturnToFirstPage(type); // 获取设置
            AutoReturnToFirstPageCheckBox.IsChecked = autoReturn; // 加载设置
        }

        // 设置场景按钮背景色
        private void SetButtonBackground()
        {
            foreach (UIElement element in ActionPagesButtonPanel.Children)
            {
                Button button = element as Button; // 转换为按钮
                button.Background = button.Name == type
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9E9E9"))
                    : Brushes.Transparent; // 设置背景色
            }
        }

        // 鼠标移入Button高亮按钮
        private void HightLightBlacklistItem(object sender, MouseEventArgs e)
        {
            Button button = sender as Button; // 转换发送者为Button对象
            button.Background = button.Name == type
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DFDFDF"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")); // 设置背景色
        }

        // 鼠标移出Button恢复原状
        private void FadeBlacklistItem(object sender, MouseEventArgs e)
        {
            Button button = sender as Button; // 转换发送者为Button对象
            button.Background = button.Name == type
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9E9E9"))
                : Brushes.Transparent; // 设置背景色
        }

        /// <summary>
        /// 生成场景按钮
        /// </summary>
        /// <param name="filter"></param>
        private void GenerateSceneButtons(string filter = null)
        {
            ActionPagesButtonPanel.Children.Clear(); // 先清空
            var sceneData = db3.GetAllSceneData(); // 获取所有场景数据
            var filteredScenes = FilterScenes(sceneData, filter); // 过滤场景
            var orderedScenes = OrderScenes(filteredScenes); // 排序场景
            foreach (var data in orderedScenes)
            {
                CreateSceneButton(data, filter); // 创建场景按钮
            }
        }

        /// <summary>
        /// 根据过滤条件筛选场景数据，只保留名称或标签包含关键字的场景。
        /// </summary>
        /// <param name="sceneData">所有场景数据</param>
        /// <param name="filter">过滤关键字</param>
        /// <returns>筛选后的场景数据列表</returns>
        private List<SceneData> FilterScenes(IEnumerable<SceneData> sceneData, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return sceneData.ToList(); // 无过滤条件直接返回所有场景数据
            string lowerFilter = filter.ToLower(); // 转换为小写字母
            return sceneData.Where(data =>
            {
                string sceneTitle = db3.GetSceneTitle(data);
                string sceneTag = data.SceneTag ?? ""; // 标签可能为空
                return sceneTitle.ToLower().Contains(lowerFilter) || sceneTag.ToLower().Contains(lowerFilter);
            }).ToList(); // 筛选
        }

        /// <summary>
        /// 对场景数据进行排序，默认场景优先，其他场景按原顺序排列。
        /// </summary>
        /// <param name="scenes">待排序的场景数据</param>
        /// <returns>排序后的场景数据列表</returns>
        private List<SceneData> OrderScenes(List<SceneData> scenes)
        {
            var defaultScenes = new List<string> { "_global", "common", "taskbar", "desktop" };
            var ordered = new List<SceneData>(new SceneData[defaultScenes.Count]); // 默认场景
            var others = new List<SceneData>(); // 其他场景
            foreach (var data in scenes)
            {
                int index = defaultScenes.IndexOf(data.SceneName);
                if (index >= 0)
                {
                    ordered[index] = data;
                }
                else
                {
                    others.Add(data);
                }
            }
            // 合并默认场景和其他场景，去除空项
            return ordered.Where(s => s != null).Concat(others).ToList();
        }

        /// <summary>
        /// 创建场景按钮
        /// </summary>
        /// <param name="sceneData"> 场景数据 </param>
        /// <param name="filter"> 过滤关键字 </param>
        private void CreateSceneButton(SceneData sceneData, string filter = null)
        {
            var button = new Button()
            {
                Style = (Style)FindResource("SceneButton"), // 应用样式
                Name = sceneData.SceneTag
            };
            Grid buttonContent = CreateButtonContent(sceneData, filter); // 使用模块化方法创建按钮内容
            button.Content = buttonContent; // 设置按钮内容
            SetupButtonEvents(button); // 使用模块化方法设置按钮事件
            ActionPagesButtonPanel.Children.Add(button); // 将按钮添加到StackPanel
        }

        /// <summary>
        /// 创建场景按钮内容（主方法，组装图片、名称、标签）
        /// </summary>
        /// <param name="sceneData"> 场景数据 </param>
        /// <param name="filter"> 过滤关键字 </param>
        /// <returns> 按钮内容 </returns>
        private Grid CreateButtonContent(SceneData sceneData, string filter = null)
        {
            Grid buttonContent = new() { Style = (Style)FindResource("SceneButtonContentGrid") }; // 创建网格
            buttonContent.Children.Add(CreateSceneImage(sceneData)); // 添加图片
            buttonContent.Children.Add(CreateSceneNameTextBlock(sceneData, filter)); // 添加场景名称
            buttonContent.Children.Add(CreateSceneTagTextBlock(sceneData, filter)); // 添加场景标签
            return buttonContent; // 返回按钮内容
        }

        /// <summary>
        /// 创建场景图片控件
        /// </summary>
        /// <param name="sceneData"> 场景数据 </param>
        /// <returns> Image控件 </returns>
        private Image CreateSceneImage(SceneData sceneData)
        {
            const string defaultImagePath = "pack://application:,,,/Resources/Images/Quicker_Enabled.png";
            const string resourcePrefix = "/Resources/Images/";
            const string packPrefix = "pack://application:,,,";
            try // 如果场景图标路径为空则使用默认图片，否则尝试加载指定图片
            {
                Image image = new Image
                {
                    Source = string.IsNullOrEmpty(sceneData.SceneIconPath)
                        ? new BitmapImage(new Uri(defaultImagePath, UriKind.Absolute))
                        : CreateImageSource(sceneData.SceneIconPath, resourcePrefix, packPrefix, defaultImagePath),
                    Style = (Style)FindResource("SceneButtonImage")
                };
                return image;
            }
            catch (Exception ex)
            {
                ShowToast("加载场景图片失败：" + ex.Message, ToastType.Error);
                return new Image
                {
                    Source = new BitmapImage(new Uri(defaultImagePath, UriKind.Absolute)),
                    Style = (Style)FindResource("SceneButtonImage")
                };
            }
        }

        /// <summary>
        /// 创建场景图片控件
        /// </summary>
        /// <param name="path"> 图片路径 </param>
        /// <param name="resourcePrefix"> 资源前缀 </param>
        /// <param name="packPrefix"> 打包前缀 </param>
        /// <param name="defaultImagePath"> 默认图片路径</param>
        /// <returns> 图片源 </returns>
        private BitmapImage CreateImageSource(string path, string resourcePrefix, string packPrefix, string defaultImagePath)
        {
            try
            {
                // 如果路径以资源前缀开头，则添加应用程序包前缀以创建完整的资源URI
                // 否则直接使用提供的路径
                string uriString = path.StartsWith(resourcePrefix)
                    ? $"{packPrefix}{path}"
                    : path;
                return new BitmapImage(new Uri(uriString, UriKind.RelativeOrAbsolute));
            }
            catch
            {
                return new BitmapImage(new Uri(defaultImagePath, UriKind.Absolute));
            }
        }

        /// <summary>
        /// 创建场景名称TextBlock控件
        /// </summary>
        /// <param name="sceneData"> 场景数据 </param>
        /// <param name="filter"> 过滤关键字 </param>
        /// <returns> TextBlock控件 </returns>
        private TextBlock CreateSceneNameTextBlock(SceneData sceneData, string filter = null)
        {
            TextBlock sceneName = new()
            {
                Style = (Style)FindResource("SceneNameTextBlock"),
            };
            TextBlockHelper.SetHighlight(sceneName, new HighlightTextData
            {
                Text = db3.GetSceneTitle(sceneData),
                Keyword = filter ?? ""
            });
            return sceneName;
        }

        /// <summary>
        /// 创建场景标签TextBlock控件
        /// </summary>
        /// <param name="sceneData"> 场景数据 </param>
        /// <param name="filter"> 过滤关键字 </param>
        /// <returns> TextBlock控件 </returns>
        private TextBlock CreateSceneTagTextBlock(SceneData sceneData, string filter = null)
        {
            TextBlock sceneTag = new()
            {
                Style = (Style)FindResource("SceneTagTextBlock"),
            };
            string tagText = sceneData.SceneTag; // 先拼接标签文本，如果不是默认场景，则加上.exe后缀（用于区分默认场景和应用场景）
            if (!new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(sceneData.SceneTag))
                tagText += ".exe"; // 普通应用场景标签加.exe后缀，便于用户识别
            // 必须在拼接好后再传递给高亮方法，否则高亮方法可能会覆盖Text属性，导致后缀丢失
            TextBlockHelper.SetHighlight(sceneTag, new HighlightTextData
            {
                Text = tagText,
                Keyword = filter ?? ""
            });
            return sceneTag;
        }

        /// <summary>
        /// 设置场景按钮事件
        /// </summary>
        /// <param name="button"> 按钮 </param>
        private void SetupButtonEvents(Button button)
        {
            button.MouseDoubleClick += EditSceneButton_Click; // 双击编辑场景信息
            button.DragEnter += ChangeSceneButtonBackground; // 设置拖拽事件
            button.DragLeave += ResetSceneButtonBackground; // 还原背景色
            button.MouseRightButtonDown += OpenContextMenu; // 右键菜单
            button.MouseEnter += HightLightBlacklistItem; // 鼠标移入高亮显示
            button.Click += ChanceSceneButton_Click; // 设置按钮点击事件
            button.MouseLeave += FadeBlacklistItem; // 鼠标移出恢复原状
            button.Drop += ChangeActionPageScene; // 设置拖拽事件
        }

        // 右键打开菜单
        private void OpenContextMenu(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button; // 转换为按钮
            if(new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(button.Name)) return; // 禁止编辑默认场景
            EditSceneMenu editSceneMenu = new(button.Name); // 创建菜单
            editSceneMenu.Show(); // 显示菜单
        }

        // 设置场景按钮背景色
        private void ChangeSceneButtonBackground(object sender, DragEventArgs e)
        {
            Button button = sender as Button; // 转换为按钮
            if (button.Name == type)
                button.AllowDrop = false; // 不允许拖拽
            else
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9E9E9")); // 设置按钮背景色

            var targetSceneData = db3.GetSceneData(button.Name); // 获取目标场景数据
            if (targetSceneData != null && targetSceneData.SceneCount == 10)
            {
                button.AllowDrop = false; // 不允许拖拽
            }
        }

        // 还原背景色
        private void ResetSceneButtonBackground(object sender, DragEventArgs e)
        {
            Button button = sender as Button; // 转换为按钮
            if (button.Name != type)
            {
                button.Background = Brushes.Transparent; // 还原背景色
                var targetSceneData = db3.GetSceneData(button.Name); // 添加场景数量检查，只有未达到上限时才允许拖拽
                if (targetSceneData != null && targetSceneData.SceneCount < 10)
                {
                    button.AllowDrop = true; // 允许拖拽
                }
            }
        }

        // 设置场景标题
        private void SetSceneTitle()
        {
            SceneTitleStackPanel.Children.Clear(); // 清空标题StackPanel
            var sceneInfo = db3.GetSceneData(type); // 获取场景信息
            SceneImage.Source = new BitmapImage(new Uri(sceneInfo.SceneIconPath, UriKind.RelativeOrAbsolute)); // 设置场景图片
            string sceneTitleText = db3.GetSceneTitle(sceneInfo); // 获取场景标题
            TextBlock sceneTitle = new()
            {
                Style = (Style)FindResource("SceneTitleTextBlock"), // 应用样式
                Text = sceneTitleText // 场景名称
            }; // 创建场景标题
            SceneTitleStackPanel.Children.Add(sceneTitle); // 添加到标题StackPanel
            TextBlock sceneDescription = new()
            {
                Style = (Style)FindResource("SceneDescriptionTextBlock"), // 应用样式
                Text = sceneInfo.SceneTag // 场景标签
            }; // 创建场景描述
            if (!new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(sceneInfo.SceneTag))
                sceneDescription.Text += ".exe"; // 标签可能为空
            SceneTitleStackPanel.Children.Add(sceneDescription); // 添加到标题StackPanel
        }

        /// <summary>
        /// 加载动作页
        /// </summary>
        /// <param name="style">动作页样式</param>
        private void LoadCanvas(string style)
        {
            MainListView.Items.Clear(); // 清空总列表视图
            UpdateUI(); // 更新UI布局
            if (!db2.TableExists(style)) return; // 如果不存在按钮数据表，则返回
            var actionPageData = db3.GetSceneData(style); // 获取动作页数据
            for (int i = 0; i < actionPageData.SceneCount; i++)
            {
                MainListView.Items.Add(GenerateCanvas(i, style)); // 生成动作页
            }
        }

        // 更新UI布局
        public void UpdateUI()
        {
            if (type == "_global") // 如果场景类型为全局
            {
                MainBorder.Height = 224; // 设置主边框高度
                ScrollBar.Margin = new Thickness(239, 250, 10, 0); // 设置滚动条边距
                AddActionPageButton.Margin = new Thickness(239, 264, 0, 0); // 设置添加动作页按钮边距
                AutoReturnToFirstPageCheckBox.Margin = new Thickness(535, 268, 0, 0); // 设置自动返回到第一页复选框边距
            }
            else
            {
                if (db2.TableExists(type)) // 如果存在通用样式按钮数据表
                {
                    MainBorder.Height = 289; // 设置主边框高度
                    ScrollBar.Margin = new Thickness(239, 315, 10, 0); // 设置滚动条边距
                    AddActionPageButton.Margin = new Thickness(239, 329, 0, 0); // 设置添加动作页按钮边距
                    AutoReturnToFirstPageCheckBox.Margin = new Thickness(535, 333, 0, 0); // 设置自动返回到第一页复选框边距
                }
                else
                {
                    MainBorder.Height = 224; // 设置主边框高度
                    ScrollBar.Margin = new Thickness(239, 250, 10, 0); // 设置滚动条边距
                    AddActionPageButton.Margin = new Thickness(239, 264, 0, 0); // 设置添加动作页按钮边距
                    AutoReturnToFirstPageCheckBox.Margin = new Thickness(535, 268, 0, 0); // 设置自动返回到第一页复选框边距
                }
            }
        }

        /// <summary>
        /// 生成动作页
        /// </summary>
        /// <param name="canvasIndex"> 动作页索引 </param>
        /// <param name="style"> 动作页类型 </param>
        private Canvas GenerateCanvas(int canvasIndex, string style)
        {
            Canvas dynamicCanvas = GenerateCanva(canvasIndex, style); // 生成画布
            Grid grid = GenerateTitle(canvasIndex, style); // 生成标题
            dynamicCanvas.Children.Add(grid); // 将网格添加到画布
            Button pageButton = GenerateChangePageButton(canvasIndex, style); // 生成标题按钮
            grid.Children.Add(pageButton);

            Button editPageButton = GenerateEditActionPageButton(canvasIndex, style); // 生成编辑动作页按钮
            grid.Children.Add(editPageButton);

            TextBlock actionPageName = GenerateActionPageName(canvasIndex); // 生成动作页名称
            grid.Children.Add(actionPageName);
    
            int rows = style == "_global" ? 3 : 4; // 行数
            int cols = 4; // 列数
            UniformGrid uniformGrid = new()
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3")),
                Margin = new Thickness(0, 20, 0, 0),
                Columns = cols,
                Rows = rows,
            };
            dynamicCanvas.Children.Add(uniformGrid);
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    string buttonName = $"{style}{canvasIndex}{row + 1}{col + 1}"; // 按钮名称
                    Button button = new Button // 创建按钮
                    {
                        Style = (Style)FindResource("ActionButton"), // 按钮样式
                        Name = buttonName // 按钮名称
                    }; // 创建按钮
                    BindButtonEvents(button); // 绑定按钮事件
                    uniformGrid.Children.Add(button); // 将按钮添加到画布

                    var data = db2.GetButtonDataByID(int.Parse(buttonName.Replace(style, "")), type); // 获取按钮数据
                    buttonManager.RefreshButtonDisplay(button, data, 60, false); // 刷新按钮显示

                    button.Background = isDarkModle
                        ? (button.Tag is ButtonData ? actionDarkModleButtonBrush : blankButtonBrush)
                        : (button.Tag is ButtonData ? actionButtonBrush : blankButtonBrush); // 设置按钮背景颜色
                }
            }
            return dynamicCanvas; // 返回画布
        }

        /// <summary>
        /// 生成画布
        /// </summary>
        /// <param name="canvasIndex"> 画布索引 </param>
        /// <param name="style"> 画布类型 </param>
        /// <returns> 画布 </returns>
        private Canvas GenerateCanva(int canvasIndex, string style)
        {
            string canvasName = $"{style}{canvasIndex}"; // 画布名称
            Canvas dynamicCanvas = new Canvas // 创建画布
            {
                Style = (Style)FindResource("ActionPageCanvas"),
                Height = style == "_global" ? 215 : 280, // 画布高度
                Name = canvasName, // 画布名称
            }; // 创建画布
            return dynamicCanvas; // 返回画布
        }

        /// <summary>
        /// 生成标题
        /// </summary>
        /// <param name="canvasIndex"> 画布索引 </param>
        /// <param name="style"> 画布类型 </param>
        /// <returns> 标题 </returns>
        private Grid GenerateTitle(int canvasIndex, string style)
        {
            Grid grid = new Grid { Style = (Style)FindResource("ActionPageTitleGrid") }; // 创建网格
            return grid; // 返回网格
        }

        /// <summary>
        /// 生成编辑标题按钮
        /// </summary>
        /// <param name="canvasIndex"></param>
        /// <param name="style"></param>
        /// <returns> 编辑标题按钮 </returns>
        private Button GenerateChangePageButton(int canvasIndex, string style)
        {
            Button pageButton = new Button
            {
                Style = (Style)FindResource("ActionChangePageButton"),
                Name = $"{style}{canvasIndex}", // 按钮名称
            }; // 创建按钮
            pageButton.PreviewMouseMove += ChangePageButton_PreviewMouseMove; // 鼠标移动事件
            pageButton.PreviewMouseLeftButtonUp += buttonManager.Button_PreviewMouseLeftButtonUp; // 鼠标左键抬起事件
            pageButton.PreviewMouseLeftButtonDown += buttonManager.Button_PreviewMouseLeftButtonDown; // 鼠标左键按下事件
            return pageButton; // 返回按钮
        }

        /// <summary>
        /// 生成编辑动作页按钮
        /// </summary>
        /// <param name="canvasIndex"> 画布索引 </param>
        /// <param name="style"> 画布类型 </param>
        /// <returns> 编辑动作页按钮 </returns>
        private Button GenerateEditActionPageButton(int canvasIndex, string style)
        {
            Button editPageButton = new Button
            {
                Style = (Style)FindResource("ActionPageEditButton"),
                Name = $"Edit{style}{canvasIndex}", // 按钮名称
            }; // 创建按钮
            editPageButton.Click += OpenEditPopup; // 点击事件
            editPageButton.MouseDoubleClick += EditActionPageInfoButton_Click; // 双击事件
            return editPageButton; // 返回按钮
        }

        /// <summary>
        /// 生成动作页名称
        /// </summary>
        /// <param name="actionPageIndex"> 动作页索引 </param>
        /// <returns> 动作页名称 </returns>
        private TextBlock GenerateActionPageName(int actionPageIndex)
        {
            var actionPageInfo = db3.GetActionPageData(type, actionPageIndex); // 获取动作页信息
            TextBlock textBlock = new TextBlock
            {
                Style = (Style)FindResource("ActionPageNameTextBlock"),
                Text = actionPageInfo.ActionPageName // 动作页名称
            }; // 创建文本块
            return textBlock; // 返回文本块
        }

        // 点击按钮编辑动作页
        private void OpenEditPopup(object sender, RoutedEventArgs e)
        {
            EditActionPagePopup.PlacementTarget = sender as Button; // 设置弹出菜单位置
            EditActionPagePopup.IsOpen = true; // 显示弹出菜单
        }

        // 滚动条值改变事件
        private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ScrollViewer.ScrollToHorizontalOffset(ScrollBar.Value); // 滚动到指定位置
        }

        // 鼠标移入按钮改变背景色
        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button button = sender as Button; // 获取按钮
            if (button.Tag is ButtonData data) // 如果按钮数据存在
            {
                button.Background = isDarkModle
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("LightGray"))
                    : actionButtonMouseOverBrush; // 设置按钮背景颜色

                if (showUsedTimes) // 如果显示使用次数
                    buttonManager.LoadActionUsedTimes(button, data); // 刷新按钮显示
            }
            else
                button.Background = blankButtonMouseOverBrush; // 设置按钮背景颜色
        }

        // 鼠标移出按钮还原背景色
        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Button button = sender as Button; // 获取按钮
            if (button.Tag is ButtonData data) // 如果按钮有数据
            {
                if(showUsedTimes) // 如果显示使用次数
                    buttonManager.RefreshButtonDisplay(button, data, 60, false); // 刷新按钮显示
                button.Background = isDarkModle
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("DarkGray"))
                    : actionButtonBrush; // 设置按钮背景颜色
            }
            else
                button.Background = blankButtonBrush; // 设置按钮背景颜色
        }

        /// <summary>
        /// 绑定按钮事件
        /// </summary>
        /// <param name="button"> 按钮 </param>
        private void BindButtonEvents(Button button)
        {
            button.PreviewMouseLeftButtonDown += Button_PreviewMouseLeftButtonDown; // 鼠标左键按下事件
            button.PreviewMouseLeftButtonUp += Button_PreviewMouseLeftButtonUp; // 鼠标左键抬起事件
            button.PreviewMouseMove += Button_PreviewMouseMove; // 鼠标移动事件
            button.PreviewMouseRightButtonDown += OpenMenu; // 右键点击事件
            button.MouseDoubleClick += ShowEditWindow; // 双击事件
            button.MouseLeave += Button_MouseLeave; // 鼠标离开事件
            button.MouseEnter += Button_MouseEnter; // 鼠标进入事件
            button.Click += ShowCreatActionMenu; // 点击事件
            button.Drop += Button_Drop; // 拖放事件
        }

        // 拖放事件
        private void Button_Drop(object sender, DragEventArgs e)
        {
            if (sender is Button TargetButton)
            {
                buttonManager.Button_Drop(sender, e, false, type); // 处理拖放事件
                NotifyActionPageChanged(type);
            }
        }

        // 鼠标左键按下事件
        private void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button)
                buttonManager.Button_PreviewMouseLeftButtonDown(sender, e); // 处理鼠标左键按下事件
        }

        // 鼠标移动事件
        private void Button_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Button button && e.LeftButton == MouseButtonState.Pressed) // 如果鼠标左键按下
                buttonManager.Button_PreviewMouseMove(sender, e, true, type); // 处理鼠标移动事件
        }

        // 鼠标左键抬起事件
        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            buttonManager.Button_PreviewMouseLeftButtonUp(sender, e); // 处理鼠标左键抬起事件
        }

        // 左键空白 Button 显示创建动作菜单
        private void ShowCreatActionMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag == null)
                buttonManager.OpenMenu(sender, "CreatActionMenu", this, type); // 打开创建动作菜单
        }

        // 显示编辑窗口
        private void ShowEditWindow(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // 阻止默认双击事件
            if (sender is Button button && button.Tag != null)
            {
                AddActionWindow addWindow = new AddActionWindow(int.Parse(button.Name.Replace(type, "")), type, 0); // 创建编辑窗口
                addWindow.Show(); // 显示编辑窗口
                addWindow.Activate(); // 激活编辑窗口
            }
        }

        // 添加动作页
        private void AddActionPage(object sender, RoutedEventArgs e)
        {
            int canvasCount = MainListView.Items.Count; // 获取画布索引
            if (canvasCount == 10) // 如果画布索引等于9
                ShowToast("当前场景动作页数量已达上限。", ToastType.Error); // 弹出消息提醒
            else if (canvasCount == 0)
            {
                db2.CreateButtonTable(type); // 创建按钮数据表
                db3.CreateActionPageTable(type); // 创建动作页数据表
                db3.UpdateSceneCount(type, 1); // 更新场景数据表
                db3.UpdateActionPageTable(type, $"{type}{0}", GetActionPageInfo().ActionPageName); // 更新动作页数据表
                MainListView.Items.Add(GenerateCanvas(canvasCount, type)); // 如果画布索引为0，则生成画布
                if (type != "_global")
                {
                    MainBorder.Height = 289; // 设置主边框高度
                    ScrollBar.Margin = new Thickness(239, 315, 10, 0); // 设置滚动条边距
                    AddActionPageButton.Margin = new Thickness(239, 337, 0, 0); // 设置添加场景按钮边距
                    AutoReturnToFirstPageCheckBox.Margin = new Thickness(535, 341, 0, 0); // 设置自动返回到第一页复选框边距
                }
            }
            else
            {
                db3.UpdateSceneCount(type, canvasCount + 1); // 更新场景数据表
                db3.UpdateActionPageTable(type, type + canvasCount.ToString(), GetActionPageInfo().ActionPageName); // 更新动作页数据表
                MainListView.Items.Add(GenerateCanvas(canvasCount, type)); // 生成画布
            }

            NotifySceneListChanged(type);
        }

        /// <summary>
        /// 设置动作页信息
        /// </summary>
        /// <returns> 动作页信息 </returns>
        private ActionPageInfo GetActionPageInfo()
        {
            int canvasCount = MainListView.Items.Count; // 获取画布索引
            var sceneData = db3.GetSceneData(type); // 获取场景信息
            var actionPageData = db3.GetActionPageData(type, canvasCount); // 获取动作页信息
            string actionPageName; // 动作页名称
            switch (type)
            {
                case "_global":
                    actionPageName = "默认全局动作页"; // 设置动作页名称
                    break;
                case "common":
                    actionPageName = "默认"; // 设置动作页名称
                    break;
                case "desktop":
                    actionPageName = $"桌面 #{MainListView.Items.Count}"; // 设置动作页名称
                    break;
                case "taskbar":
                    actionPageName = $"任务栏 #{MainListView.Items.Count}"; // 设置动作页名称
                    break;
                default:
                    actionPageName = $"{sceneData.SceneName} #{canvasCount}";
                    break;
            }
            ActionPageInfo actionPageInfo = new ActionPageInfo
            {
                ActionPageProcess = sceneData.SceneProcess,
                ActionPageName = actionPageName,
            }; // 创建动作页信息对象
            return actionPageInfo; // 返回动作页信息对象
        }

        // 滚动条滚动事件
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollBar.Maximum = ScrollViewer.ExtentWidth - ScrollViewer.ViewportWidth; // 设置滚动条最大值
            ScrollBar.ViewportSize = ScrollViewer.ViewportWidth; // 设置滚动条视口大小
            ScrollBar.Value = ScrollViewer.HorizontalOffset; // 设置滚动条值
        }

        // 点击按钮切换场景类型
        private void ChanceSceneButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button; // 获取按钮
            var sceneInfo = db3.GetSceneData(button.Name); // 获取场景信息
            TypeChanged(sceneInfo.SceneTag); // 切换类型为全局场景
        }

        // 打开创建动作菜单
        private void OpenMenu(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // 阻止默认右键菜单
            Button button = sender as Button; // 获取按钮
            buttonManager.OpenMenu(sender, button.Tag is ButtonData data ? "OperationMenu" : "CreatActionMenu", this, type); // 打开操作菜单
        }

        /// <summary>
        /// 编辑动作后刷新按钮显示
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        public void UpdateButton(int button)
        {
            int index = button / 100; // 获取按钮所在动作页索引
            var oldCanvas = MainListView.Items[index] as Canvas; // 获取旧的 Canvas
            UniformGrid targetGrid = oldCanvas.Children.Cast<UIElement>().Where(c => c is UniformGrid).First() as UniformGrid; // 获取目标网格
            foreach(var childs in targetGrid.Children)
            {
                if(childs is Button targetButton && targetButton.Name.Contains(button.ToString()))
                {
                    ButtonData data = db2.GetButtonDataByID(int.Parse(targetButton.Name.Replace(type, "")), type); // 获取按钮数据
                    buttonManager.RefreshButtonDisplay(targetButton, data, 60, false); // 刷新按钮显示
                }
            }

            NotifyActionPageChanged(type);
        }

        // 鼠标移动时检查是否满足拖拽条件
        public void ChangePageButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Button button && e.LeftButton == MouseButtonState.Pressed)
                buttonManager.Button_PreviewMouseMove(sender, e, false); // 处理鼠标移动事件
        }

        // 拖拽经过目标项
        private void ListView_PreviewDragOver(object sender, DragEventArgs e)
        {
            buttonManager.Button_PreviewDragOver(sender, e); // 处理拖放事件
        }

        // 拖拽完成
        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ButtonData"))
            {
                string sourceButtonName = e.Data.GetData("ButtonData")?.ToString(); // 获取传递的 Button Name
                if (!string.IsNullOrEmpty(sourceButtonName))
                {
                    Point point = e.GetPosition(MainListView); // 获取鼠标位置
                    var hitTestResult = System.Windows.Media.VisualTreeHelper.HitTest(MainListView, point); // 获取鼠标位置的项
                    DependencyObject targetItem = hitTestResult.VisualHit; // 获取鼠标位置的项
                    Canvas targetCanvas = VisualTreeHelper.FindParent<Canvas>(targetItem);// 查找目标 Canvas

                    if (targetCanvas == null) return; // 如果目标 Canvas 为 null，则返回
                    string targetCanvasName = targetCanvas.Name; // 获取目标 Canvas 的 Name
                    if (sourceButtonName == targetCanvasName) return; // 如果目标 Canvas 与源 Canvas 相同，则返回

                    int sourceIndex = int.Parse(sourceButtonName.Substring(sourceButtonName.Length - 1)); // 获取源 Button 索引
                    int targetIndex = int.Parse(targetCanvasName.Substring(targetCanvasName.Length - 1)); // 获取目标 Button 索引

                    db2.SwapButtonAValues(type, sourceIndex, targetIndex); // 更新数据库 Button A 值
                    db3.SwapActionPage(type, sourceIndex, targetIndex); // 更新数据库动作页数据
                    UpdateCanvasInListView(sourceIndex, type); // 更新 ListView 中的特定 Canvas
                    UpdateCanvasInListView(targetIndex, type); // 更新 ListView 中的特定 Canvas

                    NotifyActionPageChanged(type);
                }
            }
        }

        /// <summary>
        /// 更新 ListView 中的特定 Canvas
        /// </summary>
        /// <param name="canvasIndex"> 画布索引 </param>
        /// <param name="styleType"> 场景类型 </param>
        public void UpdateCanvasInListView(int canvasIndex, string styleType)
        {
            var oldCanvas = MainListView.Items[canvasIndex] as Canvas; // 获取旧的 Canvas
            MainListView.Items.Remove(oldCanvas); // 从主列表视图中移除旧的 Canvas
            Canvas newCanvas = GenerateCanvas(canvasIndex, styleType); // 生成新的 Canvas
            MainListView.Items.Insert(canvasIndex, newCanvas); // 在主列表视图中插入新的 Canvas
        }

        // 查找场景按钮
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower().Trim(); // 获取搜索文本
            GenerateSceneButtons(string.IsNullOrEmpty(searchText) ? null : searchText); // 生成场景按钮
        }

        // 双击标签切换动作按钮背景色
        private void ChangeActionButtonBakground(object sender, MouseButtonEventArgs e)
        {
            isDarkModle = !isDarkModle; // 切换模式
            foreach (var item in MainListView.Items)
            {
                Canvas canvas = item as Canvas; // 获取画布
                UniformGrid targetGrid = canvas.Children.Cast<UIElement>().Where(c => c is UniformGrid).First() as UniformGrid; // 获取目标网格
                foreach (var childs in targetGrid.Children)
                {
                    if (childs is Button button && button.Tag is ButtonData data)
                    {
                        button.Background = isDarkModle
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("DarkGray"))
                            : actionButtonBrush; // 设置按钮背景颜色
                    }
                }
            }
        }

        /// <summary>
        /// 获取绑定按钮
        /// </summary>
        /// <returns> 绑定按钮 </returns>
        private Button GetBingdingButton()
        {
            EditActionPagePopup.IsOpen = false; // 关闭编辑动作页弹出菜单
            return EditActionPagePopup.PlacementTarget as Button; // 获取弹出菜单所绑定按钮
        }

        // 点击按钮查看动作页信息
        private void CheckActionPageInfoButton_Click(object sender, RoutedEventArgs e)
        {
            Button bingdingButton = GetBingdingButton(); // 获取绑定按钮
            int actionPageIndex = int.Parse(bingdingButton.Name.Replace($"Edit{type}", "")); // 获取动作页 ID
            ActionPageData actionPageData = db3.GetActionPageData(type, actionPageIndex); // 获取动作页信息
            string actionPageSize = db3.GetActionPageSize(type, actionPageIndex); // 获取动作页大小
            using var convertion = new DataSizeHelper(); // 数据转换管理器
            string actionPageInfo = $"ID：{actionPageData.DefaultActionPageName}\n" +
                                    $"最后修改：{actionPageData.LastEditTime}\n" +
                                    $"大小：{actionPageSize}"; // 创建消息窗口
            MessageWindow messageWindow = new("动作页信息", actionPageInfo); // 创建消息窗口
            messageWindow.ShowDialog(); // 显示消息窗口
        }

        // 点击按钮复制动作页 ID
        private void CopyActionPageIDButton_Click(object sender, RoutedEventArgs e)
        {
            Button bingdingButton = GetBingdingButton(); // 获取绑定按钮
            Clipboard.SetText(bingdingButton.Name.Replace("Edit", "")); // 复制文本到剪贴板
            ShowToast($"动作页ID已复制到剪贴板：{bingdingButton.Name.Replace("Edit", "")}", ToastType.Common); // 弹出消息提醒
        }

        // 点击按钮编辑动作页信息
        private void EditActionPageInfoButton_Click(object sender, RoutedEventArgs e)
        {
            Button bingdingButton = GetBingdingButton(); // 获取绑定按钮
            if (bingdingButton == null) return; // 如果绑定按钮为 null，则返回
            switch (type)
            {
                case "_global":
                    ShowToast("默认全局动作页信息不可修改。", ToastType.Common); // 弹出消息提醒
                    break;
                case "common":
                    ShowToast("默认通用动作页信息不可修改。", ToastType.Common); // 弹出消息提醒
                    break;
                default:
                    string canvasIndex = bingdingButton.Name.Replace("Edit" + type, ""); // 获取画布索引
                    EditActionPageInfoWindow editActionPageInfoWindow = new(type, canvasIndex); // 创建编辑动作页信息窗口
                    editActionPageInfoWindow.ShowDialog(); // 显示编辑动作页信息窗口
                    break;
            }
        }

        // 点击按钮创建打开动作页动作
        private void CreatOpenActionPageActionButton_Click(object sender, RoutedEventArgs e)
        {
            Button bingdingButton = GetBingdingButton(); // 获取绑定按钮
            string actionPageIndex = bingdingButton.Name.Replace("Edit" + type, ""); // 获取场景名称
            InternalCommandManager.Instance.PublishCommand(new InternalCommand
            {
                CommandType = InternalCommandType.OpenActionPage,
                ActionPageType = type,
                ActionPageIndex = actionPageIndex
            });
            ShowToast("已创建动作，请在目标位置使用“粘贴动作”。", ToastType.Common); // 弹出消息提醒
        }

        // 点击按钮删除动作页
        private void DeleteActionPageButton_Click(object sender, RoutedEventArgs e)
        {
            EditActionPagePopup.IsOpen = false; // 关闭编辑动作页弹出菜单
            Button bingdingButton = GetBingdingButton(); // 获取绑定按钮
            // 移除画布
            Grid targetGrid = bingdingButton.Parent as Grid; // 获取目标画布
            MainListView.Items.Remove(targetGrid.Parent); // 从主列表视图中移除画布
            // 删除按钮数据页
            string targetButtonName = bingdingButton.Name.Replace("Edit", ""); // 获取目标按钮名称
            int canvadIndex = int.Parse(targetButtonName.Replace(type, "")); // 获取画布索引
            db2.DeletePageOfButtons(type, canvadIndex); // 删除按钮数据页
            db3.DeleteActionPage(type, canvadIndex); // 删除动作页数据表
            if(MainListView.Items.Count == 0)
            {
                db2.DeleteButtonTable(type); // 如果没有画布，则删除按钮数据表
                db3.DeleteActionPageTable(type); // 删除动作页数据表
            }
            LoadCanvas(type); // 刷新界面

            NotifyActionPageChanged(type);
            NotifySceneListChanged(type);
        }

        // 点击按钮添加场景
        private void AddSceneButton_Click(object sender, RoutedEventArgs e)
        {
            AddSceneWindow addSceneWindow = new(); // 创建添加场景窗口
            addSceneWindow.SceneAddCompleted += AddSceneWindow_SceneAddCompleted; // 绑定场景添加完成事件
            addSceneWindow.Closed += (s, args) =>
            {
                addSceneWindow.SceneAddCompleted -= AddSceneWindow_SceneAddCompleted; // 解绑场景添加完成事件
            };
            addSceneWindow.ShowDialog(); // 显示添加场景窗口
        }

        // 点击按钮编辑场景
        private void EditSceneButton_Click(object sender, RoutedEventArgs e)
        {
            if (new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(type)) // 如果是默认场景
                ShowToast("此项不可编辑。", ToastType.Common); // 弹出消息提醒
            else
            {
                EditSceneWindow editSceneWindow = new(type); // 创建编辑场景窗口
                editSceneWindow.ShowDialog(); // 显示编辑场景窗口
                GenerateSceneButtons(); // 刷新场景按钮
                SetButtonBackground(); // 设置按钮背景
            }
        }

        // 点击按钮删除场景
        private void DeleteSceneButton_Click(object sender, RoutedEventArgs e)
        {
            if (new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(type)) // 如果是默认场景
                ShowToast("此项不可删除。", ToastType.Common); // 弹出消息提醒
            else
            {
                db3.DeleteScene(type); // 删除场景数据表
                GenerateSceneButtons(); // 刷新场景按钮
                TypeChanged("_global"); // 切换类型为全局场景
            }
        }

        // 点击按钮前往顶层场景
        private void ToTopSceneButton_Click(object sender, RoutedEventArgs e)
        {
            TypeChanged("_global"); // 切换类型为全局场景
        }

        // 右键显示/隐藏动作使用次数
        private void AddActionPageButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            showUsedTimes = !showUsedTimes; // 切换显示/隐藏使用次数
        }

        // 滚动滚轮移动视图
        private void MainStackPanel_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true; // 标记事件为已处理，防止默认行为
            int delta = e.Delta < 0 ? 30 : -30; // 获取滚轮滚动方向
            ScrollBar.Value += delta; // 调整滚动条值
        }

        // 点击勾选框更改设置
        private void AutoReturnToFirstPageCheckBox_Click(object sender, RoutedEventArgs e)
        {
            db3.SetAutoReturnToFirstPage(type, AutoReturnToFirstPageCheckBox.IsChecked == true); // 更改设置
        }

        // 将动作页拖拽按钮拖放到其他场景按钮上给动作页切换场景
        private void ChangeActionPageScene(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ButtonData"))
            {
                Button targetButton = sender as Button; // 获取目标按钮
                var targetSceneData = db3.GetSceneData(targetButton.Name); // 获取目标场景数据
                string sourceButtonName = e.Data.GetData("ButtonData")?.ToString(); // 获取传递的 Button Name
                if (!string.IsNullOrEmpty(sourceButtonName))
                {
                    int sourceIndex = int.Parse(sourceButtonName.Replace(type, "")); // 获取源动作页索引
                    if (targetSceneData.SceneCount == 0)
                    {
                        db2.CreateButtonTable(targetSceneData.SceneName); // 创建按钮数据表
                        db3.CreateActionPageTable(targetSceneData.SceneName); // 创建动作页数据表
                    }
                    var buttons = db2.GetPagesOfButtons(type, sourceIndex); // 获取源动作页按钮数据
                    foreach (var button in buttons)
                    {
                        ButtonData newButtonData = new()
                        {
                            ButtonID = targetSceneData.SceneCount * 100 + button.ButtonID % 100,
                            Title = button.Title,
                            Location = button.Location,
                            ImagePath = button.ImagePath,
                            Data1 = button.Data1,
                            Data2 = button.Data2,
                            Data3 = button.Data3,
                            Description = button.Description,
                            CreateTime = button.CreateTime,
                            LatestEditTime = DateTime.Now,
                            ActionType = button.ActionType,
                            UsedTimes = button.UsedTimes
                        }; // 创建新按钮数据
                        db2.UpdateAction(newButtonData, targetSceneData.SceneName); // 更新按钮数据
                    }
                    var sourceActionPageData = db3.GetActionPageData(type, sourceIndex); // 获取源动作页数据
                    db3.UpdateActionPageTable(targetSceneData.SceneName, targetSceneData.SceneName + targetSceneData.SceneCount.ToString(), GetActionPageName(targetSceneData.SceneName, sourceActionPageData.ActionPageName)); // 更新动作页数据表
                    db3.UpdateSceneCount(targetSceneData.SceneName, targetSceneData.SceneCount + 1); // 更新场景数据表
                    db3.DeleteActionPage(type, sourceIndex); // 删除源动作页
                    db2.DeletePageOfButtons(type, sourceIndex); // 删除源动作页按钮数据
                    MainListView.Items.RemoveAt(sourceIndex); // 从主列表视图中移除画布

                    if (MainListView.Items.Count == 0)
                    {
                        db2.DeleteButtonTable(type); // 如果没有画布，则删除按钮数据表
                        db3.DeleteActionPageTable(type); // 删除动作页数据表
                        if (!new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(type)) // 如果不是默认场景
                            db3.DeleteScene(type); // 删除场景数据表
                    }

                    NotifyActionPageChanged(type);
                    NotifyActionPageChanged(targetSceneData.SceneName);
                    NotifySceneListChanged(type);
                    NotifySceneListChanged(targetSceneData.SceneName);

                    TypeChanged(targetSceneData.SceneName); // 刷新界面
                }
            }
        }

        /// <summary>
        /// 获取动作页名称
        /// </summary>
        /// <param name="sceneType"> 场景类型 </param>
        /// <param name="actionPageName"> 动作页名称 </param>
        /// <returns> 动作页名称 </returns>
        private string GetActionPageName(string sceneType, string actionPageName)
        {
            switch (sceneType)
            {
                case "_global":
                    return "默认全局动作页";
                case "common":
                    return "默认";
                default:
                    return actionPageName;
            }
        }

        // 点击按钮清空搜索
        private void ClearSearchTextBoxButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = ""; // 清空搜索框文本
        }

        private void ShowToast(string message, ToastType toastType)
        {
            using var toast = new ToastManager(); // 消息提醒管理器
            toast.Show(message, toastType); // 弹出消息提醒
        }

        private void NotifyActionPageChanged(string table)
        {
            _isSelfTriggered = true;
            try { AppStateManager.NotifyActionPageChanged(table); }
            finally { _isSelfTriggered = false; }
        }

        private void NotifySceneListChanged(string table)
        {
            _isSelfTriggered = true;
            try { AppStateManager.NotifySceneListChanged(table); }
            finally { _isSelfTriggered = false; }
        }

        // 关闭窗口时释放资源
        protected override void OnClosed(EventArgs e)
        {
            UnsubscribeStateEvents();
            // 释放Appearance颜色资源
            actionButtonBrush = null;
            actionButtonMouseOverBrush = null;
            blankButtonBrush = null;
            blankButtonMouseOverBrush = null;
            type = null; // 清理场景类型
            isDarkModle = false; // 清理是否为暗黑模式
            showUsedTimes = false; // 清理是否显示使用次数
            CleanupMainListView(); // 清理主列表视图
            CleanupActionPagesButtonPanel(); // 清理场景按钮面板
            buttonManager?.Dispose(); // 清理按钮管理器
            SceneImage.Source = null; // 清理场景图片

            base.OnClosed(e); // 调用基类的 OnClosed 方法

            Task.Run(() =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            }); // 在后台线程异步执行垃圾回收
        }

        // 清理主列表视图
        private void CleanupMainListView()
        {
            while (MainListView.Items.Count > 0)
            {
                if (MainListView.Items[0] is Canvas canvas)
                {
                    CleanupCanvas(canvas); // 清理画布
                    MainListView.Items.Remove(canvas); // 移除画布
                }
            }
        }

        // 清理画布
        private void CleanupCanvas(Canvas canvas)
        {
            while (canvas.Children.Count > 0)
            {
                UIElement child = canvas.Children[0];
                if (child is Grid grid)
                {
                    CleanupGrid(grid); // 清理网格
                }
                else if (child is Button button)
                {
                    CleanupButton(button); // 清理按钮
                }
                canvas.Children.Remove(child); // 移除画布子元素
            }
        }

        /// <summary>
        /// 清理网格
        /// </summary>
        /// <param name="grid"> 网格 </param>
        private void CleanupGrid(Grid grid)
        {
            while (grid.Children.Count > 0)
            {
                UIElement gridChild = grid.Children[0];
                if (gridChild is Button button)
                {
                    CleanupButton(button); // 清理按钮
                }
                grid.Children.Remove(gridChild); // 移除网格子元素
            }
        }

        /// <summary>
        /// 清理按钮
        /// </summary>
        /// <param name="button"> 按钮 </param>
        private void CleanupButton(Button button)
        {
            // 解绑所有事件
            button.PreviewMouseLeftButtonDown -= Button_PreviewMouseLeftButtonDown; // 解绑按钮鼠标左键按下事件
            button.PreviewMouseLeftButtonUp -= Button_PreviewMouseLeftButtonUp; // 解绑按钮鼠标左键释放事件
            button.PreviewMouseMove -= Button_PreviewMouseMove; // 解绑按钮鼠标移动事件
            button.PreviewMouseRightButtonDown -= OpenMenu; // 解绑按钮鼠标右键点击事件
            button.MouseDoubleClick -= ShowEditWindow; // 解绑按钮鼠标双击事件
            button.MouseEnter -= Button_MouseEnter; // 解绑按钮鼠标进入事件
            button.MouseLeave -= Button_MouseLeave; // 解绑按钮鼠标离开事件
            button.Click -= ShowCreatActionMenu; // 解绑按钮点击事件
            button.Drop -= Button_Drop; // 解绑按钮拖拽事件

            // 清理按钮资源
            button.Content = null; // 清理按钮内容
            button.Tag = null; // 清理按钮标签
            button.Background = null; // 清理按钮背景
            button.Style = null; // 清理按钮样式
        }

        // 清理场景按钮面板
        private void CleanupActionPagesButtonPanel()
        {
            while (ActionPagesButtonPanel.Children.Count > 0)
            {
                UIElement child = ActionPagesButtonPanel.Children[0];
                if (child is Button button)
                {
                    // 解绑所有事件
                    button.MouseDoubleClick -= EditSceneButton_Click;
                    button.DragEnter -= ChangeSceneButtonBackground;
                    button.DragLeave -= ResetSceneButtonBackground;
                    button.MouseRightButtonDown -= OpenContextMenu;
                    button.MouseEnter -= HightLightBlacklistItem;
                    button.MouseLeave -= FadeBlacklistItem;
                    button.Click -= ChanceSceneButton_Click;
                    button.Drop -= ChangeActionPageScene;

                    // 清理按钮资源
                    button.Content = null; // 清理按钮内容
                    button.Tag = null; // 清理按钮标签
                    button.Background = null; // 清理按钮背景
                    button.Style = null; // 清理按钮样式
                }
                ActionPagesButtonPanel.Children.Remove(child); // 移除场景按钮面板子元素
            }
        }

        /// <summary>
        /// 添加场景窗口完成后回调方法。
        /// 处理场景添加完成后的刷新和切换逻辑。
        /// </summary>
        /// <param name="isSaved">是否保存成功</param>
        /// <param name="newSceneTag">新添加场景的标签</param>
        private void AddSceneWindow_SceneAddCompleted(bool isSaved, string newSceneTag)
        {
            if (isSaved)
            {
                GenerateSceneButtons(); // 刷新场景按钮
                TypeChanged(string.IsNullOrEmpty(newSceneTag) ? newSceneTag : type); // 切换场景
            }
        }
    }

    // 动作页信息类
    public class ActionPageInfo
    {
        public string ActionPageProcess { get; set; } // 动作页所属应用程序名称
        public string ActionPageName { get; set; } // 动作页名称
    }
}