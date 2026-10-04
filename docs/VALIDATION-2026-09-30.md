# v0.3.3 工作区验证与待实机验收记录

维护者后续决定（2026-09-30）：已发布的 v0.3.3 已由维护者测试，目前没有重大 bug；以下尚未执行的实机项目暂缓，不继续催办或自动运行。历史执行结果保留，新工作区的未验证项目不改记为通过。下次更新保留事项见 [NEXT_UPDATE.md](NEXT_UPDATE.md)。

日期：2026-09-29～2026-09-30。正式版本基线为 v0.3.3，本记录包含随后新增测试与小范围游戏时间计算提取后的工作区，不代表新发行包已完成游戏验收。

环境：Windows 10.0.26200（Windows 11），win-x64，.NET SDK 8.0.425。用户要求游戏期间只做后台工作，本轮不创建窗口、不注册热键、不出声、不发送输入。

## 当前结论

| 项目 | 结果 | 证据/边界 |
|---|---|---|
| 原 v0.3.3 核心入口 | 前一轮 408 项通过 | 原实现，9 月 29 日执行 |
| 原 WindowsSmoke 入口 | 前一轮 8 组通过 | 原实现，真实 WPF 窗口，日志播放/音频替身；不是游戏输入或听感验收 |
| 当前核心入口 | 480 项通过，失败 0，无跳过机制 | 原 408 项 + 新增 72 项时序对照；无原生设备调用 |
| 当前主程序 Release build | 通过，0 警告、0 错误 | 不启动程序 |
| 当前 WindowsSmoke build | 通过，0 警告、0 错误 | 当前新增后共 9 组；本轮未运行，不能沿用此前 8 组的 PASS |
| 当前自包含发布 | 通过 | win-x64、self-contained、single-file、IncludeNativeLibrariesForSelfExtract；未打 ZIP/未上传 |
| 当前自包含 EXE 启动/运行 | 待验收 | 前一轮尝试启动后观察到旧路径 v0.2.1 窗口，未确证新 EXE 启动成功；不以旧窗口代替验证 |
| 短音主观听感、真实 WinMM | 待验收 | 波形检查不能证明实际声卡/扬声器表现 |
| 实际停止/释放键鼠、游戏内验收 | 待验收 | 用户正在游戏，本轮不执行 |

前一轮沙箱因无权读取用户 NuGet.Config 导致核心入口第一次未进入测试；正常用户权限重试后全部通过。这是环境权限问题，不计为代码断言失败。没有更改 NuGet 配置。

## 本轮代码与测试范围

- GamePlaybackTiming 从既有游戏循环提取时间计划，保留原逐音累加顺序、12ms 准备和 gap 算法。计划在 Stopwatch 启动前构建，避免计划分配占用第一音调度时间。原生输入、取消、焦点检查与最终释放流程未改。
- PlaybackTimingTests 使用独立的已知毫秒时刻、ScoreTimeline 和实际 MIDI 字节解码对照：普通/短/附点/自定义音、休止、末尾休止、升音、最高 do、重复同音、延长、40ms 下限，以及 137 BPM 下 2000 个小数拍长谱；MIDI 允许最多一个 tick 量化差。
- WindowsSmoke 增加 PlaybackRestartTests：倒计时与日志播放期间连续调用 Begin 不替换活动任务/取消源；Stop 后清理完成前拒绝再次启动；完成后新播放使用新取消源；旧进度不会污染新状态。只使用日志模式，不发送音符按键。新增场景已编译，运行待用户空闲。
- README 按正式 v0.3.3 更新；AI 模板和制谱说明版本同步；AGENTS.md 记录真实命令、旧谱兼容性、时序规则和验收边界，并纳入实际仓库。外层工作区 AGENTS.md/PROJECT_CONTEXT 与仓库对应副本同步。
- V0.3.3-BASELINE-FILES.csv 保留为上次整理时的历史快照，不覆盖成当前版本；这次源码已变化。当前构建输入校验值存于下述产物目录的 source-inputs.json。

## 产物与日志

当前本机目录：`bin/validation-v0.3.3-20260930-background/`（Git 忽略的测试产物）。

- `core-tests.log`、`release-build.log`、`windows-smoke-build.log`、`publish.log`。
- `self-contained/HarmonicaPlayer.exe`：当前开发工作区的验收程序，未替换正式 ZIP。
- `artifact.json`：EXE 文件大小、版本、SHA256；`source-inputs.json`：当前编译源码/项目/内嵌资源哈希。
- 原基线日志仍在 `bin/validation-v0.3.3-20260929-235200/`。该目录的 EXE 为修改前产物，不用于本次新增改动的验收。

当前 EXE SHA256：`92e73220cd3f40c07eb85061efa278c3cc27b9f89a9bfd1d25264a52e0e49132`

