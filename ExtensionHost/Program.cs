using System.Runtime.InteropServices;
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
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ExtensionHost <modulePath> <pipeName>");
            return 1;
        }

        // 确保 STA 线程
        try { Thread.CurrentThread.TrySetApartmentState(ApartmentState.STA); } catch { }

        // 设置资源程序集（用于 pack URI）
        TrySetResourceAssembly();

        string modulePath = args[0];
        string pipeName = args[1];

        // 全局异常处理，确保扩展崩溃不会导致宿主进程崩溃
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");
            Environment.Exit(1);
        };

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        var exitCode = 0;
        try
        {
            // 在 UI 线程中加载模块
            var loadResult = app.Dispatcher.Invoke(() => LoadModule(modulePath));
            if (!loadResult.Ok || loadResult.ModuleInstance is null || loadResult.ModuleType is null)
            {
                Console.Error.WriteLine(loadResult.Error ?? "Failed to load module.");
                exitCode = 2;
                app.Shutdown(exitCode);
                return exitCode;
            }

            // 在后台线程处理管道通信
            Task.Run(() =>
            {
                try
                {
                    RunCommandLoop(app, loadResult.ModuleInstance, loadResult.ModuleType, pipeName);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Host error: {ex}");
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
            Console.Error.WriteLine($"Fatal error: {ex}");
            exitCode = 1;
            app.Shutdown(exitCode);
        }

        return exitCode;
    }

    /// <summary>
    /// 运行命令循环
    /// </summary>
    private static void RunCommandLoop(Application app, object moduleInstance, Type moduleType, string pipeName)
    {
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.WriteThrough);

        server.WaitForConnection();

        using var reader = new StreamReader(server, Encoding.UTF8);
        using var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true };

        bool shouldExit = false;
        while (!shouldExit && server.IsConnected)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            HostCommand? command = null;
            try
            {
                command = JsonSerializer.Deserialize<HostCommand>(line);
            }
            catch
            {
                // 忽略格式错误的命令
                continue;
            }

            if (command is null)
            {
                continue;
            }

            // 在 UI 线程中执行命令，确保所有扩展调用都有异常保护
            HostResponse response = app.Dispatcher.Invoke(() => ExecuteCommand(moduleInstance, moduleType, command));

            if (response.Shutdown)
            {
                shouldExit = true;
            }

            try
            {
                writer.WriteLine(JsonSerializer.Serialize(response));
            }
            catch
            {
                // 写入失败，退出循环
                break;
            }
        }
    }

    /// <summary>
    /// 执行命令（所有调用都有异常保护）
    /// </summary>
    private static HostResponse ExecuteCommand(object moduleInstance, Type moduleType, HostCommand command)
    {
        return command.Action switch
        {
            "metadata" => GetMetadata(moduleInstance, moduleType),
            "activate" => CallMethodSafely(moduleInstance, moduleType, "Activate"),
            "deactivate" => CallMethodSafely(moduleInstance, moduleType, "Deactivate"),
            "shutdown" => new HostResponse(true, null, null, true),
            _ => new HostResponse(false, $"Unknown action: {command.Action}", null, false)
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
            if (!File.Exists(fullPath))
            {
                return new ModuleLoadResult(false, $"Module file not found: {fullPath}", null, null);
            }

            var alc = CreateLoadContext(fullPath);
            Assembly asm = alc.LoadFromAssemblyPath(fullPath);

            // 查找实现 IExtensionModule 的类型
            Type? moduleType = asm
                .GetTypes()
                .FirstOrDefault(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    ImplementsExtensionInterface(t));

            if (moduleType is null)
            {
                return new ModuleLoadResult(false, "未找到 IExtensionModule 实现。", null, null);
            }

            // 实例化模块（异常会被捕获）
            object? instance = Activator.CreateInstance(moduleType);
            if (instance is null)
            {
                return new ModuleLoadResult(false, "无法实例化扩展模块。", null, null);
            }

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
            // 先尝试从扩展目录解析
            var path = resolver.ResolveAssemblyToPath(assemblyName);
            if (path is not null)
            {
                return context.LoadFromAssemblyPath(path);
            }

            // 再尝试主程序目录
            var fallback = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
            if (File.Exists(fallback))
            {
                return context.LoadFromAssemblyPath(fallback);
            }

            return null;
        };

        alc.ResolvingUnmanagedDll += (context, libraryName) =>
        {
            var path = resolver.ResolveUnmanagedDllToPath(libraryName);
            if (path is not null)
            {
                return NativeLibrary.Load(path);
            }

            var candidate = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? libraryName
                : libraryName + ".dll";

            var fallback = Path.Combine(AppContext.BaseDirectory, candidate);
            if (File.Exists(fallback))
            {
                return NativeLibrary.Load(fallback);
            }

            return IntPtr.Zero;
        };

        return alc;
    }

    /// <summary>
    /// 判断类型是否实现 IExtensionModule 接口
    /// </summary>
    private static bool ImplementsExtensionInterface(Type type)
    {
        return type.GetInterfaces().Any(i => i.FullName == "Quicker.Extend.IExtensionModule");
    }

    /// <summary>
    /// 获取模块元数据
    /// </summary>
    private static HostResponse GetMetadata(object module, Type moduleType)
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

            return new HostResponse(true, null, meta, false);
        }
        catch (Exception ex)
        {
            return new HostResponse(false, ex.ToString(), null, false);
        }
    }

    /// <summary>
    /// 安全调用方法（异常保护）
    /// </summary>
    private static HostResponse CallMethodSafely(object module, Type moduleType, string methodName)
    {
        try
        {
            // 先尝试公共实例方法
            var method = moduleType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

            // 如果找不到，尝试显式接口实现
            method ??= moduleType
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(m =>
                        string.Equals(m.Name, methodName, StringComparison.Ordinal) ||
                        m.Name.EndsWith("." + methodName, StringComparison.Ordinal));

            if (method is null)
            {
                return new HostResponse(false, $"未找到方法 {methodName}", null, false);
            }

            // 调用方法（异常会被捕获）
            method.Invoke(module, null);
            return new HostResponse(true, null, null, false);
        }
        catch (Exception ex)
        {
            // 捕获所有异常，确保扩展崩溃不会导致宿主进程崩溃
            return new HostResponse(false, ex.InnerException?.ToString() ?? ex.ToString(), null, false);
        }
    }

    /// <summary>
    /// 设置资源程序集
    /// </summary>
    private static void TrySetResourceAssembly()
    {
        try
        {
            string mainAsm = Path.Combine(AppContext.BaseDirectory, "Quicker.dll");
            if (File.Exists(mainAsm))
            {
                Application.ResourceAssembly = Assembly.LoadFrom(mainAsm);
            }
        }
        catch
        {
            // 忽略资源程序集绑定错误
        }
    }

    /// <summary>
    /// 获取字符串属性
    /// </summary>
    private static string GetStringProperty(object module, Type type, string propertyName)
    {
        try
        {
            return type.GetProperty(propertyName)?.GetValue(module)?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取布尔属性
    /// </summary>
    private static bool GetBoolProperty(object module, Type type, string propertyName)
    {
        try
        {
            object? value = type.GetProperty(propertyName)?.GetValue(module);
            return value is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取图标属性
    /// </summary>
    private static string? GetIconBase64(object module, Type type, string propertyName)
    {
        try
        {
            object? value = type.GetProperty(propertyName)?.GetValue(module);
            return value is byte[] buffer && buffer.Length > 0 ? Convert.ToBase64String(buffer) : null;
        }
        catch
        {
            return null;
        }
    }

    // 数据记录类型
    internal record HostCommand(string Action);
    internal record HostResponse(bool Ok, string? Error, ModuleMetadata? Metadata, bool Shutdown);
    internal record ModuleLoadResult(bool Ok, string? Error, Type? ModuleType, object? ModuleInstance);
    internal record ModuleMetadata(string Name, string Version, string Author, string Description, bool HasContextMenu, string? IconBase64);
}
