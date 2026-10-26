using LunarWard.Core.GameRecord;

namespace LunarWard.Features.GameRecord;

internal class GameRecordRoleChangedMessage
{

    public GameRecordRole? GameRole { get; set; }

    public GameRecordRoleChangedMessage(GameRecordRole? gameRole)
    {
        GameRole = gameRole;
    }

}
