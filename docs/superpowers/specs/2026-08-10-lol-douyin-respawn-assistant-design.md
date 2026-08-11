# 英雄联盟阵亡抖音助手设计规格

- 日期：2026-08-10
- 内部项目名：RespawnSwitch
- 目标平台：Windows x64
- 技术栈：C#、.NET 8、WPF、Win32/WinRT
- 设计状态：已获用户口头批准，等待书面规格复核

## 1. 摘要

RespawnSwitch 是一个 Windows 常驻托盘程序。玩家本人在《英雄联盟》对局中阵亡时，程序读取 Riot 官方本机 Live Client Data API，打开或恢复电脑上的抖音桌面客户端、开始播放，并在屏幕顶部显示透明复活倒计时。玩家复活时，程序暂停抖音、隐藏倒计时、移走或隐藏抖音窗口，让游戏画面重新露出；若游戏焦点已经被抖音取得，程序会尝试恢复游戏焦点，并在 Windows 拒绝自动切换时提供全局热键。

程序只处理本机玩家自己的公开游戏内状态，不读取游戏内存、不注入、不设置进程 Hook、不修改游戏文件，也不模拟任何游戏键鼠输入。

## 2. 已确认环境

当前目标电脑已确认：

- 已安装 .NET SDK 8.0.423、.NET 8 Windows Desktop Runtime 和 x64 运行时。
- 抖音桌面客户端快捷方式指向 `D:\douyin\douyin.exe`。
- 抖音和 Riot Client 当前均可在普通用户会话中运行。
- 英雄联盟使用无边框窗口模式；全屏独占不属于正式支持范围。
- 工作目录原先为空，本规格将项目从零建立。

最终发布物应采用 self-contained 的 Windows x64 单文件或小型目录发布，不依赖目标用户预先安装 .NET 运行时。开发时继续使用当前已安装的 .NET 8 SDK。

## 3. 目标与非目标

### 3.1 第一版目标

1. 自动发现进行中的英雄联盟对局和本机玩家。
2. 从首个携带新 `isDead` 值的成功 HTTP 响应完成，到状态机发出对应事件，应用内处理延迟 P95 小于 50 毫秒；目标电脑观察到的端到端响应 P95 目标为 500 毫秒以内，但它是当前补丁的实测目标，不是 Riot 接口 SLA。
3. 在当前游戏补丁的真实客户端语义探针确认单位和行为后，使用 Riot `respawnTimer` 字段显示平滑、持续校准的剩余复活秒数。Riot 公开文档列出了字段，但没有公开承诺其单位和所有特殊机制下的严格语义。
4. 阵亡后自动恢复抖音窗口，并以优先级明确的控制链尝试播放。
5. 在英雄联盟无边框窗口上方显示不抢焦点、鼠标穿透的倒计时悬浮窗。
6. 状态机发出复活事件后立即尝试暂停抖音、隐藏悬浮窗并露出游戏；应用内调度延迟纳入 50 毫秒 P95 指标，抖音和 Windows 完成外部动作的耗时单独记录。
7. 自动返回游戏失败时，不循环抢焦点；改为显示短提示并允许用户按全局热键返回。
8. 提供托盘设置页、媒体诊断、窗口诊断、悬浮窗预览、手动模拟和本地日志。
9. 所有自动化动作均可由用户一键暂停；正常退出和可捕获异常必须完成窗口清理，进程被强杀或掉电后由下次启动依据恢复记录尽力恢复，不作绝对保证。

### 3.2 第一版非目标

- 不支持全屏独占模式的可靠悬浮显示。
- 不支持观战、回放或没有本机 active player 的场景。
- 不读取或推断敌方隐藏状态、技能冷却或战争迷雾信息。
- 不自动操作英雄联盟中的移动、施法、聊天、商店或任何输入。
- 不自动登录抖音、不处理验证码、不绕过登录或更新弹窗，也不自动挑选特定视频。
- 不反向工程 Vanguard、抖音私有 IPC 或抖音网络协议。
- 不上传游戏状态、账号、日志或媒体信息；第一版完全本地运行。
- 不在第一版提供 macOS、Linux、移动端或公开云服务。

### 3.3 模式支持矩阵

- 训练工具：正式支持，也是破坏性边界测试和首次验收环境。
- 普通召唤峡谷非排位对局：在训练工具通过后完成一次只读人工验收，验收通过才标记正式支持。
- 排位对局：读取路径预计相同，但不作为首次测试环境；普通非排位验收和政策复核完成前不标记正式支持。
- ARAM 与限时模式：实验性支持；每种模式必须单独记录 `isDead`、`respawnTimer` 和特殊复活行为后才能升级为正式支持。
- 观战与回放：不支持。

## 4. 前置条件

运行自动流程需要同时满足：

1. Windows 10 1809 或更高版本，推荐 Windows 11。
2. 英雄联盟处于无边框窗口模式。
3. 英雄联盟、抖音和 RespawnSwitch 以相同的普通用户权限运行。
4. 抖音已安装并完成登录，当前处于可播放内容页；登录页、更新页或阻塞弹窗不属于自动导航范围。
5. 用户已在首次运行诊断中确认抖音主窗口和至少一种可工作的播放控制方式。
6. 游戏内本机接口 `https://127.0.0.1:2999` 在对局期间可访问。

程序不会为了提高控制成功率要求管理员权限。若目标程序以管理员权限运行，设置页会提示权限级别不一致，并停用可能失败的 UI Automation 回退。

## 5. 用户体验

### 5.1 首次运行

