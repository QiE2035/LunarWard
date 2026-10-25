# 潜在风险审计

> 审计对象：`/home/qie2035/projects/lunarward`（HEAD `d596a7a`，.NET `10.0.302`，Avalonia **12.1.4** —— 任务书写 11，以 `Directory.Packages.props` 为准；`src/**/*.cs` 453 个）。
> 方法：只读静态扫描（`rg -n <pat> -g '*.cs' src/`，`bin/`/`obj/` 必须排除）+ 3 处实测复现（WAL 侧车文件权限与明文位置、数据目录权限、冷启动耗时）。
> 本次**未修改任何业务代码、未 commit**；实测产物只落在 `/tmp`。除标注「实测」外，结论均来自代码路径推演；仅靠推演无法定量的一律进 §3。

## 1. 结论摘要

**最该先修的三件事：**

1. **凭据在 SQLite WAL 侧车文件里是全局可读的（P0，已实测复现）。** `GameAccountStore` 只把 `LunarWardDatabase.db` 收紧到 0600（`src/LunarWard.Games/GameAccountStore.cs:175-186`），但库本身开了 WAL（`src/LunarWard.Launch/Database/LunarWardDatabase.cs:109`）。SQLite 新建的 `.db-wal` / `.db-shm` **不继承主库权限**（按 umask 得 0644），而数据目录也只 `Directory.CreateDirectory`、是 0755（`src/LunarWard.Platform/XdgPaths.cs:111-122`，本机实测 `drwxr-xr-x`）。结果：本机任何用户都能在应用运行期间 `strings` 出账号 cookie / 抽卡 authkey。**现有 0600 单测只断言主库文件、给出了假安全感**（`tests/LunarWard.UnitTests/GameAccountStoreTests.cs:66-69` 只对 `LunarWardDatabase.FileName` 断言）。
2. **同一个安装任务会被启动两次（P0）。** `GameInstallService.StartOrContinueTask` 在置 `Waiting` 后已经 fire-and-forget 跑了一份（`src/LunarWard.Installer/GameInstallService.cs:224-226`），而 UI 的「继续/预下载」分支又自己 `Task.Run` 了一份（`src/LunarWard.App/ViewModels/Starward/GameLauncherPageViewModel.cs:1002-1005`、`:1134-1137`）；`GameInstallController.RunAsync`（`src/LunarWard.Installer/GameInstallController.cs:106-109`）还绕过了 service 的状态门（`:220-223`）。两条流水线同时往同一安装目录写、共用同一份 `GameInstallContext`。
3. **游戏日志泵在 8 MiB 处停止读取，失败被整段吞掉（P1，接近 P0）。** 子进程以 `RedirectOutput = true` 启动（`src/LunarWard.Launch/LaunchEngine.cs:288-289`），日志泵读满 `GameLogLimitBytes`（默认 8 MiB，`:173`）就 `break` 退出（`:699-702`）不再消费管道；整段又被 `catch (Exception)` 静默吞掉（`:730-733`）。管道缓冲一旦写满，**游戏进程阻塞在 write 上永久卡住**（Proton/DXVK 调试日志下必现）。

附带一句判断：项目在**资源释放的常规动作上其实是干净的**（见 §3 的「已核实安全」清单）——问题集中在「权限模型」和「进程/管道边界」，而不是「忘了 using」。

## 2. 风险清单

### RK-01 凭据落在 WAL/SHM 侧车文件，同机任何用户可读 · **P0**
- **位置**：`src/LunarWard.Games/GameAccountStore.cs:173-186`（只 chmod `_database.Path`）、`src/LunarWard.Launch/Database/LunarWardDatabase.cs:109`（`PRAGMA JOURNAL_MODE = WAL`）、`src/LunarWard.Platform/XdgPaths.cs:111-122`（目录无 mode）
- **触发**：登录一次或写一次抽卡 URL 后；应用运行期间（`-wal` 未 checkpoint 时）。
- **后果**：`GameAccount.Value`（cookie/uid）与 `GachaLogUrl`（含 authkey）明文躺在 0644 的 `*.db-wal` 里，目录 0755 可遍历。多用户机器 = 凭据泄露。
- **建议修复**：三处一起做——① `EnsureDirectories` 后对所有凭据相关目录 `File.SetUnixFileMode(dir, 0700)`；② `LunarWardDatabase` 每次 `CreateConnection` 后对 `Path + "-wal"` / `"-shm"` 各 `SetUnixFileMode 0600`（文件不存在则跳过，幂等）；③ 退出时 `PRAGMA wal_checkpoint(TRUNCATE)` 收尾。不要只靠 ②——① 才能挡住未来新增的文件。

