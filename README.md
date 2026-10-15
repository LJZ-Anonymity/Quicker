# Quicker

[![Merge Project](https://github.com/LJZ-Anonymity/Quicker/actions/workflows/CodeMerging.yml/badge.svg?branch=Quicker)](https://github.com/LJZ-Anonymity/Quicker/actions/workflows/CodeMerging.yml)
[![Version](https://img.shields.io/github/v/release/LJZ-Anonymity/Quicker)](https://github.com/LJZ-Anonymity/Quicker/releases)
[![最后更新](https://img.shields.io/github/last-commit/LJZ-Anonymity/Quicker)](https://github.com/LJZ-Anonymity/Quicker/commits)
[![开源协议](https://img.shields.io/badge/License-GNU%20AGPL%20v3.0-blue.svg)](https://github.com/LJZ-Anonymity/Quicker/blob/main/LICENSE)
[![代码行数](https://aschey.tech/tokei/github/LJZ-Anonymity/Quicker)](https://github.com/LJZ-Anonymity/Quicker)
[![主要语言](https://img.shields.io/github/languages/top/LJZ-Anonymity/Quicker)](https://github.com/LJZ-Anonymity/Quicker)

---

## 项目简介

这是一个基于WPF开发的学习项目，参考了Quicker软件的界面和功能，但代码完全独立实现。

该项目旨在提供一个高效、便捷的操作界面，帮助用户快速执行各种任务。

**注意**：仅供学习与研究，不用于商业用途。

- [扩展/插件仓库](https://github.com/LJZ-Anonymity/Extensions "查看Quicker扩展项目")
- [使用说明/文档](https://github.com/LJZ-Anonymity/Instructions "查看Quicker说明项目")

---

## 开源声明

- 本项目为开源学习项目，作者非计算机专业，仍有优化空间。
- 项目持续更新，如有侵权，请[联系作者](#contact)删除。
- 与原版 Quicker 软件 **无任何代码关联**，仅参考界面和功能。

正版 Quicker：https://getquicker.net/

---

## 开源协议

本项目采用 [GNU AGPL v3.0](LICENSE) 协议开源。

**重要声明**：

- 仅作为学习交流使用，不得用于商业用途。
- 任何修改必须开源并保留原始版权声明和许可证。

---

## 贡献者版权说明

所有贡献者提交的代码将自动采用 GNU AGPL v3.0 协议开源。提交代码即视为同意本项目的开源协议和相关声明。

---

## 扩展开发

Quicker 支持通过创建扩展模块来增强应用功能。你可以通过以下步骤开发扩展模块：

1. 创建一个 WPF 类库项目。

2. 引用Quicker的扩展接口（Extend/IExtensionModule.cs）。

3. 实现扩展接口，开发你的扩展功能。

4. 将编译后的扩展 DLL 文件放置在 Quicker 的扩展目录中，应用将会在需要时加载。

更多详情请见[扩展项目](https://github.com/LJZ-Anonymity/Extensions "查看Quicker 扩展项目")。

---

## 致谢

感谢 [Quicker 软件](https://getquicker.net/ "访问 Quicker 官方网站")提供界面和功能参考。

---

## 版权声明

本项目界面、图标及部分资源参考正版 Quicker，仅用于学习/非商业用途。

相关版权归原作者及 Quicker 官方所有。如有侵权请及时联系删除。

---

## 免责声明

本项目为非商业学习项目，未获得 Quicker 官方或其他第三方的商业授权。

**因使用本项目造成的任何后果，作者不承担任何责任。**

---

## 项目文件结构

<details>
<summary>详细结构</summary>

```
Quicker/
├─.gitattributes                                      # Git属性配置
├─.gitignore                                          # Git忽略文件配置
├─App.xaml                                            # 应用入口点
├─App.xaml.cs                                         # 应用入口点的代码后台
├─AssemblyInfo.cs                                     # 项目的属性和版本信息
├─Converters/                                         # XAML 绑定用的值转换器
│   ├─HighlightTextConverter.cs                       # 高亮文本转换器
│   ├─GridWidthConverter.cs                           # 网格宽度计算转换器
│   ├─GridHeightConverter.cs                          # 网格高度计算转换器
│   ├─BorderHeightConverter.cs                        # 边框高度转换器
│   ├─LighterColorConverter.cs                        # 亮色系转换器
│   ├─SmartHoverColorConverter.cs                     # 智能悬停色转换器
│   ├─FontWeightConverter.cs                          # 字体粗细与枚举转换器
│   ├─ButtonBackgroundConverter.cs                    # 按钮背景色转换器
│   ├─IntToCornerRadiusConverter.cs                   # int转圆角半径转换器
│   ├─BlurEffectConverter.cs                          # 模糊效果转换器
│   ├─PathToImageSourceConverter.cs                   # 路径转图片源转换器
│   ├─ThicknessConverter.cs                           # 边距厚度转换器
│   └─SearchHintVisibilityConverter.cs                # 搜索框提示可见性转换器，根据TextBox文本内容控制提示Label的显示/隐藏
├─Database/                                           # 数据库相关
│   ├─Core/                                            # 数据库核心操作
│   │   ├─ActionPageDatabase.cs                         # 动作页数据库，管理动作页和场景数据
│   │   ├─ButtonDatabase.cs                             # 按钮数据库，管理动作按钮的数据存储
│   │   ├─DatabaseHelper.cs                             # 数据库操作助手类，提供统一的数据库连接和初始化功能
│   │   └─SettingDatabase.cs                            # 设置数据库，管理应用配置和设置信息
│   └─Upgrade/                                         # 数据库升级相关
│       ├─IDatabaseUpgradeStep.cs                       # 升级步骤接口，定义数据库升级的标准接口
│       ├─DatabaseUpdateManager.cs                      # 升级调度主类，负责数据库版本检查和升级流程管理
│       └─Versions/                                     # 各版本升级实现类（每个版本一个类，便于维护和扩展）
│           └─Upgrade_2_3_0.cs                          # 2.2.0到2.3.0版本升级实现
├─Extend/                                             # 扩展文件夹
│   ├─IExtensionModule.cs                              # 实现扩展的接口
│   └─ModuleLoader.cs                                  # 扩展模块加载器
├─Helpers/                                            # 通用辅助类/附加属性
│   ├─AppPathHelper.cs                                 # 应用程序路径管理助手类，统一管理Quicker应用程序的所有路径
│   ├─ButtonDataHelper.cs                              # 按钮数据辅助类，提供从SQLiteDataReader读取数据并构造ButtonData对象的方法，包含处理可空字段和枚举类型转换的静态方法
│   ├─TextBlockHelper.cs                               # 文本块辅助类
│   ├─VersionHelper.cs                                 # 版本号比较工具类，用于版本号比较和检查更新
│   ├─DataSizeHelper.cs                                # 数据大小换算工具类，用于字节、KB、MB、GB等单位转换
│   ├─ClipHelper.cs                                    # UI裁剪附加属性，用于为按钮和边框设置自定义裁剪（圆角）
│   ├─ShortcutHelper.cs                                # 快捷键辅助类，提供快捷键字符串生成、友好显示、比对等功能
│   ├─LowLevelGlobalHookHelper.cs                      # 全局钩子辅助类，封装底层键鼠监听
│   └─VisualTreeHelper.cs                              # WPF视觉树辅助类，提供视觉树遍历和查找功能
├─Internal/                                           # 内部命令相关
│   └─InternalCommand.cs                               # 内置命令定义
├─VersionInfo.json                                    # 版本信息
├─LICENSE                                             # 开源协议文件
├─Managers/                                           # 管理器文件夹
│   ├─ActionManager.cs                                 # 动作管理器
│   ├─AnimationManager.cs                              # 动画效果管理器
│   ├─AppManager.cs                                    # 应用管理器
│   ├─AppStateManager.cs                               # 应用状态管理器
│   ├─AppUpdateManager.cs                              # 应用更新管理器
│   ├─ButtonManager.cs                                 # 按钮管理器
│   ├─DatabaseUpdateManager.cs                         # 数据库更新管理器
│   ├─IconManager.cs                                   # 图标管理器
│   ├─InternalCommandManager.cs                        # 内部命令调度器
│   ├─MenuManager.cs                                   # 菜单管理器
│   ├─SettingManager.cs                                # 设置管理器
│   ├─SingleInstanceManager .cs                        # 互斥锁管理器
│   ├─ToastManager.cs                                  # 消息管理器
│   └─WindowManager.cs                                 # 窗口管理器
├─Models/                                             # 数据模型
│   ├─ActionPageData.cs                                # 动作页数据模型
│   ├─ButtonData.cs                                    # 按钮数据模型
│   ├─SceneData.cs                                     # 场景数据模型
│   └─Settings/                                        # 设置相关数据模型
│       ├─Appearance.cs                                 # 外观设置数据模型
│       ├─Blacklist.cs                                  # 黑名单设置数据模型
│       ├─Convention.cs                                 # 常规设置数据模型
│       └─OpenMainWindow.cs                             # 打开主窗口条件数据模型
├─Properties/                                         # 项目属性文件夹
│   ├─Settings.Designer.cs                             # 自动生成的设置代码
│   ├─Settings.settings                                # 应用程序设置
│   └─PublishProfiles/                                 # 发布配置文件夹
│       ├─FolderProfile.pubxml                          # 文件夹发布配置文件
│       └─FolderProfile.pubxml.user                     # 文件夹发布配置用户文件
├─Quicker.csproj                                      # 项目的核心配置文件
├─Quicker.csproj.user                                 # 项目用户配置文件
├─Quicker.ico                                         # 应用图标
├─Quicker.sln                                         # 项目解决方案文件
├─README.md                                           # 项目说明文档
├─Resources/                                          # 资源文件夹
│   ├─Images/                                          # 图像资源文件夹
│   │   ├─AboutQuicker.png                              # 关于Quicker的图片
│   │   ├─ActionInformation.png                         # 动作信息的图片
│   │   ├─ActionPagesManager.jpg                        # 动作场景的图片
│   │   ├─Add.png                                       # 主面板添加动作的图片
│   │   ├─BasicSettingsButton.png                       # 基础设置按钮的图片
│   │   ├─BlackList.png                                 # 黑名单设置按钮的图片
│   │   ├─ClearButton.png                               # 下载窗口中清理按钮的图片
│   │   ├─CloseDownloadWindow.png                       # 关闭下载窗口的按钮图片
│   │   ├─CloseQuicker.png                              # 退出Quicker的图片
│   │   ├─CloseWindow.png                               # 关闭主面板的图片
│   │   ├─CommonSceneImage.png                          # 通用场景图片
│   │   ├─CopyAction.png                                # 复制动作按钮的图片
│   │   ├─DeleteImage.png                               # 删除动作按钮图像的图片
│   │   ├─DesktopSceneImage.png                         # 桌面场景的图片
│   │   ├─EditButton.png                                # 编辑动作的图片
│   │   ├─GlobalSceneImage.png                          # 全局场景图片
│   │   ├─Locked.png                                    # 锁住通用动作页的图片
│   │   ├─MessageImage.jpg                              # 消息弹窗的图片
│   │   ├─MoreSelection.png                             # 主面板更多选择的图片
│   │   ├─OpenFile.png                                  # 打开文件所在文件夹的图片
│   │   ├─OpenFileImage.png                             # 添加窗口中打开文件动作的图片
│   │   ├─OpenFolder.png                                # 打开文件夹的图片
│   │   ├─OpenMainWindow1.png                           # 菜单中打开主面板的图片
│   │   ├─OpenMainWindow2.png                           # 设置窗口中弹出面板按钮的图片
│   │   ├─OpenWebsiteImage.png                          # 添加窗口中打开网站动作的图片
│   │   ├─PasteAction.png                               # 粘贴动作按钮图片
│   │   ├─Pause.png                                     # 暂停Quicker的图片
│   │   ├─PinToDesktop.png                              # 订住主面板的图片
│   │   ├─Quicker_Enabled.png                           # Quicker运行中的图片
│   │   ├─Quicker_Disabled.png                          # Quicker暂停时的图片
│   │   ├─RestartQuicker.png                            # 重启Quicker的图片
│   │   ├─Search.png                                    # 搜索按钮的图片
│   │   ├─SelectLocalImage.png                          # 从本地文件选择动作按钮图像的图片
│   │   ├─SettingImage1.png                             # 菜单中打开设置窗口的按钮图片
│   │   ├─SettingImage2.png                             # 主面板打开设置面板的图片
│   │   ├─SettingWindow.png                             # 设置面板的图片
│   │   ├─StartApp.png                                  # 启动应用图标
│   │   ├─TaskbarSceneImage.png                         # 任务栏场景图片
│   │   ├─Target.png                                    # 目标图标
│   │   ├─UnLocked.png                                  # 不锁住通用动作页的图片
│   │   └─UnpinFromDesktop.png                          # 不订住Quicker的图片
│   └─Styles/                                         # 样式资源文件夹
│       ├─BorderStyles.xaml                            # 边框样式
│       ├─ButtonStyles.xaml                            # 按钮样式
│       │  └─ButtonStyles.xaml.cs
│       ├─CanvasStyles.xaml                            # 画布样式
│       ├─CheckBoxStyle.xaml                           # 勾选框样式
│       ├─ComboBoxStyle.xaml                           # 下拉框样式
│       │  └─ComboBoxStyle.xaml.cs
│       ├─CustomContextMenuStyle.xaml                  # 右键菜单样式
│       ├─GridStyles.xaml                              # 表格样式
│       ├─GroupBoxStyle.xaml                           # GroupBox 样式
│       ├─ImageStyles.xaml                             # 图片样式
│       ├─StackpanelStyles.xaml                        # 面板样式
│       ├─ScrollBarStyle.xaml                          # 滚动条样式
│       ├─SliderStyle.xaml                             # 滑动条样式
│       │  └─SliderStyle.xaml.cs
│       ├─TextBlockStyles.xaml                         # 文本块样式
│       ├─TextBoxStyles.xaml                           # 文本框样式
│       └─TooltipStyle.xaml                            # 提示框样式
├─UserControls/                                       # 自定义控件文件夹
│   ├─AddWindow/                                       # 添加窗口的自定义控件
│   │   ├─LoadExtension.xaml                            # 加载扩展界面
│   │   │   └─LoadExtension.xaml.cs
│   │   ├─OpenFile.xaml                                 # 打开文件界面
│   │   │   └─OpenFile.xaml.cs
│   │   └─OpenWebsite.xaml                              # 打开网站界面
│   │       └─OpenWebsite.xaml.cs
│   └─SettingWindow/                                   # 设置窗口的自定义控件
│       ├─Auxiliary_Functions/                          # 辅助功能控件文件夹
│       ├─BasicSettings/                                # 基础设置控件文件夹
│       │   ├─AboutQuickerGrid.xaml                      # 关于界面
│       │   │   └─AboutQuickerGrid.xaml.cs
│       │   ├─AppearanceGrid.xaml                        # 外观设置界面
│       │   │   └─AppearanceGrid.xaml.cs
│       │   ├─BlacklistGrid.xaml                         # 黑名单设置界面
│       │   │   └─BlacklistGrid.xaml.cs
│       │   ├─ConventionGrid.xaml                        # 常规设置界面
│       │   │   └─ConventionGrid.xaml.cs
│       │   ├─FunctionShortcutKeysGrid.xaml              # 快捷键设置界面
│       │   │   └─FunctionShortcutKeysGrid.xaml.cs
│       │   └─OpenMainWindowGrid.xaml                    # 打开主窗口设置界面
│       │       └─OpenMainWindowGrid.xaml.cs
│       ├─ColorPicker.xaml                              # 颜色选择器
│       │   └─ColorPicker.xaml.cs
│       └─Tools/                                        # 工具控件文件夹
│          └─ExtensionManagementGrid.xaml               # 扩展管理界面
│              └─ExtensionManagementGrid.xaml.cs
├─Windows/                                            # 界面文件夹
│   ├─AddWindows/                                     # 添加相关窗口（如添加场景、添加动作等）
│   │   ├─AddActionWindow.xaml                        # 添加动作窗口
│   │   │   └─AddActionWindow.xaml.cs
│   │   └─AddSceneWindow.xaml                         # 添加场景窗口
│   │       └─AddSceneWindow.xaml.cs
│   ├─FloatingWindows/                                # 悬浮窗口相关
│   │   ├─Windows/                                    # 悬浮窗口实现文件夹
│   │   │   ├─FloatingActionPageWindow.xaml           # 悬浮动作页窗口
│   │   │   │   └─FloatingActionPageWindow.xaml.cs
│   │   │   └─FloatingActionWindow.xaml               # 悬浮动作窗口
│   │   │       └─FloatingActionWindow.xaml.cs
│   │   └─ViewModels/                                 # 悬浮窗口ViewModel文件夹
│   │       ├─FloatingActionPageWindow.cs             # 悬浮动作页窗口ViewModel
│   │       └─FloatingActionWindow.cs                 # 悬浮动作窗口ViewModel
│   ├─EditWindows/                                     # 编辑窗口文件夹
│   │   ├─EditActionPageInfoWindow.xaml                 # 编辑动作页信息窗口
│   │   │   └─EditActionPageInfoWindow.xaml.cs
│   │   └─EditSceneWindow.xaml                          # 编辑场景窗口
│   │       └─EditSceneWindow.xaml.cs
│   ├─MainWindows/                                      # 主窗口文件夹
│   │   ├─ActionPageManageWindow.xaml                    # 动作页管理窗口
│   │   │   └─ActionPageManageWindow.xaml.cs
│   │   ├─FindAppsWindow.xaml                            # 查找应用窗口
│   │   │   └─FindAppsWindow.xaml.cs
│   │   ├─MainWindow/                                    # 主功能面板MVVM文件夹
│   │   │   ├─MainWindow.xaml                             # 主面板
│   │   │   │   └─MainWindow.xaml.cs
│   │   │   └─MainWindowViewModel.cs                      # 主面板ViewModel
│   │   ├─SearchWindow.xaml                              # 搜索窗口
│   │   │   └─SearchWindow.xaml.cs
│   │   ├─SelectImageWindow.xaml                         # 选择图片窗口
│   │   │   └─SelectImageWindow.xaml.cs
│   │   ├─SettingWindow.xaml                             #  设置窗口
│   │   │   └─SettingWindow.xaml.cs
│   │   └─UpdateWindow.xaml                              #  更新窗口
│   │       └─UpdateWindow.xaml.cs
│   ├─Menus/                                           # 菜单文件夹
│   │   ├─BaseMenuWindow.cs                             # 菜单窗口基类
│   │   ├─CreatActionMenu.xaml                          # 创建动作菜单界面
│   │   │   └─CreatActionMenu.xaml.cs
│   │   ├─CustomMenu.xaml                               # 自定义菜单界面
│   │   │   └─CustomMenu.xaml.cs
│   │   ├─EditSceneMenu.xaml                            # 编辑场景菜单界面
│   │   │   └─EditSceneMenu.xaml.cs
│   │   ├─OperationMenu.xaml                            # 操作菜单界面
│   │   │   └─OperationMenu.xaml.cs
│   │   ├─SelectActionPageMenu.xaml                     # 选择动作页菜单界面
│   │   │   └─SelectActionPageMenu.xaml.cs
│   │   └─SelectActionPageMenu.xaml.cs                  # 选择动作页菜单逻辑
│   └─ToolWindows/                                     # 工具窗口文件夹
│       ├─ActionInfoWindow.xaml                         # 动作信息窗口
│       │   └─ActionInfoWindow.xaml.cs
│       ├─DownloadWindow.xaml                           # 下载窗口
│       │   └─DownloadWindow.xaml.cs
│       ├─ImageCropWindow.xaml                          # 图片裁剪窗口
│       │   └─ImageCropWindow.xaml.cs
│       ├─LoadingWindow.xaml                            # 加载窗口
│       │   └─LoadingWindow.xaml.cs
│       ├─MessageWindow.xaml                            # 消息窗口
│       │   └─MessageWindow.xaml.cs
│       ├─SelectWindowWindow.xaml                       # 选择窗口
│       │   └─SelectWindowWindow.xaml.cs
│       └─ToastWindow.xaml                              # Toast提示窗口
│           └─ToastWindow.xaml.cs
```

</details>

---

<p align="center">
  ⭐️ 如果你觉得本项目有帮助，欢迎 Star 支持！也欢迎 Issue 和 PR 共同完善！
</p>