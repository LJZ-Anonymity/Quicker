using System.Runtime.InteropServices;
using Quicker.Contracts.Extensions;
using System.Threading.Channels;
using Quicker.Contracts.Hosting;
using System.Runtime.Loader;
using System.Reflection;
using System.Text.Json;
using System.IO.Pipes;
using System.Windows;
using System.Text;
using System.IO;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // 初始化日志
        HostLog.Init();
        HostLog.Write("=== ExtensionHost 启动 ===");

        if (args.Length < 2)
        {
            HostLog.Write("参数不足，用法: ExtensionHost <modulePath> <pipeName>");
            return 1;
        }

        try { Thread.CurrentThread.TrySetApartmentState(ApartmentState.STA); } catch { }

        string modulePath = args[0];
        string pipeName = args[1];
        HostLog.Write($"目标模块路径: {modulePath}");
        HostLog.Write($"管道名称: {pipeName}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            HostLog.Write($"[致命异常] Unhandled exception: {e.ExceptionObject}");
            Environment.Exit(1);
        };

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        var exitCode = 0;
        try
        {
            HostLog.Write("正在加载模块...");
            var loadResult = app.Dispatcher.Invoke(() => LoadModule(modulePath));
            if (!loadResult.Ok || loadResult.ModuleInstance is null || loadResult.ModuleType is null)
            {
                HostLog.Write($"[加载失败] {loadResult.Error}");
                exitCode = 2;
                app.Shutdown(exitCode);
                return exitCode;
            }
            HostLog.Write("模块加载成功，准备启动通信循环。");

            Task.Run(() =>
            {
                try
                {
                    RunCommandLoop(app, loadResult.ModuleInstance, loadResult.ModuleType, pipeName);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // 主程序主动断开了管道，视为正常退出，不记录异常，也不标记为错误码
                    HostLog.Write("主程序断开了连接，通信循环结束。");
                }
                catch (Exception ex)
                {
                    HostLog.Write($"[通信循环异常] {ex}");
                    exitCode = 3;
                }
                finally
                {
                    app.Dispatcher.Invoke(() => app.Shutdown(exitCode));
                }
            });

            app.Run();
        }
        catch (Exception ex)
        {
            HostLog.Write($"[致命错误] Fatal error: {ex}");
            exitCode = 1;
            app.Shutdown(exitCode);
        }

        HostLog.Write($"=== ExtensionHost 退出，ExitCode: {exitCode} ===");
        return exitCode;
    }

    private static void RunCommandLoop(Application app, object moduleInstance, Type moduleType, string pipeName)
    {
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
        server.WaitForConnection();
        HostLog.Write("命名管道已连接。");

        using var reader = new StreamReader(server, Encoding.UTF8);
        var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true };

        var outbox = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        Task writerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (object payload in outbox.Reader.ReadAllAsync())
                {
                    try { await writer.WriteLineAsync(JsonSerializer.Serialize(payload)); }
                    catch { break; } // 写入断开直接退出
                }
            }
            catch { }
        });

        ExtensionHostBridge.OnShowToast = (msg, type) =>
        {
            outbox.Writer.TryWrite(new HostCommand("toast", msg, type));
        };

        bool shouldExit = false;
        try
        {
            while (!shouldExit && server.IsConnected)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                HostCommand? command;
                try { command = JsonSerializer.Deserialize<HostCommand>(line); }
                catch { continue; }

                if (command is null) continue;

                HostLog.Write($"收到命令: {command.Action}");
                HostResult response = app.Dispatcher.Invoke(() => ExecuteCommand(moduleInstance, moduleType, command));

                if (response.Shutdown) shouldExit = true;
                outbox.Writer.TryWrite(response);
            }
        }
        finally
        {
            ExtensionHostBridge.OnShowToast = null;
            outbox.Writer.TryComplete();
            try { writerTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
            try { writer.Dispose(); } catch { }

            HostLog.Write("通信循环结束。");
        }
    }

    /// <summary>
    /// 执行命令（所有调用都有异常保护）
    /// </summary>
    private static HostResult ExecuteCommand(object moduleInstance, Type moduleType, HostCommand command)
    {
        return command.Action switch
        {
            "metadata" => GetMetadata(moduleInstance, moduleType),
            "activate" => CallMethodSafely(moduleInstance, moduleType, "Activate"),
            "deactivate" => CallMethodSafely(moduleInstance, moduleType, "Deactivate"),
            "shutdown" => new HostResult(true, null, null, true),
            _ => new HostResult(false, $"Unknown action: {command.Action}", null, false)
        };
    }

    /// <summary>
    /// 加载模块
    /// </summary>
    private static ModuleLoadResult LoadModule(string modulePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(modulePath);
            if (!File.Exists(fullPath)) return new ModuleLoadResult(false, $"Module file not found: {fullPath}", null, null);

            var alc = CreateLoadContext(fullPath);
            Assembly asm = alc.LoadFromAssemblyPath(fullPath);
            HostLog.Write($"成功加载 DLL: {asm.FullName}");

            Type? moduleType = asm.GetTypes().FirstOrDefault(t => t.IsClass && !t.IsAbstract && ImplementsExtensionInterface(t));

            if (moduleType is null) return new ModuleLoadResult(false, "未找到 IExtensionModule 实现。", null, null);

            object? instance = Activator.CreateInstance(moduleType);
            if (instance is null) return new ModuleLoadResult(false, "无法实例化扩展模块。", null, null);

            return new ModuleLoadResult(true, null, moduleType, instance);
        }
        catch (Exception ex)
        {
            return new ModuleLoadResult(false, ex.ToString(), null, null);
        }
    }

    /// <summary>
    /// 创建加载上下文
    /// </summary>
    private static AssemblyLoadContext CreateLoadContext(string moduleFullPath)
    {
        var resolver = new AssemblyDependencyResolver(moduleFullPath);
        var alc = new AssemblyLoadContext($"ext-{Guid.NewGuid():N}", isCollectible: true);

        alc.Resolving += (context, assemblyName) =>
        {
            // 过滤掉 .resources 避免干扰日志
            if (assemblyName.Name != null && assemblyName.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                var shared = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
                if (shared is not null) return shared;
            }
            catch (FileNotFoundException) { }

            var path = resolver.ResolveAssemblyToPath(assemblyName);
            if (path is not null) return context.LoadFromAssemblyPath(path);

            var fallback = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
            if (File.Exists(fallback)) return context.LoadFromAssemblyPath(fallback);

            HostLog.Write($"[依赖解析失败] 找不到 {assemblyName.Name}");
            return null;
        };

        alc.ResolvingUnmanagedDll += (context, libraryName) =>
        {
            var path = resolver.ResolveUnmanagedDllToPath(libraryName);
            if (path is not null) return NativeLibrary.Load(path);

            var candidate = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : libraryName + ".dll";
            var fallback = Path.Combine(AppContext.BaseDirectory, candidate);
            if (File.Exists(fallback)) return NativeLibrary.Load(fallback);
            return IntPtr.Zero;
        };

        return alc;
    }

    /// <summary>
    /// 判断类型是否实现 IExtensionModule 接口
    /// </summary>
    private static bool ImplementsExtensionInterface(Type type)
    {
        return type.GetInterfaces().Any(i => i.FullName == "Quicker.Contracts.Extensions.IExtensionModule");
    }

    /// <summary>
    /// 获取模块元数据
    /// </summary>
    private static HostResult GetMetadata(object module, Type moduleType)
    {
        try
        {
            var meta = new ModuleMetadata(
                Name: GetStringProperty(module, moduleType, "Name"),
                Version: GetStringProperty(module, moduleType, "Version"),
                Author: GetStringProperty(module, moduleType, "Author"),
                Description: GetStringProperty(module, moduleType, "Description"),
                HasContextMenu: GetBoolProperty(module, moduleType, "HasContextMenu"),
                IconBase64: GetIconBase64(module, moduleType, "IconData"));

            return new HostResult(true, null, meta, false);
        }
        catch (Exception ex) { return new HostResult(false, ex.ToString(), null, false); }
    }

    /// <summary>
    /// 安全调用方法（异常保护）
    /// </summary>
    private static HostResult CallMethodSafely(object module, Type moduleType, string methodName)
    {
        try
        {
            // 优先从接口取方法，避免被 Window.Activate() 之类的基类同名方法抢先匹配
            var iface = moduleType.GetInterfaces()
                .FirstOrDefault(i => i.FullName == "Quicker.Contracts.Extensions.IExtensionModule");
            var method = iface?.GetMethod(methodName);

            // 兜底：保持原来的查找逻辑
            method ??= moduleType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            method ??= moduleType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == methodName || m.Name.EndsWith("." + methodName, StringComparison.Ordinal));

            if (method is null) return new HostResult(false, $"未找到方法 {methodName}", null, false);

            HostLog.Write($"开始执行方法: {methodName}（{method.DeclaringType?.Name}）");
            method.Invoke(module, null);
            HostLog.Write($"方法执行完成: {methodName}");
            return new HostResult(true, null, null, false);
        }
        catch (Exception ex)
        {
            string errorDetail = ex.InnerException?.ToString() ?? ex.ToString();
            HostLog.Write($"[方法执行异常] {methodName} 抛出异常:\n{errorDetail}");
            return new HostResult(false, errorDetail, null, false);
        }
    }

    /// <summary>
    /// 获取字符串属性
    /// </summary>
    private static string GetStringProperty(object module, Type type, string propertyName)
    {
        try { return type.GetProperty(propertyName)?.GetValue(module)?.ToString() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static bool GetBoolProperty(object module, Type type, string propertyName)
    {
        try { return type.GetProperty(propertyName)?.GetValue(module) is bool b && b; }
        catch { return false; }
    }

    private static string? GetIconBase64(object module, Type type, string propertyName)
    {
        try
        {
            object? value = type.GetProperty(propertyName)?.GetValue(module);
            return value is byte[] buffer && buffer.Length > 0 ? Convert.ToBase64String(buffer) : null;
        }
        catch { return null; }
    }

    internal record ModuleLoadResult(bool Ok, string? Error, Type? ModuleType, object? ModuleInstance);
}

// 日志静态类
internal static class HostLog
{
    private static readonly object _lock = new();
    private static string _logFile = "";

    public static void Init()
    {
        try
        {
            string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            _logFile = Path.Combine(logDir, "ExtensionHost.log");
        }
        catch { }
    }

    public static void Write(string message)
    {
        if (string.IsNullOrEmpty(_logFile)) return;
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
            }
        }
        catch { }
    }
}