### RK-02 安装任务被重复启动，两条流水线写同一目录 · **P0**
- **位置**：`src/LunarWard.Installer/GameInstallService.cs:219-226`（状态门只挡 `Waiting/Downloading/Decompressing/Verifying`，置 `Waiting` 后立刻 `_ = PrepareGameInstallTaskAsync(...)`）、`src/LunarWard.Installer/GameInstallController.cs:106-109`（`RunAsync` 直调 service，绕过状态门）、`src/LunarWard.App/ViewModels/Starward/GameLauncherPageViewModel.cs:1002-1005`、`:1134-1137`（UI 侧再 `Task.Run` 一次）
- **触发**：安装页点「继续」/「预下载」，或 `StartOrContinue` 返回的 `progress` 非空时（即任务已在跑）。
- **后果**：两个解压/下载器写同一批文件 → 校验失败、文件交错损坏；`ChangeToAnotherTask` 的 `Progress.Report` 互相覆盖，进度条跳变；取消其一可能删掉另一条正在写的临时目录。**这是能造成安装目录半损坏的路径。**
- **建议修复**：把「启动」收敛成唯一入口——`StartOrContinue` 内做 `Interlocked.CompareExchange` 单飞（或给 context 加 `RunToken`，`PrepareGameInstallTaskAsync` 入口校验 token 未被换过直接 return）；`GameInstallController.RunAsync` 改为 `StartOrContinue(...)` 而不是直调；删掉 UI 两处 `_ = Task.Run(() => ...RunAsync(task))`。

### RK-03 日志泵停读 → 子进程管道写满卡死 · **P1（接近 P0）**
- **位置**：`src/LunarWard.Launch/LaunchEngine.cs:288-289`（`RedirectOutput/RedirectError = true`）、`:293`（`AttachLogPump`）、`:699-702`（`fs.Length > GameLogLimitBytes` 后 `break`）、`:730-733`（`catch (Exception)` 空处理）、`:173`（默认 8 MiB）
- **触发**：游戏 stdout 超过 8 MiB（Proton/DXVK/gamemode 调试日志很常见）；或 `new FileStream` 建文件失败（只读 `$HOME`、磁盘满）。
- **后果**：读端退出后仍 `RedirectOutput`，子进程下一次 write 阻塞 → 游戏永不进入画面，父进程 `WaitForExitAsync` 永不返回，launcher 永远显示「运行中」并累计游玩时长。
- **建议修复**：到达上限后**继续 drain 但不落盘**（`while ((line = reader.ReadLine()) != null) { if (written < limit) write; }` 二选一形态），不要裸 `break`；`AttachLogPump` 的 catch 至少 `Log.Warn` 并在失败时 `child.StandardOutput.Close()`/改为不重定向，避免无读者管道。

### RK-04 退出时不停止安装任务，留下半写安装目录 · **P1**
- **位置**：`src/LunarWard.App/App.axaml.cs:157-165`（`desktop.Exit` 只调 `SingleInstance/_tray/_shell/Background`）、`src/LunarWard.App/AppServices.cs:103-113`（`ShutdownInstaller()` 写明「应用退出前调用」）、`src/LunarWard.App/Services/StartupCommands.cs:202`（**唯一**调用点，CLI 路径）
- **触发**：安装/解压进行中关闭主窗口（托盘「退出」或窗口关闭）。
- **后果**：解压/下载在任意字节处被硬中断，安装目录留下半套文件与 `.part` 残留；`GameInstallState` 不持久化，下次启动不会自愈，用户看到的是「目录有文件但版本/校验不对」。
- **建议修复**：`desktop.Exit` 里在 `_shell.Shutdown()` 之前加 `Services.ShutdownInstaller()`；`ShutdownInstaller` 内先 `StopAllTasks` 再 `await`（或短超时 `Task.Wait(2s)`）ticker 退出；解压写临时目录、成功后 `Directory.Move` 落位，使中断天然可恢复。

