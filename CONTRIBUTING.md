# 参与开发（Contributing）

感谢你愿意改进这个项目。本项目是上游派生版，且上游没有授权（见 [`ATTRIBUTION.md`](ATTRIBUTION.md)），因此协作方式与普通开源仓库略有不同，请先读完本文。

## 1. 前置条件（重要）

- **不要搬运外部代码**：不要在 PR 里粘贴来自其他项目、论坛或 AI 生成但来源不明的成段代码。上游未授权，本仓库无法接受会引入新权利主体的代码。
- 新增或修改的代码默认视为你**自己撰写**且你有权提交；你仍需在 PR 描述里说明思路来源（例如“参考上游 PR #1 的校验思路，代码为本仓库自行实现”），并更新 [`CONTRIBUTIONS.md`](CONTRIBUTIONS.md)。
- 不接受把本项目重新许可（relicense）的改动；不要为本仓库添加 MIT/Apache/GPL 等协议文件。
- 涉及上游原有行为（曲谱语法、文件头、BPM/gap 语义、试听、MIDI、游戏输入时间线）的改动，必须说明兼容性与回退方式。

## 2. 开发环境

- Windows 10/11 + **.NET 8 SDK**（`net8.0-windows`，WPF）。
- 不需要 Visual Studio：命令行 `dotnet` 即可编译与运行全部测试。
- 仓库没有 `.sln`，直接构建项目文件 `HarpKit.csproj`。

## 3. 提交前必须执行的检查

```powershell
dotnet build .\HarpKit.csproj -c Release
dotnet build .\Tests\ParserTests.csproj -c Release
dotnet run   --project .\Tests\ParserTests.csproj -c Release

dotnet build .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
dotnet run   --project .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
```

- 要求：**0 警告 0 错误**，核心检查全部 `PASS`，WindowsSmoke 全部 `PASS`。
- 测试项目是自定义控制台检查，**不能用 `dotnet test` 代替**。
- WindowsSmoke 会创建窗口并注册全局热键，**不要在游戏进行中或正式使用时运行**；它不验证真实音频与游戏输入，改过调度/热键/取消逻辑后必须由人手实机复验。
- 只改文档（`*.md`、`*.txt`）时，至少执行一次 `dotnet build .\HarpKit.csproj -c Release`，确认未误改源码。

推荐的定向检查（快速、不占用热键）：

```powershell
dotnet run --project .\Tests\ParserTests.csproj -c Release -- --score-library-only
dotnet run --project .\Tests\ParserTests.csproj -c Release -- --playback-timing-only
dotnet run --project .\Tests\ParserTests.csproj -c Release -- --midi-import-only
```

## 4. 提交信息与分支

- 提交信息用**祈使句 + 范围**，首行不超过 72 字符，例如：
  - `Fix library hotkey rollback when registration fails`
  - `Docs: record v0.5.0 validation boundary`
- 一个 PR 只做一件事；不要混入无关的格式化、重命名或全仓行尾转换。
- 行尾与编码：仓库统一 **LF**（`.gitattributes` 已声明 `eol=lf`）；`使用说明.txt` 与 `Build-Release.ps1` 保留 **UTF-8 BOM**（Windows PowerShell 5.1 中文兼容所需），不要删除它们的 BOM。
- 版本号（`HarpKit.csproj` 的 `<Version>`）、发行包与 GitHub Release 由维护者统一处理；PR 不要自行升版。

## 5. 需要提供的验证证据

PR 描述中请写清：

1. 你实际运行的命令与结果（测试计数、`PASS`/`FAIL`、编译告警数）；
2. 哪些行为是**实机验证过**的（Windows 窗口、热键、试听、游戏内演奏），哪些只是自动检查；
3. 明确写出**未验证**的边界，不要用“应该没问题”替代证据；
4. 若改动了界面文案，说明中文/英文两种语言下的显示效果。

## 6. 文档与语言

- 面向用户的文档保持中文为主（`README.md`、`使用说明.txt`、`制谱说明.txt`），界面文案必须同时提供中英两套（见 `Loc.cs` 的用法：中文原文为键，英文在表中；缺项回退中文）。
- 用户界面字符串不要硬编码，统一走 `Loc.T()` / `Loc.F()`；新增词条需同时给出英文。
- 不要删除或改写上游历史文档（`UPDATE-*.md`、`VALIDATION-*.md`、`RELEASE-*.md`）；新记录请另建文件或追加“本轮”小节。

## 7. 行为准则

保持友善、就事论事：讨论代码与证据，不攻击个人；不接受发布破解、外挂或规避游戏反作弊的内容；不接受要求作者为此承担因游戏自动化导致的账号后果的诉求。
