# v0.5.0 验证（曲谱库与单曲快捷键）

v0.5.0 是源码内的本地增量：功能、检查与文档已完成，**尚未升版、未打包、未提交**。下面只记录本轮实际执行的命令与真实输出，以及仍未验证的边界；不含维护者的游戏验收结论，也不把后台结果写成实机结论。

## 本轮实际执行

在 `D:\HarmonicaPlayer` 下执行（.NET 8 SDK，Release）：

```powershell
dotnet build .\Tests\ParserTests.csproj -c Release
dotnet run --project .\Tests\ParserTests.csproj -c Release -- --score-library-only
dotnet run --project .\Tests\ParserTests.csproj -c Release
dotnet build .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
dotnet exec .\Tests\WindowsSmoke\bin\Release\net8.0-windows\WindowsSmokeTests.dll
dotnet build .\HarmonicaPlayer.csproj -c Release
```

- 核心测试工程 Release 编译：**0 警告 0 错误**。
- 仅曲谱库分支：`PASS score library: 56 tests`，退出码 0。
- 核心入口全量：各分组均 PASS，退出码 0——32、62、71（含异步设置写入）、99（含 v0.2.0）、v0.2.1 54、gap 24、v0.3.0 30、v0.3.1 49、v0.3.2 88、v0.3.3 65、playback timing 72、playback guard 15、MIDI import 99、pitch/input 157（无原生输入）、score library 56。各分组存在重叠，**不重复相加**，沿用既往“752项”的说明口径不再另报总数。
- WindowsSmoke Release 编译：**0 警告 0 错误**。
- 主工程 Release 编译：`dotnet build .\HarmonicaPlayer.csproj -c Release` **0 警告 0 错误**，退出码 0（本轮文档更新与行尾调整未影响编译）。
- WindowsSmoke 实跑：**11 组全部 PASS**，退出码 0——`idle`、`pending-save`、`countdown`、`playback`、`error-recovery`、`MIDI-window`、`settings-generation`、`playback-restart`、`document UI`、`v0.3.0 audio UI`、`library UI: card, add, bind, conflict, hotkey load, plays switch, playback lock, remove, persist`。
- 行尾统一后复跑：仓库既有源码与文档都是 LF，本轮有5个源码文件被整文件写成 CRLF（`ScoreLibrary.cs`、`PlayerWindow.Library.cs`、`PlayerWindow.Shortcuts.cs`、`PlayerWindow.Theme.cs`、`Tests\ScoreLibraryTests.cs`，其中后两者的整个文件行尾被改写，会产生“整文件改动”的噪声差异）。已统一回 LF——这些文件里没有逐字字符串（`@"`）或原始字符串（`"""`），换行不会进入任何字符串常量，随后**重新编译并复跑核心入口全量与 WindowsSmoke 11 组，结果仍全部 PASS、退出码 0**。
- 顺带核对：现有文档（README、使用说明、制谱说明、docs\NEXT_UPDATE.md）与本轮新增的 UPDATE-0.5.0.md、VALIDATION-0.5.0.md 均为 LF；`使用说明.txt` 保留原有 UTF-8 BOM。

本轮窗口运行全部使用“仅测试（不会操作游戏）”与日志路径：不发送真实键鼠、不发声、不操作游戏、不要求管理员权限。