### RK-05 原子写失败/崩溃残留 0644 明文 `.tmp` · **P1**
- **位置**：`src/LunarWard.Platform/FileSystem.cs:81-96`（`tmp = full + ".tmp"`、`FileMode.Create` 默认 0644、`File.Move` 无 `finally` 清理）、调用方 `src/LunarWard.Launch/DailyNoteService.cs:361-372`（先 `AtomicFile.WriteAllText` 再 chmod）
- **触发**：写 `credentials.json`/便笺凭据过程中进程被杀、磁盘满、或 chmod 前的时间窗。
- **后果**：慢下一份 0644 的 `xxx.json.tmp`，内容含 cookie 明文且**永不被清理**（下次写会覆盖同名 tmp，但崩溃后再不写就永久留存）。
- **建议修复**：`AtomicFile` 在 `try/finally` 里删 tmp；建文件时用 `FileStreamOptions { UnixCreateMode = 0600 }` 直接以 0600 创建，而不是写完再 chmod；`DailyNoteService.SaveStore` 的 chmod 挪到写之前。

### RK-06 图标/缩略图内存只增不减 · **P1**
- **位置**：`src/LunarWard.App/Services/GachaIconCache.cs:34`（`static ConcurrentDictionary<string, Bitmap> Memory`，无上限、无淘汰、无 Dispose）、`:36`/`:66`/`:102`（`Pending` 只在 finally 移除，异常路径无泄漏）、`src/LunarWard.App/ViewModels/ScreenshotViewModel.cs:224`（`_thumbnail = bitmap` 直接覆盖旧位图）、`src/LunarWard.App/ViewModels/Starward/ImageViewWindow2ViewModel.cs:280`（`Thumbnail = bitmap` 同样不释放旧值）
- **触发**：长时间浏览抽卡历史（每张新图标一条常驻）、反复切换截图/大图。
- **后果**：`Bitmap` 持有非托管像素缓冲，旧引用被覆盖后靠 GC 兜底但 Avalonia `Bitmap` 有终结器延迟，峰值内存与句柄数持续上涨。
- **建议修复**：`Memory` 换成带容量上限的 LRU（或 `MemoryCache` + size limit），淘汰时 `Dispose()`；覆盖 `_thumbnail`/`Thumbnail` 前 `Interlocked.Exchange` 旧值并 Dispose。

### RK-07 单实例监听任务异常即静默失效 · **P1**
- **位置**：`src/LunarWard.App/Services/SingleInstance.cs:57-69`（`_ = Task.Run(async () => { while (...) { using Socket connection = await listener.AcceptAsync()... } })`，循环体内无 `try/catch`）
- **触发**：`AcceptAsync`/`ReceiveAsync` 抛 `SocketException`（对端半关闭、`Dispose` 竞态）、`Dispatcher.UIThread.Post` 抛（应用正在关闭）。
- **后果**：`Task` 被丢弃，异常进 `TaskScheduler.UnobservedTaskException`，监听器死亡但 socket 文件还在 → 用户第二次启动时「无反应」，且没有任何日志。
- **建议修复**：循环体包 `try/catch`，`OperationCanceledException`/`ObjectDisposedException` 正常退出，其余 `Log.Warn` 后 `continue`；`_disposed` 标志位控制 `while`。

