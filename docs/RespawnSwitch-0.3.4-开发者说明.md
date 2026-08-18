<!-- Author: Stress Monster -->
# RespawnSwitch 0.3.4 开发者说明

作者：**Stress Monster**

## 本版范围

0.3.4 只重构主界面表达与交付结构，不改变已经实测通过的阵亡/复活状态机、抖音窗口切换、媒体控制、倒计时锚点和悬浮窗置顶策略。主界面采用成熟、克制的深色二次元猫娘主题；技术状态仍保留在内部事件日志和开发文档中，不向普通用户直接展示。

## 数据与控制链路

- League：赛前使用进程/顶层窗口确认客户端存在；对局中读取 Riot 本机 Live Client Data。`LifeState` 与连接状态分别维护，避免短暂失联被当成复活。
- 倒计时：每次阵亡读取一次 `respawnTimer` 建立单调时钟锚点，界面本地刷新；Riot 存活状态仍是最终复活确认。
- 桌面抖音：要求用户预先打开。窗口目标和 GSMTC 会话必须唯一；死亡热路径复用赛前缓存，并行执行前台切换与 Play。
- 网页抖音：Chrome / Edge Manifest V3 扩展通过仅绑定 `127.0.0.1:17653` 的本地桥接控制唯一 `douyin.com` 视频标签页。
- 窗口切换：读取并验证可见、最小化、前台、Topmost 与边界等真实后置条件；兼容 League 无边框窗口。复活时取消抖音置顶并明确恢复游戏前台。
- 悬浮窗：无激活、鼠标穿透；100 ms 刷新倒计时时重新确认 Topmost，因此抖音置顶不会覆盖英雄、K/D/A 和倒计时。

## 用户界面原则

普通界面只回答三个问题：英雄联盟是否已打开、抖音是否已打开、现在能否开始。GSMTC、Automation、窗口类名、内部异常类型和原始诊断文本不得直接显示；它们只写入隐藏事件日志或开发诊断信息。用户错误信息通过 `FriendlyIssue` 转换为可执行操作。

## 已知限制

- Firefox 暂不支持。
- 抖音桌面窗口、媒体会话或网页标签不唯一时会失败关闭，不猜测目标。
- League、抖音及浏览器更新后，窗口类名、媒体会话或页面结构可能变化；每次大版本更新应重新执行训练模式实测。
- Windows 禁止低权限进程强行覆盖高权限前台窗口；游戏、抖音和本程序需要保持相同权限级别。
- 自动化测试能验证状态机、窗口后置条件、最小化/恢复、悬浮窗倒计时与 UI 契约，无法替代每个 Riot/抖音当前版本的真实对局测试。

## 构建与验证

```powershell
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test RespawnSwitch.sln -c Release --no-build --no-restore
powershell -ExecutionPolicy Bypass -File .\build\publish.ps1
```

发布脚本会运行 `RespawnSwitch.exe --self-test`、生成逐文件 `SHA256SUMS`，并产生 `artifacts/RespawnSwitch-0.3.4-win-x64.zip`。

## 署名

`Stress Monster` 写入应用的 Company/Authors/Copyright 元数据、浏览器扩展 `author`、猫娘 PNG 文本元数据、源文件注释、`AUTHOR.txt` 和两份说明书。发布输出内的 `AUTHOR.txt` 是人类可读的永久标记。