首次运行向导依次执行：

1. 解析抖音官方快捷方式并确认可执行文件路径；当前机器默认识别为 `D:\douyin\douyin.exe`。
2. 提示用户打开抖音并播放任意视频数秒。
3. 枚举 Windows Global System Media Transport Controls（GSMTC）会话，显示来源应用标识、标题和播放状态，让用户确认抖音会话。若同一来源标识匹配多个会话，必须由用户消除歧义，不能自动选择“当前会话”。
4. 在已播放和已暂停两种起始状态下分别测试 Play 与 Pause，检查 API 布尔返回值并重新读取播放状态，确认两种操作均为幂等的目标状态命令。
5. 枚举抖音进程所属的可见顶层窗口，让用户确认主窗口；保存可执行文件路径、进程特征和窗口特征，不把易变化的中文标题当成唯一标识。
6. 若 GSMTC 不可用，运行 UI Automation 诊断，尝试在抖音进程的 UI 树中识别明确的播放/暂停控件或可靠播放状态，并完成“播放时 Play、暂停时 Play、暂停时 Pause、播放时 Pause”四种校准。只能 Invoke 一个未知状态的切换按钮不算校准成功。
7. 如果两种定向控制方式均失败，抖音自动显示和播放功能保持禁用并给出诊断；游戏监听、倒计时预览和手动测试仍可使用。程序不启用全局媒体键或屏幕坐标点击。
8. 预览顶部倒计时悬浮窗，并注册默认全局返回热键 `Ctrl+Alt+F9`。若热键冲突，要求用户在设置页选择新的组合后才能启用自动切换。

诊断通过后，程序最小化到系统托盘并开始监听。

### 5.2 正常阵亡流程

1. 监听器观察到本机玩家连续有效状态从 `isDead=false` 变为 `isDead=true`。
2. 创建本次阵亡周期 ID，并捕获当前英雄联盟游戏窗口句柄和窗口矩形。
3. 从同一份玩家数据读取 `respawnTimer` 并立即显示顶部倒计时。若计时值缺失、NaN、无穷或负数，显示“正在读取复活时间”，暂不启动抖音。
4. 媒体/窗口附着统一使用 2 秒门槛：有效计时大于或等于 2 秒时执行一次；已知计时在 `[0, 2)` 秒时只显示倒计时；计时稍后恢复为大于或等于 2 秒且仍属于同一周期时，允许执行一次迟到附着。
5. 确保抖音进程已运行；若只有托盘进程，则通过已确认的快捷方式启动主程序。
6. 解析抖音当前有效主窗口。在任何变更前先持久化恢复记录，保存原始 `WINDOWPLACEMENT`、置顶状态、可见状态以及将要应用的目标状态。
7. 使用非激活显示路径把抖音调整到英雄联盟所在显示器的目标矩形，并临时置于游戏上方；每次请求后验证真实前台窗口、可见状态、矩形和 Topmost 状态。
8. 抖音显示、恢复、重建或重新置顶后，再次把倒计时悬浮窗排到 Topmost 组顶部，确保它位于真实抖音窗口之上。
9. 使用已校准的媒体控制器发送明确的 Play 命令，并验证结果。
10. 在阵亡期间持续用最新的 `respawnTimer` 校准倒计时，但不会持续重发 Play 或强制抖音回到前面。

如果程序首次连接对局时玩家已经阵亡，同样先进入 `LifeState=Dead`，再使用上述计时门槛决定是否附着媒体和窗口；首次同步、实时死亡和失联后迟到发现死亡采用同一套规则。

### 5.3 正常复活流程

1. 监听器观察到本机玩家连续有效状态从 `isDead=true` 变为 `isDead=false`。
2. 取消该阵亡周期尚未完成的抖音启动、窗口恢复或播放任务，防止复活后迟到的异步任务重新打开抖音。
3. 使用同一个已校准的媒体控制器发送明确的 Pause 命令并验证结果。
4. 隐藏倒计时悬浮窗。
5. 取消本程序仍可证明由本周期施加的抖音临时置顶，并按逐属性 compare-and-restore 规则恢复阵亡前的位置、尺寸和可见状态：只有当前值仍等于本程序最后实际应用的值时才恢复；用户在阵亡期间主动移动、缩放、最大化、最小化或改变置顶后的属性不被覆盖。若抖音是本周期才启动的，默认使用非激活最小化进入任务栏，不把隐藏窗口冒充“最小化到托盘”，也不强制结束抖音进程。
6. 如果英雄联盟仍是前台窗口，则无需焦点操作，游戏画面会随着抖音隐藏立即露出。
7. 如果抖音或其他窗口已经取得焦点，则先恢复游戏窗口，再调用一次 `SetForegroundWindow`，随后用 `GetForegroundWindow` 验证结果。
8. 若 Windows 拒绝切回，程序不循环抢焦点。它在屏幕顶部显示最多 5 秒的“已复活，按 Ctrl+Alt+F9 尝试返回游戏”提示，并闪烁游戏任务栏按钮。热键处理也只尝试一次并验证；仍失败时提示用户点击任务栏或手动 Alt+Tab。

### 5.4 用户主动操作