### RK-08 `_ = Task.Run` 丢弃安装任务，异常无人观察 · **P1**
- **位置**：`src/LunarWard.Installer/GameInstallService.cs:226`（`_ = PrepareGameInstallTaskAsync(context, context.CancellationToken)`）、`src/LunarWard.App/ViewModels/Starward/GameLauncherPageViewModel.cs:1005`、`:1137`（`_ = Task.Run(() => _services.Installer.RunAsync(task))`）
- **触发**：上述任一后台任务抛出非 `TaskCanceledException`/`OperationCanceledException` 的异常（如 `UnauthorizedAccessException`、`IOException` 在 `ChangeToAnotherTask` 之前）。
- **后果**：任务静默消失，`context.State` 停在 `Verifying`，进度条卡住；unobserved 异常只在 GC 后打一行 stderr，用户侧无任何提示。
- **建议修复**：统一一个 `SafeFireAndForget(Task, string op, ILogger)`（内部 `await` + `catch` 落日志 + 置 `Error` 态），替换全部 `_ = Task.Run`；`GameInstallService` 记住 `_runningTasks` 供 `Dispose` 时汇总。

### RK-09 `lock` 内做 SQLite 查询 · **P2**
- **位置**：`src/LunarWard.Launch/PlayTimeService.cs:52-61`（`lock (_gate) { return _store.GetAll(gameBiz).Select(...).OrderByDescending(...).ToList(); }`）、`:65-72` 同理
- **触发**：UI 线程调 `Sessions()`/`TotalFor()`，同时另一线程持锁写（`Record` 同锁）。
- **后果**：DB 查询 + LINQ 排序在锁内执行，UI 线程被迫等待磁盘 IO；游玩记录表大时可见卡顿。
- **建议修复**：锁只保护内存/句柄，查询在锁外做（`GetAll` 返回快照后释放锁再排序），或改为不可变快照 + `Volatile.Read`。

### RK-10 状态事件在线程池线程上抛，靠订阅方自律 · **P2**
- **位置**：`src/LunarWard.App/Services/LauncherService.cs:143-232`（`await _gate.WaitAsync().ConfigureAwait(false)` 之后 `SetState` 在池线程执行）、`:75-79`（`StateChanged?.Invoke`）；订阅方 `src/LunarWard.App/ViewModels/Starward/GameLauncherPageViewModel.cs:545-546`、`:1020-1021`、`src/LunarWard.App/ViewModels/Starward/GameSettingPageViewModel.cs:1527-1528`（都 `Dispatcher.UIThread.Post`，**当前安全**）
- **触发**：新增任何订阅方忘记 Post。
- **后果**：`[ObservableProperty]` 在非 UI 线程触发 → 绑定层直接触控件，Avalonia 抛 `InvalidOperationException` 或静默错绘。
- **建议修复**：在 `SetState` 内统一 `Dispatcher.UIThread.Post(() => StateChanged?.Invoke(...))`，把线程契约写进实现而不是文档；安装进度用的 `Progress<T>`（`GameLauncherPageViewModel.cs:59-60` UI 线程构造）已是正确形态，照抄。

### RK-11 16 处 `new HttpClient`，含每次点击新建 · **P2**
- **位置**：`src/LunarWard.App/ViewModels/Starward/Settings/SettingAboutViewModel.cs:103`（点击时 `using var http = new HttpClient { Timeout = 15s }`）、`src/LunarWard.Games/BilibiliPluginDownloader.cs:117`（`new HttpClient(handler)`，未见 Dispose）；另有 14 处 `private static readonly HttpClient Http = new() { Timeout = 20s }` 的静态复用形态（如 `src/LunarWard.App/ViewModels/Starward/ToolboxPageViewModel.cs:35`）
- **触发**：「检查更新」连点、每次下载插件。
- **后果**：每次实例化新建 `SocketsHttpHandler` 与连接池，短连接不复用、端口耗尽（`TIME_WAIT` 堆积）；handler 不 Dispose 时 socket 泄漏。
- **建议修复**：全部收敛到一个 `IHttpClientFactory`/单例 `HttpClient`（项目里已有 `InstallerHttpClientFactory` 与 `IHttpClientFactoryTests`，App 侧照搬）；必须有 `using` 或由容器托管。

