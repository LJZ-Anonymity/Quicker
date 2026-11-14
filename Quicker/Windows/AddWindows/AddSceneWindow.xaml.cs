using System.Collections.Generic;
using Quicker.Windows.ToolWindows;
using System.Windows.Controls;
using System.Threading.Tasks;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Windows.Input;
using System.Diagnostics;
using Quicker.Managers;
using System.Windows;
using Quicker.Models;
using System.IO;
using System;

namespace Quicker.Windows.AddWindows
{
    public partial class AddSceneWindow : Window
    {
        public event Action<bool, string?>? SceneAddCompleted; // 参数1: 是否保存, 参数2: 新场景名
        private WindowSelectWindow windowSelectWindow;
        private SceneData currentSceneData; // 当前场景数据
        public AppInfo SelectedApp { get; set; } // 选中的应用

        public AddSceneWindow()
        {
            InitializeComponent();
            LoadProcessesAsync();
        }

        // 选择进程后，显示进程名和图标
        private void AppComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ProcessItem item = AppComboBox.SelectedItem as ProcessItem; // 获取选择的进程
            string fileNameWithExt = Path.GetFileName(item.FileName)?.ToLower(); // 文件名（带后缀，小写）
            SceneTagTextBlock.Text = fileNameWithExt; // 显示文件名
            string processNameNoExt = Path.GetFileNameWithoutExtension(item.ProcessName); // ProcessName 去除后缀
            SceneNameTextBox.Text = processNameNoExt; // 显示进程名
            currentSceneData = new SceneData
            {
                SceneName = processNameNoExt,
                SceneIconPath = item.FileName,
                SceneCount = 0,
                SceneTag = processNameNoExt,
                AutoReturnToFirstPage = false,
                SceneProcess = item.FileName
            }; // 构造场景数据模型
            SaveButton.IsEnabled = true; // 启用保存按钮
        }

        /// <summary>
        /// 异步加载进程列表
        /// </summary>
        public async void LoadProcessesAsync()
        {
            var processList = await Task.Run(() =>
            {
                var items = new List<(string FileName, string ProcessName)>();
                var processes = GetFilteredProcessList();
                foreach (var (fileName, processName) in processes)
                {
                    items.Add((fileName, processName));
                }
                return items;
            });

            // 2. UI线程创建Icon
            var uiList = new List<ProcessItem>();
            foreach (var (fileName, processName) in processList)
            {
                var icon = IconManager.GetIcon(fileName); // 这行必须在UI线程
                uiList.Add(new ProcessItem
                {
                    FileName = fileName,
                    ProcessName = processName,
                    Icon = icon
                });
            }
            AppComboBox.ItemsSource = uiList;
        }

        /// <summary>
        /// 判断进程是否有窗口（包括最小化的窗口）
        /// </summary>
        private bool HasWindow(Process process) => process.MainWindowHandle != IntPtr.Zero;

        /// <summary>
        /// 获取符合条件的进程列表
        /// </summary>
        private List<(string FileName, string ProcessName)> GetFilteredProcessList()
        {
            var processList = new List<(string FileName, string ProcessName)>();
            var db = new ActionPageDatabase();
            var allScenes = db.GetAllSceneData(); // 获取所有场景数据
            try
            {
                var processes = Process.GetProcesses().Where(p => HasWindow(p)).Take(20); // 获取进程列表，最多20个
                foreach (var process in processes)
                {
                    try
                    {
                        string processFileName = process.MainModule.FileName; // 进程文件名
                        string fullProcessName = Path.GetFileName(processFileName)?.ToLower(); // 进程名（带后缀，小写）
                        string processName = Path.GetFileNameWithoutExtension(process.ProcessName); // 进程名（去除后缀）

                        // 检查是否与数据库中已有场景重复
                        bool isDuplicate = allScenes.Any(scene =>
                            scene.SceneTag?.ToLower() == processName &&
                            scene.SceneProcess?.Equals(processFileName, StringComparison.OrdinalIgnoreCase) == true);

                        if (isDuplicate) continue; // 跳过重复的进程

                        if (processList.Any(p => p.FileName.Equals(processFileName, StringComparison.OrdinalIgnoreCase)))
                            continue; // 已经添加过该应用，跳过

                        processList.Add((processFileName, fullProcessName)); // 添加到列表
                    }
                    catch { }
                    finally
                    {
                        process?.Dispose(); // 释放资源
                    }
                }
            }
            catch { } // 忽略异常
            return processList; // 返回进程列表
        }

