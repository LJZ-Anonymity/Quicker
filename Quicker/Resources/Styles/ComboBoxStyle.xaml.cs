using System.Windows.Controls.Primitives;
using System.Collections.Concurrent;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;
using System.Reflection;
using System.Windows;
using System.IO;

namespace Quicker.Resources.Styles
{
    public partial class ComboBoxStyle
    {
        // 用于跟踪最后一次更新的时间戳，使用弱引用避免内存泄漏
        private static readonly ConcurrentDictionary<WeakReference<ComboBox>, long> _lastUpdateTicks = new();
        private static readonly Timer _cleanupTimer; // 清理过期引用的定时器
        
        static ComboBoxStyle()
        {
            // 每5分钟清理一次过期的引用
            _cleanupTimer = new Timer(CleanupExpiredReferences, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
        
        /// <summary>
        /// 清理过期的引用
        /// </summary>
        /// <param name="state">状态</param>
        private static void CleanupExpiredReferences(object state)
        {
            var expiredKeys = _lastUpdateTicks.Keys
                .Where(wr => !wr.TryGetTarget(out _))
                .ToList(); // 获取所有过期的引用
                
            foreach (var key in expiredKeys)
            {
                _lastUpdateTicks.TryRemove(key, out _); // 移除过期的引用
            }
        }
        
        // ComboBox选择项改变事件处理程序
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                ForceRefreshComboBoxDisplay(comboBox); // 强制刷新显示内容
            }
        }
        
