## 这个 PR 做了什么

（一句话说明；若修复 Issue，请填 `Closes #编号`）

## 类型

- [ ] Bug 修复
- [ ] 新功能（说明是否改动曲谱语法/文件头/快捷键/游戏输入时间线）
- [ ] 文档或注释
- [ ] 重构（不改变行为）
- [ ] 构建/发布脚本

## 影响面与兼容性

- 是否改动上游行为（曲谱格式 `@format=HarmonicaPlayer/1`、BPM/gap 语义、试听、MIDI、游戏输入时间线）：
- 是否改动设置文件字段（新增字段必须为**可选**且非法值可回退）：
- 是否新增界面文案（是 → 中文与英文两套是否都已提供）：

## 验证证据

- [ ] `dotnet build .\HarpKit.csproj -c Release` → 0 警告 0 错误
- [ ] `dotnet run --project .\Tests\ParserTests.csproj -c Release` → 全部 PASS
- [ ] `dotnet build .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release` 通过
- [ ] `dotnet run --project .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release` 全部 PASS（如已运行；注意会注册全局热键）

实际命令与结果（请粘贴计数与关键输出）：

```text

```

**未验证的边界**（必须写清，不接受“应该没问题”）：

## 提交规范自查

- [ ] 没有引入来源不明的外部代码（见 `CONTRIBUTING.md` 第 1 节）
- [ ] 没有添加 MIT/Apache/GPL 等许可文件或改写版权声明
- [ ] 保留了 `使用说明.txt` / `Build-Release.ps1` 的 UTF-8 BOM
- [ ] 新增/修改的文件行尾为 LF
- [ ] 已在 `CONTRIBUTIONS.md` 记录本次来源与采纳范围（如涉及上游 PR 思路）
- [ ] 没有自行修改版本号或打包发布
