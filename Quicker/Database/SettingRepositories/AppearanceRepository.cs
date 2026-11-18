using Quicker.Models.Settings;
using Quicker.Database.Core;
using System.Data.SQLite;

namespace Quicker.Database.SettingRepositories
{
    /// <summary>
    /// Appearance 表数据访问：外观设置、主题选择、背景图片路径迁移
    /// 依赖 Convention 表的 IsDarkTheme 字段确定当前主题（原代码即有的跨表读取）
    /// </summary>
    public static class AppearanceRepository
    {
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public static SQLiteConnection OpenConnection() => DatabaseHelper.OpenConnection("Setting.db");

        /// <summary>
        /// 建表并插入浅色/深色两套默认数据（仅在数据库文件首次创建时调用）
        /// </summary>
        public static void EnsureInitialized()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createAppearanceCommand = new SQLiteCommand(SQLStatements.CreateAppearanceTable, connection); // 创建 SQLiteCommand 对象
            createAppearanceCommand.ExecuteNonQuery(); // 执行创建表的命令
            InsertDefaultAppearanceData(); // 插入默认数据
        }

        // 插入默认数据
        private static void InsertDefaultAppearanceData()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            var lightDefaults = ("Light", 77.6, 0.2, 0.0, 0.0, "#FFF3F3F3", "#FFD3D3D3", "#22F3F3F3", "#FFA1A1A1", "#FFFFFFFF", "#FFBEE6FD", "#FFF3F3F3", "#FFEAEAEA", "#FF000000", "#FF696969", "#D0FF8C00", "#FF666666", -1, -1, 12, 4, "", 1.0, 0, 0, false, false, false, false, false); // 使用参数元组封装默认值
            var darkDefaults = ("Dark", 77.6, 0.2, 0.0, 0.0, "#FFF3F3F3", "#FFD3D3D3", "#22F3F3F3", "#FFA1A1A1", "#FFFFFFFF", "#FFBEE6FD", "#FFF3F3F3", "#FFEAEAEA", "#FF000000", "#FF696969", "#D0FF8C00", "#FF666666", -1, -1, 12, 4, "", 1.0, 0, 0, false, false, false, false, false); // 使用参数元组封装默认值
            InsertSingleAppearance(connection, lightDefaults); // 插入浅色主题
            InsertSingleAppearance(connection, darkDefaults); // 插入深色主题
        }

        // 辅助方法，简化插入逻辑
        private static void InsertSingleAppearance(SQLiteConnection connection, (string, double, double, double, double, string, string, string, string, string, string, string, string, string, string, string, string, int, int, int, int, string, double, int, int, bool, bool, bool, bool, bool) defaults)
        {
            var parameters = new Dictionary<string, object>
            {
                ["@ThemeName"] = defaults.Item1,
                ["@ButtonSize"] = defaults.Item2, // 按钮大小
                ["@ButtonGap"] = defaults.Item3, // 按钮间隙
                ["@BorderWidth"] = defaults.Item4, // 边框宽度
                ["@ButtonCornerRadius"] = defaults.Item5, // 按钮圆角
                ["@BackgroundColor"] = defaults.Item6, // 背景颜色
                ["@BorderColor"] = defaults.Item7, // 边框颜色
                ["@ToolbarColor"] = defaults.Item8, // 工具栏颜色
                ["@ToolbarIconColor"] = defaults.Item9, // 工具栏图标颜色
                ["@ActionButtonColor"] = defaults.Item10, // 动作按钮颜色
                ["@ActionButtonMouseOverColor"] = defaults.Item11, // 动作按钮鼠标悬停颜色
                ["@BlankButtonColor"] = defaults.Item12, // 空白按钮颜色
                ["@BlankButtonMouseOverColor"] = defaults.Item13, // 空白按钮鼠标悬停颜色
                ["@ButtonTextColor"] = defaults.Item14, // 按钮文字颜色
                ["@ActionIconColor"] = defaults.Item15, // 动作图标颜色
                ["@TriggerKeyTextColor"] = defaults.Item16, // 触发键文字颜色
                ["@OtherIconColor"] = defaults.Item17, // 其他位置图标颜色
                ["@Font1"] = defaults.Item18, // 字体1
                ["@Font2"] = defaults.Item19, // 字体2
                ["@FontSize"] = defaults.Item20, // 字体大小
                ["@FontWeight"] = defaults.Item21, // 字体粗细
                ["@BackgroundImagePath"] = defaults.Item22, // 背景图片路径
                ["@BackgroundImageOpacity"] = defaults.Item23, // 背景图片不透明度
                ["@Blur"] = defaults.Item24, // 模糊模式
                ["@Win11CornerRadius"] = defaults.Item25, // Win11圆角模式
                ["@AutoHideTitleBar"] = defaults.Item26, // 自动缩小动作名称文字
                ["@ShowActionButtonMouseOver"] = defaults.Item27, // 鼠标悬浮在动作按钮上时，放大显示按钮
                ["@HideActionNameAfterIcon"] = defaults.Item28, // 设置动作图标后隐藏动作名称
                ["@ShowActionIconShadow"] = defaults.Item29, // 动作图标显示阴影
                ["@EnablePreview"] = defaults.Item30 // 开启预览功能
            };
            using var command = new SQLiteCommand(SQLStatements.InsertAppearance, connection);
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 获取外观设置（根据 Convention 表的 IsDarkTheme 自动选择当前主题）
        /// </summary>
        public static List<Appearance> GetAppearanceSettings()
        {
            // 获取当前主题状态：读取 Convention 表，确定 IsDarkTheme 的值
            var conventions = ConventionRepository.GetAllConventions().FirstOrDefault();
            bool isDarkTheme = conventions.IsDarkTheme;
            string themeName = isDarkTheme ? "Dark" : "Light";

            // 执行 Appearance 表的查询
            var appearances = new List<Appearance>();
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); // 开启只读事务
            using var command = new SQLiteCommand(SQLStatements.GetAppearanceSettings, connection);
            command.Parameters.AddWithValue("@ThemeName", themeName);  // 绑定确定的主题名称
            using var reader = command.ExecuteReader(); // 执行查询并获取数据读取器
            while (reader.Read())
            {
                appearances.Add(new Appearance
                {
                    ThemeName = reader.GetString(0), // 外观ID
                    ButtonSize = reader.GetDouble(1), // 按钮大小
                    ButtonGap = reader.GetDouble(2), // 按钮间隙
                    BorderWidth = reader.GetDouble(3), // 边框宽度
                    ButtonCornerRadius = reader.GetDouble(4), // 按钮圆角
                    BackgroundColor = reader.GetString(5), // 背景颜色
                    BorderColor = reader.GetString(6), // 边框颜色
                    ToolbarColor = reader.GetString(7), // 工具栏颜色
                    ToolbarIconColor = reader.GetString(8), // 工具栏图标颜色
                    ActionButtonColor = reader.GetString(9), // 动作按钮颜色
                    ActionButtonMouseOverColor = reader.GetString(10), // 动作按钮悬浮颜色
                    BlankButtonColor = reader.GetString(11), // 空白按钮颜色
                    BlankButtonMouseOverColor = reader.GetString(12), // 空白按钮悬浮颜色
                    ButtonTextColor = reader.GetString(13), // 按钮文字颜色
                    Font1 = reader.GetInt32(14), // 字体1
                    Font2 = reader.GetInt32(15), // 字体2
                    FontSize = reader.GetDouble(16), // 字体大小
                    FontWeight = reader.GetInt32(17), // 字体粗细
                    BackgroundImagePath = reader.GetString(18), // 背景图片路径
                    BackgroundImageOpacity = reader.GetDouble(19), // 背景图片不透明度
                    Blur = reader.GetInt32(20), // 模糊模式
                    Win11CornerRadius = reader.GetInt32(21), // Win11圆角模式
                    AutoHideTitleBar = reader.GetBoolean(22), // 自动隐藏标题栏
                    ShowActionButtonMouseOver = reader.GetBoolean(23), // 动作按钮悬浮放大
                    HideActionNameAfterIcon = reader.GetBoolean(24), // 设置动作图标后隐藏名称
                    ShowActionIconShadow = reader.GetBoolean(25), // 动作图标显示阴影
                    EnablePreview = reader.GetBoolean(26) // 启用预览
                });
            }
            transaction.Commit(); // 提交事务
            return appearances; // 返回当前主题的外观设置
        }

        /// <summary>
        /// 更新外观设置
        /// </summary>
        public static void UpdateAppearance(Appearance appearance)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.UpdateAppearance, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@ThemeName", appearance.ThemeName); // 主题名称
            command.Parameters.AddWithValue("@ButtonSize", appearance.ButtonSize); // 按钮大小
            command.Parameters.AddWithValue("@ButtonGap", appearance.ButtonGap); // 按钮间隙
            command.Parameters.AddWithValue("@BorderWidth", appearance.BorderWidth); // 边框宽度
            command.Parameters.AddWithValue("@ButtonCornerRadius", appearance.ButtonCornerRadius); // 按钮圆角
            command.Parameters.AddWithValue("@BackgroundColor", appearance.BackgroundColor); // 背景颜色
            command.Parameters.AddWithValue("@BorderColor", appearance.BorderColor); // 边框颜色
            command.Parameters.AddWithValue("@ToolbarColor", appearance.ToolbarColor); // 工具栏颜色
            command.Parameters.AddWithValue("@ToolbarIconColor", appearance.ToolbarIconColor); // 工具栏图标颜色
            command.Parameters.AddWithValue("@ActionButtonColor", appearance.ActionButtonColor); // 动作按钮颜色
            command.Parameters.AddWithValue("@ActionButtonMouseOverColor", appearance.ActionButtonMouseOverColor); // 动作按钮悬浮颜色
            command.Parameters.AddWithValue("@BlankButtonColor", appearance.BlankButtonColor); // 空白按钮颜色
            command.Parameters.AddWithValue("@BlankButtonMouseOverColor", appearance.BlankButtonMouseOverColor); // 空白按钮悬浮颜色
            command.Parameters.AddWithValue("@ButtonTextColor", appearance.ButtonTextColor); // 按钮文字颜色
            command.Parameters.AddWithValue("@Font1", appearance.Font1); // 字体1
            command.Parameters.AddWithValue("@Font2", appearance.Font2); // 字体2
            command.Parameters.AddWithValue("@FontSize", appearance.FontSize); // 字体大小
            command.Parameters.AddWithValue("@FontWeight", appearance.FontWeight); // 字体粗细
            command.Parameters.AddWithValue("@BackgroundImagePath", appearance.BackgroundImagePath); // 背景图片路径
            command.Parameters.AddWithValue("@BackgroundImageOpacity", appearance.BackgroundImageOpacity); // 背景图片不透明度
            command.Parameters.AddWithValue("@Blur", appearance.Blur); // 模糊模式
            command.Parameters.AddWithValue("@Win11CornerRadius", appearance.Win11CornerRadius); // Win11圆角模式
            command.Parameters.AddWithValue("@AutoHideTitleBar", appearance.AutoHideTitleBar); // 自动隐藏标题栏
            command.Parameters.AddWithValue("@ShowActionButtonMouseOver", appearance.ShowActionButtonMouseOver); // 动作按钮悬浮放大
            command.Parameters.AddWithValue("@HideActionNameAfterIcon", appearance.HideActionNameAfterIcon); // 设置动作图标后隐藏名称
            command.Parameters.AddWithValue("@ShowActionIconShadow", appearance.ShowActionIconShadow); // 动作图标显示阴影
            command.Parameters.AddWithValue("@EnablePreview", appearance.EnablePreview); // 启用预览
            command.ExecuteNonQuery(); // 执行命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 将 Appearance 表中的 BackgroundImagePath 的旧根路径批量替换为新根路径。
        /// </summary>
        /// <param name="oldRoot">旧图片根路径（例如 C:\\Users\\LENOVO\\AppData\\Roaming\\Anonymity\\Quicker）</param>
        /// <param name="newRoot">新图片根路径（例如 C:\\Downloads）</param>
        public static void MigrateAppearanceBackgroundImagePathRoot(string oldRoot, string newRoot)
        {
            if (string.IsNullOrWhiteSpace(oldRoot) || string.IsNullOrWhiteSpace(newRoot))
            {
                return;
            }

            string normalizedOld = oldRoot.Trim();
            string normalizedNew = newRoot.Trim();
            if (string.Equals(normalizedOld, normalizedNew, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string sql = "UPDATE Appearance SET " +
                         "BackgroundImagePath = REPLACE(COALESCE(BackgroundImagePath, ''), @OldRoot, @NewRoot) " +
                         "WHERE BackgroundImagePath LIKE @OldLike";
            using var cmd = new SQLiteCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@OldRoot", normalizedOld);
            cmd.Parameters.AddWithValue("@NewRoot", normalizedNew);
            cmd.Parameters.AddWithValue("@OldLike", normalizedOld + "%");
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }

        // SQL 语句
        private static class SQLStatements
        {
            public const string CreateAppearanceTable = @"
            CREATE TABLE IF NOT EXISTS Appearance
            (
                ThemeName TEXT PRIMARY KEY NOT NULL,
                ButtonSize REAL,
                ButtonGap REAL,
                BorderWidth REAL,
                ButtonCornerRadius REAL,
                BackgroundColor TEXT,
                BorderColor TEXT,
                ToolbarColor TEXT,
                ToolbarIconColor TEXT,
                ActionButtonColor TEXT,
                ActionButtonMouseOverColor TEXT,
                BlankButtonColor TEXT,
                BlankButtonMouseOverColor TEXT,
                ButtonTextColor TEXT,
                Font1 INTEGER,
                Font2 INTEGER,
                FontSize REAL,
                FontWeight INTEGER,
                BackgroundImagePath TEXT,
                BackgroundImageOpacity REAL,
                Blur INTEGER,
                Win11CornerRadius INTEGER,
                AutoHideTitleBar BOOLEAN,
                ShowActionButtonMouseOver BOOLEAN,
                HideActionNameAfterIcon BOOLEAN,
                ShowActionIconShadow BOOLEAN,
                EnablePreview BOOLEAN
            );";
            public const string InsertAppearance = @"
            INSERT INTO Appearance
            (
                ThemeName,
                ButtonSize,
                ButtonGap,
                BorderWidth,
                ButtonCornerRadius,
                BackgroundColor,
                BorderColor,
                ToolbarColor,
                ToolbarIconColor,
                ActionButtonColor,
                ActionButtonMouseOverColor,
                BlankButtonColor,
                BlankButtonMouseOverColor,
                ButtonTextColor,
                Font1,
                Font2,
                FontSize,
                FontWeight,
                BackgroundImagePath,
                BackgroundImageOpacity,
                Blur,
                Win11CornerRadius,
                AutoHideTitleBar,
                ShowActionButtonMouseOver,
                HideActionNameAfterIcon,
                ShowActionIconShadow,
                EnablePreview
            )
            VALUES
            (
                @ThemeName,
                @ButtonSize,
                @ButtonGap,
                @BorderWidth,
                @ButtonCornerRadius,
                @BackgroundColor,
                @BorderColor,
                @ToolbarColor,
                @ToolbarIconColor,
                @ActionButtonColor,
                @ActionButtonMouseOverColor,
                @BlankButtonColor,
                @BlankButtonMouseOverColor,
                @ButtonTextColor,
                @Font1,
                @Font2,
                @FontSize,
                @FontWeight,
                @BackgroundImagePath,
                @BackgroundImageOpacity,
                @Blur,
                @Win11CornerRadius,
                @AutoHideTitleBar,
                @ShowActionButtonMouseOver,
                @HideActionNameAfterIcon,
                @ShowActionIconShadow,
                @EnablePreview
            );"; // 插入外观设置
            public const string GetAppearanceSettings = "SELECT * FROM Appearance WHERE ThemeName = @ThemeName;"; // 获取所有外观设置
            public const string UpdateAppearance = @"
            UPDATE Appearance SET
                ButtonSize = @ButtonSize,
                ButtonGap = @ButtonGap,
                BorderWidth = @BorderWidth,
                ButtonCornerRadius = @ButtonCornerRadius,
                BackgroundColor = @BackgroundColor,
                BorderColor = @BorderColor,
                ToolbarColor = @ToolbarColor,
                ToolbarIconColor = @ToolbarIconColor,
                ActionButtonColor = @ActionButtonColor,
                ActionButtonMouseOverColor = @ActionButtonMouseOverColor,
                BlankButtonColor = @BlankButtonColor,
                BlankButtonMouseOverColor = @BlankButtonMouseOverColor,
                ButtonTextColor = @ButtonTextColor,
                Font1 = @Font1,
                Font2 = @Font2,
                FontSize = @FontSize,
                FontWeight = @FontWeight,
                BackgroundImagePath = @BackgroundImagePath,
                BackgroundImageOpacity = @BackgroundImageOpacity,
                Blur = @Blur,
                Win11CornerRadius = @Win11CornerRadius,
                AutoHideTitleBar = @AutoHideTitleBar,
                ShowActionButtonMouseOver = @ShowActionButtonMouseOver,
                HideActionNameAfterIcon = @HideActionNameAfterIcon,
                ShowActionIconShadow = @ShowActionIconShadow,
                EnablePreview = @EnablePreview
            WHERE ThemeName = @ThemeName;"; // 更新外观设置
        }
    }
}