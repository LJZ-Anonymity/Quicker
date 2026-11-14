using Quicker.Contracts.Constants;
using Quicker.Contracts.Hosting;
using System.Diagnostics;
using System.Text.Json;
using Quicker.Managers;
using System.IO.Pipes;
using Quicker.Models;
using System.Windows;
using System.Text;
using System.IO;

namespace Quicker.Extend
{
    /// <summary>
    /// 通过外部宿主进程加载扩展，防止扩展崩溃牵连主程序。
    /// </summary>
    public class ModuleLoader
    {
        private readonly Dictionary<string, ExtensionHostHandle> _hosts = new();
        private readonly ToastManager _toast = new();

        /// <summary>
        /// 加载并激活扩展模块（进程隔离）。
        /// </summary>
        /// <param name="modulePath">扩展 DLL 路径</param>
        public void LoadModule(string modulePath)
        {
            try
            {
                string assemblyName = Path.GetFileNameWithoutExtension(modulePath);

                // 检查是否已经加载
                if (_hosts.TryGetValue(assemblyName, out var existingHandle))
                {
                    // 如果进程还在运行，说明确实加载了，直接返回
                    if (existingHandle.Process != null && !existingHandle.Process.HasExited)
                    {
                        _toast.Show($"扩展 {assemblyName} 已加载。", ToastType.Common);
                        return;
                    }

                    // 如果进程已经退出了（崩溃了），清理掉旧的死句柄，准备重新拉起
                    existingHandle.Dispose();
                    _hosts.Remove(assemblyName);
                    _toast.Show($"检测到扩展 {assemblyName} 已崩溃，正在重新拉起...", ToastType.Common);
                }

                string hostPath = ResolveHostExecutable();
                string pipeName = $"{HostProtocol.PipePrefix}{Guid.NewGuid():N}";

                var process = StartHostProcess(hostPath, modulePath, pipeName);
                var handle = ConnectToHost(pipeName, process);

                var metadata = RequestMetadata(handle);
                if (metadata is null)
                {
                    throw new InvalidOperationException("无法读取扩展元数据");
                }

                var activateResult = SendCommand(handle, new HostCommand("activate"));
                if (!activateResult.Ok)
                {
                    throw new InvalidOperationException(activateResult.Error ?? "激活失败");
                }

                handle.Process = process;
                _hosts[assemblyName] = handle;

                // 绑定进程退出事件，监听运行时的意外崩溃
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    // 跨线程安全：后台进程退出事件触发时，UI 线程可能正在执行其他操作
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        // 如果进程还在字典里，说明不是我们主动卸载的（主动卸载会提前移出字典），而是意外崩溃
                        if (_hosts.TryGetValue(assemblyName, out var h) && h.Process == process)
                        {
                            _toast.Show($"警告：扩展 {assemblyName} 意外崩溃！", ToastType.Error);
                            h.Dispose();
                            _hosts.Remove(assemblyName);
                        }
                    });
                };

