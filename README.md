# LunarWard

Linux 下的《原神》/《绝区零》启动器，定位是 **Starward 在 Linux 的重实现**。

不是 AGL（an-anime-team 的 `anime-games-launcher`）的改造，也不是从零造轮子——
而是在 **复用 `Starward.Core` 全部 API/模型层** 的前提下，重写平台相关的启动、
运行时管理与 UI 视图。

> 当前状态：**设计阶段，尚无代码**。本文档集是与需求方多轮确认后的定稿，
> 供后续会话直接实施。

---

## 文档索引

| 文档 | 内容 | 何时读 |
|---|---|---|
| [00-DECISIONS.md](docs/00-DECISIONS.md) | 已定决策与理由、明确否决的方案 | **先读这个** |
| [01-RESEARCH.md](docs/01-RESEARCH.md) | 参考项目实证结论、事实核查记录 | 实施前，避免重复调研 |
| [02-ARCHITECTURE.md](docs/02-ARCHITECTURE.md) | 技术选型（**Avalonia**）、仓库结构、依赖核验 | 搭建骨架前 |
| [03-LAUNCH-ENGINE.md](docs/03-LAUNCH-ENGINE.md) | 相位化启动引擎（**本项目最核心的技术**） | 写启动逻辑前 |
| [04-FEATURES.md](docs/04-FEATURES.md) | 外部程序、区服切换、运行时、env、UI | 逐模块实施时 |
| [05-ROADMAP.md](docs/05-ROADMAP.md) | 里程碑、测试、验收清单 | 排期时 |
| [06-OPEN-QUESTIONS.md](docs/06-OPEN-QUESTIONS.md) | **尚未解决的问题**，需需求方拍板 | 开工前必须清空 |

---

## 一分钟理解这个项目

### 为什么不能直接用现成的

| 候选 | 为什么不行 |
|---|---|
| Starward 本身 | `net10.0-windows` + WinUI3，Windows 专属 |
| AGL | 差距较大的另一个项目；没有 B 站渠道、没有"首个 wine 客户"顺序保证、没有外部程序模型 |
| an-anime-team SDK | 上游自述 "legacy, no deep changes"；缺系统 wine / Proton / 自定义运行时 |
| 从零写 | 要重写 Starward.Core 的 236 个文件 |

### 关键复用点

`Starward.Core` 是 `net10.0`（**非** `-windows`）、零 NuGet 依赖、236 个 .cs 文件、
**仅 1 处**真正调 Windows 注册表。可以直接原样引用，继承全部抽卡/便笺/公告/
HoYoPlay/UIGF 能力。

### 本项目的真正增量

1. **相位化启动引擎** —— 保证"游戏是该 wine prefix 的第一个客户端"这条不变式，
   把三个参考项目各自用 shell 脚本手工维持的约束上升为架构
2. **通用外部程序模型** —— 后启动与补丁注入统一（同一个东西，靠 `Trigger` 区分）
3. **分层运行时** —— Managed / System / Proton / Custom × 四作用域
4. **区服切换** —— B 站渠道（Starward 有，AGL 没有）
5. **环境 shell** —— 一键进入与游戏完全一致的 env/cwd

---

## 快速开始（给实施者）

```bash
# 1. 确认工具链
dotnet --version        # 需 10.x；本机 10.0.302
pacman -Q libvlc        # 动态背景依赖；本机已装 3.0.24（或 apt install libvlc5）

# 2. 读文档
cat docs/00-DECISIONS.md docs/06-OPEN-QUESTIONS.md

# 3. 清空 OPEN-QUESTIONS 后再开始 M0
```

---

## 许可

建议 **MIT**（与 Starward 一致）——最终以 `docs/06-OPEN-QUESTIONS.md` 的结论为准。

不打包任何补丁二进制（`Hk4eTouch` 是 AGPL-3.0、`ZZZ-touch-unlocker` 无 LICENSE），
全部由用户自备，以此规避分发风险。