### RK-12 `launch-report.json` 0644 且写入完整环境变量 · **P2**
- **位置**：`src/LunarWard.Launch/LaunchReport.cs:41-48`（`env` / `env_sources` / `command` 全量序列化）、写出点 `src/LunarWard.App/Services/LauncherService.cs:195-202`（catch 后静默忽略）
- **触发**：每次启动游戏。
- **后果**：本机实测该文件是 `-rw-r--r--`，目录 0755；若用户在 profile 里塞了 token（`BILI_TOKEN` 之类），会明文落盘且随 bug 报告被上传。
- **建议修复**：写文件时用 `UnixCreateMode = 0600`；`env` 输出前过白名单（只留 `WINEPREFIX`/`WINE*`/`DXVK*`/`PROTON*`），其余打码。

### RK-13 无 `$HOME` 时回退到 `/tmp` · **P2**
- **位置**：`src/LunarWard.Platform/XdgPaths.cs:63-68`（`home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp"`）
- **触发**：容器/系统账号里 `UserProfile` 与 `HOME` 双空。
- **后果**：`DataHome = /tmp/lunarward`，凭据库落在全局可写的 `/tmp`；攻击者可预建同名目录（0755，属主为攻击者）造成符号链接攻击面，且重启即丢数据。
- **建议修复**：`/tmp` 也不可用时拒绝启动并报错（`try --help` 提示 `XDG_DATA_HOME`），或回退到 `Path.GetTempPath() + $"lunarward-{uid}"` 并 chmod 0700。

### RK-14 `GetFolderPath(UserProfile)` 无空值兜底 · **P2**
- **位置**：`src/LunarWard.App/Services/GameScanner.cs:40`（`Path.Join(home, "games")` 等）、`src/LunarWard.Launch/ProfileConfig.cs:116-118`（`expanded = home + expanded[1..]`）
- **触发**：同 RK-13 的容器场景。
- **后果**：`home == ""` 时 `Path.Join("", "games")` 得相对路径 `games`（按 CWD 解析）；`"~/Games"` 被展开成 `/Games`——扫描不到游戏，或（root 容器里）在根目录造出目录。
- **建议修复**：复用 `XdgPaths` 里已有的「空则回退」逻辑，抽成 `HomeDirectory.Resolve()` 单一实现，禁止直接调 `GetFolderPath`（可用一条 `rg "GetFolderPath\\(Environment.SpecialFolder.UserProfile\\)"` 做回归门禁）。

### RK-15 SQLite 初始化无 `user_version`/无迁移 · **P2**
- **位置**：`src/LunarWard.Launch/Database/LunarWardDatabase.cs:104-352`（一大段 `CREATE TABLE IF NOT EXISTS`，无 `PRAGMA user_version` 读写、无 `ALTER TABLE`）
- **触发**：任何一次表结构变更（加列/改约束）发布后，用户升级。
- **后果**：旧库不会被迁移，新代码读旧表要么列不存在抛 `SqliteException`（启动即崩，因为 8 个 store 在构造期就打开库），要么静默读到默认值。
- **建议修复**：加 `user_version` 阶梯迁移（`if (v < 2) ALTER TABLE ...; v = 2`），迁移包在一个事务里；升级前 `VACUUM INTO` 备份一份（`BackupTo` 已存在，`LunarWardDatabase.cs:87-93`）。

### RK-16 `async void` 出口无 try/catch · **P2**
- **位置**：`src/LunarWard.App/Views/Starward/Pages/GachaPage.axaml.cs:257-278`（`private async void OnUigfRequested()`，`await window.ShowDialog(owner)` 未包 try）
- **触发**：UIGF 导入/导出对话框内异常上抛（导出失败、权限不足）。
- **后果**：`async void` 的异常直接进 `Dispatcher.UnhandledException` → 进程终止；同文件 `:186-218` 的导出路径有 try/catch，导入路径没有，行为不一致。
- **建议修复**：`OnUigfRequested` 整体包 `try/catch (Exception ex) { NotifyError(ex.Message); }`；或改用 `Dispatcher.UIThread.Post(async () => ...)` + 统一 `SafeAsync` 包装器。

