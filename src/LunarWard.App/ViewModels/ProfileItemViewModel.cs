using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 一个游戏档案的可绑定视图（旧 UI 与 Starward 外壳共用）。
/// </summary>
public sealed partial class ProfileItemViewModel : ObservableObject
{
    public ProfileItemViewModel(string id, ProfileConfig profile, string displayName)
    {
        Id = id;
        Profile = profile;
        DisplayName = displayName;
    }


    public string Id { get; }

    public ProfileConfig Profile { get; }

    public string DisplayName { get; }

    public string Display => $"{DisplayName} · {Id}";


    /// <summary>游戏目录存在（Starward 游戏选择器里"已安装"的判定）。</summary>
    public bool IsInstalled { get; init; }


    public string Path
    {
        get => Profile.Path;
        set
        {
            Profile.Path = value;
            OnPropertyChanged();
        }
    }


    public string Channel
    {
        get => Profile.Channel;
        set
        {
            Profile.Channel = value;
            OnPropertyChanged();
        }
    }


    public IReadOnlyList<string> Channels => Games.ChannelCatalog.SupportedFor(
        Games.GameCatalog.Get(Profile.Game, string.Equals(Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
            ? Games.GameEdition.Global
            : Games.GameEdition.China) ?? Games.GameCatalog.GenshinChina)
        .Select(x => x.ToString())
        .ToList();
}


/// <summary>环境变量表格的一行（键 / 值 / 来源层）。</summary>
public sealed record EnvironmentRowViewModel(string Key, string Value, string Source);
