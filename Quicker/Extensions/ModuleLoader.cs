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

namespace Quicker.Extensions
{
    /// <summary>
    /// 通过外部宿主进程加载扩展，防止扩展崩溃牵连主程序。
    /// </summary>
    public class ModuleLoader
    {
        private readonly Dictionary<string, ExtensionHostHandle> _hosts = new();
        private readonly ToastManager _toast = new();

        /// <summary>
        /// 加载并激活扩展；若宿主进程已在运行，则直接向其发送 activate 指令。
        /// </summary>
        public void LoadModule(string modulePath)
        {
            try
            {
                string assemblyName = Path.GetFileNameWithoutExtension(modulePath);

                // 宿主已存在且存活，直接复用连接并再次触发 Activate
                if (_hosts.TryGetValue(assemblyName, out var existingHandle))
                {
                    if (existingHandle.Process != null && !existingHandle.Process.HasExited)
                    {
                        ActivateHandle(existingHandle);
                        return;
                    }

                    existingHandle.Dispose();
                    _hosts.Remove(assemblyName);
                }

                // 启动新宿主进程并建立管道连接
                string hostPath = ResolveHostExecutable();
                string pipeName = $"{HostProtocol.PipePrefix}{Guid.NewGuid():N}";

                var process = StartHostProcess(hostPath, modulePath, pipeName);
                var handle = ConnectToHost(pipeName, process);
                handle.Process = process;

                var metadata = RequestMetadata(handle);
                if (metadata is null)
                {
                    handle.Dispose();
                    throw new InvalidOperationException("无法读取扩展元数据");
                }

                // 首次激活
                ActivateHandle(handle);
                _hosts[assemblyName] = handle;
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (_hosts.TryGetValue(assemblyName, out var h) && h.Process == process)
                        {
                            _toast.Show($"警告：扩展 {assemblyName} 意外崩溃！", ToastType.Warning);
                            h.Dispose();
                            _hosts.Remove(assemblyName);
                        }
                    });
                };
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => _toast.Show($"加载或激活模块时出错：{ex.Message}", ToastType.Error));
            }
        }

        /// <summary>
        /// 单独向已加载的扩展发送激活命令（供解耦后的点击事件调用）。
        /// </summary>
        public void ActivateModule(string moduleName)
        {
            try
            {
                if (!_hosts.TryGetValue(moduleName, out var handle) ||
                    handle.Process == null ||
                    handle.Process.HasExited)
                {
                    throw new InvalidOperationException($"扩展 {moduleName} 尚未加载或宿主已退出");
                }

                ActivateHandle(handle);
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => _toast.Show($"激活模块时出错：{ex.Message}", ToastType.Error));
            }
        }

        private void ActivateHandle(ExtensionHostHandle handle)
        {
            var activateResult = SendCommand(handle, new HostCommand("activate"));
            if (!activateResult.Ok)
            {
                throw new InvalidOperationException(activateResult.Error ?? "激活失败");
            }
        }

        public void UnloadModule(string moduleName)
        {
            if (!_hosts.TryGetValue(moduleName, out var handle))
            {
                return;
            }

            _hosts.Remove(moduleName);

            if (handle.Process != null)
            {
                try { handle.Process.EnableRaisingEvents = false; } catch { }
            }

            try
            {
                _ = SendCommand(handle, new HostCommand("deactivate"));
            }
            catch { }
            finally
            {
                try
                {
                    SendCommand(handle, new HostCommand("shutdown"));
                }
                catch { }

                handle.Dispose();
            }
        }

        /// <summary>
        /// 连接到扩展宿主并启动后台监听。
        /// </summary>
        private static ExtensionHostHandle ConnectToHost(string pipeName, Process process)
        {
            var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            var timeoutMs = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
            var sw = Stopwatch.StartNew();
            while (!client.IsConnected && sw.ElapsedMilliseconds < timeoutMs)
            {
                if (process.HasExited)
                {
                    throw new IOException($"扩展宿主已退出：{ReadProcessError(process)}");
                }
                try { client.Connect(200); } catch (TimeoutException) { }
            }

            if (!client.IsConnected)
            {
                throw new TimeoutException("连接扩展宿主超时");
            }

            var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
            var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };

            var handle = new ExtensionHostHandle(client, reader, writer);

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync();
                    }
                    catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
                    {
                        break; // 管道已断开或已释放，退出循环防止空转吃满 CPU
                    }

                    if (line == null) break;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        if (root.ValueKind != JsonValueKind.Object) continue;

                        if (root.TryGetProperty("Action", out var action) &&
                            action.ValueKind == JsonValueKind.String &&
                            action.GetString() == "toast")
                        {
                            var cmd = root.Deserialize<HostCommand>();
                            if (cmd != null)
                            {
                                Application.Current?.Dispatcher.BeginInvoke(() =>
                                {
                                    try
                                    {
                                        ToastType type = Enum.TryParse<ToastType>(cmd.ToastType, out var t)
                                            ? t : ToastType.Common;
                                        new ToastManager().Show(cmd.Message ?? "", type);
                                    }
                                    catch { }
                                });
                            }
                        }
                        else
                        {
                            var response = root.Deserialize<HostResult>();
                            if (response != null)
                            {
                                handle.LastResponse = response;
                                handle.ResponseEvent.Set();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // 单行格式错误，忽略
                    }
                }
            });

            return handle;
        }

        private static Process StartHostProcess(string hostPath, string modulePath, string pipeName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = hostPath,
                ArgumentList = { modulePath, pipeName },
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(hostPath) ?? AppContext.BaseDirectory
            };

            var process = Process.Start(psi)
                          ?? throw new InvalidOperationException("无法启动扩展宿主进程");

            if (process.HasExited)
            {
                throw new InvalidOperationException($"扩展宿主启动失败：exit code {process.ExitCode}");
            }

            return process;
        }

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

                handle.ResponseEvent.Reset();
                handle.LastResponse = null;

                string payload = JsonSerializer.Serialize(command);
                handle.Writer.WriteLine(payload);

                if (handle.ResponseEvent.Wait(5000))
                {
                    var response = handle.LastResponse;
                    if (response == null) throw new InvalidOperationException("扩展宿主无响应或连接已断开");

                    return response;
                }
                else
                {
                    throw new TimeoutException("扩展宿主响应超时");
                }
            }
        }

        private string ResolveHostExecutable()
        {
            string exeName = HostProtocol.HostExecutableName;
            if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exeName += ".exe";

            string directPath = Path.Combine(AppContext.BaseDirectory, exeName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

#if DEBUG
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\ExtensionHost\bin\Debug\net8.0-windows10.0.17763.0", exeName)),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\ExtensionHost\bin\Debug\net8.0-windows10.0.17763.0", exeName)),
            };
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
#endif

            throw new FileNotFoundException(
                $"未找到扩展宿主 {exeName}，请确认它和主程序在同一目录。");
        }

        /// <summary>
        /// 卸载并关闭所有运行中的扩展宿主进程
        /// </summary>
        public void UnloadAll()
        {
            var moduleNames = _hosts.Keys.ToList();
            foreach (var name in moduleNames)
            {
                UnloadModule(name);
            }
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

            public ManualResetEventSlim ResponseEvent { get; } = new(false);
            public HostResult? LastResponse { get; set; }

            public void Dispose()
            {
                try { Pipe.Dispose(); } catch { }
                try { Reader.Dispose(); } catch { }
                try { Writer.Dispose(); } catch { }
                try { ResponseEvent.Dispose(); } catch { }
                try { Process?.Kill(true); } catch { }
                try { Process?.Dispose(); } catch { }
            }
        }
    }
}