### RK-17 安装器 `Dispose` 不回收 ticker 任务 · **P2**
- **位置**：`src/LunarWard.Installer/GameInstallService.cs:75-90`（`_tickerTask = Task.Run(async () => { using var timer = new PeriodicTimer(100ms); ... })`）、`:121-134`（`Dispose` 只 `Cancel`+`Dispose` cts，不 `await _tickerTask`）
- **触发**：`Dispose` 后 `PublishProgress()` 仍在执行（例如 `Progress.Report` 抛异常）。
- **后果**：`_tickerTask` 异常无人观察；`Dispose` 返回时任务可能仍在跑，访问已释放的 cts。
- **建议修复**：`Dispose` 里 `_tickerCts?.Cancel(); try { _tickerTask?.Wait(1s); } catch { }`，循环体包 catch 落日志。

### RK-18 LibVLC `Media` 不释放 · **P2**
- **位置**：`src/LunarWard.App/Services/BackgroundService.cs:150`（`player.Play(new LibVLCSharp.Shared.Media(_libVlc, new Uri(videoPath)))`）
- **触发**：切换/重播背景视频。
- **后果**：每次泄漏一个原生 `Media`（持有解码器与已打开的文件句柄），长期切换后视频文件无法删除/替换。
- **建议修复**：`using var media = new Media(_libVlc, new Uri(videoPath)); player.Play(media);`，或字段持有 `_media` 并在替换时 `Dispose`。

### RK-19 列表 `Clear()` + 逐个 `Add` 刷新 · **P3**
- **位置**：`src/LunarWard.App/ViewModels/GachaViewModel.cs:188-201`（`Records.Clear()` 后最多 500 次 `Add`）、`src/LunarWard.App/ViewModels/ScreenshotViewModel.cs:96-103`（同型）
- **触发**：每次筛选/刷新。
- **后果**：Avalonia 无 `AddRange`，`Reset` + N 次 `Add` 触发 N 次布局/容器生成，500 项时可见掉帧。
- **建议修复**：整体替换集合引用（`Records = new ObservableCollection<T>(...)`，属性是 `[ObservableProperty]` 即可）只触发一次 `Reset`；或先 `Clear` 再 `Add` 期间用 `ICollectionView` 的 `DeferRefresh`。

### RK-20 安装 wine 组件时把所有文件设为全局可执行 · **P3**
- **位置**：`src/LunarWard.Wine/ComponentInstaller.cs:450-457`（对解出的文件 `SetUnixFileMode` 追加 `User/Group/Other Execute`）
- **触发**：安装任何 wine runner/组件。
- **后果**：`~/.local/share/lunarward/runners` 下二进制为 0755，同机其他用户可执行（当前目录 0755 也可遍历）。不是提权链，但违反最小权限。
- **建议修复**：只加 `UserExecute`；目录仍保持 0700（与 RK-01 的目录收紧一起做）。

### RK-21 打开 URL/文件的两种写法不一致 · **P3**
- **位置**：`src/LunarWard.App/Views/Starward/ImageViewWindow2.axaml.cs:470-481`（`new ProcessStartInfo(fileName, argument) { UseShellExecute = true }`，仅 `#if DEBUG` 打日志）；对比 `src/LunarWard.App/ViewModels/Starward/ScreenshotPageViewModel.cs:295-296`、`src/LunarWard.App/ViewModels/Starward/SettingShellActions.cs:22-23`、`src/LunarWard.App/ViewModels/Starward/GachaPageViewModel.cs:580-581`（`FileName = "xdg-open"` + `ArgumentList.Add(path)`）
- **触发**：Release 下 `UseShellExecute` 分支失败（无默认浏览器/xdg 配置损坏）。
- **后果**：静默无反应且无日志；`UseShellExecute` 走 shell 也扩大了 `fileName` 被解释的面。
- **建议修复**：统一到一个 `IUrlLauncher`（`FileName="xdg-open"` + `ArgumentList`），失败返回值上抛到 UI 提示。

