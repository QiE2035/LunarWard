using Dapper;
using LunarWard.Core;
using LunarWard.Games;
using LunarWard.Launch.Database;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.GameAccount;

/// <summary>
/// 账号读写。<b>移植自</b> <c>Starward/src/Starward/Features/GameAccount/GameAccountService.cs</c>。
/// </summary>
/// <remarks>
/// <para>
/// <b>平台替换</b>：上游用 <c>Registry.GetValue/SetValue</c>（Windows 注册表）；
/// Linux 下同样的数据在 Wine prefix 的 <c>user.reg</c> 里，交由 <see cref="WineAccountService"/>
/// 解析与原子写回。因此构造参数比上游多一个 <c>prefix</c>——上游的「当前游戏注册表」
/// 在这里就是「当前档案的 prefix」。见 <c>docs/09-LINUX-DIVERGENCES.md</c>。
/// </para>
/// <para>
/// 其余部分（SQL、SHA256、UID 回填、日志文案）与上游逐行一致。
/// </para>
/// </remarks>
public class GameAccountService
{

    private readonly ILogger<GameAccountService> _logger;

    private readonly WineAccountService _wine;


    public GameAccountService(ILogger<GameAccountService> logger, string prefix)
    {
        _logger = logger;
        _wine = new WineAccountService(prefix);
    }



    public GameAccount? GetGameAccountFromRegistry(GameBiz biz)
    {
        // 上游：key = biz.GetGameRegistryKey(); keyName = cn ? MIHOYOSDK_ADL_PROD_CN_h3123967166 : MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810
        // 平台替换：WineAccountService.Read 按 GameBiz 选 section + key 并读取 hex 值
        if (_wine.Read(biz) is not { } snapshot)
        {
            return null;
        }
        var account = new GameAccount
        {
            SHA256 = snapshot.Sha256,
            GameBiz = biz,
            Value = snapshot.Value,
            Name = "-",
            Uid = snapshot.Uid,
        };
        return account;
    }



    public IEnumerable<GameAccount> GetGameAccountsFromDatabase(GameBiz biz)
    {
        using var dapper = DatabaseService.CreateConnection();
        var list = dapper.Query<GameAccount>("SELECT * FROM GameAccount WHERE GameBiz = @biz;", new { biz });
        foreach (var item in list)
        {
            item.IsSaved = true;
            yield return item;
        }
    }



    public List<GameAccount> GetGameAccounts(GameBiz biz)
    {
        var databaseAccounts = GetGameAccountsFromDatabase(biz).ToList();
        var regAccount = GetGameAccountFromRegistry(biz);
        if (regAccount != null)
        {
            if (databaseAccounts.FirstOrDefault(x => x.SHA256 == regAccount.SHA256) is GameAccount ga)
            {
                databaseAccounts.Remove(ga);
                databaseAccounts.Insert(0, ga);
            }
            else
            {
                databaseAccounts.Insert(0, regAccount);
            }
        }
        return databaseAccounts;
    }



    public IEnumerable<long> GetSuggestionUids(GameBiz biz)
    {
        using var dapper = DatabaseService.CreateConnection();
        // 上游是 IEnumerable<int>；本仓库 Uid 统一用 long（游戏 UID 已超过 int 上限的边缘情况更安全）
        List<long> uids = dapper.Query<long>("SELECT DISTINCT Uid FROM GameAccount WHERE GameBiz = @biz AND Uid > 0;", new { biz }).ToList();
        return uids.Distinct().Order();
    }



    public void SaveGameAccount(GameAccount account)
    {
        account.Name ??= "";
        account.IsSaved = true;
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("INSERT OR REPLACE INTO GameAccount (SHA256, GameBiz, Uid, Name, Value, Time) VALUES (@SHA256, @GameBiz, @Uid, @Name, @Value, @Time);", account);
        _logger.LogInformation("Save account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void DeleteGameAccount(GameAccount account)
    {
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("DELETE FROM GameAccount WHERE SHA256=@SHA256;", account);
        _logger.LogInformation("Delete account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void ChangeGameAccount(GameAccount account)
    {
        // 上游：Registry.SetValue(key, keyName, account.Value) + hkrpg/bh3 写 DWord UID、hk4e 写 UTF8 UID
        // 平台替换：WineAccountService.Write 一次完成（见 docs/09）
        var snapshot = new GameAccountSnapshot
        {
            Sha256 = account.SHA256,
            GameBiz = account.GameBiz,
            Uid = account.Uid,
            Name = account.Name,
            Value = account.Value,
            Time = account.Time,
        };
        if (_wine.Write(snapshot) is { } error)
        {
            _logger.LogWarning("Change account {name} ({biz}) failed: {error}", account.Name, account.GameBiz, error);
            return;
        }
        _logger.LogInformation("Change account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }


}
