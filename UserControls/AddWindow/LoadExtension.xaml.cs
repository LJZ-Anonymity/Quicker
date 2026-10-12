using System.Windows.Media.Imaging;
using Quicker.Windows.AddWindows;
using System.Windows.Controls;
using Quicker.Database.Core;
using Quicker.Managers;
using Microsoft.Win32;
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;
using WpfAnimatedGif;
using Quicker.Extend;
using System.IO;

namespace Quicker.UserControls.AddWindow
{
    /// <summary>
    /// 扩展加载控件
    /// </summary>
    public partial class LoadExtension : UserControl,AddActionWindow.IActionEditor
    {
        #region 字段和属性
        
        private Quicker.Windows.AddWindows.AddActionWindow _addWindow; // AddWindow 的引用
        private readonly ButtonManager _buttonManager = new(); // 按钮管理器接口
        private readonly IconManager _iconManager = new(); // 图标管理器接口
        private ButtonDatabase _buttonDb = new(); // 按钮数据库
        private string _selectedPath; // 选中的文件夹路径
        private bool isLoading = true; // 是否正在加载

        #region IActionEditor

        public ActionType GetActionType() => ActionType.LoadExtension;
        public string GetLocation() => LocationTextBox.Text;
        public string GetData1() => "";
        public string GetData2() => "";
        public string GetData3() => "";

        #endregion

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="addWindow">AddWindow 的引用</param>
        public LoadExtension(Quicker.Windows.AddWindows.AddActionWindow addWindow)
        {
            _addWindow = addWindow; // 保存 AddWindow 的引用
            InitializeComponent();
        }

        // 控件加载事件
        private void LoadExtension_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ExecuteChoiceAction(); // 根据上个窗口数据执行对应命令
            isLoading = false; // 加载完成
        }

        #endregion

        #region 数据加载和处理

        // 根据上个窗口数据执行对应命令
        private void ExecuteChoiceAction()
        {
            if (_addWindow.Choice == 0) // 编辑动作
            {
                LoadButtonInformation(); // 加载动作信息
            }
        }

        // 加载按钮信息
        private void LoadButtonInformation()
        {
            ButtonData buttonData = _buttonDb.GetButtonDataByID(_addWindow.ButtonID, _addWindow.TableName); // 获取按钮数据
            if (buttonData.ActionType != ActionType.LoadExtension) return; // 验证动作类型
            UpdateUIFromButtonData(buttonData); // 更新UI
        }

        /// <summary>
        /// 从按钮数据更新UI
        /// </summary>
        /// <param name="buttonData">按钮数据</param>
        private void UpdateUIFromButtonData(ButtonData buttonData)
        {
            UpdateTitleFromButtonData(buttonData); // 更新标题
            LocationTextBox.Text = buttonData.Location; // 设置路径
            _selectedPath = buttonData.Location; // 保存路径
            UpdateIconFromButtonData(buttonData); // 更新图标
            _addWindow.DescriptionTextBox.Text = buttonData.Description; // 设置描述
            _addWindow.UpdateTooltip(); // 更新提示
            LoadExtensionInfo(_selectedPath, false); // 加载扩展信息，但不覆盖图标
        }

        /// <summary>
        /// 更新标题
        /// </summary>
        /// <param name="buttonData">按钮数据</param>
        private void UpdateTitleFromButtonData(ButtonData buttonData)
        {
            if (!string.IsNullOrWhiteSpace(buttonData.Title))
            {
                _addWindow.ButtonTitle.Visibility = Visibility.Visible; // 设置标题可见
                _addWindow.ButtonTitle.Text = buttonData.Title; // 设置标题文本
            }
            _addWindow.TitleTextBox.Text = buttonData.Title; // 设置标题文本
        }

