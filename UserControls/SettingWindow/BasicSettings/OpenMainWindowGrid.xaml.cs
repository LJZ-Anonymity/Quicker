using MouseButton = System.Windows.Input.MouseButton;
using Quicker.Windows.MainWindows;
using System.Windows.Controls;
using Quicker.UserControls;
using System.Windows.Input;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class OpenMainWindowControl : UserControl
    {
        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow; // 弱引用设置窗口
        private List<string> ShortcutKeys = new(); // 保存快捷键
        SettingManager settingManager; // 设置管理器

        public OpenMainWindowControl(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent();
            settingManager = settingWindow._settingManager; // 创建设置管理器
            weakSettingWindow = new(settingWindow); // 保存设置窗口
            InitializeAsync(); // 异步初始化
        }

        // 异步初始化方法
        private async void InitializeAsync()
        {
            await LoadSettingsAsync(); // 异步加载设置
        }

        // 异步加载设置
        private async Task LoadSettingsAsync()
        {
            settingManager.LoadOpenMainWindowConditionsAsync(); // 初始化数据库缓存
            Application.Current.Dispatcher.Invoke(() =>
            {
                OpenMainWindowByMiddleMouseClickCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByMiddleMouseClick; // 按下中键
                OpenMainWindowByX1MouseClickCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByX1MouseClick; // 按下X1键
                OpenMainWindowByX2MouseClickCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByX2MouseClick; // 按下X2键
                OpenMainWindowByCtrl_MiddleMouseClickCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByCtrl_MiddleMouseClick; // Ctrl+中键单击
                OpenMainWindowByCtrl_RightMouseClickCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByCtrl_RightMouseClick; // Ctrl+右键单击
                OpenMainWindowByMiddleMouseClickLongerCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByMiddleMouseClickLonger; // 长按中键
                OpenMainWindowByRightMouseClickLongerCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByRightMouseClickLonger; // 长按右键
                OpenMainWindowByRightMouseClick_MoveCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByRightMouseClick_Move; // 按右键移动
                OpenMainWindowByCtrlCheckBox.IsChecked = settingManager.openMainWindowConditions.OpenMainWindowByCtrl; // 单击Ctrl键
                WindowStartupLocationComboBox.SelectedIndex = settingManager.openMainWindowConditions.WindowStartupLocation; // 功能面板打开位置
            });
        }

        // 勾选框点击事件
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;
            bool value = checkBox.IsChecked == true;
            switch (checkBox.Name)
            {
                case "OpenMainWindowByMiddleMouseClickCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByMiddleMouseClick = value;
                    break;
                case "OpenMainWindowByX1MouseClickCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByX1MouseClick = value;
                    break;
                case "OpenMainWindowByX2MouseClickCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByX2MouseClick = value;
                    break;
                case "OpenMainWindowByCtrl_MiddleMouseClickCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByCtrl_MiddleMouseClick = value;
                    break;
                case "OpenMainWindowByCtrl_RightMouseClickCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByCtrl_RightMouseClick = value;
                    break;
                case "OpenMainWindowByMiddleMouseClickLongerCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByMiddleMouseClickLonger = value;
                    break;
                case "OpenMainWindowByRightMouseClickLongerCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByRightMouseClickLonger = value;
                    break;
                case "OpenMainWindowByRightMouseClick_MoveCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByRightMouseClick_Move = value;
                    break;
                case "OpenMainWindowByCtrlCheckBox":
                    settingManager.openMainWindowConditions.OpenMainWindowByCtrl = value;
                    break;
                default:
                    return;
            }
        }

        // 下拉框选择改变事件
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;
            switch (comboBox.Name)
            {
                case "WindowStartupLocationComboBox":
                    settingManager.openMainWindowConditions.WindowStartupLocation = comboBox.SelectedIndex;
                    break;
                default:
                    return;
            }
        }

        // 基础设置-弹出面板-弹出面板
        private void OpenMainWindowButton_Click(object sender, RoutedEventArgs e)
        {
            settingManager.SetGridVisible(OpenMainWindowButtonGrid, MainGrid); // 设置Grid可见性
            settingManager.ButtonStyle3_Click(OpenMainWindowButton, MainGrid); // 设置Button样式
        }

        // 测试鼠标按键名称
        private void TestMouseKey(object sender, MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    TestButton.Content = "左键";
                    break; // 左键
                case MouseButton.Right:
                    TestButton.Content = "右键";
                    break; // 右键
                case MouseButton.Middle:
                    TestButton.Content = "中键";
                    break; // 中键
                case MouseButton.XButton1:
                    TestButton.Content = "X1键";
                    break; // X1键
                case MouseButton.XButton2:
                    TestButton.Content = "X2键";
                    break; // X2键
                default:
                    TestButton.Content = "未知按键";
                    break; // 未知按键
            }
        }

        // 阻止文本输入
        private void DIY_ShortcutKeys_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true; // 阻止文本输入
        }

        // 自定义快捷键
        private void DIY_ShortcutKeys_KeyDown(object sender, KeyEventArgs e)
        {
            //if (DIY_ShortcutKeys.IsFocused)
            //{
            //    InputMethod.SetIsInputMethodEnabled(DIY_ShortcutKeys, false); // 禁用输入法
            //    string shortcut = ShortcutHelper.GetShortcutString(e);
            //    DIY_ShortcutKeys.Text = shortcut;
            //}
        }

        // 当快捷键输入框获得焦点时，切换输入法为英文
        private void DIY_ShortcutKeys_GotFocus(object sender, RoutedEventArgs e)
        {
            //InputMethod.SetPreferredImeState(DIY_ShortcutKeys, InputMethodState.Off); // 关闭输入法，强制英文
        }

        // 清除之前的输入
        private void DIY_ShortcutKeys_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            ShortcutKeys.Clear();
        }

        // 清除文本框内容
        private void ClearDIY_ShortcutKeys(object sender, RoutedEventArgs e)
        {
            //DIY_ShortcutKeys.Text = null;
            ShortcutKeys.Clear();
        }

        // 清理资源
        private void OpenMainWindowGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            settingManager = null; // 释放设置管理器资源
            weakSettingWindow = null; // 清空弱引用
            ShortcutKeys?.Clear();
            ShortcutKeys = null;
        }
    }
}