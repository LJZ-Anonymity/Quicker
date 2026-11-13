using Quicker.Database.Core;
using System.Windows.Input;
using Quicker.Models;
using System.Windows;

namespace Quicker.Windows.EditWindows
{
    public partial class EditSceneWindow : Window
    {
        private string SceneType { get; set; } // 场景类型
        private readonly ActionPageDatabase db3 = new(); // 动作页数据库

        public EditSceneWindow(string sceneType)
        {
            InitializeComponent();
            SceneType = sceneType; // 设置场景类型
        }

        private void EditSceneWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var sceneData = db3.GetSceneData(SceneType); // 获取场景数据
            SceneTagTextBlock.Text = SceneType + ".exe"; // 设置场景类型
            LocationTextBlock.Text = sceneData.SceneProcess; // 设置场景位置
            SceneNameTextBox.Text = sceneData.SceneName; // 设置场景名称
        }

        // 点击按钮保存场景信息
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var sceneData = db3.GetSceneData(SceneType); // 获取场景数据
            SceneData newSceneData = new()
            {
                SceneName = SceneNameTextBox.Text,
                SceneIconPath = sceneData.SceneIconPath,
                SceneCount = sceneData.SceneCount,
                SceneTag = SceneType,
                AutoReturnToFirstPage = sceneData.AutoReturnToFirstPage,
                SceneProcess = LocationTextBlock.Text
            };
            db3.UpdateSceneTable(newSceneData); // 更新场景数据
            this.Close(); // 关闭窗口
        }

        // 点击按钮关闭窗口
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close(); // 关闭窗口

        // 点击按钮更新场景位置
        private void UpdateLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行文件 (*.exe)|*.exe",
                Title = "请选择程序路径"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                LocationTextBlock.Text = openFileDialog.FileName; // 设置场景位置
            }
        }

        // 按下 S 键保存
        private void EditSceneWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S)
                SaveButton_Click(null, null); // 按下 S 键保存
        }
    }
}