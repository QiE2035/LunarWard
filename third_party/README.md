# third_party

**本目录为空是有意的，不是遗漏。**

`docs/02-ARCHITECTURE.md` 的仓库结构图里写了 `third_party/Starward/`（git submodule，
只读参考 + 移植来源），但同一份文档 §3.1 又明确推荐：

> **推荐用 `git subtree`**（而非 submodule），因为我们要就地打补丁

两者是冲突的。本项目采用了 **§3.1 的方案**，因此：

| 文档里的位置 | 实际做法 |
|---|---|
| `third_party/Starward/`（submodule） | **不建**。改用 vendored 副本 |
| 移植来源 | `src/LunarWard.Core/`（Starward.Core 的 236 个 `.cs`，已改命名空间） |
| 安装器来源 | `src/LunarWard.Installer/`（Starward.RPC/GameInstall 的移植） |

## 为什么选 subtree 而不是 submodule

1. **要就地打补丁**：`GachaLogClient.cs` 的 `Registry.GetValue` 必须换成
   `IGameInstallPathProvider`。submodule 里改文件既别扭又容易在 `submodule update` 时丢失。
2. **构建不该依赖外部仓库**：`dotnet build LunarWard.slnx` 必须能在一个干净 clone 里直接跑通，
   不能要求先 `git submodule update --init --recursive` 且能访问 GitHub。
3. **来源可追溯**：每个移植文件保留上游路径与类名，`THIRD-PARTY-NOTICES.md` 记录来源与许可（MIT）。

## 上游在哪里

本机参考副本（只读，不在本仓库内）：

```
/home/qie2035/projects/csharp/Starward
```

迁移到新机器时，把它 clone 到任意位置即可对照；**不需要**放进本目录。

## 如果将来需要第三方源码随仓库分发

放在这里，并在 `THIRD-PARTY-NOTICES.md` 里补一条。注意
`docs/00-DECISIONS.md` D11：**不打包任何补丁二进制**。
