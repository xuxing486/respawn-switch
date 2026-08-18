<!-- Author: Stress Monster -->
# RespawnSwitch 0.3.5 开发者说明

作者：**Stress Monster**

## 本版范围

0.3.5 只增加 League 进程音频静音与恢复。主界面、阵亡/复活状态机、桌面与网页抖音 Play/Pause、窗口切换、倒计时锚点和悬浮窗置顶策略保持不变。

## 音频控制链路

- `AttachmentRequested` 捕获已验证 League 游戏窗口的 PID，在执行抖音播放/窗口切换前取得进程音频静音租约。
- Windows 实现通过 Core Audio 枚举全部活动渲染端点及其音频会话，使用 `IAudioSessionControl2.GetProcessId` 精确匹配 League PID，并通过 `ISimpleAudioVolume` 只静音匹配会话。
- 租约只记录程序从“未静音”改为“静音”的会话。复活后先暂停/还原抖音、恢复 League 前台，再按端点和会话实例标识恢复这些会话。
- 用户原本已静音的 League 会话不会被恢复；抖音、其他进程和系统主音量不会被修改。若用户在观看抖音期间主动取消 League 静音，释放租约时不会再次改写。
- 抖音切换失败、程序退出以及复活与静音并发发生时都会释放或拒绝迟到的租约，避免残留静音。Core Audio 失败采取不影响原切换流程的失败关闭策略。

## 已知限制

- Windows 音频服务必须正常运行；League 若在阵亡切换后才创建新的音频会话，该新会话不属于本周期已经取得的租约。
- Firefox 暂不支持。
- League、抖音及浏览器更新后，窗口、媒体会话或页面结构可能变化；首次使用仍应在训练模式执行完整阵亡/复活测试。
- 自动化测试验证精确 PID、原静音状态保持、恢复和并发安全，但不能代替当前 Riot/抖音版本的真实对局听觉验收。

## 构建与验证

```powershell
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test RespawnSwitch.sln -c Release --no-build --no-restore
powershell -ExecutionPolicy Bypass -File .\build\publish.ps1
```

发布脚本会运行 `RespawnSwitch.exe --self-test`、生成逐文件 `SHA256SUMS`，并产生 `artifacts/RespawnSwitch-0.3.5-win-x64.zip`。

## 署名

`Stress Monster` 保留在程序集元数据、浏览器扩展、素材元数据、源文件注释、`AUTHOR.txt` 与两份说明书中。
