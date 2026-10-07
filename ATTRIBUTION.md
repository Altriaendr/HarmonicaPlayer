# 来源与署名（Attribution & Notices）

本文件说明本仓库的来源、著作权归属与许可状态。**发布任何副本、发行包或衍生版本时，请连同本文件一起保留。**

## 1. 本仓库的性质

本仓库是 **[lanselanlanxi-wq/HarmonicaPlayer](https://github.com/lanselanlanxi-wq/HarmonicaPlayer) 的派生版本（derivative work）**，不是原项目，也不代表原作者发布。

| 项目 | 内容 |
| --- | --- |
| 上游项目名 | HarmonicaPlayer |
| 上游仓库 | https://github.com/lanselanlanxi-wq/HarmonicaPlayer |
| 上游作者 | GitHub [@lanselanlanxi-wq](https://github.com/lanselanlanxi-wq)（下称“原作者”） |
| 派生基线提交 | `a24e6565ef01ba3fdad7ae21afd2b442c0df29d3`（上游 `main`，即上游已发布的 v0.4.0 源码） |
| 派生内容 | 曲谱库与每曲快捷键（v0.5.0 源码增量）、界面改版与中英双语、4:3 浮窗、矢量图标、项目更名 HarpKit 等；详见本仓库提交历史、`UI-REFRESH.md`、`UPDATE-0.5.0.md` |
| 上游曲谱格式 | `@format=HarmonicaPlayer/1`（本派生版**未改动**曲谱语法与文件头） |

本派生版中，C# 命名空间仍保留为 `HarmonicaPlayer`，以避免破坏与上游源码、曲谱格式的兼容性；可执行文件名与界面标题改为 HarpKit。

## 2. 著作权归属与许可状态

- 上游代码、文档、界面设计、曲谱格式与音源处理脚本的著作权归**原作者所有**。本仓库对其的修改、翻译与排版属于派生作品，其中源于上游的部分，著作权仍归原作者。
- **上游仓库未提供任何开源许可（上游仓库没有 LICENSE 文件）。** 因此本派生版**不对上游部分授予任何许可**，本仓库整体按“**保留所有权利 / No license granted**”处理：未授予使用、复制、修改、分发、再许可或商业使用的许可，除非原作者另行给出书面许可。
- 本仓库维护者新增的内容（例如 `Loc.cs`、`UiIcon.cs`、`UiPalette.cs`、`PlayerWindow.Shell.cs`、`PlayerWindow.Theme.cs`、`PlayerWindow.Library.cs`、`PlayerWindow.Shortcuts.cs`、`ScoreLibrary.cs`、`Assets/Icon/`、`Tools/IconGen/` 等）同样**不授予许可**，除非维护者另行声明。
- 本仓库**有意不添加** MIT/Apache/GPL 等协议文件：在未获原作者授权的情况下为派生版统一附加开源协议，等同于替原作者授权，是不正确的做法。
- 如果你是原作者，希望本仓库采用某个协议、调整署名方式、或要求下线本仓库，请通过本仓库的 Issue 联系维护者，我们会配合处理。

## 3. 历史与外部贡献者

上游项目已合并/参考的社区贡献，按其原始记录保留在 [`CONTRIBUTIONS.md`](CONTRIBUTIONS.md)：

- [@izumikonata10](https://github.com/izumikonata10) — 上游 PR #1（校验与确定性等待思路，音频部分未整体采纳）。
- [@daailiuying](https://github.com/daailiuying) — 上游 PR #3（设置保存 generation 方案，防旧保存结果覆盖新提示）。
- 上游部分提交由 GitHub Copilot 参与生成，保留其 `Co-authored-by` 记录；本派生版按事实记录，不改变原作者信息。

## 4. 第三方组件与素材

- **口琴采样**：Versilian Studios LLC 的 Versilian Community Sample Library（VCSL），授权 **CC0-1.0**。完整来源、固定上游提交、逐文件 SHA256、修改说明见 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)、[`Assets/Harmonica/SOURCES.json`](Assets/Harmonica/SOURCES.json) 与 [`Assets/Harmonica/LICENSE-CC0.txt`](Assets/Harmonica/LICENSE-CC0.txt)。
  Hohner 为原乐器品牌，本软件与乐器厂商不存在隶属关系。
- **.NET / WPF**：Microsoft，按其各自许可（MIT 等）提供；本仓库只引用框架，不重新分发框架源码。
- 本项目图标（`Assets/Icon/harpkit.svg`、`Assets/Icon/harpkit.ico`）为本派生版新增，其使用同样受本文件第 2 节约束。

## 5. 免责声明

- 本软件通过**模拟键鼠操作**在游戏内演奏，可能违反相关游戏的用户协议，存在**账号处罚风险**；请先使用“仅测试（不会操作游戏）”模式，实际使用风险由使用者自行承担。
- 本派生版**未经原作者审核**，可能与上游不一致或存在缺陷；本地试听音色与实际游戏音色、绝对音高不保证一致。
- 本软件不联网、不上传曲谱或录音；内置采样已随程序分发。

---

*Attribution notice: this repository is an unofficial derivative of [lanselanlanxi-wq/HarmonicaPlayer](https://github.com/lanselanlanxi-wq/HarmonicaPlayer) based on commit `a24e6565ef01ba3fdad7ae21afd2b442c0df29d3`. The upstream project is provided **without any license**, so no license is granted for the upstream portions or for this repository as a whole. Third-party harmonica samples are CC0-1.0 (VCSL).*
