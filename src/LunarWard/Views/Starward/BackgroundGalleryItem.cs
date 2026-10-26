using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Localization;

namespace LunarWard.Views.Starward;

/// <summary>背景图库里的一张（官方 / 本地自定义）。</summary>
public sealed partial class BackgroundGalleryItem : ObservableObject
{
    private static AppStrings Strings => AppStrings.Instance;

    public int Index { get; init; }

    public string Title { get; init; } = "";

    public bool IsVideo { get; init; }

    public bool IsCurrent { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private Bitmap? _thumbnail;

    public bool HasThumbnail => Thumbnail is not null;

    public string VideoLabel => Strings["Launcher.BackgroundVideo"];

    public string CurrentLabel => Strings["Launcher.BackgroundCurrent"];
}
