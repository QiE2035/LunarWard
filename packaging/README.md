# 打包与安装

本目录提供 Linux 下的打包资产。

| 文件 | 用途 |
|---|---|
| `lunarward.desktop` | 桌面入口（含 URL scheme handler） |
| `PKGBUILD` | Arch / CachyOS 打包脚本 |

图标复用应用内资产：`src/LunarWard/Assets/lunarward.ico`（16×16，32bpp）。
**不要**在本目录再放一份副本——避免图标改了但包里还是旧图。

---

## 一、依赖说明

### 1.1 必需

| 依赖 | 类型 | 为什么必需 |
|---|---|---|
| **`dotnet-runtime>=10.0`** | **运行时硬依赖** | 发行包**不是** self-contained（`--self-contained false`）。没有 .NET 10 运行时，`dotnet LunarWard.dll` 直接失败 |

### 1.2 可选（`optdepends`）

| 依赖 | 提供的工具 | 缺失时的行为 |
|---|---|---|
| **`ffmpeg`** | `ffmpeg` 命令 | 视频背景**回退静态图**，不崩溃 |
| **`hdiffpatch`** | `hpatchz` | 差量补丁失败 → 记录 `Patch file failed` → 该文件走**全量下载** |
| **`7zip`** | `7z` / `7za` | 抛 `InstallerCapabilityException`；**纯 `.zip` 仍可解**（BCL `ZipFile` 回退） |

> **动态背景依赖 `ffmpeg` 命令（不是 libvlc）。**
> 用 `ffmpeg` 子进程解码可以绕开两件事：VLC 的 X11 子窗口在 Wayland 会话里嵌不进去，
> 以及 Arch 把 VLC 拆成 `vlc-plugin-*` 多个包后经常缺解码器（连 H.264 都解不了）。
> 这是本机实测踩过的坑，见 `docs/M0-RESULTS.md` §2.2 与
> `docs/TROUBLESHOOTING.md` §5.1。

### 1.3 构建期（`makedepends`）

| 依赖 | 用途 |
|---|---|
| **`dotnet-sdk>=10.0`** | `dotnet publish` |

`options=('!strip')` 是**有意为之**：.NET 程序集的 IL 元数据会被 `strip`
破坏或改变签名，托管程序集不应 strip。

---

## 二、构建与安装

### 2.1 从本仓库直接打包

```bash
cd /path/to/lunarward/packaging
makepkg -si
```

- `makepkg` 在 `packaging/` 下运行；`PKGBUILD` 里的路径都**相对仓库根**
  （`realpath "$startdir/.."`），不需要额外挪文件。
- `source=()` 为空：源码就是本仓库，不下载任何东西。
- `-s` 自动装 `makedepends`；`-i` 装完后立即安装。

只构建不安装：

```bash
makepkg -f            # 产出 lunarward-0.1.0-1-x86_64.pkg.tar.zst
makepkg --printsrcinfo > .SRCINFO
```

### 2.2 手动构建（不走 makepkg）

想跳过包管理器时，`dotnet publish` 的等价命令：

```bash
cd /path/to/lunarward

dotnet publish src/LunarWard/LunarWard.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o /tmp/lunarward-out

/tmp/lunarward-out/LunarWard          # 直接跑（等价于 dotnet LunarWard.dll）
```

`--self-contained false` 意味着目标机器**必须**有 .NET 10 运行时。
若要做免依赖分发包，改成 `--self-contained true -p:PublishSingleFile=true`，
但包体积会涨到 ~70 MB 以上。

### 2.3 安装后的文件布局

| 路径 | 内容 |
|---|---|
| `/usr/bin/lunarward` | 包装脚本（`exec dotnet /usr/lib/lunarward/LunarWard.dll "$@"`） |
| `/usr/lib/lunarward/` | 全部托管程序集 + 原生依赖（`*.so`） |
| `/usr/share/applications/lunarward.desktop` | 桌面入口 |
| `/usr/share/pixmaps/lunarward.ico` | 图标 |
| `/usr/share/licenses/lunarward/` | `LICENSE`、`THIRD-PARTY-NOTICES.md` |
| `/usr/share/doc/lunarward/` | `USER-GUIDE.md`、`TROUBLESHOOTING.md` |