        // 保存场景数据
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSceneData != null) // 场景数据不为空
            {
                if (AppComboBox.SelectedItem is ProcessItem item && item.Icon != null) // 保存到数据库前，保存图标到本地文件
                {
                    IconManager iconManager = new();
                    string iconPath = iconManager.SaveIconToFile(item.Icon); // 保存图标到本地文件
                    if (!string.IsNullOrEmpty(iconPath)) // 图标保存成功
                    {
                        currentSceneData.SceneName = SceneNameTextBox.Text; // 更新场景名
                        currentSceneData.SceneIconPath = iconPath; // 更新图标路径
                    }
                }
                var db = new ActionPageDatabase(); // 数据库连接
                db.CreateAndInitTable(currentSceneData.SceneTag, currentSceneData.SceneIconPath, currentSceneData.SceneTag); // 初始化数据表
                db.UpdateSceneTable(currentSceneData); // 更新场景数据
            }
            SceneAddCompleted?.Invoke(true, currentSceneData?.SceneTag); // 通知父窗口保存完成
            this.Close(); // 关闭窗口
        }

        // 取消场景添加
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SceneAddCompleted?.Invoke(false, null);
            this.Close(); // 关闭窗口
        }

        // 按下S或C键保存或取消
        private void AddSceneWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S) // 按下S
            {
                SaveButton_Click(sender, e); // 保存按钮
            }
            else if (e.Key == Key.C) // 按下C
            {
                CancelButton_Click(sender, e); // 取消按钮
            }
        }

        // 选择窗口
        private void SelectWindow_Click(object sender, RoutedEventArgs e)
        {
            windowSelectWindow = new();
            windowSelectWindow.WindowSelected += OnWindowSelected; // 订阅 WindowSelected 事件
            windowSelectWindow.StartSelecting(this); // 开始选择窗口
        }

        /// <summary>
        /// 处理窗口选择事件，分发到各个子方法
        /// </summary>
        private void OnWindowSelected(object sender, WindowSelectWindow.WindowSelectedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.ProcessPath))
            {
                try
                {
                    var processItem = CreateProcessItem(e);
                    var existItem = AddOrGetProcessItem(processItem);
                    SelectProcessItem(existItem);
                    UpdateCurrentSceneData(e);
                    UpdateUI(); // 更新UI
                }
                catch { }
                finally
                {
                    windowSelectWindow?.Close();
                }
            }
        }

        /// <summary>
        /// 根据窗口选择事件参数创建一个新的 ProcessItem。
        /// </summary>
        private ProcessItem CreateProcessItem(WindowSelectWindow.WindowSelectedEventArgs e)
        {
            return new ProcessItem
            {
                FileName = e.ProcessPath,
                ProcessName = Path.GetFileName(e.ProcessPath),
                Icon = e.ProcessIcon
            };
        }

        /// <summary>
        /// 将 ProcessItem 添加到 ComboBox 的 ItemsSource（如果不存在），并返回集合中的引用。
        /// </summary>
        /// <param name="processItem">要添加的 ProcessItem</param>
        private ProcessItem AddOrGetProcessItem(ProcessItem processItem)
        {
            var items = AppComboBox.ItemsSource as List<ProcessItem> ?? new List<ProcessItem>();
            var existItem = items.FirstOrDefault(x => x.FileName == processItem.FileName);
            if (existItem == null)
            {
                items.Add(processItem);
                AppComboBox.ItemsSource = null; // 触发刷新
                AppComboBox.ItemsSource = items;
                existItem = processItem;
            }
            return existItem;
        }

        /// <summary>
        /// 选中 ComboBox 中指定的 ProcessItem。
        /// </summary>
        private void SelectProcessItem(ProcessItem item)
        {
            AppComboBox.SelectedItem = item;
        }

        /// <summary>
        /// 根据窗口选择事件参数，更新当前场景数据 currentSceneData。
        /// </summary>
        private void UpdateCurrentSceneData(WindowSelectWindow.WindowSelectedEventArgs e)
        {
            currentSceneData = new SceneData
            {
                SceneName = Path.GetFileNameWithoutExtension(e.ProcessPath),
                SceneIconPath = e.ProcessPath, // 后续保存时再处理
                SceneCount = 0,
                SceneTag = Path.GetFileNameWithoutExtension(e.ProcessPath).ToLower(),
                AutoReturnToFirstPage = false,
                SceneProcess = e.ProcessPath
            };
        }

        /// <summary>
        /// 同步界面控件内容，将 currentSceneData 的信息显示到界面上。
        /// </summary>
        private void UpdateUI()
        {
            SceneNameTextBox.Text = currentSceneData.SceneName;
            SceneTagTextBlock.Text = currentSceneData.SceneTag + ".exe";
        }

    }

    /// <summary>
    /// 进程信息
    /// </summary>
    public class ProcessItem
    {
        public string FileName { get; set; } // 进程文件名
        public string ProcessName { get; set; } // 进程名（带后缀）
        public ImageSource Icon { get; set; } // 图标
    }
}