- 用户在阵亡期间手动暂停抖音：程序尊重用户操作，不持续强制播放；复活时仍发送幂等的 Pause。
- 用户在阵亡期间关闭抖音：程序不重新无限拉起；本周期记录失败并继续显示倒计时。
- 用户手动切回游戏：悬浮倒计时继续显示，抖音不被再次强制切到前面。
- 用户在阵亡期间移动、缩放、最大化、最小化或改变抖音置顶状态：恢复时只撤销当前仍等于程序所施加值的属性，不覆盖用户之后作出的窗口选择。
- 用户暂停 RespawnSwitch：立即取消活动周期、隐藏悬浮窗、取消抖音临时置顶并恢复已保存的窗口状态。
- 程序正常退出或捕获到可恢复异常：执行同样的清理流程。

## 6. 总体架构

```text
Riot Live Client Data API
          |
          v
 LeagueGameProbe ----> GameSample
          |                 |
          v                 v
   RespawnStateMachine -> RespawnCoordinator
                              |
              +---------------+----------------+
              |               |                |
              v               v                v
       DouyinMedia       DouyinWindow      RespawnOverlay
       Controller        Controller        Controller
              |               |                |
              +---------------+----------------+
                              |
                              v
                     Diagnostics / Local Log
```

每个模块通过接口和不可变数据对象通信；模块不得直接访问另一个模块的 UI 控件或内部状态。协调器是唯一允许同时编排媒体、窗口和悬浮窗动作的组件。

## 7. 组件设计

### 7.1 `LeagueGameProbe`

职责：只读访问 Riot 本机 Live Client Data API，输出标准化的 `GameSample`。

核心请求：

```text
GET https://127.0.0.1:2999/liveclientdata/activeplayername
GET https://127.0.0.1:2999/liveclientdata/playerlist
GET https://127.0.0.1:2999/liveclientdata/gamestats
```

兼容回退：

```text
GET https://127.0.0.1:2999/liveclientdata/allgamedata
GET https://127.0.0.1:2999/swagger/v3/openapi.json
```

匹配规则：

- 首选完整 `riotId`，包含 Game Name 和 Tag Line。
- 不按英雄名、数组位置或不含 Tag 的名称匹配。
- `playerlist` 正常时不持续请求体积更大的 `allgamedata`。
- `allgamedata` 只在当前版本的 `playerlist` 端点缺失或结构异常时启用。

采样频率：

- 对局中只把 `playerlist` 作为 250 毫秒高频请求；`activeplayername` 在首次连接、新时间线或匹配失效时读取，`gamestats` 每 1 秒读取并用于模式/时间线诊断。高频状态判定不等待三个端点串行完成。
- 未进入对局、连接被拒绝或游戏退出：从 1 秒退避到 2 秒。
- 每次请求设置短超时并支持取消；旧请求结果不得覆盖更新的阵亡周期。

TLS：

- 仅允许目标主机 `127.0.0.1` 和固定端口 `2999`。
- 使用 Riot 官方 `riotgames.pem` 建立应用内自定义信任或执行严格的证书校验。
- 不修改系统全局证书策略，也不全局关闭 HTTPS 证书验证。

`GameSample` 至少包含：

```text
SampleId
ObservedAtMonotonic
RiotId
IsDead
RespawnTimerRaw
RespawnTimerSeconds
GameTimeSeconds
GameMode
IsStale
SchemaSource
```

### 7.2 `RespawnStateMachine`

生命状态和连接状态是两个正交维度，不能用连接失败覆盖最后可信的生命状态：

```text
LifeState       = Unknown | Alive | Dead
ConnectionState = NoGame | Online | Stale

LastConfirmedLifeState
ActiveCycleId
ActiveCycleStatus = None | Active | AbandonedUnknown | Completed
```

连接规则：

```text
NoGame -> Online
  第一次成功读取到 active player

Online -> Stale
  距最后成功样本超过 1 秒

Stale -> Online
  恢复有效样本

Online/Stale -> NoGame
  Toolhelp 进程快照与顶层窗口检查均确认游戏进程/窗口消失，
  且在跨越至少 2 秒的两个连续检查中仍然消失
```

仅仅连续 5 秒无法访问 2999 端口不能进入 `NoGame`，也不能更新 `LifeState`。为了避免接口长时间失联时抖音永久遮挡桌面，`Dead + Stale` 持续 5 秒会执行独立的 `AbandonCycleDueToUnknown` 安全收起路径：暂停本周期启动的媒体、隐藏悬浮窗、撤销仍由本周期拥有的窗口副作用，并把周期标记为 `AbandonedUnknown`。它不是复活事件，不把 `LifeState` 改成 Alive，也不记录“玩家已复活”。

生命状态与失联恢复规则：

```text
Unknown + Online sample Alive -> Alive
  只同步，不产生复活事件

Unknown + Online sample Dead -> Dead
  创建周期，并按统一的 2 秒计时门槛决定是否附着媒体/窗口

Alive + Online sample Dead -> Dead
  创建一次新阵亡周期

Dead + Online sample Alive -> Alive
  对原周期执行一次复活清理

LastConfirmed Alive + Stale + recovered Dead
  创建一次新阵亡周期，按迟到附着规则处理

LastConfirmed Dead + Stale + recovered Alive
  对原周期执行一次复活清理，然后进入 Alive

LastConfirmed Dead + Stale + recovered Dead
  继续原 Cycle，只重新锚定计时，不重放 Play 或置顶；
  若周期已 AbandonedUnknown，只恢复准确倒计时，不重新打开抖音
```

新游戏进程、新 Riot ID 或 `gameTime` 大幅回退代表新时间线：先幂等清理旧周期，再清空 Cycle ID 和去重缓存，重新同步；这个过程也不伪装成复活事件。

约束：