### RK-22 启动参数在设置页往返时被重新切分 · **P3**
- **位置**：`src/LunarWard.App/ViewModels/Starward/GameSettingPageViewModel.cs:79`（`StartArguments = string.Join(' ', profile.Args)`，无引号）、`:932-942`（`OnStartArgumentsChanged` → `Profile.Profile.Args = CommandLineSplitter.Split(value)`）、`src/LunarWard.Wine/KeywordReplacer.cs:136-138`、`src/LunarWard.Launch/ProfileConfig.cs:33`（`List<string> Args`）
- **触发**：`Args` 里含空格元素（合法，启动走 `ArgumentList.Add`），用户在设置页改动该输入框。
- **后果**：一个元素被静默拆成两个，后续启动参数错位（`-screen-fullscreen` 变成 `-screen-fullscreen` + 残留参数）。
- **建议修复**：回填时对含空格元素加引号（用与 `Split` 对称的 `Quote`），或把该字段改成可增删的列表控件。

### RK-23 `LogService` 吞掉一切写日志失败 · **P3**
- **位置**：`src/LunarWard.App/Services/BackgroundService.cs:288-301`（`Write(Action)` 包 `try/catch (Exception)` 且注释「日志失败绝不影响主流程」）、`:304-314`（`Dispose` 直接 `_logger.Dispose()`）
- **触发**：日志目录不可写、磁盘满、Serilog sink 抛错。
- **后果**：线上排障时日志静默缺失（RK-03/RK-07 这类本该留痕的路径尤其致命）；退出时未 `Flush` 会丢尾部缓冲。
- **建议修复**：失败计数 + 首次失败写 stderr（`Console.Error.WriteLine`）；`Dispose` 前 `Flush`。至少别让「日志丢了」比「日志没写」更安静。

## 3. 需要实测确认的风险

**已用现有测试核实为安全（不必再查）**：计时器启停与事件退订（`src/LunarWard.App/Views/Starward/Pages/GameLauncherPage.axaml.cs:566-568`/`:580-584`、`src/LunarWard.App/ViewModels/Starward/GameLauncherPageViewModel.cs:1730-1733`、`src/LunarWard.App/ViewModels/Starward/ScreenshotPageViewModel.cs:419-423` 均在 `Dispose` 里 `Stop()`+`-=`）；`Process` 句柄（`src/LunarWard.Launch/LaunchEngine.cs:302` `using (child)`、外部程序 `:113-122`/`:447` 订阅+进程双释放）；HTTP 取消令牌（`Installer/Games/Core` 内不存在无 token 的 `SendAsync/GetAsync`）；UIGF4 导入先全量 `Validate` 再单事务 `Merge`（`src/LunarWard.Launch/Uigf4/Uigf4Service.cs:366-402`）；三处 SQLite `Merge` 都在单事务内（`src/LunarWard.Launch/Database/GachaLogStore.cs:74-125`、`src/LunarWard.Launch/Database/GenshinBeyondGachaStore.cs:120-200`、`src/LunarWard.Launch/Database/SelfQueryLogStore.cs:98-187`）。

**需要跑起来才能定量的项**（现有 `tests/` 覆盖不到：`AcceptanceTests` 只验相位与 shell，`LunarWard.Installer.UnitTests` 只覆盖 `DriveHelper`/`InstallerHttpClientFactory`）：

