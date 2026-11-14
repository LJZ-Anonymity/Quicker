namespace Quicker.Contracts.Hosting
{
    /// <summary>
    /// 扩展模块的元数据
    /// </summary>
    public sealed record ModuleMetadata(
        string Name,
        string Version,
        string Author,
        string Description,
        bool HasContextMenu,
        string? IconBase64
    );
}