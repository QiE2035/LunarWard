using Dapper;
using LunarWard.Core;
using LunarWard.Core.GameNotice;
using LunarWard.Games;
using LunarWard.Features.Database;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 游戏内公告。<b>移植自</b> <c>Starward/src/Starward/Features/GameLauncher/GameNoticeService.cs</c>。
/// </summary>
/// <remarks>
/// 与上游的两处平台/依赖差异（均登记在 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="number">
///   <item>上游注入 <c>GameRecordService</c> 取「上次选择的角色」；本仓库的 GameRecord
///   迁移（task-5）尚未落地，这里用<b>同一段 SQL</b>（<c>Setting.last_select_game_record_role_*</c>
///   → <c>GameRecordRole</c> 首行）直接查库，表结构对齐后可直接切回服务调用。</item>
///   <item>上游读 Windows 注册表取 UID；这里读当前 prefix 的 <c>user.reg</c>
///   （<see cref="WineAccountService.Read"/> 已按游戏解析同样的三个键）。</item>
/// </list>
/// </remarks>
internal class GameNoticeService
{


    private readonly ILogger<GameNoticeService> _logger;


    private readonly GameNoticeClient _gameNoticeClient;



    public GameNoticeService(ILogger<GameNoticeService> logger, GameNoticeClient gameNoticeClient)
    {
        _logger = logger;
        _gameNoticeClient = gameNoticeClient;
    }




    public string GetGameNoticeUrl(GameBiz biz)
    {
        long uid = GetLastUid(biz);
        return GameNoticeClient.GetGameNoticeUrl(biz, uid, CultureInfo.CurrentUICulture.Name);
    }




    public async Task<bool> IsNoticeAlertAsync(GameBiz gameBiz, CancellationToken cancellationToken = default)
    {
        long uid = GetLastUid(gameBiz);
        if (uid is 0)
        {
            return false;
        }
        return await _gameNoticeClient.IsNoticeAlertAsync(gameBiz, uid, CultureInfo.CurrentUICulture.Name, cancellationToken);
    }




    private long GetLastUid(GameBiz gameBiz)
    {
        long uid = GetLastSelectGameRecordRoleUid(gameBiz);
        if (uid is not 0)
        {
            return uid;
        }
        return GetUidFromRegistry(gameBiz);
    }




    /// <summary>
    /// 与上游 <c>GameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne</c> 相同的查询。
    /// </summary>
    private long GetLastSelectGameRecordRoleUid(GameBiz gameBiz)
    {
        try
        {
            using var dapper = DatabaseService.CreateConnection();
            long uid = dapper.QueryFirstOrDefault<long>("""
                SELECT r.Uid FROM GameRecordRole r INNER JOIN Setting s ON s.Value = r.Uid WHERE r.GameBiz = @gameBiz AND s.Key = @key LIMIT 1;
                """, new { gameBiz, key = $"last_select_game_record_role_{gameBiz}" });
            if (uid is not 0)
            {
                return uid;
            }
            return dapper.QueryFirstOrDefault<long>("SELECT Uid FROM GameRecordRole WHERE GameBiz = @gameBiz LIMIT 1;", new { gameBiz });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Query last select game record role failed: {gameBiz}", gameBiz);
            return 0;
        }
    }




    private static long GetUidFromRegistry(GameBiz gameBiz)
    {
        // 平台替换：Windows 注册表 → 当前 prefix 的 user.reg
        if (GamePrefixContext.CurrentPrefix is not { } prefix)
        {
            return 0;
        }
        return new WineAccountService(prefix).Read(gameBiz.Value)?.Uid ?? 0;
    }





}
