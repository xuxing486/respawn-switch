<!-- Author: Stress Monster -->
# RespawnSwitch 0.3.7 开发者说明

作者：**Stress Monster**

## 本版范围

0.3.7 针对真实桌面反馈修复贴边透明留白、拖拽与触摸命中冲突、拖动起点跳位，以及侧边互动反馈被窗口裁切。媒体切换、Riot Live Client 读取、单次复活时间锚点、本地 100ms 倒计时、英雄/KDA 悬浮窗和 League 进程级音频租约均未改动。

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

主窗口为 `WindowStyle=None`、`AllowsTransparency=True`、任务栏隐藏的 WPF 透明窗口。自由态真实窗口约 155×205。`PetEdgeDragGeometry` 使用 24px 进入阈值和 48px 离开阈值，在自由 Q 版与顶部、底部、侧边三张透明贴图之间切换；右侧只镜像 side 素材。贴边态仅固定垂直于边框的坐标，另一轴持续跟随鼠标，因此可以沿边框自由滑动。

三张 PNG 会按 alpha>8 的可见边界裁切；`PetDockPresentation` 使用与裁切后资源一致的宽高比，使 `Stretch=Uniform` 不再重新引入空隙。回归测试将最终 WPF 显示比例折算为边缘间隙，四向均要求不超过 1px。

贴边输入由窗口统一处理：触摸区只标记待执行动作，不再阻止鼠标捕获。`PetPointerGesture` 在位移达到 6px 后才把操作判为拖拽；未达到阈值的 MouseUp 执行一次互动，达到阈值则只移动和保存位置。拖拽使用按下时的真实抓取偏移，不再把贴图中心强行吸到鼠标下。窄侧边的气泡宽度限制在当前窗口内，互动缩放中心按 Left/Right/Top/Bottom 固定到相应屏幕边缘。

拖拽改为 WPF 鼠标捕获和逐次位置更新，不再调用阻塞式 `DragMove` 后补动画，也不再对 Window 的 Left/Top/Width/Height 注册完成回调。双击或打开面板会同步停止拖拽、退出贴边形态并强制恢复 420×390，防止旧动画迟到后将成人面板再次裁成小窗口。

用户点开状态后扩展为 420×390：195px 成人角色左对齐、215px 状态面板右对齐，两者总宽不超过窗口，不再叠盖人物。关闭时窗口尺寸和命中范围一起缩小。成人与 Q 版各自拥有头、手、尾巴局部命中区和独立台词；交互仍只发生在桌宠窗口内部。

状态面板默认收起，点击或双击展开。普通 Q 版、三种贴边形态和成人版都有窗口内部互动区域；未安装全局输入钩子。原创透明 ARGB 素材位于 `src/RespawnSwitch.App/Assets`，署名记录在同目录 README。

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
