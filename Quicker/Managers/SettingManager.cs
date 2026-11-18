using Quicker.Models.Settings;
using System.Windows.Controls;
using System.Threading.Tasks;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows;

namespace Quicker.Managers
{
    public class SettingManager
    {
        private const string DefaultButtonColor1 = "#FFE0E0E0"; // 默认按钮颜色
        private const string SelectedButtonColor1 = "#FFF4F4F4"; // 选中按钮颜色
        private const string DefaultButtonColor2 = "#FFF0F0F0"; // 默认按钮颜色
        private const string SelectedButtonColor2 = "#FFFAFAFA"; // 选中按钮颜色

        public OpenMainWindow openMainWindowConditions; // 弹出面板设置缓存对象
        public Appearance appearanceConditions; // 外观设置缓存对象
        public Convention conventions; // 常规设置缓存对象

        // 原始设置缓存
        private Convention _originalConventions;
        private OpenMainWindow _originalOpenMainWindowConditions;
        private Appearance _originalAppearanceConditions;

        /// <summary>
        /// 缓存窗口加载时的原始设置
        /// </summary>
        public async Task CacheOriginalSettingsAsync()
        {
            // 确保设置已加载
            await LoadConventionsAsync(); // 加载常规设置
            await LoadOpenMainWindowConditionsAsync(); // 加载弹出面板设置
            await LoadAppearanceAsync(); // 加载外观设置

            // 复制当前设置作为原始设置
            _originalConventions = CloneSettingsCache(conventions); // 复制常规设置
            _originalOpenMainWindowConditions = CloneSettingsCache(openMainWindowConditions); // 复制弹出面板设置
            _originalAppearanceConditions = CloneSettingsCache(appearanceConditions); // 复制外观设置
        }

        /// <summary>
        /// 恢复到原始设置
        /// </summary>
        public void RestoreOriginalSettings()
        {
            // 恢复原始设置
            if (_originalConventions != null)
                conventions = CloneSettingsCache(_originalConventions);
            
            if (_originalOpenMainWindowConditions != null)
                openMainWindowConditions = CloneSettingsCache(_originalOpenMainWindowConditions);
        }

        /// <summary>
        /// 恢复原始外观设置
        /// </summary>
        public void RestoreOriginalAppearanceSettings()
        {
            if (_originalAppearanceConditions != null)
                appearanceConditions = CloneSettingsCache(_originalAppearanceConditions);
        }

