using Quicker.Windows.MainWindows;
using Quicker.Database.Core;
using System.Windows;
using Quicker.Models;

namespace Quicker.Windows.EditWindows
{
    public partial class EditActionPageInfoWindow : Window
    {
        private readonly ActionPageDatabase db3 = new(); // 动作页数据库
        private readonly ActionPageData actionPage; // 动作页信息
        private readonly string actionPageIndex; // 动作页索引
        private readonly string actionPageType; // 动作页类型

        public EditActionPageInfoWindow(string actionPageType, string actionPageIndex)
        {
            InitializeComponent();
            this.actionPageType = actionPageType; // 设置动作页类型
            this.actionPageIndex = actionPageIndex; // 设置动作页索引
            actionPage = db3.GetActionPageData(actionPageType, int.Parse(actionPageIndex)); // 获取动作页信息
        }

        // 加载动作页信息
        private void EditActionPageInfoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var sceneData = db3.GetSceneData(actionPageType); // 获取场景信息
            ActionPageProcess.Text = sceneData.SceneTag + ".exe"; // 设置动作页所属进程名称
            ActionPageName.Text = actionPage.ActionPageName; // 设置动作页名称
        }

        // 保存动作页信息
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            db3.UpdateActionPageTable(actionPageType, actionPage.DefaultActionPageName, ActionPageName.Text); // 更新动作页信息
            ActionPageManageWindow window = Application.Current.Windows.OfType<ActionPageManageWindow>().FirstOrDefault(); // 获取动作页管理窗口
            window.UpdateCanvasInListView(int.Parse(actionPageIndex), actionPageType); // 更新动作信息
            this.Close(); // 关闭窗口
        }

        // 取消编辑动作页
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close(); // 关闭窗口
    }
}