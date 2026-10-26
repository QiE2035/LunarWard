namespace LunarWard.Launch.Uigf4;

/// <summary>
/// UIGF 导入失败（上游 <c>UIGF4ImportException</c>）。
/// </summary>
/// <remarks>
/// 只携带结构化信息（类别、字段名、UID），本地化文案由界面层按
/// <see cref="Kind"/> 拼装 —— 上游是把已经格式化的字符串放进异常里，
/// 那样在 Launch 层就得依赖语言资源。
/// </remarks>
public sealed class Uigf4ImportException : Exception
{
    public Uigf4ImportException(
        Uigf4ImportErrorKind kind,
        string path = "",
        string? detail = null,
        Exception? innerException = null,
        long uid = 0,
        long expectedUid = 0,
        long? foundUid = null,
        string? field = null)
        : base(detail ?? kind.ToString(), innerException)
    {
        Kind = kind;
        Path = path;
        Uid = uid;
        ExpectedUid = expectedUid;
        FoundUid = foundUid;
        Field = field;
    }


    public Uigf4ImportErrorKind Kind { get; }

    /// <summary>出错的文件路径（仅 <see cref="Uigf4ImportErrorKind.InvalidFile"/>）。</summary>
    public string Path { get; }

    /// <summary>出错的存档 UID。</summary>
    public long Uid { get; }

    public long ExpectedUid { get; }

    public long? FoundUid { get; }

    /// <summary>缺失 / 出错的字段名（缺字段时用于提示）。</summary>
    public string? Field { get; }
}
