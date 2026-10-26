using LunarWard.Games;

namespace LunarWard.App.Services;

/// <summary>
/// 上游 <c>Starward.Assets</c> 内容包里的品牌素材（游戏图标、区服 logo 等）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 的 <c>src/Starward/Assets/Image</c> 是空的：这些图片放在仓库的
/// <c>large-resource</c> 分支，打包成 NuGet 内容包 <c>Starward.Assets</c>，包内
/// <c>contentFiles</c> 全部 <c>copyToOutput=true</c>，还原后落进输出目录的
/// <c>Assets/</c>（代码里用 <c>ms-appx:///Assets/Image/...</c> 引用）。LunarWard 引用同一个包
/// （见 <c>Directory.Packages.props</c>），这里给出对应的相对/绝对路径。
/// </para>
/// <para>
/// 区服 logo 的映射与上游 <c>GameBizIcon.GameBizToServerIcon</c> 一致：
/// cn → <c>gameicon_hyperion.png</c>、global → <c>gameicon_hoyolab.png</c>、
/// bilibili → <c>gameicon_bilibili.png</c>。
/// </para>
/// </remarks>
public static class GameAssets
{
    /// <summary>输出目录下品牌素材所在的子目录。</summary>
    public const string ImageFolder = "Assets/Image";


    /// <summary>区服 logo 文件名（上游 <c>GameBizToServerIcon</c> 的 <c>Server</c> 分支）。</summary>
    /// <remarks>B 站优先于版本：上游 A 服的 <c>hk4e_bilibili</c> 属于 cn 服务器但图标是 B 站。</remarks>
    public static string ServerIconFileName(GameEdition edition, GameChannel channel)
    {
        if (channel == GameChannel.Bilibili)
        {
            return "gameicon_bilibili.png";
        }

        return edition == GameEdition.Global ? "gameicon_hoyolab.png" : "gameicon_hyperion.png";
    }


    /// <summary>游戏方形图标文件名（上游 <c>GameBizToIcon</c> 的 <c>Game</c> 分支）。</summary>
    public static string GameIconFileName(string kind) => kind switch
    {
        GameCatalog.Genshin => "icon_ys.jpg",
        GameCatalog.Zzz => "icon_zzz.jpg",
        "honkai3" => "icon_bh3.jpg",
        "starrail" => "icon_sr.jpg",
        _ => "Transparent.png",
    };


    /// <summary>品牌素材在输出目录里的绝对路径。</summary>
    public static string GetImagePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Assets", "Image", fileName);


    /// <summary>区服 logo 在输出目录里的绝对路径。</summary>
    public static string ServerIconPath(GameEdition edition, GameChannel channel)
        => GetImagePath(ServerIconFileName(edition, channel));


    /// <summary>游戏方形图标在输出目录里的绝对路径。</summary>
    public static string GameIconPath(string kind) => GetImagePath(GameIconFileName(kind));
}