        // ComboBox加载事件处理程序，确保初始化时正确处理显示内容
        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                ForceRefreshComboBoxDisplay(comboBox); // 强制刷新显示内容
            }
        }
        
        /// <summary>
        /// 强制刷新ComboBox显示内容
        /// </summary>
        /// <param name="comboBox">要刷新的ComboBox</param>
        private void ForceRefreshComboBoxDisplay(ComboBox comboBox)
        {
            if (comboBox == null) return; // 如果ComboBox为null，返回
            long currentTicks = DateTime.Now.Ticks; // 获取当前时间戳
            
            // 更新最后一次更新的时间戳
            var weakRef = new WeakReference<ComboBox>(comboBox); // 创建弱引用
            _lastUpdateTicks[weakRef] = currentTicks; // 更新最后一次更新的时间戳
            
            // 使用Dispatcher延迟执行，确保UI已经更新
            comboBox.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                // 检查是否是最新的更新请求
                if (!_lastUpdateTicks.TryGetValue(weakRef, out long lastTicks) || lastTicks != currentTicks)
                {
                    return; // 不是最新的，放弃更新
                }

                try
                {
                    // 获取当前选中项
                    object selectedItem = comboBox.SelectedItem;
                    if (selectedItem == null) return; // 如果选中项为null，放弃更新

                    // 强制更新布局
                    comboBox.InvalidateVisual();
                    comboBox.UpdateLayout(); // 强制更新布局

                    // 获取ToggleButton
                    if (comboBox.Template.FindName("ToggleButton", comboBox) is ToggleButton toggleButton)
                    {
                        // 强制更新ToggleButton
                        toggleButton.InvalidateVisual();
                        toggleButton.UpdateLayout(); // 强制更新ToggleButton
                    }
                }
                catch
                {
                    // 忽略任何异常
                }
            }));
        }
    }

    // 内容克隆转换器，用于解决ComboBox中视觉树冲突问题
    public class ContentCloneConverter : IValueConverter, IMultiValueConverter
    {
        private static readonly Lazy<ContentCloneConverter> _instance = new(() => new ContentCloneConverter()); // 延迟初始化
        public static ContentCloneConverter Instance => _instance.Value; // 获取实例
        
        // 使用弱引用缓存，并添加清理机制
        private static readonly ConcurrentDictionary<int, WeakReference> _cloneCache = new(); // 缓存克隆结果
        private static readonly Timer _cleanupTimer; // 清理过期缓存的定时器
        
        static ContentCloneConverter()
        {
            // 每10分钟清理一次过期的缓存
            _cleanupTimer = new Timer(CleanupExpiredCache, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }
        
        /// <summary>
        /// 清理过期的缓存
        /// </summary>
        /// <param name="state">状态</param>
        private static void CleanupExpiredCache(object state)
        {
            var expiredKeys = _cloneCache.Keys
                .Where(key => !_cloneCache[key].IsAlive)
                .ToList(); // 获取所有过期的缓存键
                
            foreach (var key in expiredKeys)
            {
                _cloneCache.TryRemove(key, out _); // 移除过期的缓存
            }
        }

        /// <summary>
        /// 将值转换为克隆后的元素
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">文化信息</param>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ConvertCore(value, null); // 单值绑定时只传入值
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            object selectedItem = values != null && values.Length > 0 ? values[0] : null;
            ComboBox? comboBox = values != null && values.Length > 1 ? values[1] as ComboBox : null;
            return ConvertCore(selectedItem, comboBox);
        }

        /// <summary>
        /// 计算缓存键
        /// </summary>
        /// <param name="value">要计算的值</param>
        /// <returns>缓存键</returns>
        private int CalculateCacheKey(object value)
        {
            if (value == null) return 0; // 如果值为null，返回0
            
            // 组合类型和HashCode
            int typeHash = value.GetType().GetHashCode();
            int valueHash = value.GetHashCode();
            
            // 如果是ComboBoxItem，考虑其内容
            if (value is ComboBoxItem comboBoxItem && comboBoxItem.Content != null)
            {
                valueHash = comboBoxItem.Content.GetHashCode(); // 获取内容哈希值
            }
            
            return typeHash ^ valueHash; // 返回类型和值的哈希值
        }
        
        /// <summary>
        /// 创建标准化的TextBlock
        /// </summary>
        /// <param name="text">要显示的文本</param>
        /// <returns>TextBlock</returns>
        private TextBlock CreateTextBlock(string text)
        {
            return new TextBlock
            {
                Text = text,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// 将克隆的元素转换回原始对象
        /// </summary>
        /// <param name="value">克隆后的元素</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">文化信息</param>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 不实现ConvertBack
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 不实现多值ConvertBack
        }

        /// <summary>
        /// 统一处理逻辑
        /// </summary>
        private object ConvertCore(object value, ComboBox? comboBox)
        {
            if (value == null)
                return null;

            if (value is string || value.GetType().IsPrimitive)
                return value;

            try
            {
                // 优先读取附加的显示文本
                if (value is DependencyObject depObj)
                {
                    string displayText = ComboBoxHelper.GetDisplayText(depObj);
                    if (!string.IsNullOrEmpty(displayText))
                    {
                        return CreateTextBlock(displayText);
                    }
                }

                // 支持 ItemTemplate / ItemTemplateSelector
                if (comboBox != null)
                {
                    var templatedContent = CreateTemplateContent(comboBox, value);
                    if (templatedContent != null)
                    {
                        return templatedContent;
                    }
                }

                // 原有克隆逻辑
                int cacheKey = CalculateCacheKey(value);
                if (_cloneCache.TryGetValue(cacheKey, out WeakReference weakRef) &&
                    weakRef.IsAlive &&
                    weakRef.Target is FrameworkElement cachedElement)
                {
                    return cachedElement;
                }

                if (value is FrameworkElement element)
                {
                    var cloned = CloneElement(element);
                    if (cloned is FrameworkElement clonedElement)
                    {
                        _cloneCache[cacheKey] = new WeakReference(clonedElement);
                    }
                    return cloned;
                }

                // 对普通对象尝试使用 DisplayMemberPath
                if (comboBox != null)
                {
                    string? displayText = GetDisplayMemberValue(comboBox.DisplayMemberPath, value);
                    if (!string.IsNullOrEmpty(displayText))
                    {
                        return CreateTextBlock(displayText);
                    }
                }

                return CreateTextBlock(value.ToString());
            }
            catch (Exception ex)
            {
                return CreateTextBlock($"[显示错误: {ex.Message}]");
            }
        }

        /// <summary>
        /// 根据 ComboBox 的模板生成内容
        /// </summary>
        /// <param name="comboBox">要创建模板的ComboBox</param>
        /// <param name="dataContext">数据上下文</param>
        /// <returns>创建的模板内容</returns>
        private object? CreateTemplateContent(ComboBox comboBox, object dataContext)
        {
            DataTemplate? template = null;
            if (comboBox.ItemTemplateSelector != null) // 如果ItemTemplateSelector不为null，选择模板
            {
                template = comboBox.ItemTemplateSelector.SelectTemplate(dataContext, comboBox); // 选择模板
            }

            template ??= comboBox.ItemTemplate; // 如果模板为null，使用默认模板
            if (template == null) // 如果模板为null，返回null
            {
                return null; // 返回null
            }

            try
            {
                if (template.LoadContent() is FrameworkElement element) // 如果模板加载内容为UI元素，设置数据上下文并重置元素状态
                {
                    element.DataContext = dataContext; // 设置数据上下文
                    ResetElementState(element); // 重置元素状态
                    return element;
                }
            }
            catch { }
            return null; 
        }

        /// <summary>
        /// 获取 DisplayMemberPath 指定的字符串
        /// </summary>
        /// <param name="displayMemberPath">显示成员路径</param>
        /// <param name="data">数据</param>
        /// <returns>获取的显示成员值</returns>
        private string? GetDisplayMemberValue(string? displayMemberPath, object data)
        {
            if (string.IsNullOrWhiteSpace(displayMemberPath) || data == null)
            {
                return null;
            }

            object? current = data; // 当前数据
            string[] parts = displayMemberPath.Split('.'); // 分割显示成员路径
            foreach (string part in parts)
            {
                if (current == null) return null; // 如果当前数据为null，返回null
                var property = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null) // 如果属性为null，返回null
                {
                    return null;
                }

                current = property.GetValue(current);
            }

            return current?.ToString();
        }

        /// <summary>
        /// 尝试克隆一个UI元素
        /// </summary>
        /// <param name="original">要克隆的元素</param>
        /// <returns>克隆后的元素</returns>
        private object CloneElement(FrameworkElement original)
        {
            try
            {
                // 如果是ComboBoxItem，直接提取内容而不是克隆整个项
                if (original is ComboBoxItem comboBoxItem)
                {
                    object content = comboBoxItem.Content; // 获取ComboBoxItem的内容
                    if (content is FrameworkElement contentElement) // 如果内容是UI元素，克隆它
                    {
                        return CloneElement(contentElement);
                    }
                    else if (content != null) // 如果内容是简单类型，创建TextBlock显示它
                    {
                        return CreateTextBlock(content.ToString());
                    }
                }
                
                // 如果是TextBlock，直接创建新的TextBlock而不是克隆
                if (original is TextBlock textBlock)
                {
                    return new TextBlock
                    {
                        Text = textBlock.Text,
                        Background = Brushes.Transparent,
                        FontFamily = textBlock.FontFamily,
                        FontSize = textBlock.FontSize,
                        FontWeight = textBlock.FontWeight,
                        Foreground = textBlock.Foreground,
                        VerticalAlignment = VerticalAlignment.Center
                    }; // 返回新的TextBlock
                }
                
                // 使用XamlWriter和XamlReader完整克隆元素
                try
                {
                    string xaml = XamlWriter.Save(original); // 保存XAML
                    StringReader stringReader = new StringReader(xaml); // 创建字符串读取器
                    System.Xml.XmlReader xmlReader = System.Xml.XmlReader.Create(stringReader); // 创建XML读取器
                    object cloned = XamlReader.Load(xmlReader); // 加载克隆的元素
                    
                    // 重置状态和设置透明背景
                    if (cloned is FrameworkElement clonedElement)
                    {
                        ResetElementState(clonedElement); // 重置元素状态
                        
                        // 确保元素可见
                        clonedElement.Visibility = Visibility.Visible;
                    }
                    
                    return cloned; // 返回克隆的元素
                }
                catch (Exception ex)
                {
                    // 如果完整克隆失败，尝试简化克隆
                    
                    // 如果是Panel，创建一个新的Grid并克隆其子元素
                    if (original is Panel panel)
                    {
                        Grid grid = new Grid
                        {
                            Background = Brushes.Transparent,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        
                        // 克隆所有可见子元素
                        foreach (UIElement child in panel.Children)
                        {
                            if (child is FrameworkElement childElement && childElement.Visibility == Visibility.Visible)
                            {
                                try
                                {
                                    var clonedChild = CloneElement(childElement);
                                    if (clonedChild is UIElement uiElement)
                                    {
                                        grid.Children.Add(uiElement);
                                    }
                                }
                                catch
                                {
                                    // 忽略单个子元素克隆失败
                                }
                            }
                        }
                        
                        return grid;
                    }
                    
                    // 如果是其他类型，创建一个TextBlock显示其内容
                    string text = original.ToString();
                    if (original is ContentControl contentControl && contentControl.Content != null)
                    {
                        text = contentControl.Content.ToString();
                    }
                    
                    return CreateTextBlock(text);
                }
            }
            catch (Exception ex)
            {
                // 如果克隆失败，创建一个简单的TextBlock
                return CreateTextBlock(original.ToString());
            }
        }
        
        /// <summary>
        /// 重置元素状态并设置透明背景
        /// </summary>
        /// <param name="element">要重置的元素</param>
        private void ResetElementState(DependencyObject element)
        {
            if (element == null) return;
            
            try
            {
                // 重置常见状态
                if (element is Control control)
                {
                    // 设置背景透明
                    if (!(control is Button || control is TextBox))
                    {
                        control.Background = Brushes.Transparent;
                    }
                    
                    // 重置控件状态
                    control.IsEnabled = true; // 设置为启用
                    
                    // 重置选择器状态
                    if (element is Selector selector)
                    {
                        selector.SelectedIndex = -1; // 重置选择索引
                    }
                    
                    // 重置列表项状态
                    if (element is ListBoxItem listBoxItem)
                    {
                        listBoxItem.IsSelected = false; // 重置选择状态
                    }
                    
                    // 重置组合框项状态
                    if (element is ComboBoxItem comboBoxItem)
                    {
                        comboBoxItem.IsSelected = false; // 重置选择状态
                    }
                }
                else if (element is Panel panel)
                {
                    // 只有在不是特殊面板时设置透明背景
                    if (!(panel is Canvas || panel is UniformGrid))
                    {
                        panel.Background = Brushes.Transparent; // 设置背景
                    }
                    
                    // 递归处理子元素
                    foreach (UIElement child in panel.Children)
                    {
                        ResetElementState(child as DependencyObject); // 重置子元素状态
                    }
                }
                else if (element is Border border)
                {
                    // 保留Border的背景，因为它可能是设计的一部分
                }
                
                // 处理ContentControl的Content
                if (element is ContentControl contentControl && 
                    contentControl.Content is DependencyObject contentElement)
                {
                    ResetElementState(contentElement); // 重置ContentControl的Content
                }
            }
            catch
            {
                // 忽略任何无法设置的属性
            }
        }
    }

    // 用于克隆ComboBoxItem内容的帮助类
    public static class ComboBoxHelper
    {
        // 附加属性，用于存储ComboBoxItem的显示文本
        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.RegisterAttached(
                "DisplayText",
                typeof(string),
                typeof(ComboBoxHelper),
                new PropertyMetadata(null)); // 设置默认值为null

        // 获取显示文本
        public static string GetDisplayText(DependencyObject obj)
        {
            return (string)obj.GetValue(DisplayTextProperty); // 获取显示文本
        }

        // 设置显示文本
        public static void SetDisplayText(DependencyObject obj, string value)
        {
            obj.SetValue(DisplayTextProperty, value); // 设置显示文本
        }
    }
} 