- `isDead` 是存活状态的唯一主判据。
- `respawnTimer > 0` 不能单独触发阵亡；它只决定显示和媒体附着门槛。
- 请求失败、JSON 错误或端口关闭不能触发复活，也不能抹去 `LastConfirmedLifeState`。
- `isDead=false` 与正数 `respawnTimer` 冲突时优先信任 `isDead`，不显示该次计时值并记录诊断。
- `scores.deaths` 只用于诊断一致性，不驱动状态机。
- 每个 Cycle 记录 Play、窗口变更、Overlay 显示等副作用是否实际发生；清理只撤销真实发生且仍由本周期拥有的动作。

### 7.3 `RespawnClock`

Riot 公开文档列出了 `respawnTimer` 数值字段，但没有规范性说明其单位、递减方式、暂停行为和特殊复活语义。因此，自动显示真实“秒”数之前必须在当前补丁完成阻断性的真实客户端语义探针：

1. 在训练工具中同步记录 `isDead`、`respawnTimer`、`gameTime` 和单调时钟，并录制游戏画面中的可见复活倒计时。
2. 验证普通死亡时字段为有限非负数、近似按秒递减，并在复活时归零或进入一致的非死亡值。
3. 对比画面倒计时，确认单位、向上取整规则和允许误差。
4. 验证训练工具暂停、重置和快速复活。
5. 观察至少一种特殊复活机制；结果不符合普通死亡模型时，该机制标记为实验性而不是硬编码公式。
6. 训练工具通过后，在普通非排位对局做一次只读记录验证；未单独验证的 ARAM 和限时模式继续标记实验性。

探针未通过前，程序可以显示“已阵亡”并记录原始字段，但不能把该字段标注为经过验证的剩余秒数，也不能宣称完整验收通过。

有效样本到达时保存：

```text
anchorRemaining = max(0, respawnTimer)
anchorMonotonic = current monotonic timestamp
```

显示值：

```text
displaySeconds = ceil(max(0,
  anchorRemaining - (nowMonotonic - anchorMonotonic)))
```

每个新样本重新锚定。悬浮窗可每 100 毫秒刷新动画，但数字按向上取整后的整数显示。系统墙上时钟变化不会影响倒计时。

只有有限、非负的计时值才允许建立锚点。最后成功样本超过 1 秒后，不再继续把本地插值显示为准确时间；悬浮窗改为“连接不稳定”。确认进入 `NoGame` 或执行 `AbandonCycleDueToUnknown` 后隐藏。

### 7.4 `IDouyinMediaController`

统一接口提供：

```text
ProbeAsync
PlayAsync
PauseAsync
GetPlaybackStateAsync
```

所有 Play/Pause 操作都返回结构化结果：是否已发送、是否由目标接受、能否验证最终状态、失败原因和使用的控制器名称。

控制器优先级：

1. `GsmtcDouyinMediaController`
   - 每次操作前重新枚举 GSMTC 会话，并在 `SessionsChanged` 后使旧选择失效；不长期持有旧 session 对象。
   - 使用首次运行时确认过的 `SourceAppUserModelId` 和辅助诊断指纹匹配。匹配结果必须恰好一个；零个或多个都 fail closed 并要求重新选择，媒体标题不能作为稳定唯一键。
   - 使用 `TryPlayAsync` 和 `TryPauseAsync`，不使用状态易漂移的 Toggle。
   - 调用后重新读取播放状态；无法验证时记录“命令已接受但状态未知”。
   - Debug 构建和最终 self-contained x64 发布物分别执行完整 GSMTC 验收，不能用开发环境通过替代发布物验证。

2. `UiaDouyinMediaController`
   - 仅搜索已验证的抖音进程窗口和其 UI Automation 子树。
   - 优先使用 AutomationId、ControlType、父子结构和可调用 Pattern；可见文字只能作为辅助信号。
   - 只有存在不同的 Play/Pause 控件，或能通过可靠 UIA 属性/状态源判断当前播放状态时才可启用。若只有未知状态的 Toggle，控制器 fail closed。
   - 调用 Toggle 前先读状态；已经处于目标状态时不调用，状态未知时不猜测。
   - 在专用后台 UIA 线程执行并设置有界超时，不从 WPF Dispatcher 线程直接调用。目标 Provider 挂起后隔离该控制器，不阻塞状态协调器。
   - 每次抖音版本变化或选择器失效后停止使用，要求重新校准。
   - 不使用绝对屏幕坐标，也不发送空格键。

3. 可选实验性 `WM_APPCOMMAND` 诊断
   - Microsoft 定义了离散的 `APPCOMMAND_MEDIA_PLAY` 和 `APPCOMMAND_MEDIA_PAUSE`，但目标窗口若未处理消息可能把它交给系统 Shell。
   - 因此第一版只把它放在诊断页面，不作为自动流程的默认回退，也不在存在其他媒体会话时自动调用。用户必须明确确认潜在副作用。
   - 使用带短超时和 `SMTO_ABORTIFHUNG` 的 `SendMessageTimeout`，不使用无界 `SendMessage`；返回值不能证明抖音已经处理，仍需单独验证播放状态。

全局 `VK_MEDIA_PLAY_PAUSE`、`SendInput` 空格键和坐标点击均不进入自动控制链。

### 7.5 `DouyinWindowController`

职责：启动、识别、显示、置顶、隐藏和恢复抖音主窗口。

识别流程：