## 执行命令与实际结果

从 HarmonicaPlayer.csproj 所在目录执行；核心测试是 dotnet run 控制台入口，不能换成 dotnet test。

```powershell
dotnet run --project .\Tests\ParserTests.csproj -c Release
dotnet build .\HarmonicaPlayer.csproj -c Release
dotnet build .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
dotnet publish .\HarmonicaPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\bin\validation-v0.3.3-20260930-background\self-contained
```

当前核心检查原始输出（前几行是累积计数，勿重复相加）：

```text
PASS: 32 tests
PASS TOTAL: 62 tests (parser, hotkeys, settings)
PASS TOTAL: 71 tests including async settings writer
PASS FINAL: 99 tests including v0.2.0
PASS v0.2.1: 54 document and validation tests
PASS gap update: 24 targeted checks
PASS v0.3.0: 30 audio/timeline/MIDI tests
PASS v0.3.1: 48 recorded-sample checks
PASS v0.3.2: 88 release/gap/buffer checks
PASS v0.3.3: 65 short-note phrase checks
PASS playback timing: 72 game/audio/MIDI checks
```

当前主程序构建输出：

```text
正在确定要还原的项目…
  已还原 D:\HarmonicaPlayer\HarmonicaPlayer\HarmonicaPlayer.csproj (用时 79 毫秒)。
  HarmonicaPlayer -> D:\HarmonicaPlayer\HarmonicaPlayer\bin\Release\net8.0-windows\HarmonicaPlayer.dll

已成功生成。
    0 个警告
    0 个错误

已用时间 00:00:01.66
```

当前窗口测试项目构建输出：

```text
正在确定要还原的项目…
  所有项目均是最新的，无法还原。
  WindowsSmokeTests -> D:\HarmonicaPlayer\HarmonicaPlayer\Tests\WindowsSmoke\bin\Release\net8.0-windows\WindowsSmokeTests.dll

已成功生成。
    0 个警告
    0 个错误

已用时间 00:00:01.55
```

此前原实现 WindowsSmoke 的实际结果：idle 60ms、pending-save 33ms、countdown 27ms、playback 44ms、error-recovery 18ms；settings-generation、document UI、audio UI 均 PASS。上述毫秒数为窗口关闭耗时，不是游戏停止/释放输入延迟。

## 用户空闲后继续的验收表

尚未实际执行的项目一律保留“待验收”，填写设备/日期/实际现象后才能改为通过。

| 项目 | 操作及预期 | 状态 |
|---|---|---|
| 当前 WindowsSmoke | 关闭旧播放器、退出游戏操作后运行下方入口，9 组 PASS，包含 playback-restart | 待验收 |
| 新 EXE 启动与版本 | 从本记录产物目录打开，核对进程完整路径和标题 0.3.3，F6/F8 可用；再次打开只有一个实例 | 待验收 |
| 自包含部署 | 将产物复制到没有单独安装 .NET 的 Windows x64 测试环境，确认启动与采样加载 | 待验收 |
| 短音听感 | 使用下方片段，BPM 90/120/180，gap20，音量60%；记录爆音、突突尾音、重复触发感及实际设备 | 待验收 |
| 普通/长/重复音 | `1_ 1 1:8 【1】_ 【1】` 及 `【3】_ 【3】_`；听取收尾、循环、分音；与 AudioExamples 对照 | 待验收 |
| 试听停止与重启 | 播放中按钮停止/F8，确认实际声音停止、编辑/光标恢复，再播放成功；试听中关闭后无残留声音 | 待验收 |
| 游戏倒计时停止 | 实际游戏口琴界面 F6 开始，倒计时按 F8；不得发音或残留修饰键，随后可重新开始 | 待验收 |
| 游戏长音停止/释放 | 低音、升音、高音、最高 do 长音中 F8；确认鼠标左/中/右键和音符键均释放，手动操作正常 | 待验收 |
| 游戏重复开始/再开始 | 连续按 F6/长按后松开，不产生并行演奏；F8 停止后再次 F6 可演奏 | 待验收 |
| 游戏失焦/关闭 | 演奏中切离目标窗口应停止并释放输入；关闭播放器后无残留键鼠 | 待验收 |

待执行的 Windows 入口（会创建窗口、注册全局热键）：

```powershell
dotnet run --project .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
```

短音片段：

```text
【3】 【2_】 【1】 【2_】 |
【3_.】 【4__】 【3_】 【2.】 |
```

实际反馈至少记录：验收 EXE 路径/哈希、日期、Windows/游戏环境、输出设备、BPM/gap/音量、停止方式及有无残留。不得仅凭日志将“已释放输入”或“杂音已消失”记为实机通过。
