# 2026-10-03 WindowsSmoke 反馈与测试修正

本记录续接 [2026-10-02 开发验证](VALIDATION-2026-10-02.md)。用户手动执行窗口入口并提供输出；本轮 Codex 继续纯后台，没有创建窗口、注册热键、发送输入或调用声音设备。

## 用户提供的执行结果

命令：`dotnet run --project .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release`。

前9组输出PASS：idle、pending-save、countdown、playback、error-recovery、MIDI-window、settings-generation、playback-restart、document UI。最后AudioWindowTests在旧372行报 `Local audio depends on game hotkey readiness`，入口整体未通过；尚未执行到该组后续试听、停止、失败重试、完成、关闭断言。这是维护者提供的Windows执行证据，不能标记成Codex实际运行，也不能据此前9组推断游戏输入、真实声音或自包含EXE已验收。

## 原因与最小修正

当前“仅测试”启动默认已经是false。旧测试直接调用HotkeyController.Suspend绕过窗口的热键刷新，然后再给复选框赋false；属性值没有变化，Unchecked事件不触发，UpdateAlert/UpdateAvailability不会重算。旧开始按钮状态因此保留，而旧断言将“开始按钮仍可用”与“试听按钮不可用”合成一个条件，统一报成试听依赖热键。

源码核对：UpdateAvailability只有游戏开始按钮检查StopReady，试听、从光标试听和MIDI导出只依赖忙碌/曲谱状态；未发现这里新增了试听依赖热键的产品逻辑。

本轮仅修正Tests/WindowsSmoke/WindowsSmokeTests.cs中的AudioWindowTests：先明确切到仅测试，再暂停热键，最后切回游戏模式，真实触发窗口已有刷新路径。分别断言停止热键不可用时游戏开始被禁用，以及从头试听、光标试听、MIDI导出仍可用。没有放宽停止保护，也没有将启动默认值改回勾选，不调用Begin或游戏输入。

## 后台验证结果

| 命令 | 结果 | 本机日志 |
|---|---|---|
| `dotnet build .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release` | 0警告、0错误；仅编译 | bin/validation-2026-10-03/windows-smoke-build.log |
| `dotnet run --project .\Tests\ParserTests.csproj -c Release` | 588项通过，退出码0 | bin/validation-2026-10-03/core-tests.log |

核心检查覆盖热键假后端和音频模型；它不执行WPF窗口，不能证明这次窗口断言已实际通过。主程序源码、采样、解析和时序均未因本次测试修正改变。保留原有Git暂存成果和用户文件，没有提交、推送或发布。

## 维护者复验结果：10组全部通过

2026-10-03维护者在同一仓库重跑同一命令，并提供完整输出。以下10组均PASS，无异常：

| 场景 | 结果 |
|---|---|
| idle | PASS，关闭59ms |
| pending-save | PASS，关闭28ms |
| countdown | PASS，关闭26ms |
| playback | PASS，关闭13ms |
| error-recovery | PASS，关闭20ms |
| MIDI-window | PASS：取消、未保存TXT、源文件保持、模式、移调阻止 |
| settings-generation | PASS：慢写入、A-B-A、关闭最终值 |
| playback-restart | PASS：重复开始、倒计时及播放中的停止再开始、过期进度 |
| document UI | PASS：未保存、取消、保存、旧格式保护、无效导入、关闭保存 |
| v0.3.0 audio UI | PASS：光标、进度、互斥、音量、停止、失败重试、完成、关闭 |

复验时测试源码SHA256为 `B3B001E47F26267931B7CA8E7FF47FA8ED3EAEE51AF5C43D99A0E72270E26FF6`，与本轮修正后后台编译的源码一致。维护者输出保存在 `bin/validation-2026-10-03/user-windows-smoke-rerun-pass.log`；首次失败输出另行保留，不覆盖。

该入口使用真实WPF窗口及热键注册；演奏场景使用仅测试，音频场景使用假设备。实际音频听感、游戏按键释放、真实调度错误弹窗、普通/管理员完整交互及自包含EXE继续按上一记录待验收。本次不再将WindowsSmoke本身列为未通过，也不自动重跑窗口。

本机产物bin/validation-2026-10-03还保存用户原始输出、修改前测试副本、最小diff及当前文件SHA256；它们为忽略的验证产物，不是Git提交。