        /// <summary>
        /// 更新图标
        /// </summary>
        /// <param name="buttonData">按钮数据</param>
        private void UpdateIconFromButtonData(ButtonData buttonData)
        {
            if (string.IsNullOrEmpty(buttonData.ImagePath)) return; // 如果图标路径为空，则返回
            _addWindow.SetButtonImage(buttonData.ImagePath); // 设置图标
            _addWindow.iconPath = buttonData.ImagePath; // 同步iconPath
        }

        #endregion

        #region 扩展选择和加载

        // 打开菜单
        private void OpenContextMenu(object sender, RoutedEventArgs e)
        {
            LoadExtensionPopup.IsOpen = true; // 打开弹出菜单
        }

        // 复制地址
        private void CopyLocation(object sender, RoutedEventArgs e)
        {
            string clipboardText = System.Windows.Clipboard.GetText(); // 获取剪贴板文本
            LocationTextBox.Text = _buttonManager.ProcessLocation(clipboardText); // 设置地址栏文本
            try
            {
                LoadExtensionInfo(LocationTextBox.Text); // 加载扩展信息
            }
            catch
            {
                using var toast = new ToastManager(); // 创建ToastManager
                toast.Show("扩展信息加载失败", ToastType.Error); // 显示错误提示
            }
            LoadExtensionPopup.IsOpen = false; // 关闭弹出菜单
        }

        // 选择扩展按钮点击事件
        private void SelectExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ShowFileDialog(); // 改为选择文件
            if (string.IsNullOrEmpty(selectedPath)) return;

