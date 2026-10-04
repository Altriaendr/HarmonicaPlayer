# v0.4.0 验证

维护者于2026-10-04明确反馈“测试完毕，可以发布，版本号为0.4.0”。该反馈作为实机验收与发布授权记录，不扩写为未逐项报告的测试结果。

## 发布前后台复核

从HarmonicaPlayer.csproj所在目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1 -Background
```

- 完整核心入口再次752项通过，退出码0；包含157项纯输入计划、99项MIDI导入、72项游戏/共享时间线对照，不重复相加。
- WindowsSmoke Release编译通过，0警告、0错误；本轮未运行窗口。此前维护者提供的10组PASS与本次“测试完毕”确认按各阶段原始反馈保留。
- Release发布构建通过，目标win-x64、self-contained、single-file；采样与原生依赖内嵌，版本统一为0.4.0，窗口开发标记移除。
- Build-Release.ps1语法解析与Git差异检查（允许Windows CRLF及已有末尾空行）通过。后台参数不启动窗口或资源管理器；默认不包含简谱，仅主动传入-IncludeScores时才附带本机TXT。
- 本轮没有启动播放器、注册热键、播放声音、模拟输入或操作游戏；新的自包含EXE启动不冒充自动验收。

发布包为HarmonicaPlayer-v0.4.0-win-x64.zip，保留格式示例、音色对比WAV、正式说明与CC0授权，**不包含简谱目录**。本地简谱新增、删除、重命名不纳入发布提交；远端历史曲谱保持原状。源码提交以远端正式v0.3.3为父提交，保留旧版本标签和历史发布记录。

包内文件、EXE版本、ZIP完整性及SHA256在上传前核对；对应ZIP的.sha256随发布附带。本机bin/release-0.4.0-20261004保存后台日志、修改前文件、原暂存区副本、源码与采样校验清单和发布信息。源代码与最终产物通过提交SHA及发布附件校验值追踪；不把缓存产物当作正式发布包。

历史过程见docs/VALIDATION-2026-10-02.md、docs/VALIDATION-2026-10-03.md及docs/VALIDATION-2026-10-03-PITCH.md，按其日期理解“待验收”与开发版本描述。