| 风险 | 现有测试覆盖 | 最小验证方法 |
|---|---|---|
| RK-02 双启动 | 无。`GameInstallService` 在 `tests/` 零命中 | 单测：mock `IInstallerLogger` + 计数型 `GamePackageService`，并发调 `StartOrContinue` 两次后断言下载器只被创建 1 次 |
| RK-03 管道卡死 | 无 | 用 `FakeWine` 造一个向 stdout 写 10 MiB 的子进程，断言 `LaunchAsync` 在 5 s 内返回且退出码正常（当前会挂到超时） |
| RK-04 退出中断 | 无 | 启动一次假安装（`--quit-after 500` 期间触发 `StartOrContinue`），退出后 `find <installdir> -name '*.part'` 应为空 |
| RK-06 内存增长 | 无 | `ps -o rss= -p <pid>` 每 500 次 `GachaIconCache.GetOrLoadAsync` 采样一次，RSS 斜率应近似 0（当前线性增长） |
| RK-19 列表掉帧 | 无 | `--quit-after` 下用 1000 条记录切筛选，Avalonia DevTools 计一次筛选的 UI 线程耗时；>16 ms 即确认 |
| 冷启动基线 | 无 | 已实测：`src/LunarWard.App/bin/Debug/net10.0/LunarWard --quit-after 800` 连跑两次总计 **2044 / 2170 ms**（含 800 ms 常驻）→ 到窗口可用约 **1.24–1.37 s**；`--version` 仅 30–38 ms（不含 Avalonia/XAML） |

**已实测确认（无需再验）**：RK-01——`umask 022` 下对已 `chmod 0600` 的库再写一行，`t.db-wal`/`t.db-shm` 仍是 `0644`，且 `SECRET_COOKIE_PLAINTEXT` 在 `-wal` 中命中 1 次、主库 0 次；`~/.local/share/lunarward` 实测 `drwxr-xr-x`，主库 `-rw-------`。**这正是现有测试漏掉的维度**：`tests/LunarWard.UnitTests/GameAccountStoreTests.cs:66-69` 断言的是 `File.GetUnixFileMode(Path.Join(_dir, LunarWardDatabase.FileName))`，从未看过 `-wal`。

## 4. 建议修复批次

**批次 1：止血（凭据 + 双启动，1 天）** —— RK-01、RK-02
- 顺序：目录 0700 → `-wal/-shm` 幂等 chmod → 退出 checkpoint；再改 `StartOrContinue` 单飞 + 删 UI 两处 `Task.Run`。
- 验收：`stat -c '%a %n' ~/.local/share/lunarward ~/.local/share/lunarward/LunarWardDatabase.db*` → 目录 `700`、三个 db 文件 `600`；`dotnet test tests/LunarWard.UnitTests --filter "FullyQualifiedName~GameAccountStore"` 通过且新增「wal 也是 600」断言；新增双启动单测通过。

**批次 2：进程与写入健壮性（2 天）** —— RK-03、RK-04、RK-05、RK-08、RK-17
- 顺序：日志泵改「drain 不落盘」+ 失败降级（先做，用户可见收益最大）→ 退出链路补 `ShutdownInstaller` → `AtomicFile` 改 `UnixCreateMode` + finally 清理 → `SafeFireAndForget` 统一收口。
- 验收：`dotnet build LunarWard.slnx`（0 error）；`dotnet test LunarWard.slnx`（589 用例不回归）；新增 10 MiB stdout 用例通过；`ls ~/.local/share/lunarward/*.tmp` 为空。

**批次 3：泄漏与生命周期（2 天）** —— RK-06、RK-07、RK-18、RK-11
- 验收：`dotnet test`；连续切换背景视频 50 次后 `ls /proc/<pid>/fd | wc -l` 稳定；`rg -c "new HttpClient" -g '*.cs' src/` 降到 ≤2。

**批次 4：平台假设、性能与一致性（按需）** —— RK-09、RK-10、RK-12、RK-13、RK-14、RK-15、RK-16、RK-19、RK-20、RK-21、RK-22、RK-23
- 验收：`rg "GetFolderPath\(Environment.SpecialFolder.UserProfile\)" -g '*.cs' src/` 只剩 `XdgPaths.cs` 一处；新增单测覆盖 `XdgPaths` 的 `home == ""` 分支（构造 `new XdgPaths()` 时注入空 home 需先抽参数，或直接断言回退值不是 `/tmp` 且目录 mode 为 0700）；`dotnet test` 全绿。

> 长期门禁建议：把 RK-01（`stat` 权限断言）、RK-14（`GetFolderPath` 唯一入口）、RK-19（无 `Clear()+Add` 循环）三条做成 `scripts/check-risks.sh` 挂进 CI——这三类问题靠人工 review 必然复发。