1. `EnumWindows` 枚举顶层窗口。
2. `GetWindowThreadProcessId` 获取 PID。
3. 校验规范化完整可执行文件路径严格等于首次校准确认的 `D:\douyin\douyin.exe`，并核对进程启动时间、窗口类和首次确认的文件签名身份；安装目录只能作为辅助信号。
4. 排除不可见工具窗、托盘窗、零尺寸窗和子进程无 UI 窗口。
5. 使用首次诊断保存的窗口类和尺寸特征评分；存在歧义时停止自动动作并提示重新选择。
6. 缓存 HWND，但每次操作前后都调用 `IsWindow` 并重新核对 PID、进程启动时间、路径和窗口类；任何不一致都视为 HWND 销毁/复用竞态并停止操作。

阵亡前保存：

- `WINDOWPLACEMENT`
- 是否可见、是否最小化/最大化
- 是否已经 Topmost
- 原始显示器和窗口矩形

阵亡时以英雄联盟窗口所在显示器为目标。非激活路径不得调用会激活窗口的 `SW_RESTORE`、`SW_SHOW` 或 `SW_SHOWMAXIMIZED`；优先现场验证 `SW_SHOWNOACTIVATE` / `SW_SHOWNA`、正确初始化的 `SetWindowPlacement`，以及 `SetWindowPos(SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS)` 的组合。铺满画面优先调整普通窗口到目标显示器工作区矩形，而不切换真实最大化状态。若当前抖音版本无法在不激活的情况下从最小化恢复，诊断应明确记录该能力缺失，允许窗口取得焦点但不得继续宣称焦点被保留。

所有 Win32 调用按其真实语义记录结果：

- `ShowWindow` 返回值只表示调用前是否可见，不是成功标志。
- `SetWindowPos` 搭配 `SWP_ASYNCWINDOWPOS` 成功只表示请求已接受或投递，不表示已经生效。
- 每个窗口请求后在短期限内轮询验证 `IsWindowVisible`、`GetWindowRect`、扩展边框矩形、`WS_EX_TOPMOST`、真实前台窗口和目标身份。
- 验证超时返回“请求已投递但状态未确认”，不记录成功。

恢复使用 compare-and-restore：恢复记录同时保存原始状态、目标状态、每一步实际应用位和本程序最后验证到的状态。恢复某项属性前，只有当前值仍等于本程序最后应用值才写回原值；用户期间改变的属性保持不动。Topmost 原本已存在时不得清除。每次抖音重建、调整或重新置顶后，协调器必须再次把悬浮窗排到 Topmost 组顶部。

### 7.6 `LeagueWindowController`

职责：识别真正的 `League of Legends.exe` 游戏窗口，而不是 Riot Client 或大厅窗口；验证无边框模式；必要时恢复游戏焦点。

为了保持“不打开英雄联盟进程句柄”的硬边界，识别只使用 `EnumWindows`、`GetWindowThreadProcessId`、`CreateToolhelp32Snapshot` / `Process32First` / `Process32Next` 得到的进程名，以及已校准的窗口类、可见顶层窗口和窗口矩形特征。实现不得对 LoL PID 调用 `Process.MainModule`、`OpenProcess`、`QueryFullProcessImageName` 或申请任何 `PROCESS_*` 访问权。开发验收通过代码审计并在目标电脑用 ETW 或 Sysinternals 句柄跟踪确认未创建指向 LoL PID 的进程句柄。

焦点恢复顺序：

1. 若 `GetForegroundWindow` 已是游戏，返回成功。
2. `ShowWindow(gameHwnd, SW_RESTORE)`。
3. 调用一次 `SetForegroundWindow`。
4. 再次读取 `GetForegroundWindow` 验证。
5. 失败则闪烁任务栏并显示全局热键提示，不进行循环、模拟 Alt、`AttachThreadInput` 或注入操作。收到热键后也只尝试一次并再次验证；仍失败则明确要求用户点击游戏任务栏按钮或手动 Alt+Tab。

### 7.7 `RespawnOverlay`

悬浮窗采用 WPF 呈现并通过 Win32 扩展样式实现：

- WPF `WindowStyle=None`；如使用 `AllowsTransparency=True`，始终与 `WindowStyle=None` 配套。
- 在 `SourceInitialized` 后取得 HWND，再施加并读取验证扩展样式。
- `WS_EX_LAYERED`
- `WS_EX_TRANSPARENT`
- `WS_EX_NOACTIVATE`
- `WS_EX_TOOLWINDOW`
- `WS_EX_TOPMOST`

默认布局：英雄联盟所在显示器顶部中央，宽 360 WPF 设备无关单位、高 72 WPF 设备无关单位，并按显示器 DPI 自动缩放；使用半透明深色圆角背景、白色大号数字和短中文状态文字。默认显示形式为：

```text
复活还有 17 秒
```

状态文案限定为：

- `复活还有 N 秒`
- `正在读取复活时间`
- `连接不稳定`
- `已复活，按 Ctrl+Alt+F9 返回游戏`
- `抖音播放失败，倒计时仍在运行`

第一版允许在设置页调整位置、缩放、透明度和是否显示背景；默认保持鼠标穿透且不接收键盘焦点。

悬浮窗和抖音同属 Topmost 组时，协调器必须在抖音窗口操作之后再把悬浮窗排到该组顶部，并验证真实显示效果。多显示器验收覆盖 100%、125% 和 150% DPI、负坐标副屏、主副屏不同 DPI 和游戏跨显示器切换。若无边框模式在目标机器的全屏优化、HDR 或硬件 Overlay 组合下仍看不到悬浮窗，诊断标记该显示组合不支持并降级为声音/任务栏提示，不能宣称悬浮显示成功。

