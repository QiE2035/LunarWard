using Avalonia.Data.Converters;
using LunarWard.Core.GameRecord.Genshin.SpiralAbyss;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace LunarWard.Features.GameRecord.Genshin;

public partial class SpiralAbyssBattleAvatarsSelectConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SpiralAbyssLevel level)
        {
            if (parameter is "1")
            {
                if (level.Battles?.Count > 1)
                {
                    return level.Battles[1].Avatars;
                }
            }
            else
            {
                return level.Battles?.FirstOrDefault()?.Avatars!;
            }
        }
        if (value is IList<SpiralAbyssBattle> battles)
        {
            if (parameter is "1")
            {
                if (battles.Count > 1)
                {
                    return battles[1].Avatars;
                }
            }
            else
            {
                return battles.FirstOrDefault()?.Avatars!;
            }
        }
        return null!;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
