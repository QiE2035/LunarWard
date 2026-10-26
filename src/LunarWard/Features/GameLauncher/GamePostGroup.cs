using LunarWard.Core.HoYoPlay;
using LunarWard.Core.Localization;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 公告按类型分组。<b>原样搬自</b> <c>Starward/src/Starward/Features/GameLauncher/GamePostGroup.cs</c>。
/// </summary>
public class GamePostGroup
{

    public string Header { get; set; } = null!;

    public List<GamePost> List { get; set; } = null!;


    public static List<GamePostGroup> FromGameContent(GameContent content)
    {
        return content.Posts.GroupBy(x => x.Type)
                      .OrderBy(x => x.Key)
                      .Select(x => new GamePostGroup
                      {
                          Header = LocalizePostType(x.Key),
                          List = x.ToList(),
                      }).ToList();
    }


    private static string LocalizePostType(string postType)
    {
        return postType switch
        {
            GamePostType.POST_TYPE_ACTIVITY => CoreLang.PostType_Activity,
            GamePostType.POST_TYPE_ANNOUNCE => CoreLang.PostType_Announcement,
            GamePostType.POST_TYPE_INFO => CoreLang.PostType_Information,
            _ => "",
        };
    }

}
