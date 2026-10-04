# 2026-10-02 开发工作区验证记录

本轮已获维护者授权记录需求和修改源码，随后明确要求纯后台。正式版本仍为 v0.3.3，下一版本号未定；本记录是尚未发布的工作区增量，不代表发布包或游戏验收通过。

## 实际环境与范围

- 仓库已移动至 `D:\codex\HarmonicaPlayer\HarmonicaPlayer`，没有 `.sln`；外层工作区为 `D:\codex\HarmonicaPlayer`。旧 `D:\HarmonicaPlayer` 不存在。
- 本机 .NET SDK 8.0.425；主项目和 WindowsSmoke 为 `net8.0-windows` / WPF，核心入口为 `net8.0` 控制台程序。
- 本轮实现严格单音 MIDI 导入、短休止调度保护修复、管理员权限/仅测试提示。需求见 [NEXT_UPDATE.md](NEXT_UPDATE.md)。暂停、播放列表、管理员重启及“确认后调整 MIDI”暂不实现。
- 只编辑、编译和运行无设备核心入口。未启动播放器、WPF 测试或 EXE，未注册全局热键、调用声音设备、发送游戏输入或运行发布脚本。

## 源码与检查对应关系

| 变更 | 主要文件 | 已自动检查的内容 |
|---|---|---|
| 共享原有 MIDI 音高映射 | HarmonicaPitchMap.cs、ScoreTimeline.cs | 48～84 全部半音映射、原音频/MIDI 时间线回归 |
| SMF 导入和 TXT 转换 | MidiImporter.cs、PlayerWindow.MidiImport.cs、ScoreDialogs.cs | 原始音高、实际起音/Note Off/总时间、变速、轨道/通道选择、严格重叠拒绝、主动移调、gap补偿及超范围阻止 |
| 短休止与调度错误 | PlaybackTimingGuard.cs、Program.cs | 用户短休止、独立50ms休止阈值、原发声音符阈值、实际按键起音阶段迟到、行列/片段/时间/阈值定位 |
| 权限与模式提示 | PlayerWindow.Mode.cs、Program.cs、PlayerWindow.Audio.cs | 静态检查、窗口测试编译；默认未勾选、提示顺序与允许继续的运行行为待窗口验收 |
| 窗口生命周期 | Program.cs、Tests/WindowsSmoke/WindowsSmokeTests.cs | 重复开始/停止再开始、导入取消/未保存保护等测试已编译；运行待验收 |

MIDI 不自动删音、截短、量化或应用移调。来源音尾至下一起音/所选轨道结束不足目标gap（最低10ms）、发声不足52ms、复音或移调后仍超音域时拒绝导入。Format 2、SMPTE、打击乐、不能还原的踏板/弯音/SysEx明确拒绝。Format 1 同通道其他轨道中的踏板/弯音也纳入检查；SysEx按文件级检查。

休止不产生按键事件，也不改变时间计划。发声音符仍按 `min(50ms, 音符总时长/4)` 保护；按键前额外核对自己的绝对起音时刻，避免准备阶段卡顿后补发输入。错误对话框排在最终释放和取消清理之后，采用播放代次和当前错误匹配排除旧回调。这里只确认代码顺序与核心模型，实际 Windows 调度和输入释放仍待验证。

## 已执行结果

以下命令均从实际仓库根目录运行，退出码均为0。日志位于 `bin/validation-2026-10-02/`（忽略的本机测试产物，未纳入 Git）。

```powershell
dotnet run --project .\Tests\ParserTests.csproj -c Release
dotnet build .\HarmonicaPlayer.csproj -c Release
dotnet build .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
dotnet run --project .\Tests\ParserTests.csproj -c Release -- --feedback-scores '<已解压反馈目录>'
```

| 入口 | 本轮最终结果 | 日志 |
|---|---|---|
| 完整核心检查 | 588项通过 | core-tests.log |
| 主程序 Release build | 0警告、0错误 | release-build.log |
| WindowsSmoke Release build | 0警告、0错误；仅编译，未执行 | windows-smoke-build.log |
| 用户反馈谱回归 | 4份通过，注入6ms迟到，无输入/无真实等待 | feedback-scores.log |

588项按各组最终独立计数相加：99（历史核心汇总）+54文档/校验+24gap+30音频/时间线/MIDI+48采样+88释放/缓冲+65短音+72游戏时间计划+15调度保护+93MIDI导入。核心日志中的32/62/71是99的阶段累计，不能重复相加。核心声音检查只计算PCM/缓冲，不调用声音设备；快捷键检查使用假后端。

两个定向入口继续保留：`--midi-import-only`、`--playback-timing-only`；完整核心入口已包含两组，不必为同一源码重复执行。新增MIDI边界含导出再导入、running status、力度0作为Note Off、同音重叠、丢失/孤立Note Off、错误轨道结束、变速、导引轨道控制事件、大文件/谱面容量限制等。