### 7.8 `RespawnCoordinator`

职责：把状态事件转换为一次性、可取消的副作用序列。

- 每次阵亡创建唯一 Cycle ID 和 `CancellationTokenSource`。
- 所有异步窗口、进程和媒体任务携带 Cycle ID；结果返回时若周期已过期则丢弃并执行清理。
- 阵亡流程最多执行一次；相同状态样本只更新倒计时。
- 每个 Cycle 分别记录 Overlay、Play、窗口显示、位置调整和 Topmost 是否已经实际发生，以及程序最后验证到的属性值。
- 复活、用户暂停、确认游戏退出或应用退出都会取消活动周期并只清理实际发生的副作用；接口失联本身不触发复活清理。
- `Dead -> Stale -> Alive` 的成功 Alive 样本结束原周期；`Dead/Stale -> NoGame` 在确认游戏退出后取消周期；没有执行媒体或窗口动作的短周期不会发送无来源的恢复操作。
- `Dead + Stale` 超过 5 秒执行单独的 `AbandonCycleDueToUnknown` 安全收起，并保持最后可信生命状态，不把安全收起记成复活。
- 清理操作必须幂等，多次调用不会再次改变已经恢复的窗口状态。
- 媒体失败不阻止倒计时；窗口失败不伪造媒体成功；各模块的失败相互隔离。

### 7.9 托盘、设置和日志

托盘菜单：

- 状态：未在对局 / 存活 / 阵亡 / 连接异常
- 启用或暂停自动监听
- 打开设置
- 测试抖音播放/暂停
- 预览倒计时
- 手动模拟阵亡/复活
- 打开诊断日志目录
- 退出

设置以本地 JSON 保存到用户的 LocalApplicationData 目录，采用原子替换写入。保存内容仅包括路径、媒体会话标识、窗口识别特征、UIA 选择器、悬浮窗样式和热键。不得保存 Riot 登录令牌、抖音 Cookie、密码或游戏接口响应全文。

在改变抖音窗口的可见性、位置或 Topmost 状态前，程序会原子写入一个独立的最小活动周期恢复记录，包含 Cycle ID、规范化可执行文件路径、PID、进程启动时间、窗口类、原始 `WINDOWPLACEMENT`、原始 Topmost/可见状态、准备施加的目标状态、`MutationStarted` 标志，以及每一步经过验证后实际应用的变更位。每次窗口变更前先持久化意图，变更验证后再持久化结果；只有清理并验证成功后才删除记录。

下次启动不能直接信任旧 HWND。只有规范化路径、PID、进程启动时间和窗口类仍同时匹配，且当前属性仍等于记录中本程序最后实际应用的值时，程序才按逐属性 compare-and-restore 恢复；无法证明目标身份或属性所有权时只报告人工恢复提示，不把旧快照应用到新窗口或复用 PID。

日志默认保留最近 7 天或最多 20 MB，以先达到者为准。日志包含时间、组件、状态转换、错误类型、耗时和脱敏后的窗口/媒体诊断；完整 Riot ID 只在内存中匹配，写日志时进行掩码处理。

## 8. 错误处理与降级

| 情况 | 行为 |
|---|---|
| 2999 端口未开放 | 若游戏进程/窗口不存在并经两次检查确认，进入 `NoGame`；若游戏仍存在则进入 `Stale`，保留最后可信生命状态并退避重试 |
| `playerlist` 暂时失败 | 尝试 `allgamedata` 兼容读取；超过 1 秒显示连接不稳定 |
| JSON 结构变化 | 读取本机 OpenAPI 文档做特性诊断，记录 schema 错误并停用相关功能 |
| `isDead` 可用但 `respawnTimer` 缺失/非法 | 仍按 `isDead` 进入 Dead 或 Alive；Dead 时显示“正在读取复活时间”且暂不启动抖音，后续有效值达到 2 秒门槛时允许附着一次 |
| 抖音没有 GSMTC 会话 | 使用已校准的 UI Automation 控制器 |
| GSMTC 和 UIA 均不可用 | 不发送全局媒体键；显示播放失败，仍显示倒计时 |
| 抖音未登录或被弹窗阻塞 | 不自动导航；显示诊断并保持倒计时 |
| 抖音启动慢，英雄已复活 | Cycle ID 取消迟到任务，不再显示或播放 |
| 抖音窗口在周期内重建 | 旧 HWND 失效后重新解析一次；仍歧义则停止本周期窗口动作 |
| 接口在 Dead 状态持续失联 5 秒 | 执行 `AbandonCycleDueToUnknown` 安全收起窗口/媒体副作用，但保留 Dead + Stale，不记录复活 |
| Windows 拒绝恢复游戏焦点 | 显示 5 秒“热键尝试返回”提示并闪烁任务栏，不循环抢焦点；热键仍失败则提示手动切换 |
| 用户锁屏、UAC 安全桌面或远程会话异常 | 暂停窗口自动化；恢复普通桌面后重新同步状态 |
| 程序异常退出 | 下次启动验证恢复记录和目标身份后逐属性尽力恢复；无法证明所有权时只提示人工处理，不强制终止抖音 |

任何接口错误都不得被解释为“玩家已经复活”。任何媒体命令返回都不得在未检查结果时记录为成功。

## 9. 安全与 Riot 政策边界

### 9.1 技术边界

