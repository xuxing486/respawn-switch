<!-- Author: Stress Monster -->
# RespawnSwitch 0.3.6 开发者说明

作者：**Stress Monster**

## 本版范围

0.3.6 修复桌面/网页抖音进入与返回竞态，并把主窗口改为透明可贴边桌宠。Riot Live Client 读取、单次复活时间锚点、本地 100ms 倒计时、英雄/KDA 悬浮窗和 League 进程级音频租约继续沿用。

## 每轮状态与返回顺序

每个 `RespawnCycleId` 创建独立 `RespawnCycleRuntime`：`Created → EnteringDouyin → WatchingDouyin → ReturningToLeague → Completed`。媒体控制器、桌面窗口附着、网页命令和音频租约不再跨轮共享。

`RespawnConfirmed` 先隐藏倒计时悬浮窗并将本轮置为不可逆返回状态，然后取消并等待真实进入任务结束。任何晚到 Attach/Play/设置保存提交都会被拒绝。清理固定顺序：

1. 桌面 GSMTC 或网页视频执行明确 Pause 并验证；
2. 还原抖音窗口；
3. 恢复已验证 League 窗口前台；
4. 释放 League 进程音频静音租约。

各步骤独立捕获失败，前一步失败不会阻止后续窗口、焦点和音频恢复。GSMTC 接受命令后按 40/70/110ms 轮询；Pause 首轮仍未验证时只补发一次明确 Pause，再按 70/120/180ms 验证。绝不发送 Toggle、空格或全局媒体键。

浏览器桥接命令同时携带 `cycleId` 与单调 `sequence`；Pause 会覆盖尚未确认的 Play，其他轮或旧序列回执不能完成当前命令。

## 桌宠

主窗口为 `WindowStyle=None`、`AllowsTransparency=True`、任务栏隐藏的 WPF 透明窗口。自由收起态真实窗口约 155×205，只显示独立 Q 版素材。`PetDockGeometry` 负责四边命中与负坐标显示器；`PetDockPresentation` 为上、下、左、右提供不同窗口裁切、位移、旋转、扒边装饰及 220ms `CubicEase/EaseOut` 吸附参数。鼠标移入恢复完整 Q 版，移开再次进入对应边缘姿态。

用户点开状态后扩展为 420×390：195px 成人角色左对齐、215px 状态面板右对齐，两者总宽不超过窗口，不再叠盖人物。关闭时窗口尺寸和命中范围一起缩小。成人与 Q 版各自拥有头、手、尾巴局部命中区和独立台词；交互仍只发生在桌宠窗口内部。

状态面板默认收起，点击或双击展开。摸头、牵手和碰尾巴使用短台词与局部缩放动画；未安装全局输入钩子。原创透明 ARGB 素材位于 `src/RespawnSwitch.App/Assets`，署名记录在同目录 README。

## 构建、测试与真实验证边界

```powershell
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test RespawnSwitch.sln -c Release --no-build --no-restore
powershell -ExecutionPolicy Bypass -File .\build\publish.ps1
```

`RespawnSwitch.MediaSmoke cycle-test --aumid ... --fingerprint ...` 只接受唯一精确目标，并在 `finally` 恢复测试前的 Playing/Paused 状态。2026-08-19 的一次有条件检查没有发现 GSMTC 会话，因此遵照用户要求跳过真实播放循环；自动化测试不得描述为真实 League 对局验收。

Firefox、独占全屏和不唯一的媒体/窗口目标仍不支持。当前 Riot/抖音版本的最终体验边界仍是用户方便时的一次训练模式阵亡/复活观察。

## 署名

`Stress Monster` 位于程序集元数据、源文件/素材说明、浏览器扩展、`AUTHOR.txt`、用户说明和开发者说明中。
