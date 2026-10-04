# Codex 首次扫描提示词

请将下面内容作为 **第一次打开 HarmonicaPlayer 仓库时的任务提示词** 使用。

---

你现在第一次接手 HarmonicaPlayer 项目。

当前正式版本基线是 **v0.3.3**。

这是一个 C# / .NET 8 / Windows 项目，主要用于《三角洲行动》的口琴自动演奏，同时已经包含 TXT 曲谱解析、本地试听、MIDI 导出、快捷键和测试等功能。

请先阅读：

- `AGENTS.md`
- `docs/PROJECT_CONTEXT.md`
- `README.md`
- solution / project 文件
- 测试项目
- 构建 / 发布脚本
- 与 Parser、Playback、Preview、Audio、MIDI、Hotkey、Settings 相关的主要源码

## 本轮严格要求

**只分析，不修改任何代码、配置、文档或项目文件。**

不要执行：

- 自动格式化
- 自动升级 NuGet
- 自动重构
- 自动修复
- git commit
- git push
- git reset
- git clean
- Release 发布

可以运行只读或低风险命令，例如：

```powershell
git status
git branch --show-current
git log -n 10 --oneline
dotnet --info
dotnet restore
dotnet build
dotnet test
```

如果某个命令可能改写重要文件、生成覆盖性产物或需要管理员权限，先不要执行，只说明原因。

---

## 第一部分：仓库结构

请确认并输出：

1. Solution 文件名
2. 所有主要 `.csproj`
3. UI 项目
4. 测试项目
5. 主要目录结构
6. 程序入口
7. 配置文件
8. 发布脚本
9. CI / GitHub Actions
10. 资源文件 / 音频采样位置

不要只列文件名，要说明各目录负责什么。

---

## 第二部分：核心模块关系

请定位并解释：

### Parser / Score

- TXT 谱面的解析入口
- 音高解析
- 时值解析
- 休止解析
- BPM
- gap
- 自定义时值
- `【【1】】`
- 升半音
- 错误位置报告

### Playback

- 从曲谱到实际播放事件的流程
- BPM 如何换算成时间
- gap 如何参与计算
- stop / cancel 如何工作
- 是否存在并行播放风险

### Preview / Local Audio

- 本地试听入口
- 从头播放
- 从光标播放
- 当前音符高亮
- 播放进度
- 剩余时间
- 音量
- 音频库 / 播放实现
- 采样资源加载方式

### MIDI

- 使用的 MIDI 库
- 单轨导出实现
- BPM
- note on / off
- 音长
- gap
- rest

### Hotkey / Input

- 全局快捷键注册
- 开始快捷键
- 停止 / 紧急停止
- 输入模拟
- 鼠标 / 键盘事件
- 冲突处理

### Settings

- 配置模型
- 保存位置
- 保存时机
- 是否异步
- 是否有序列化写入
- 是否可能出现旧保存覆盖新保存
- UI 状态如何更新

---

## 第三部分：一致性检查

重点检查是否存在以下风险：

1. Parser、Preview、Playback、MIDI 对时值的计算是否重复实现
2. BPM 换算是否存在多个版本
3. gap 是否在不同模块解释不同
4. rest 是否在不同模块行为不同
5. 自定义时值是否所有模块都支持
6. 最高音 `【【1】】` 是否所有模块一致支持
7. 升半音是否所有模块一致支持
8. 播放进度是否基于真实播放时长
9. MIDI 与试听的时序是否可能不同
10. stop 是否能完整取消正在进行的播放 / 音频 / 输入任务

此阶段不要重构，只报告。

---

## 第四部分：测试

请确认：

- 当前测试项目名
- 当前测试总数
- Parser 相关测试
- BPM / gap 测试
- Document 测试
- Playback 测试
- Preview 测试
- Hotkey 测试
- Settings 测试
- Async / cancellation 测试
- Close / shutdown 测试
- MIDI 测试
- Audio 测试

如果可以安全运行，请执行完整测试。

输出：

```text
总测试数：
通过：
失败：
跳过：
```

如果测试失败：

- 不要立即修改
- 先列出失败测试
- 判断是环境问题还是代码问题
- 给出最可能原因
- 标出是否与 Windows 平台有关

---

## 第五部分：Build 与 Release

请确认：

1. `dotnet restore` 是否成功
2. `dotnet build` 是否成功
3. 正式项目 Target Framework
4. Runtime Identifier
5. 当前发布方式
6. 是否 self-contained
7. 是否 single-file
8. 是否存在 `Build-Release.ps1`
9. 脚本实际做什么
10. Release 输出目录
11. ZIP 如何生成
12. 版本号从哪里读取

不要实际发布 GitHub Release。

---

## 第六部分：Windows 特有依赖

列出所有不能仅通过普通单元测试完全验证的部分，例如：

- 全局快捷键
- SendInput / 模拟输入
- Windows message hook
- 游戏内按键
- 鼠标操作
- 音频设备
- 窗口焦点
- 管理员权限
- Windows 路径
- 文件权限

分别标记：

- 可自动测试
- 需要 Windows 本机测试
- 需要实际进入游戏测试

---

## 第七部分：Git 状态

请输出：

- 当前 branch
- 是否有未提交修改
- 是否有 staged 修改
- 是否有 untracked 文件
- 最近 10 个 commit
- 是否存在明显未合并的开发残留

禁止执行：

```text
git reset
git clean
git checkout .
```

---

## 第八部分：文档一致性

对比：

- README
- AGENTS.md
- PROJECT_CONTEXT.md
- 当前代码
- 当前测试

列出：

1. 已经一致的内容
2. 文档过时内容
3. 无法从代码确认的历史内容
4. 建议补充进 `AGENTS.md` 的实际开发规则

此阶段仍然不要修改文档。

---

## 最终输出格式

最后给我一份：

# HarmonicaPlayer Codex 接手报告

## A. 项目结构

## B. 当前架构

## C. Build 状态

## D. Test 状态

## E. Windows / 游戏内必须人工验证项

## F. 发现的潜在风险

按严重程度分为：

- High
- Medium
- Low

但不要为了凑数量强行列问题。

## G. AGENTS.md 建议调整项

只给建议，不修改。

## H. 建议的下一步

最多给 5 项，并优先选择：

- 修复明确问题
- 补关键测试
- 消除 Parser / Playback / Preview / MIDI 不一致
- 稳定异步 / 快捷键 / 音频
- 再考虑新功能

---

再次强调：

**这一轮只做接手分析，不修改任何文件。**