        /// <summary>
        /// 克隆设置缓存对象
        /// </summary>
        /// <typeparam name="T">设置缓存类型</typeparam>
        /// <param name="source">源设置对象</param>
        private T CloneSettingsCache<T>(T source) where T : class
        {
            if (source == null) return null; // 如果源设置对象为空，返回空
            var clone = Activator.CreateInstance<T>(); // 创建新的设置对象
            var properties = typeof(T).GetProperties(); // 获取设置对象的属性
            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite)
                {
                    var value = property.GetValue(source); // 获取源设置对象的属性值
                    property.SetValue(clone, value); // 设置新设置对象的属性值
                }
            }
            return clone; // 返回克隆的设置对象
        }

        // 异步加载常规设置信息
        public async Task LoadConventionsAsync()
        {
            if (conventions != null) return; // 如果已经初始化了数据，直接返回
            var Conventions = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取设置信息
            conventions = new Convention // 常规设置
            {
                AutoStart = Conventions.AutoStart, // 开机自启
                ShowNotification = Conventions.ShowNotification, // 显示通知
                ShowAddImage = Conventions.ShowAddImage, // 显示添加图片按钮
                HideTooltip = Conventions.HideTooltip, // 隐藏工具提示
                LongPressThreshold = Conventions.LongPressThreshold, // 长按阈值
                MouseMovePixels = Conventions.MouseMovePixels, // 鼠标移动像素
                LoopPageFlipping = Conventions.LoopPageFlipping, // 循环翻页
                RememberLastPage = Conventions.RememberLastPage, // 是否记住设置窗口中最后打开的页面
                LastPage = Conventions.LastPage, // 最后打开的页面
                EnableMemoryOptimization = Conventions.EnableMemoryOptimization, // 是否启用内存优化
                TrayIconPathRunning = Conventions.TrayIconPathRunning, // 运行时托盘图标路径
                TrayIconPathPaused = Conventions.TrayIconPathPaused, // 暂停时托盘图标路径
                UseMenuAnimation = Conventions.UseMenuAnimation // 是否启用菜单动画
            }; // 加载设置数据到缓存
        }

        // 异步加载弹出面板设置信息
        public async Task LoadOpenMainWindowConditionsAsync()
        {
            if (openMainWindowConditions != null) return; // 如果已经初始化了数据，直接返回
            var OpenMainWindowConditions = SettingDatabase.GetAllOpenMainWindowConditions().FirstOrDefault(); // 获取弹出面板设置信息
            openMainWindowConditions = new OpenMainWindow // 弹出面板设置
            {
                OpenMainWindowByMiddleMouseClick = OpenMainWindowConditions.OpenMainWindowByMiddleMouseClick,
                OpenMainWindowByX1MouseClick = OpenMainWindowConditions.OpenMainWindowByX1MouseClick,
                OpenMainWindowByX2MouseClick = OpenMainWindowConditions.OpenMainWindowByX2MouseClick,
                OpenMainWindowByCtrl_MiddleMouseClick = OpenMainWindowConditions.OpenMainWindowByCtrl_MiddleMouseClick,
                OpenMainWindowByCtrl_RightMouseClick = OpenMainWindowConditions.OpenMainWindowByCtrl_RightMouseClick,
                OpenMainWindowByMiddleMouseClickLonger = OpenMainWindowConditions.OpenMainWindowByMiddleMouseClickLonger,
                OpenMainWindowByRightMouseClickLonger = OpenMainWindowConditions.OpenMainWindowByRightMouseClickLonger,
                OpenMainWindowByRightMouseClick_Move = OpenMainWindowConditions.OpenMainWindowByRightMouseClick_Move,
                OpenMainWindowByCtrl = OpenMainWindowConditions.OpenMainWindowByCtrl,
                WindowStartupLocation = OpenMainWindowConditions.WindowStartupLocation
            }; // 加载设置数据到缓存
        }

        // 异步加载外观设置信息
        public async Task LoadAppearanceAsync()
        {
            if (appearanceConditions != null) return; // 如果已经初始化了数据，直接返回
            var Appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault(); // 获取外观设置信息
            appearanceConditions = new Appearance // 外观设置
            {
                ThemeName = Appearance.ThemeName, // 主题名称

                // 按钮
                ButtonSize = Appearance.ButtonSize, // 按钮大小
                ButtonGap = Appearance.ButtonGap, // 按钮间距
                BorderWidth = Appearance.BorderWidth, // 边框宽度
                ButtonCornerRadius = Appearance.ButtonCornerRadius, // 按钮圆角半径

                // 颜色
                BackgroundColor = Appearance.BackgroundColor, // 背景颜色
                BorderColor = Appearance.BorderColor, // 边框颜色
                ToolbarColor = Appearance.ToolbarColor, // 工具栏颜色
                ToolbarIconColor = Appearance.ToolbarIconColor, // 工具栏图标颜色
                ActionButtonColor = Appearance.ActionButtonColor, // 动作按钮颜色
                ActionButtonMouseOverColor = Appearance.ActionButtonMouseOverColor, // 动作按钮鼠标悬停颜色
                BlankButtonColor = Appearance.BlankButtonColor, // 空白按钮颜色
                BlankButtonMouseOverColor = Appearance.BlankButtonMouseOverColor, // 空白按钮鼠标悬停颜色
                ButtonTextColor = Appearance.ButtonTextColor, // 按钮文字颜色

                // 字体
                Font1 = Appearance.Font1, // 字体1
                Font2 = Appearance.Font2, // 字体2
                FontSize = Appearance.FontSize, // 字体大小
                FontWeight = Appearance.FontWeight, // 字体粗细

                // 背景图片
                BackgroundImagePath = Appearance.BackgroundImagePath, // 背景图片路径
                BackgroundImageOpacity = Appearance.BackgroundImageOpacity, // 背景图片不透明度

                // 模糊与圆角
                Blur = Appearance.Blur, // 模糊模式
                Win11CornerRadius = Appearance.Win11CornerRadius, // Win11圆角模式

                // 选项
                AutoHideTitleBar = Appearance.AutoHideTitleBar, // 自动隐藏标题栏
                ShowActionButtonMouseOver = Appearance.ShowActionButtonMouseOver, // 鼠标悬停显示动作按钮
                HideActionNameAfterIcon = Appearance.HideActionNameAfterIcon, // 隐藏动作名称
                ShowActionIconShadow = Appearance.ShowActionIconShadow, // 显示动作图标阴影

                EnablePreview = Appearance.EnablePreview // 显示设置应用效果
            }; // 加载设置数据到缓存
        }

        // 设置Grid可见性
        public void SetGridVisible(Grid childrengrid, Grid fathergrid)
        {
            foreach (var grid in fathergrid.Children.OfType<Grid>())
                grid.Visibility = grid == childrengrid ? Visibility.Visible : Visibility.Collapsed; // 设置Grid可见性
        }

        /// <summary>
        /// 改变Button类型1样式
        /// </summary>
        /// <param name="targetStackPanel"> 目标StackPanel </param>
        /// <param name="targetButton"> 目标Button </param>
        /// <param name="fatherStackPanel"> 父级StackPanel </param>
        public void ButtonStyle1_Click(StackPanel targetStackPanel, Button targetButton, StackPanel fatherStackPanel, Grid fathergrid)
        {
            if (targetStackPanel.Visibility == Visibility.Visible) return; // 如果目标面板已经打开，则不执行任何操作
            foreach (var stackpanel in fathergrid.Children.OfType<StackPanel>())
                stackpanel.Visibility = stackpanel == targetStackPanel ? Visibility.Visible : Visibility.Collapsed; // 设置StackPanel可见性

            foreach (var button in fatherStackPanel.Children.OfType<Button>())
                button.Background = button == targetButton
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedButtonColor1))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString(DefaultButtonColor1)); // 设置Button类型1颜色
        }

        /// <summary>
        /// 设置Button类型1背景色
        /// </summary>
        /// <param name="sender"> 目标Button </param>
        /// <param name="stackPanel"> 目标StackPanel </param>
        public void ButtonStyle1_MouseLeave(object sender, StackPanel stackPanel)
        {
            Button button = sender as Button; // 获取Button
            button.Background = stackPanel.Visibility == Visibility.Visible ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedButtonColor1)) :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(DefaultButtonColor1)); // 设置Button颜色
        }

        /// <summary>
        /// 改变Button类型2样式
        /// </summary>
        public async Task ButtonStyle2_Click(Button targetButton, UserControl targetGrid, Grid fatherGrid, Grid stackPanelGrid)
        {
            var existingGrid = fatherGrid.Children.OfType<UserControl>().FirstOrDefault(); // 获取第一个 UserControl 子元素
            if (existingGrid == null || existingGrid.Name != targetGrid.Name)
            {
                if (existingGrid != null) fatherGrid.Children.Remove(existingGrid); // 移除旧的 UserControl 子元素
                targetGrid.SetValue(Grid.ColumnSpanProperty, 2); // 设置目标Grid列跨度为2
                fatherGrid.Children.Add(targetGrid); // 添加目标Grid子元素
            }
            else return; // 如果目标面板已经打开，则不执行任何操作

            foreach(var childStackpanel in stackPanelGrid.Children.OfType<StackPanel>())
            {
                foreach (var button in childStackpanel.Children.OfType<Button>())
                {
                    button.Background = button == targetButton
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedButtonColor2))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString(DefaultButtonColor2)); // 设置Button类型2颜色
                    button.FontWeight = button == targetButton ? FontWeights.Bold : FontWeights.Normal; // 设置Button类型2粗体
                } // 设置Button类型2颜色&&粗体
            }
        }

        /// <summary>
        /// 设置Button类型2背景色
        /// </summary>
        public void ButtonStyle2_MouseLeave(object sender, UserControl targetGrid, Grid fatherGrid)
        {
            Button button = sender as Button; // 获取Button
            var existingGrid = fatherGrid.Children.OfType<UserControl>().FirstOrDefault(); // 获取第一个 UserControl 子元素
            if (existingGrid.Name == targetGrid.Name)
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedButtonColor2)); // 设置Button颜色
        }

        /// <summary>
        /// 设置Button类型3边框
        /// </summary>
        public void ButtonStyle3_Click(Button clickedButton, Grid buttonPanelGrid)
        {
            foreach (var button in buttonPanelGrid.Children.OfType<Button>()) // 遍历Button容器中所有Button，设置边框
                button.BorderThickness = button == clickedButton ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }

        /// <summary>
        /// 判断当前设置是否与原始设置相同
        /// </summary>
        public bool IsSettingsChanged()
        {
            // 判断常规设置是否变化
            if (_originalConventions != null && conventions != null && !AreSettingsEqual(_originalConventions, conventions))
                return true;

            // 判断弹出面板设置是否变化
            if (_originalOpenMainWindowConditions != null && openMainWindowConditions != null && 
                !AreSettingsEqual(_originalOpenMainWindowConditions, openMainWindowConditions))
                return true;

            return false;
        }

        /// <summary>
        /// 判断主要外观设置（除预览设置外）是否与原始外观设置相同
        /// </summary>
        public bool IsMainAppearanceSettingsChanged()
        {
            if (_originalAppearanceConditions == null || appearanceConditions == null)
                return false;

            // 获取所有属性
            var properties = typeof(Appearance).GetProperties();
            foreach (var property in properties)
            {
                // 跳过预览相关属性
                if (property.Name == "EnablePreview")
                    continue;

                if (property.CanRead)
                {
                    var value1 = property.GetValue(_originalAppearanceConditions);
                    var value2 = property.GetValue(appearanceConditions);

                    if (value1 == null && value2 == null) continue;
                    if (value1 == null || value2 == null) return true;

                    if (!value1.Equals(value2))
                        return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 比较两个设置对象是否相等
        /// </summary>
        /// <typeparam name="T">设置对象类型</typeparam>
        /// <param name="obj1">第一个设置对象</param>
        /// <param name="obj2">第二个设置对象</param>
        private bool AreSettingsEqual<T>(T obj1, T obj2) where T : class
        {
            if (obj1 == null && obj2 == null) return true; // 如果两个对象都为null，认为相等
            if (obj1 == null || obj2 == null) return false; // 只有一个为null，认为不相等

            var properties = typeof(T).GetProperties(); // 获取所有属性
            foreach (var property in properties)
            {
                if (property.CanRead)
                {
                    var value1 = property.GetValue(obj1); // 获取第一个对象的属性值
                    var value2 = property.GetValue(obj2); // 获取第二个对象的属性值

                    if (value1 == null && value2 == null) continue; // 都为null，继续比较下一个属性
                    if (value1 == null || value2 == null) return false; // 只有一个为null，不相等

                    if (!value1.Equals(value2))
                        return false; // 属性值不相等，返回false
                }
            }
            return true; // 所有属性都相等，返回true
        }
    }
}