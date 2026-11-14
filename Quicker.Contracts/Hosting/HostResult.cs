namespace Quicker.Contracts.Hosting
{
    /// <summary>
    /// IPC 命令执行返回的结果
    /// </summary>
    public sealed record HostResult(
        bool Ok,
        string? Error,
        ModuleMetadata? Metadata,
        bool Shutdown,
        HostErrorCode ErrorCode = HostErrorCode.None
    );
}