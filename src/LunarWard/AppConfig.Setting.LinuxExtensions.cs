namespace LunarWard;

/// <summary>
/// <see cref="AppConfig"/> 的 LunarWard 扩展静态设置（上游 Windows 版没有这三个字段）。
/// </summary>
/// <remarks>
/// <para>
/// 这三个开关原来在旧 <c>LauncherConfig</c>（<c>config.json</c> 的 <c>launcher</c> 段）里，
/// 迁移到 <see cref="AppConfig"/> 的 <c>Setting</c> 表后与上游「静态设置」写法一致：
/// <c>GetValue</c>/<c>SetValue</c> + <c>CallerMemberName</c> 作键，立即写入配置。
/// </para>
/// <para>
/// 默认值与旧实现保持一致（视频背景开、公告开、清理背景开），所以用户升级后行为不变。
/// </para>
/// </remarks>
public static partial class AppConfig
{


    #region LunarWard Extension


    /// <summary>主界面视频背景（Linux 侧的 <c>AppBackground</c> 视频层；上游没有开关）。</summary>
    public static bool VideoBackground
    {
        get => GetValue(true);
        set => SetValue(value);
    }


    /// <summary>启动时显示公告（旧 <c>launcher.show_announcements</c>；上游无此开关）。</summary>
    public static bool ShowAnnouncements
    {
        get => GetValue(true);
        set => SetValue(value);
    }


    /// <summary>清理缓存时同时删除启动器背景文件（上游是 <c>AppConfig.ClearLauncherBackground</c>，本仓库对齐）。</summary>
    public static bool ClearLauncherBackground
    {
        get => GetValue(true);
        set => SetValue(value);
    }


    #endregion


}