## 用户反馈证据

原始文件：`D:\BaiduNetdiskDownload\报错处理.rar`。SHA256：`3DF19459606A4E83236C6637CA90EA14C9DB2A46ADFEC4E1192BBF31783FF876`。只读取原包，解压副本位于本机临时目录 `harmonica-error-analysis-560d7084fc7947219f1b75ef936ee992\报错处理`，原包和曲谱未修改。

| 文件 | 音/休止数量 | 总时长（秒） | 本轮回归 |
|---|---:|---:|---|
| 问题文件/Anytime Anywhere_bpm163.txt | 692 | 227.590395 | 6ms注入通过 |
| 修复后文件/Anytime_Anywhere_bpm163.txt | 666 | 227.590395 | 6ms注入通过 |
| 问题文件/Barricades_bpm105.txt | 1024 | 224.006120 | 6ms注入通过 |
| 修复后文件/Barricades_bpm105.txt | 1021 | 224.006120 | 6ms注入通过 |

Anytime原谱有26处不足50ms休止，最短3.834354ms；旧四分之一阈值只有0.958589ms，第一处约2.572852秒。Barricades原谱有3处23.809542ms休止，旧阈值5.952385ms。这解释了短休止对普通调度抖动敏感的机制，并不证明用户每次停止都由同一原因引起。

回归使用实际解析器、游戏时间计划及共享时间线，检查注入迟到不误停且总时长一致；没有复现真实机器时钟、游戏键鼠或主观听感。包内作者的“测试通过”是反馈来源的历史记录，不作为本轮验收结果。

## 等维护者有空后的验收

以下全部待验收。不要在维护者游戏期间执行。

1. 执行窗口入口 `dotnet run --project .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release`。共有10组窗口场景；会创建WPF窗口、注册热键，演奏场景显式勾选仅测试，音频场景使用假设备。该入口不能替代实际SendInput、声音设备或游戏验收。
2. MIDI导入界面：文件选择取消、轨道/通道切换、复音/超范围/时序不足的提示、移调默认0和主动确认、覆盖率、成功生成未保存TXT、原MIDI字节不变、现有编辑取消保存保护。用真实文件检查转换弹窗布局和大谱面响应。
3. 普通及管理员启动：每次启动仅测试均未勾选；标签/开始按钮/权限/模式正确。非管理员游戏模式优先提示且允许继续；仅测试、试听、导入、导出不被权限拦截。停止热键不可用时仍禁止真实演奏。
4. 演奏状态：快速重复开始、倒计时停止再开始、播放停止再开始、关闭、紧急停止；只保留一条播放任务，没有过期进度/弹窗。清理结束后方可再次开始；实际键鼠均已释放。
5. 用户两首原谱：在实际游戏和仅测试下对照，短休止不误停，原BPM/gap/时间不变，无积压音符补发。真实超时显示当前行列、片段、序号、曲内时间和迟到值；错误窗口出现前已经停止/释放，点击定位落在正确位置。切换焦点仍即时停止。
6. 本地试听：短音听感、连续同音、停止/关闭后的设备释放及导入谱试听。主观效果不能用核心PCM检查代替。
7. 下一版本号和发行集合确认后再执行打包；自包含win-x64 EXE启动、内嵌资源、无SDK环境运行仍待验收。本轮没有新发布包，不覆盖v0.3.3发行文件。

## Git 与可追踪产物

保留原有v0.3.3暂存成果、未追踪曲谱及ZIP。本轮未执行stage、commit、reset、clean、push、创建标签或发布；新增源码/测试/验证记录尚未提交。已有基线审计见 [V0.3.3-BASELINE-AUDIT.md](V0.3.3-BASELINE-AUDIT.md) 和 V0.3.3-BASELINE-FILES.csv。

外层/仓库AGENTS.md已同步实际命令和后台边界；README、使用说明、PROJECT_CONTEXT、NEXT_UPDATE已注明开发与正式版本的区别。工作区的 `bin/validation-2026-10-02/source-inputs.json` 记录最终源码、测试、项目、文档、采样/示例、用户曲谱/ZIP及构建文件的字节SHA256，`git-status.txt`记录未提交状态，`current-change.diff`记录本轮相对修改前快照的差异。它们是本机校验产物，不代表Git提交。

临时目录 `C:\Users\Administrator\AppData\Local\Temp\HarmonicaPlayer-development-20261002\before` 保留本轮开始时已编辑文件的副本；不以当前Git HEAD冒充发布源码。既有采样、源许可和来源清单未改动。

2026-10-03后续状态：维护者首次手动运行窗口入口在音频组断言失败；修正测试步骤后重跑，10组全部PASS。详见 [后续验证记录](VALIDATION-2026-10-03.md)。本记录中的“仅编译/窗口待验收”为10月2日当时状态；窗口入口现已通过维护者本机验证，真实音频、游戏输入和自包含EXE仍待验收。