- 只向 loopback 地址 `127.0.0.1:2999` 发送只读 GET。
- 不打开英雄联盟进程句柄，不读内存，不扫描模块。
- 不注入 DLL，不设置全局或进程 Hook。
- 不抓取、修改或解码游戏网络通信。
- 不发送英雄联盟键盘、鼠标、聊天或 API 写操作。
- Windows 自动化只作用于抖音和本程序自己的窗口。
- 不以管理员权限常驻。

### 9.2 产品边界

- 只使用本机玩家本人已经能看到的阵亡状态和复活倒计时。
- 不显示或分享其他玩家的隐藏、推断或不公平信息。
- 不向队伍聊天广播计时，不替玩家作游戏决策。
- 当前阶段是仅在本机进行、未分发的内部原型验证。把构建交给任何其他玩家之前，包括封闭测试、公开测试或正式发布，都必须重新读取当时有效的 Riot 政策，在 Developer Portal 登记产品并接受适用审查。
- 分发门禁还包括用途与端点说明、隐私与本地日志说明，以及在首次运行/About 页面展示 Riot 当时要求的完整非背书声明。声明正文必须在发布时从当前官方政策取得，不能沿用可能过期的缓存文案。
- 使用 Riot 当前公开记录的本机游戏内接口不等于产品自动获准，也不保证未来版本继续兼容；发布说明必须保留这一区分。

## 10. 测试策略

### 10.1 单元测试

- Riot JSON 正常、缺字段、字段类型错误和 schema 兼容解析。
- Riot ID 精确匹配与重复英雄场景。
- `LifeState` 与 `ConnectionState` 的正交组合，以及全部失联恢复分支。
- 首次连接、正常转换和迟到发现阵亡时，计时值大于、等于、小于 2 秒，以及缺失、NaN、无穷和负数的统一附着门槛。
- 请求失败不能触发复活。
- 倒计时向上取整、单调时钟插值、重锚和 stale 截止。
- `AbandonCycleDueToUnknown` 安全收起不改变最后可信生命状态。
- Cycle ID 取消迟到任务，副作用位只记录实际完成的动作。
- 窗口逐属性 compare-and-restore 不覆盖用户后续更改。
- GSMTC 零匹配、唯一匹配和同 AUMID 多匹配，以及 UIA 四种幂等状态校准。
- 设置原子保存、热键冲突和日志脱敏。
- 恢复记录的意图/结果事务、目标身份验证和清理幂等性。

### 10.2 组件测试

- 使用本地假 HTTPS 服务模拟 Riot 2999 端点和证书场景。
- 通过可注入 HTTP Handler 模拟超时、连接拒绝和 schema 损坏；不通过防火墙、代理、终止游戏组件或改动真实 2999 端口制造故障。
- 使用假的 `IDouyinMediaController` 验证 Play/Pause 顺序和失败隔离。
- 使用测试窗口验证枚举、异步窗口请求后的状态确认、Topmost、逐属性恢复、用户中途修改和 HWND 销毁/复用。
- 使用可控 UIA 测试 Provider 验证超时和后台线程隔离不会卡住 WPF Dispatcher 或状态协调器。
- 验证悬浮窗不进入 Alt+Tab、不获取焦点且鼠标穿透，并在另一个真实 Topmost 测试窗口之上可见。
- 验证正常退出、监听暂停和可捕获异常都会撤销拥有的临时 Topmost；受控强杀后由下次启动依据恢复记录尽力恢复。

### 10.3 目标电脑手动验收

1. 在未进入对局时启动程序，确认不误触发抖音。
2. 在英雄联盟训练工具中使用无边框模式进入对局，先执行 `respawnTimer` 真实语义探针并与游戏画面录像对比。
3. 验证训练工具普通死亡、暂停、重置和快速复活；在可安全构造时观察至少一种特殊复活机制。
4. 打开抖音任意视频，完成 GSMTC 的四种幂等状态探测；若没有唯一会话，再完成 UIA 四种探测。
5. 让本机英雄阵亡，记录采样间隔、应用内事件延迟、抖音显示、播放结果和倒计时精度。
6. 验证悬浮窗在真实抖音窗口覆盖真实游戏窗口时仍位于最上层，并测试 100%、125%、150% DPI 和可用的多显示器布局。
7. 在倒计时期间分别测试不触碰抖音、点击抖音、手动切回游戏，以及移动、缩放、最大化和最小化抖音。
8. 复活后验证 Pause、悬浮窗隐藏、逐属性窗口恢复和游戏焦点行为；热键仍失败时验证手动提示。
9. 通过测试构建的可注入 Handler 模拟 2999 超时/断开，同时关闭抖音、制造媒体控制失败和热键冲突，验证降级；不干预真实游戏端口或服务。
10. 连续完成至少 10 次阵亡/复活循环，确认没有残留 Topmost、重复播放或资源泄漏。
11. 分别从抖音原本隐藏、最小化、最大化、普通显示和本周期才启动的状态开始，验证恢复合同。
12. 完成一次正常退出、一次可捕获异常和一次受控强杀后的下次启动尽力恢复测试。
13. 训练工具通过后，在普通召唤峡谷非排位对局进行一次只读字段与状态验收；不得在排位赛中首次测试新构建。ARAM 和限时模式在单独验收前保持实验性。

## 11. 第一版验收标准

第一版被视为可交付，需要同时满足：

