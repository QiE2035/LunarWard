using LunarWard.Core;
using System;

namespace LunarWard.Features.Gacha.UIGF;

public class UIGF4ImportException : Exception
{

    public GameBiz Game { get; set; }

    public long Uid { get; set; }


    public UIGF4ImportException(GameBiz game, long uid, string message) : base(message)
    {
        Game = game;
        Uid = uid;
    }


}