            _selectedPath = selectedPath; // 设置选中的路径
            LoadExtensionInfo(selectedPath); // 加载扩展信息
            _addWindow.SaveButton.IsEnabled = true; // 如果文件夹里有.dll文件，则启用保存按钮
        }

        /// <summary>
        /// 显示文件选择对话框
        /// </summary>
        /// <returns>文件路径</returns>
        private string ShowFileDialog()
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "扩展模块 (*.dll)|*.dll",
                InitialDirectory = AppPathHelper.ExtensionsFolder, // 默认路径
                Title = "选择扩展模块DLL文件"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null; // 如果选择文件，则返回文件路径，否则返回null
        }

        /// <summary>
        /// 加载扩展信息
        /// </summary>
        /// <param name="dllPath">扩展DLL文件路径</param>
        /// <param name="setIcon">是否设置图标，默认为true</param>
        private void LoadExtensionInfo(string dllPath, bool setIcon = true)
        {
            using var toast = new ToastManager();
            if (!File.Exists(dllPath) || !dllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                toast.Show("未找到扩展DLL文件", ToastType.Error); // 如果未找到扩展DLL文件，则显示错误提示
                return;
            }

            try
            {
                var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IExtensionModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType == null)
                {
                    toast.Show("未找到有效的扩展模块类型", ToastType.Error); // 如果未找到有效的扩展模块类型，则显示错误提示
                    return;
                }

                var module = (IExtensionModule)Activator.CreateInstance(moduleType); // 创建扩展模块实例

                // 用模块属性更新UI
                NameTextBlock.Text = module.Name;
                VersionTextBlock.Text = module.Version;
                AuthorTextBlock.Text = module.Author;
                DescriptionTextBlock.Text = module.Description;
                LocationTextBox.Text = dllPath;
                _addWindow.TitleTextBox.Text = module.Name;
                _addWindow.DescriptionTextBox.Text = module.Description;
                _addWindow.UpdateTooltip();
                
                // 根据参数决定是否设置扩展模块图标
                if (setIcon)
                {
                    SetExtensionModuleImage(module);
                }
            }
            catch
            {
                toast.Show("扩展信息加载失败！", ToastType.Error); // 如果加载扩展信息失败，则显示错误提示
                NameTextBlock.Text = "未知";
                VersionTextBlock.Text = "未知";
                AuthorTextBlock.Text = "未知";
                DescriptionTextBlock.Text = "未知";
                LocationTextBox.Text = dllPath;
                _addWindow.TitleTextBox.Text = Path.GetFileNameWithoutExtension(dllPath);
                _addWindow.DescriptionTextBox.Text = "运行扩展" + Path.GetFileNameWithoutExtension(dllPath);
                _addWindow.UpdateTooltip();
            }
        }

        // 调整地址栏高度
        private void LocationTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isLoading) return; // 防止在加载过程中调整大小
            if (_addWindow != null)
            {
                double heightChange = e.NewSize.Height - e.PreviousSize.Height; // 计算文本框高度变化量
                _addWindow.Height += heightChange; // 调整窗口高度
                Thickness currentMargin = Grid1.Margin; // 获取当前的Margin
                Thickness newMargin = new Thickness(0, currentMargin.Top + heightChange, 0, 0); // 创建一个新的Margin对象，只修改Top属性
                Grid1.Margin = newMargin; // 将新的Margin赋给Grid1
            }
        }

        // 地址栏文本变化事件
        private void LocationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(LocationTextBox.Text))
            {
                _addWindow.SaveButton.IsEnabled = false; // 如果地址栏为空，则禁用保存按钮
                Grid1.Visibility = Visibility.Collapsed; // 隐藏Grid1
            }
            else
            {
                _addWindow.SaveButton.IsEnabled = true; // 如果地址栏不为空，则启用保存按钮
                Grid1.Visibility = Visibility.Visible; // 显示Grid1
            }
        }

        /// <summary>
        /// 设置扩展模块图标
        /// </summary>
        /// <param name="module">扩展模块实例</param>
        private void SetExtensionModuleImage(IExtensionModule module)
        {
            try
            {
                if (module.IconData != null && module.IconData.Length > 0)
                {
                    // 将字节数组转换为BitmapImage
                    using var stream = new MemoryStream(module.IconData);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // 保存图标到临时文件并设置按钮图标
                    string tempIconPath = SaveIconToTempFile(bitmap);
                    if (!string.IsNullOrEmpty(tempIconPath))
                    {
                        _addWindow.SetButtonImage(tempIconPath);
                    }
                    else
                    {
                        _addWindow.ButtonImage.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // 如果没有图标数据，使用默认图标或隐藏图标
                    _addWindow.ButtonImage.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                // 图标加载失败时，使用默认处理
                _addWindow.ButtonImage.Visibility = Visibility.Collapsed;
                using var toast = new ToastManager();
                toast.Show($"图标加载失败: {ex.Message}", ToastType.Warning);
            }
        }

        /// <summary>
        /// 将BitmapImage保存为临时文件
        /// </summary>
        /// <param name="bitmap">位图对象</param>
        /// <returns>临时文件路径</returns>
        private string SaveIconToTempFile(BitmapImage bitmap)
        {
            try
            {
                // 创建临时文件路径
                string tempPath = Path.Combine(Path.GetTempPath(), $"extension_icon_{Guid.NewGuid()}.png");
                
                // 创建编码器
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                
                // 保存到文件
                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
                
                return tempPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存临时图标文件失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 保存和清理

        // 卸载时释放资源
        private void LoadExtension_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _addWindow = null; // 释放AddWindow的引用
            ClearExtensionInfo(); // 清除扩展信息
        }

        // 清除扩展信息
        private void ClearExtensionInfo()
        {
            _selectedPath = null; // 清除选中的路径
            NameTextBlock.Text = ""; // 清除扩展名称
            VersionTextBlock.Text = ""; // 清除扩展版本
            DescriptionTextBlock.Text = ""; // 清除扩展描述
            AuthorTextBlock.Text = ""; // 清除扩展作者
        }

        #endregion

        #region 数据模型

        // 扩展信息类
        public class Info
        {
            public string Name { get; set; } // 扩展名称
            public string Version { get; set; } // 扩展版本
            public string Description { get; set; } // 扩展描述
            public string Author { get; set; } // 扩展作者
        }

        #endregion
    }
}