### 2.4 注册 URL scheme（可选）

`.desktop` 里声明了 `MimeType=x-scheme-handler/lunarward;`，
但**需要手动注册**才生效：

```bash
xdg-mime default lunarward.desktop x-scheme-handler/lunarward
xdg-mime query default x-scheme-handler/lunarward    # 验证
```

### 2.5 刷新桌面数据库

装完后图标/菜单没出现时：

```bash
update-desktop-database ~/.local/share/applications 2>/dev/null || true
sudo update-desktop-database /usr/share/applications 2>/dev/null || true
gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
```

---

## 三、卸载

```bash
# 包管理器卸载（推荐）
sudo pacman -Rns lunarward
```

`-n` 会一并清掉不再被依赖的孤儿包（**注意**：`dotnet-runtime` 若被别的包用着则不会删）。

手动安装的情况下：

```bash
sudo rm -rf /usr/lib/lunarward \
            /usr/share/applications/lunarward.desktop \
            /usr/share/pixmaps/lunarward.ico \
            /usr/share/licenses/lunarward \
            /usr/share/doc/lunarward
sudo rm -f  /usr/bin/lunarward
```

### 3.1 用户数据不会被删

包管理器**只删 /usr 下的文件**。下面这些是你的数据，卸载时保留：

| 路径 | 内容 | 想清掉时 |
|---|---|---|
| `~/.config/lunarward/` | 全部配置（`config.json`） | `rm -rf ~/.config/lunarward` |
| `~/.local/share/lunarward/` | runners / dxvks / **prefixes** / components / 启动报告 | ⚠️ **里面有 wine prefix**，删掉等于清空游戏运行环境 |
| `~/.cache/lunarward/` | 日志 / 背景缓存 / B 站插件缓存 | 可安全删除 |

> ⚠️ **`~/.local/share/lunarward/prefixes/` 里有 wine prefix**，
> 含游戏存档相关的 wine 注册表与用户目录。删之前确认存档已在游戏侧同步上云，
> 或者先备份。**不要**习惯性地对整个 `~/.local/share/lunarward` 执行 `rm -rf`。

彻底清理（确认不再使用后）：

```bash
rm -rf ~/.cache/lunarward        # 缓存，安全
rm -rf ~/.config/lunarward       # 配置，会丢设置
# 谨慎：先备份 prefix
cp --reflink=auto -a ~/.local/share/lunarward/prefixes ~/lunarward-prefixes-backup
rm -rf ~/.local/share/lunarward
```

---

## 四、其他发行版

`PKGBUILD` 是 Arch 专用语法。其他发行版用 §2.2 的 `dotnet publish`
命令构建，然后手工摆放 §2.3 的文件布局即可。依赖包名对照：

| 依赖 | Debian / Ubuntu | Fedora |
|---|---|---|
| .NET 10 运行时 | `dotnet-runtime-10.0` | `dotnet-runtime-10.0` |
| 动态背景 | `ffmpeg` | `ffmpeg` |
| 差量更新 | `hdiffpatch` | `hdiffpatch` |
| 解包安装 | `7zip` | `p7zip` |

---

## 五、发布包内容约束

打包脚本**只**安装本仓库构建出来的托管程序集与文档。验收时确认：

```bash
# 1) 包内不应有任何 .exe / 补丁 DLL
tar -tf lunarward-*.pkg.tar.zst | grep -iE '\.exe$|inject|touch' && echo "❌ 含补丁文件" || echo "✅ 无补丁"

# 2) 必须有 MIT 许可与第三方声明
tar -tf lunarward-*.pkg.tar.zst | grep -E 'licenses/lunarward/(LICENSE|THIRD-PARTY-NOTICES.md)'

# 3) 包装脚本语法正确且指向正确路径
bash -n packaging/PKGBUILD
```

**为什么这条是硬约束**：`Hk4eTouch` 是 AGPL-3.0、`ZZZ-touch-unlocker` 无 LICENSE，
分发它们的二进制会让整个发行包陷入许可冲突。详见
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) §三与 `docs/00-DECISIONS.md` D11。