1. 全部自动化测试通过。
2. 在目标电脑训练工具中连续 10 次阵亡/复活循环均正确识别，且没有把接口失败误判为复活。
3. 在 API 在线、请求无超时且机器未处于异常高负载的验收窗口内，成功样本间隔 P95 不超过 400 毫秒、P99 不超过 500 毫秒；从首个携带新 `isDead` 的完整响应结束到状态机事件发布 P95 小于 50 毫秒，从事件发布到悬浮窗首帧/窗口动作发起 P95 小于 50 毫秒。游戏画面到动作的端到端 500 毫秒目标仅记录为当前补丁观察指标，不作为 Riot SLA。
4. 当前补丁的真实语义探针确认 `respawnTimer` 单位和普通复活行为；在 API 正常时，悬浮显示与游戏画面可见倒计时误差不超过 1 秒，而不是只与同一个输入字段循环比较。
5. 至少一种定向抖音控制器在目标电脑上通过 Play 和 Pause 验证；若无任何控制器可用，程序必须明确报告“当前抖音版本不支持自动播放控制”，不能宣称完整验收通过。
6. 抖音窗口在复活、暂停监听和正常退出后不残留本程序拥有的临时 Topmost；程序只恢复仍等于其最后施加值的属性，不覆盖用户中途修改。强杀或掉电后的行为限定为下次启动依据记录尽力恢复。
7. 全局热键注册和消息接收必须可用，目标电脑上焦点恢复应通过；Windows 二次拒绝时必须提示点击任务栏或手动 Alt+Tab。程序不会循环抢焦点或发送游戏输入。
8. 程序无需管理员权限，不打开英雄联盟进程句柄，不产生 DLL 注入或 Hook；代码审计和目标电脑句柄跟踪均确认这一点。
9. 在 Spotify 或浏览器媒体同时存在时，不误控制其他媒体会话。
10. 普通召唤峡谷非排位对局的只读验收通过；未单独验证的 ARAM、限时模式和特殊复活机制继续清楚标记实验性。
11. 用户可以从托盘立即暂停自动化并完全退出。

## 12. 实现顺序

后续实现计划应按以下风险顺序展开：

1. 建立解决方案、测试工程和核心状态模型。
2. 实现 Riot 数据读取、证书处理、解析和状态机，并使用假服务完成自动测试。
3. 在目标电脑训练工具执行 Riot 当前补丁的真实字段、证书和 `respawnTimer` 语义探针；这是第一项阻断性真实环境验证。
4. 实现目标电脑上的 GSMTC 媒体会话诊断，并在 Debug 和 self-contained 发布物中分别验证。
5. 实现抖音窗口识别、事务恢复记录和可验证的 Topmost/窗口位置管理。
6. 实现透明倒计时悬浮窗及相对 Z-order、多显示器 DPI 验证。
7. 实现协调器、取消语义、失联安全收起和完整阵亡/复活流程。
8. 在 GSMTC 不可用时实现并校准 UI Automation 控制器。
9. 实现托盘设置、日志、热键和诊断界面。
10. 完成训练工具、普通非排位对局、打包和退出/崩溃恢复验收。

## 13. 关键依据

- Riot 官方 League of Legends 开发者文档：<https://developer.riotgames.com/docs/lol>
- Riot 官方 Vanguard FAQ：<https://developer.riotgames.com/docs/faqs#vanguard>
- Riot 官方通用第三方产品政策：<https://developer.riotgames.com/policies/general>
- Microsoft GSMTC Session Manager：<https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager>
- Microsoft GSMTC Session：<https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession>
- Microsoft `SetForegroundWindow`：<https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow>
- Microsoft `SetWindowPos`：<https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos>
- Microsoft `ShowWindow`：<https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow>
- Microsoft Tool Help Process Snapshot：<https://learn.microsoft.com/en-us/windows/win32/toolhelp/taking-a-snapshot-and-viewing-processes>
- Microsoft Extended Window Styles：<https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles>
- Microsoft UI Automation Threading Issues：<https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-threading-issues>
- Microsoft WPF `AllowsTransparency`：<https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency>
- Microsoft `WM_APPCOMMAND`：<https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-appcommand>
- 抖音电脑客户端官方下载：<https://www.douyin.com/downloadpage/pc>

## 14. 已作出的明确决策

- 使用 C#、.NET 8、WPF 和 Windows 原生 API，不使用 Python 或 Electron。
- 正式支持无边框窗口，不承诺全屏独占。
- `playerlist.isDead` 是死亡状态真值，`playerlist.respawnTimer` 是显示计时来源。
- `respawnTimer` 的“秒”和特殊机制语义必须由当前补丁真实客户端探针确认，不能把字段存在误写成 Riot 的严格协议保证。
- 生命状态与连接状态正交；接口失败不触发复活，长时间失联只执行明确标记的安全收起。
- 使用完整 Riot ID 匹配本机玩家。
- 抖音优先通过 GSMTC 定向控制，UI Automation 是已校准的回退。
- UI Automation 只有在能实现可验证、幂等的 Play/Pause 时才算校准成功；未知 Toggle fail closed。
- 不自动使用全局媒体键、空格键或坐标点击。
- 抖音窗口临时覆盖游戏但首选不激活；复活时隐藏抖音，只有必要时才尝试恢复游戏焦点。
- Windows 拒绝抢焦点时使用提示和全局热键，不使用不稳定的绕过技巧。
- 所有副作用都绑定阵亡周期并可取消，避免复活后迟到执行。
- 抖音窗口采用事务恢复记录和逐属性 compare-and-restore，不覆盖用户在阵亡期间的后续窗口选择。
- 英雄联盟窗口识别使用 Toolhelp 快照和窗口特征，不打开游戏进程句柄。
- 第一版完全本地运行，日志脱敏，不保存凭据。
