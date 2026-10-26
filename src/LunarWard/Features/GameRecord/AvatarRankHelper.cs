using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LunarWard.Features.GameRecord;

public static class AvatarRankHelper
{
    public static bool RankToVisibility(int rank)
    {
        return rank > 0 ? true : false;
    }

}
