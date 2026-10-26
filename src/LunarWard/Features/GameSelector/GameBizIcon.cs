using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;

namespace LunarWard.Features.GameSelector;

/// <summary>
/// 游戏 + 区服图标。<b>原样搬自</b> <c>Starward/src/Starward/Features/GameSelector/GameBizIcon.cs</c>。
/// </summary>
/// <remarks>
/// 上游图标是 <c>ms-appx:///Assets/Image/...</c>；本仓库用 <c>avares://LunarWard/Assets/Image/...</c>
/// （Avalonia 的 <c>Bitmap</c> 只认 avares），资源键见 <c>docs/09-LINUX-DIVERGENCES.md</c>。
/// </remarks>
public partial class GameBizIcon : ObservableObject, IEquatable<GameBizIcon>
{


    private const double GB = 1 << 30;


    public GameId GameId { get; set; } = null!;

    public GameBiz GameBiz { get; set; }


    public string GameIcon { get; set => SetProperty(ref field, value); } = null!;

    public string GameName { get; set => SetProperty(ref field, value); } = null!;

    public string ServerIcon { get; set => SetProperty(ref field, value); } = null!;

    public string ServerName { get; set => SetProperty(ref field, value); } = null!;

    public double MaskOpacity { get; set => SetProperty(ref field, value); } = 1.0;

    public bool IsPinned { get; set => SetProperty(ref field, value); }

    public string? InstallPath { get; set => SetProperty(ref field, value); }

    public long TotalSize { get; set { field = value; OnPropertyChanged(nameof(TotalSizeText)); } }

    public string? TotalSizeText => TotalSize == 0 ? null : $"{TotalSize / GB:F2}GB";


    public bool IsSelected
    {
        get;
        set
        {
            field = value;
            MaskOpacity = value ? 0 : 1;
        }
    }



    public GameBizIcon(GameBiz gameBiz)
    {
        GameBiz = gameBiz;
        GameId = GameId.FromGameBiz(gameBiz)!;
        GameIcon = GameBizToIcon(gameBiz);
        ServerIcon = GameBizToServerIcon(gameBiz);
        GameName = gameBiz.ToGameName();
        ServerName = gameBiz.ToGameServerName();
    }



    public GameBizIcon(GameInfo gameInfo)
    {
        GameId = gameInfo;
        GameBiz = gameInfo.GameBiz;
        GameIcon = gameInfo.Display.Icon.Url;
        ServerIcon = GameBizToServerIcon(gameInfo.GameBiz);
        GameName = gameInfo.Display.Name;
        ServerName = gameInfo.GameBiz.ToGameServerName();
    }



    public void UpdateInfo()
    {
        GameIcon = GameBizToIcon(GameBiz);
        ServerIcon = GameBizToServerIcon(GameBiz);
        GameName = GameBiz.ToGameName();
        ServerName = GameBiz.ToGameServerName();
    }


    public void UpdateInfo(GameInfo gameInfo)
    {
        GameIcon = gameInfo.Display.Icon.Url;
        ServerIcon = GameBizToServerIcon(gameInfo.GameBiz);
        GameName = gameInfo.Display.Name;
        ServerName = gameInfo.GameBiz.ToGameServerName();
    }



    private static string GameBizToIcon(GameBiz gameBiz)
    {
        return gameBiz.Game switch
        {
            GameBiz.bh3 => "avares://LunarWard/Assets/Image/icon_bh3.jpg",
            GameBiz.hk4e => "avares://LunarWard/Assets/Image/icon_ys.jpg",
            GameBiz.hkrpg => "avares://LunarWard/Assets/Image/icon_sr.jpg",
            GameBiz.nap => "avares://LunarWard/Assets/Image/icon_zzz.jpg",
            _ => "avares://LunarWard/Assets/Image/Transparent.png",
        };
    }


    private static string GameBizToServerIcon(GameBiz gameBiz)
    {
        return gameBiz.Server switch
        {
            "cn" => "avares://LunarWard/Assets/Image/gameicon_hyperion.png",
            "global" => "avares://LunarWard/Assets/Image/gameicon_hoyolab.png",
            "bilibili" => "avares://LunarWard/Assets/Image/gameicon_bilibili.png",
            _ => "avares://LunarWard/Assets/Image/Transparent.png",
        };
    }


    public bool Equals(GameBizIcon? other)
    {
        return ReferenceEquals(this, other) || GameBiz == other?.GameBiz;
    }

}