                _toast.Show($"扩展 {metadata.Name} 已在隔离进程中激活。", ToastType.Success);
            }
            catch (Exception ex)
            {
                _toast.Show($"加载模块时出错：{ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// 停用并卸载扩展。
        /// </summary>
        /// <param name="moduleName">扩展名称</param>
        public void UnloadModule(string moduleName)
        {
            if (!_hosts.TryGetValue(moduleName, out var handle))
            {
                return;
            }

            // 先将其从字典移除，这样 process.Exited 事件触发时就不会误报“意外崩溃”
            _hosts.Remove(moduleName);

            // 解除退出事件监听
            if (handle.Process != null)
            {
                try { handle.Process.EnableRaisingEvents = false; } catch { }
            }

            try
            {
                _ = SendCommand(handle, new HostCommand("deactivate"));
            }
            catch
            {
                // ignore deactivate errors
            }
            finally
            {
                try
                {
                    SendCommand(handle, new HostCommand("shutdown"));
                }
                catch
                {
                    // ignore
                }

                handle.Dispose();
            }
        }

        /// <summary>
        /// 连接到扩展宿主。
        /// </summary>
        private static ExtensionHostHandle ConnectToHost(string pipeName, Process process)
        {
            var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            // 等待管道连接，同时监控进程是否提前退出
            var timeoutMs = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
            var sw = Stopwatch.StartNew();
            while (!client.IsConnected && sw.ElapsedMilliseconds < timeoutMs)
            {
                if (process.HasExited)
                {
                    throw new IOException($"扩展宿主已退出：{ReadProcessError(process)}");
                }
                try
                {
                    client.Connect(200);
                }
                catch (TimeoutException)
                {
                    // 继续循环，直到总超时
                }
            }

            if (!client.IsConnected)
            {
                throw new TimeoutException("连接扩展宿主超时");
            }

            var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
            var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };

            return new ExtensionHostHandle(client, reader, writer);
        }

        /// <summary>
        /// 启动扩展宿主进程。
        /// </summary>
        private static Process StartHostProcess(string hostPath, string modulePath, string pipeName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = hostPath,
                ArgumentList = { modulePath, pipeName },
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            var process = Process.Start(psi)
                          ?? throw new InvalidOperationException("无法启动扩展宿主进程");

            // 如果进程瞬间退出，直接抛出可读错误
            if (process.HasExited)
            {
                string error = ReadProcessError(process);
                throw new InvalidOperationException($"扩展宿主启动失败：{error}");
            }

            return process;
        }

        /// <summary>
        /// 读取进程错误。
        /// </summary>
        private static string ReadProcessError(Process process)
        {
            try
            {
                string err = process.StandardError.ReadToEnd();
                if (string.IsNullOrWhiteSpace(err))
                {
                    err = process.StandardOutput.ReadToEnd();
                }
                return string.IsNullOrWhiteSpace(err) ? $"exit code {process.ExitCode}" : err.Trim();
            }
            catch
            {
                return $"exit code {process.ExitCode}";
            }
        }

        /// <summary>
        /// 请求扩展元数据。
        /// </summary>
        private ModuleMetadata? RequestMetadata(ExtensionHostHandle handle)
        {
            var response = SendCommand(handle, new HostCommand("metadata"));
            return response.Metadata;
        }

        private HostResult SendCommand(ExtensionHostHandle handle, HostCommand command)
        {
            lock (handle.SyncRoot)
            {
                if (handle.Process is not null && handle.Process.HasExited)
                {
                    throw new IOException($"扩展宿主已退出：{ReadProcessError(handle.Process)}");
                }

                string payload = JsonSerializer.Serialize(command);
                handle.Writer.WriteLine(payload);
                string? line = handle.Reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (handle.Process is not null && handle.Process.HasExited)
                    {
                        throw new IOException($"扩展宿主已退出：{ReadProcessError(handle.Process)}");
                    }

                    throw new IOException("扩展宿主无响应");
                }

                var response = JsonSerializer.Deserialize<HostResult>(line);
                if (response is null)
                {
                    throw new InvalidOperationException("扩展宿主响应格式错误");
                }

                if (!response.Ok && response.Error is not null)
                {
                    _toast.Show($"扩展错误：{response.Error}", ToastType.Error);
                }

                return response;
            }
        }

        private string ResolveHostExecutable()
        {
            string baseDir = AppContext.BaseDirectory;
            string exeName = HostProtocol.HostExecutableName;

            var candidates = new[]
            {
                Path.Combine(baseDir, exeName),
                Path.Combine(baseDir, "Extend", "ExtensionHost", exeName),
                Path.Combine(baseDir, "Extend", "ExtensionHost", "bin", "Release", "net8.0", exeName),
                Path.Combine(baseDir, "Extend", "ExtensionHost", "bin", "Debug", "net8.0", exeName),
                Path.Combine(baseDir, "Extend", "ExtensionHost", "bin", "Release", "net8.0-windows10.0.17763.0", exeName),
                Path.Combine(baseDir, "Extend", "ExtensionHost", "bin", "Debug", "net8.0-windows10.0.17763.0", exeName),

                // 如果宿主项目被移到解决方案根目录（与 Quicker 平级）
                Path.Combine(baseDir, "..", "ExtensionHost", exeName),
                Path.Combine(baseDir, "..", "ExtensionHost", "bin", "Release", "net8.0", exeName),
                Path.Combine(baseDir, "..", "ExtensionHost", "bin", "Debug", "net8.0", exeName),
                Path.Combine(baseDir, "..", "ExtensionHost", "bin", "Release", "net8.0-windows10.0.17763.0", exeName),
                Path.Combine(baseDir, "..", "ExtensionHost", "bin", "Debug", "net8.0-windows10.0.17763.0", exeName)
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            throw new FileNotFoundException($"未找到 {exeName}，请先构建扩展宿主项目。");
        }

        private sealed class ExtensionHostHandle : IDisposable
        {
            public ExtensionHostHandle(NamedPipeClientStream pipe, StreamReader reader, StreamWriter writer)
            {
                Pipe = pipe;
                Reader = reader;
                Writer = writer;
            }

            public Process? Process { get; set; }
            public NamedPipeClientStream Pipe { get; }
            public StreamReader Reader { get; }
            public StreamWriter Writer { get; }
            public object SyncRoot { get; } = new();

            public void Dispose()
            {
                try { Pipe.Dispose(); } catch { }
                try { Reader.Dispose(); } catch { }
                try { Writer.Dispose(); } catch { }
                try { Process?.Kill(true); } catch { }
                try { Process?.Dispose(); } catch { }
            }
        }
    }
}