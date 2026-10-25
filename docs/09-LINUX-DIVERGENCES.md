# 09 — Linux 与上游的差异清单（不可移植项与替代方案）

> Starward 是 Windows 应用；LunarWard 的目标是「Linux 上能用、UI 与上游一致」。
> 本文把**所有主动与上游不一致的地方**集中登记：为什么不做、做了什么替代、在
> `08-UI-PARITY.md` 的哪一行。原则：能移植的必须移植；不能移植的显式说明，
> 不画假按钮、不假装成功。

## 一、没有 Linux 等价物（保留结构，禁用并说明）

| 能力 | 上游做法 | LunarWard | 理由 / 替代 |
|---|---|---|---|
| 卸载游戏 | `UninstallGame` 调 Windows 卸载器 | 按钮保留、禁用并给原因 | Linux 无注册表卸载器；删除目录由用户自理 |
| 命令行启动游戏 | 写 `.cmd`/快捷方式 | `lunarward startgame --biz` | 见 README「命令行」 |
| 渠道 SDK 分组（腾讯/哔哩哔哩） | 注册表读取渠道分组 | 只保留官方区服 | 渠道 SDK 不提供 Linux 客户端 |
| 背景音量 / 系统媒体控制 | WinRT `MediaPlayer` 音量 | 视频静音播放 | 没有跨桌面音量总线 |
| HDR / 自动 HDR | Windows 图形设置 | 不做 | 与 Wine/HDR 输出无关，由显示栈决定 |
| 内嵌更新器 | MSIX 更新 | 「检查更新」打开 releases 页 | Linux 走发行包（deb/AppImage） |
| 全局热键注册 | `RegisterHotKey` | 显示快捷键但禁用 | X11/Wayland 全局热键需 portal，未接 |
| Discord RPC | 命名管道 | 「RPC 服务」段保留、禁用 | 需要额外 IPC 客户端，非首期目标 |
| 崩溃上报 | WER + 自建服务 | 只写本地日志 / launch-report | 不向外发送数据 |
| 手柄增强（DS4Windows 式） | ViGEm 驱动 | 保留入口、禁用 | 需要内核驱动 |

## 二、素材缺失（占位结构 + 注释，不画假图）

| 位置 | 上游素材 | LunarWard |
|---|---|---|
| 卡池统计卡底图 | `Assets/Gacha/*.png` | 纯色圆角底（parity 抽卡页行） |
| 空态插画（抽卡/账号记录/千星奇域） | 上游插画 | 星形/图标占位 + 文案 |
| 千星奇域远程图标 | 远程 PNG | 文字首字占位 |
| 应用图标 | `Assets/AppIcon.ico` | 生成的 `lunarward.ico`（占位） |

## 三、平台差异（同一功能、不同实现）

| 能力 | 上游 | LunarWard |
|---|---|---|
| 系统托盘 | `H.NotifyIcon` | `TrayIcon` + 自建菜单（`76e749d`） |
| 单实例 | 命名 Mutex | Unix socket（`c635365`） |
| 关闭窗口 | 托盘/退出 | 托盘/退出（`CloseWindowOption`） |
| 开机自启 | 注册表 Run | XDG autostart `.desktop`（`9366077`） |
| URL 协议 | `starward://` 注册表 | `lunarward://` + 桌面文件 MimeType（`cfef0a5`） |
| 文件占用大小 | 文件 ID 去重 | 同名同大小估算（`GameSelector` 行） |
| MD5 校验 | 上游清单 | 未实现（下载器用 HTTP Range + 长度校验） |
| 检查更新 | 自建 API | GitHub releases API（`a6f0464`） |
| 备份数据库 | `File.Copy(StarwardDatabase.db)` | `VACUUM INTO` 热备份（WAL 合并，单文件） |

## 四、Linux 独有（上游没有，追加在对应页面）

| 能力 | 位置 | 入口 |
|---|---|---|
| GameMode / MangoHud 开关 | 游戏设置 › 启动参数 | `9d026e0` |
| gamescope（Wrapper 链） | 游戏设置 › 启动参数 | 既有 `WrapArgs` |
| Steam 库游戏扫描 | 游戏选择 › 自动查找 | `c6f17e4` |
| Steam/Proton 检测 | 游戏设置 › wine 运行时 | `RuntimeScanner` |
| 托管 runner 安装 | `lunarward components install` + 设置页组件源 | `d309637` |
| 崩溃恢复验证 | 验收文档 R15 | `8a4a194` |
| 关于页「运行环境」（内核 / 会话 / 显卡驱动 + PCI ID） | 设置 › 关于 | `LinuxSystemInfo`，只读 `/proc`、`/sys` |
| 开机自启（XDG autostart） | 设置 › 通用 | `9366077` |
| 系统托盘 / 单实例 | 关闭窗口行为 | `76e749d`、`c635365` |
| URL 协议 `lunarward://startgame` | 桌面文件 | `cfef0a5` |

## 五、维护规则

- 新增「不做」的决定必须同步本表 + `08-UI-PARITY.md` 对应行，并在提交信息里写明理由。
- 上游升级后如果补上了等价能力（例如 Wine 侧 HDR），以新证据为准更新本表。