本轮日志位于 `%TEMP%\hp_read\`（第一轮 `smoke_build6.txt`、`smoke_run8.txt`、`pt_build2.txt`、`pt_lib.txt`、`pt_all2.txt`；行尾统一后的复跑 `v2_pt_build.txt`、`v2_lib.txt`、`v2_all.txt`、`v2_smoke_build.txt`、`v2_smoke_run.txt`；主工程 `v3_main_build.txt`），未复制进仓库；如需留档，可照 v0.4.0 的做法另存到 `bin\validation-20261006\`。

## 新增与扩写的检查

- 新增 `Tests\ScoreLibraryTests.cs`（56项，纯逻辑、不创建窗口）：快捷键编号从100起并与开始/停止隔离、`IndexFromHotkeyId`/`IsLibraryHotkeyId` 边界、列表上限60、路径必须完整且不带控制字符、曲名长度上限、显示名与 `IndexOfPath` 规则、`Conflict`/`HotkeyTaken`（与开始键、停止键、重复绑定、乱值）、`Sanitize` 的丢弃与清除计数及顺序保持、`HotkeyController` 注册顺序（停止→曲谱库→开始）与单键失败隔离。
- 新增 `Tests\Program.cs` 的 `--score-library-only` 分支，并把 `ScoreLibraryTests.Run()` 接入全量流程。
- 扩写 `Tests\WindowsSmoke\WindowsSmokeTests.cs`：新增 `LibraryWindowTests()` 与 `TestScoreDialogs.OpenLibrary` 替身，覆盖空库占位、添加两首、绑定 F1、与开始键冲突被拒、快捷键加载曲谱、开关文案与设置值、演奏中锁定曲谱库、移除不影响文件与编辑器、快捷键触发倒计时、关闭后设置落盘。

## 本轮修复的测试问题

1. `LibraryWindowTests` 原来在 `await LibraryActivateAsync(...)` 之后才检查状态文本，但该调用会 `await` 整首演奏，状态此时已被“演奏结束”覆盖。改为拿到返回的 `Task` 不 await，用 `WaitUntil(...Contains("秒后开始"))` 观测倒计时，关窗后再 `WaitAsync` 收尾，顺带验证关窗会取消倒计时。
2. 同一场景原先把“按快捷键立即演奏”开关复位为关闭，结尾却断言设置文件里是打开，自相矛盾。改为保持开关打开、在倒计时阶段断言其状态，再由关窗保存路径验证 `LibraryHotkeyPlays` 落盘。
3. 复核了 `Sanitize` 用例的预期：`first + "-2.txt"` 是合法绝对路径，属可修复条目。按实现应为丢弃3条（非绝对路径、超长曲名、null）、清除3个快捷键（无效键 `0x5A`、等于停止键、`f5` 重复）、保留5条，与测试断言一致，无需改实现。

## 代码核对结论（阅读源码得出，不是测试结论）

- 设置读取：`SettingsStore.Load` 在整体校验失败且曲谱库非空时，只用 `ScoreLibrary.Sanitize` 替换曲谱库并重新校验；失败再退回默认值，提示语区分“曲谱库已清理”和“无法读取原设置”。
- 快捷键注册：`HotkeyController.Apply` 先整体校验（编号范围、绑定自身有效性、与开始/停止重复），再 `Suspend` 并顺序注册停止、曲谱库、开始；`Suspend` 会同时注销曲谱库编号，避免下次注册因“已注册”失败。
- 触发路径：`Hook` 收到 `WM_HOTKEY` 时用当前设置比对按键与修饰键，编号属于曲谱库且 `LibraryReady` 为真才调用 `LibraryActivateAsync(index, key, settings.LibraryHotkeyPlays)`，旧注册的排队消息会被丢弃。
- 加载路径：`PrepareLibraryEntryAsync` 已加载同一文件且无未保存修改时不重复读盘；快捷键路径（`interactive: false`）在有未保存修改时直接取消并提示，不弹对话框；读盘后重新读取一次，避免“保存当前文档”改动了目标文件。
- 界面锁定：演奏、起奏或关闭期间 `LibraryEditingBlocked` 阻止添加/移除/绑定并提示先停止；`SetBusy` 期间曲谱库控件整体禁用。

## 未验证 / 需维护者实机确认

1. 游戏窗口为前台时按曲谱快捷键，是否真的加载并演奏（含倒计时内切入游戏）。
2. 演奏中按另一首曲谱的快捷键：状态提示是否符合预期，当前演奏是否完全不受影响。
3. 按键被其他程序占用时的列表状态与提示文案、“重试注册”后的恢复。
4. 真实按键与曲谱快捷键混用时的释放手感（曲谱键与开始键路径共用 `WaitForRelease`）。
5. 接近上限（60首）时的列表滚动、逐个注册耗时与状态栏汇总可读性。
6. 手工改坏 settings.json 后的实际启动表现（本轮只在单元层面覆盖 `Sanitize`）。

## 文档同步

- `README.md`：新增 v0.5.0 小节与曲谱库操作说明，并在开头注明“已发布版本仍为 v0.4.0、v0.5.0 未升版”。
- `使用说明.txt`：新增“十一、曲谱库与单曲快捷键（v0.5.0 本地增量，未升版）”，开头标注该节在已发布 0.4.0 中不存在。
- `制谱说明.txt`：新增“十二、曲谱库与单曲快捷键（不改格式）”，明确不改变任何谱面语法与文件头要求。
- `docs\NEXT_UPDATE.md`：新增“2026-10-06 曲谱库与单曲快捷键（本地增量，未升版）”。
- `UPDATE-0.5.0.md`、`VALIDATION-0.5.0.md`：本文件与版本更新记录。
- `AGENTS.md` 的“当前正式版本基线”保持 v0.4.0（尚未升版，符合事实），待升版时一并更新。

## 本轮涉及的文件

- 新增：`ScoreLibrary.cs`、`PlayerWindow.Library.cs`、`Tests\ScoreLibraryTests.cs`、`UPDATE-0.5.0.md`、`VALIDATION-0.5.0.md`。
- 修改且包含曲谱库代码：`Program.cs`（快捷键分发、初始化、注册与状态汇总）、`AppSettings.cs`（曲谱库字段、内容比较、加载清理）、`HotkeySettings.cs`（`HotkeyController` 曲谱库注册与结果查询）、`ScoreDialogs.cs`（多选 TXT 的 `OpenLibrary`）、`PlayerWindow.Theme.cs`（列表项样式）、`Tests\Program.cs`、`Tests\WindowsSmoke\WindowsSmokeTests.cs`。
- 同一时段被改动但不含曲谱库引用：`PlayerWindow.Shortcuts.cs`、`PlayerWindow.Mode.cs`、`HotkeyDialog.cs`（其 `BindingEditor` 被曲谱库对话框复用）。
- 文档：`README.md`、`使用说明.txt`、`制谱说明.txt`、`docs\NEXT_UPDATE.md`。

本轮目录下没有 `.git`（`git status` 报 “not a repository”），无法用 Git 差异核对改动范围；上面的清单依据“文件中是否引用曲谱库”和文件最后修改时间整理，提交前建议再做一次人工对照。

