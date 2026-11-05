using Quicker.Database.Core;
using Quicker.Managers;
using Quicker.Models;
using System;

namespace Quicker.Helpers
{
    /// <summary>
    /// 动作保存逻辑的集中处理器。
    /// 所有"写入按钮数据到数据库"的操作都应该走这里：
    /// - AddActionWindow 等编辑器场景走 IActionEditor 重载；
    /// - ButtonManager 等已构建好 ButtonData 的场景走 ButtonData 重载。
    /// </summary>
    public static class ActionSaveHelper
    {
        /// <summary>
        /// 按钮数据库实例。
        /// </summary>
        private static readonly ButtonDatabase Db2 = new();

        /// <summary>
        /// 保存动作（面向 AddActionWindow 等编辑器：从 IActionEditor 收集字段）。
        /// </summary>
        public static ButtonData SaveAction(
            int buttonId,
            string tableName,
            IActionEditor editor,
            string title,
            string description,
            Func<string> saveIconFunc)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));

            string savedIconPath = saveIconFunc?.Invoke() ?? ""; // 图标由调用方决定

            var buttonData = new ButtonData
            {
                ButtonID = buttonId,
                Title = title,
                Description = description,
                ImagePath = savedIconPath,
                Location = ButtonManager.ProcessLocation(editor.GetLocation()),
                ActionType = editor.GetActionType(),
                Data1 = editor.GetData1(),
                Data2 = editor.GetData2(),
                Data3 = editor.GetData3(),
            };

            return SaveAction(buttonData, tableName);
        }

        /// <summary>
        /// 保存动作（直接接收构建好的 ButtonData，供 ButtonManager 等复用）。
        /// </summary>
        public static ButtonData SaveAction(ButtonData buttonData, string tableName, bool notify = true)
        {
            if (buttonData == null) throw new ArgumentNullException(nameof(buttonData));
            var oldData = Db2.GetButtonDataByID(buttonData.ButtonID, tableName);
            if (oldData != null)
            {
                buttonData.CreateTime = oldData.CreateTime;
                buttonData.UsedTimes = oldData.UsedTimes;
            }
            else if (buttonData.CreateTime == default)
            {
                buttonData.CreateTime = DateTime.Now;
            }

            Db2.UpdateAction(buttonData, tableName);
            if (notify) AppStateManager.NotifyActionPageChanged(tableName);
            return buttonData;
        }
    }
}