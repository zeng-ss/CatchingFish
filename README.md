# 捕鱼小游戏 Demo

Unity **2022.3.52f1** + **URP** + **DOTween**，主场景 `Assets/Scenes/GameScene.unity`。

一个"下潜躲鱼、上浮抓鱼"的 2D 横版捕鱼小游戏。重点展示**怎么把玩法逻辑、表现、界面和美术资源
用一套可维护的结构组织起来**：MVC 单向依赖、事件总线、状态机、对象池、数据驱动，
以及"没有美术资源时用代码把资源造出来"。

---

## 〇、MVC 分层一览（先看这张表）

| 层 | 职责 | 代表文件 | 允许依赖谁 |
| --- | --- | --- | --- |
| **Model**（数据） | 一局的数值：氧气 / 深度 / 分数 / 渔获。数值一变就发事件 | `GameModel` | 只依赖 `Config` 与事件总线，**不引用任何 MonoBehaviour** |
| **Controller**（逻辑） | 鱼钩分段、鱼群模拟与回收、背景滚动、对象池。构造函数注入依赖 | `HookController` `FishController` `BGController` `FishSpawner` `InputController` | 依赖 Model 与同层，**不认识任何 View** |
| **View**（表现） | 把控制器算出来的状态贴到 Transform / UI 上；订阅事件刷界面 | `HookView` `FishView` `FishHang` `HUDView` `ResultPanel` `TutorialPanel` | 依赖 Controller（每帧"拉"状态）+ 事件总线 |
| **入口 / 组合根** | 唯一驱动控制器的地方：创建依赖、绑引用、跑状态机 | `GameMgr` | 认识所有层，但只暴露两个 View 引用 |
| **工具 / 配置** | 与业务无关的纯函数、对象池、两张 SO 数据表 | `MathUtil` `ViewportUtil` `PoolMgr` `GameConfig` `FishConfig` | 谁都能用，自己不依赖别人 |

依赖方向只有两条，代码里严格保持：

```
   Model ◄── Controller ◄── View            View ──► Controller（每帧读状态）
```

- **Controller 从不引用、不写任何 View**：它只更新自己的纯数据（`FishRuntime` 里没有 Transform、没有 FishView）。
- **View 与控制层靠"槽位 id"关联**：`FishView.Register()` 领 id → 每帧 `Get(id)` → 控制层回收后自己 `Release()`。
- **跨层通信只走事件总线**（`EventMgr`）：Model / Controller 广播，View 订阅，谁都拿不到对方的引用。

> 每个脚本的类注释第一行都标了所属层级（如 `【表现层】`、`【控制层】`），打开文件就能对上这张表。

---

## 一、这个 Demo 包含什么

| 模块 | 内容 |
| --- | --- |
| 玩法 | 抛钩 → 下潜（碰鱼扣氧气）→ 触底 → 上浮（碰鱼抓走）→ 满仓加速返回 → 结算 |
| 操控 | 长按鼠标（触摸）开始，按住左右拖动控制鱼钩横向移动 |
| 开场教程 | 开局前在面板里**实时**演示三段操作要点，可重播、可跳过（不依赖视频文件） |
| 结算 | 散开 + 飘字动画 → 面板弹入 → 渔获条目依次登场 → 总分滚动 → 按钮浮现 |
| 界面 | HUD / 教程面板 / 结算面板统一视觉：圆角面板、进度条三件套、文字投影、按钮主次配色 |
| 美术 | 鱼图标由**现有 3D 模型烘焙**成 PNG；UI 图（面板/卡片/按钮/色条/柔光/投影/徽章）由代码程序化生成 |
| 工程化 | 11 个编辑器菜单，一键生成配置资产、搭建界面、烘焙资源、检查接线、统一美化 |

**技术点**：MVC（Model ← Controller ← View，View → Controller 单向拉取）、观察者（静态事件总线）、
状态机、对象池、ScriptableObject 数据驱动、DOTween 时间轴、RenderTexture 实时取景、
Unity Trigger 碰撞、程序化纹理生成。

---

## 二、快速开始

```
1. 用 Unity 2022.3.52f1 打开工程
2. 工具 / 捕鱼 / 1.  生成配置资源                 ← 可选，缺了会走代码内默认鱼表
3. 工具 / 捕鱼 / 2.  搭建界面并接线
4. 工具 / 捕鱼 / 3.  给鱼预制体补 FishView        ← 到这里就已经能玩了（界面是纯色块版）
   ───────── 以下是教程与美术，可以整段跳过 ─────────
5. 工具 / 捕鱼 / 5.  搭建教程面板并接线           ← 需要场景里先有教程布景；没有就把下一行 `playTutorial` 关掉
6. 工具 / 捕鱼 / 6.  烘焙鱼的结算图标             ← 可选，不跑就用程序化剪影兜底
7. 工具 / 捕鱼 / 7.  生成结算界面用的 UI 图片
8. 工具 / 捕鱼 / 8.  重建结算条目预制体 ResultItem
9. 工具 / 捕鱼 / 10. 统一美化所有界面             ← 内含 9 号菜单的美化逻辑
10. 打开 Assets/Scenes/GameScene.unity，点 Play
```

- 菜单 4（检查场景接线）随时可以跑，它会打印一份接线体检报告，排查问题时先看它。
- 全流程缺任何资源都会**自动降级**（没图标走剪影、没 UI 图走纯色块），不会卡住。
- 教程布景 = `GuideBG / GuideHook / GuideCamera`（挂在 `GuideMgr` 上的 `TutorialDirector` 驱动）；
  不想要教程就在 `GameMgr` 上取消勾选 `playTutorial`，直接进 `Ready`。
- 操作：**长按鼠标**开始下潜，**按住拖动**左右移动鱼钩；结算后点"再来一局"或按 `R` 重开。

> DOTween 在 `Assets/Plugins/Demigiant`，`DOTweenSettings.asset` 已生成，开箱可用。

---

## 三、玩法与状态机

### 一趟下潜 = "鱼钩动" + "背景动"

纵向分成**由深度直接推导的六段**，不需要额外维护状态机：

| 段 | 深度区间 | 背景 | 鱼钩（DOTween） |
| --- | --- | --- | --- |
| ① 抛钩 Casting | 0 → `CastDepth`(13) | 不动 | `hookStartY`(15) → `hookMiddleY`(2) |
| ② 常规下潜 Cruising | 13 → `FinalDiveStartDepth`(74) | **滚动**（刷鱼：从屏幕下方进场） | 停在 `hookMiddleY` |
| ③ 触底冲刺 FinalDive | 74 → `maxDepth`(80) | **停住**（刷鱼：从左右两侧进场） | `hookMiddleY` → `hookBottomY`(-3.8) |
| ④ 收线起钩 PullUp | 80 → 74（反向） | **停住**（刷鱼：从左右两侧进场） | `hookBottomY` → `hookMiddleY` |
| ⑤ 常规上浮 Cruising | 74 → 13（反向） | **往回滚**（刷鱼：从屏幕上方进场） | 停在 `hookMiddleY` |
| ⑥ 收钩出水 ReelingOut | 13 → 0 | 不动（不刷鱼，避免鱼出现在开始画面） | `hookMiddleY` → `hookStartY` |

关键点：

- **世界滚动量和鱼钩高度都是深度的纯函数**（`GameConfig.WorldScrollAt` + `HookController.ResolvePhase`），
  上浮方向天然对称，不用写第二套逻辑。
- **补间时长 = 该段剩余深度 ÷ 当前速度**，所以鱼钩探到底的那一刻深度也刚好到底，不会割裂。
- **只在跨段的那一帧触发一次 DOTween**，不会每帧打断补间。
- ③④ 两段背景静止，鱼被"冻"在屏幕上只有鱼钩在压，是"要潜到最深了"的压迫感来源。

### 状态机

| 状态 | 说明 |
| --- | --- |
| `Tutorial` | 开局前的实时教程。主流程完全静默，点击只归面板按钮（`playTutorial` 可关） |
| `Ready` | 准备，等待长按开始 |
| `CastingDown` | 下潜：可左右控制，碰鱼扣氧气 |
| `ReelingUp` | 上浮：可左右控制，碰鱼抓取 |
| `FastReturn` | 满仓：不可操作，加速收线并结算 |
| `Settlement` / `Failed` | 结算 / 氧气耗尽；失败时镜头与鱼钩同步收线回起点 |

### 鱼群：单向游动 + 泳道防重叠

- 出生时定好方向，**一路游到底，不回头、不反弹**。
- 位置一律用**世界坐标**，父节点 `BG` 的缩放 `(1.17, 1.26, 1)` 完全不用管；
  纵向一行代码都不用写 —— 鱼是 BG 的子物体，世界整体滚动时自动与背景同速。
- **泳道系统（防重叠）**：
  - 生成前查重 `LaneOccupied()`：和已有鱼纵向挨得太近这一拍就不生成 ——
    密度由"一屏能排下几条"决定，**刷鱼间隔调到 0.1 也不会叠在一起**；
  - 游动时排队 `AllowedStep()`：同泳道里追上前面的鱼就贴住它，等它让开再走（不同泳道互不影响）。
- **回收**：完全移出屏幕 + 再往外走 `despawnMargin` 才回收；没进过画面的鱼若横向游出范围、
  或在屏幕外待超过 `maxOutsideLife` 秒，也一并回收（否则它们会永远占着刷鱼名额）。
- **刷鱼名额只算"自由游动"的鱼**，挂在钩上的不算 —— 否则每抓到一条就越来越少。

---

## 四、架构细节

### 分层与依赖

```
        ┌──────────── 依赖方向只有两条 ────────────┐
        │                                          │
   Model ◄── Controller ◄── View            View ──► Controller（每帧"拉"状态）
```

- **Controller 是纯 C# 类**，构造函数注入依赖，由 `GameMgr.Awake` `new` 出来并每帧驱动。
- **View 只做三件事**：把自己注册进控制层、每帧拉状态贴到 Transform/UI 上、订阅事件更新界面。
- **Model 是纯数据**，不引用 `MonoBehaviour`，数值变化时自己发事件。
- **数据引用只存在于控制层**：`FishRuntime` 里只有值类型 + `FishData`，没有 `Transform`、没有 `FishView`；
  View 与控制层之间用**槽位 id** 关联。

```
Assets/Scripts
├── Config/       GameConfig / FishConfig / DefaultGameData          数据表（ScriptableObject）
├── Core/         GameMgr(MonoBehaviour) / GameState / GameEvent / EventMgr
├── Model/        GameModel（含 CaughtFish）                          纯数据
├── Controller/   BGController / HookController / FishController / FishRuntime
│                 FishSpawner / InputController / CameraController
├── View/         HookView / FishView / FishHang / HUDView / ResultPanel / ResultItem
│                 SettleFx / FishIconLibrary / DynamicFontBootstrap
│                 TutorialDirector / TutorialFishView / TutorialPanel
├── Tool/         MonoSingleton / PoolMgr / ResourcesMgr / ViewportUtil / MathUtil
└── Editor/       FishAssetGenerator / GameSceneBuilder / ResultAssetGenerator / UiSkinBuilder
```

### GameMgr：唯一的逻辑入口

```csharp
private void Update()
{
    float dt = Time.deltaTime;

    _input.Tick();
    bool canControl = _state is GameState.CastingDown or GameState.ReelingUp;
    _hookCtrl.TickHorizontal(dt, canControl, _input);   // ① 驱动控制器
    TickState(dt);                                      // ② 状态机
    _fishCtrl.Tick(dt, _state, _gModel.Depth, ScrollDir);
    _bgCtrl.Tick();                                     // 背景贴图循环，放最后
}
```

`GameMgr` 是**组合根**：创建控制器、把 `HookController` 绑给 `HookView`、订阅"教程结束"事件，
其余 MonoBehaviour 都是表现层。它自己不做 `Find`、不做运行时 `AddComponent`；
唯一会自动补组件的地方是 `FishView`（给鱼补 kinematic `Rigidbody` 和 `FishHang`，
省得去改 9 个预制体）。

### 事件总线（`EventMgr` + `GameEvent`）

| 事件 | 负载 | 用途 |
| --- | --- | --- |
| `StateChanged` | `GameState` | HUD 切换提示文案 |
| `DepthChanged` / `HpChanged` / `ScoreChanged` / `FishCaught` | float / `HpPayload` / int / `CatchPayload` | HUD 数值 |
| `FishHurt` | int（扣掉的氧气） | 鱼钩受击闪烁（教程也复用它） |
| `GameSettle` | `SettlePayload` | 结算开始，`SettleFx` 播散开 + 飘字 |
| `SettleFishBurst` | `FishBurstPayload` | 某条鱼缩完 → 原地冒 "+分数" |
| `SettleAnimDone` | `SettlePayload` | 散开动画播完 → 结算面板才弹 |
| `TutorialHint` / `TutorialOxygen` / `TutorialFinished` | string / float / — | 教程面板显示 |
| `TutorialReplay` / `TutorialStartGame` | — | 面板按钮 → 导演 / 主流程 |

**约束**：View 之间不互相持有引用；跨模块只走事件，`EventMgr` 分发时带 try/catch，
单个订阅者异常不会打断其他订阅者。

### 设计模式落点

| 模式 | 位置 |
| --- | --- |
| 单例 | `MonoSingleton<T>` / `GameMgr` |
| 状态机 | `GameState` + `GameMgr.TickState`（含 `Tutorial` 前置态） |
| 观察者 | `EventMgr`：Model/Controller 广播，View 订阅 |
| 对象池 | `PoolMgr`，每种鱼一个池，`FishSpawner` 懒创建、池空自动补 |
| 工厂 | `FishSpawner`：只管造鱼/收鱼，不管鱼怎么动 |
| 数据驱动 | `GameConfig` / `FishConfig` 两张 SO 表；教程剧本、界面配色也都是 Inspector 字段 |
| 时间轴 / 序列编排 | `TutorialDirector` 的协程剧本、`ResultPanel` / `ResultItem` 的 DOTween 时间轴 |
| 单向数据流（拉模型） | View 每帧从控制层读状态贴到自己身上；控制层不推、不持有任何 View |

---

## 五、几个关键实现

**1. 补间的是 float，不是 `DOMoveY`**
`DOMoveY` 会把整条 `position` 锁成补间创建时的值，而 X 是每帧手改的，两者会打架。
所以补间的是一个私有 float `_visualY`，`HookView` 每帧把 `_hookCtrl.Position` 写进 Transform：

```csharp
_yTween = DOTween.To(() => _visualY, v => _visualY = v, targetY, duration).SetEase(ease);
```

**2. 相机与鱼钩"同速同步"**
开场镜头在鱼钩落到屏幕下边界往上 1/5 处时开始下移，时长 = 鱼钩剩余行程 ÷ 速度，
两者用同一个 `Ease.Linear` —— 所以鱼钩停到游玩位置的那一刻镜头也刚好到位。
收场同理；只有"氧气耗尽"没有上浮过程，`GameMgr` 会额外给鱼钩补一段和镜头**同参数**的收线
（`HookController.ReelOutForSettle`），否则会出现"只有镜头在动、鱼钩被甩出画面"。

**3. 碰撞用 Unity Trigger，但加了一层"钩头复核"**
鱼的 `CapsuleCollider` 是 trigger，鱼钩根节点是 kinematic `Rigidbody` + 实体碰撞体，
`HookView.OnTriggerEnter` 里回调控制层判定。
由于钩子上挂着**鱼线**这类细长装饰物，它们身上的碰撞体也会参与触发（表现为"没看见鱼却抓到了"），
所以 `HookView.IsNearHead()` 会再用**钩头包围盒**复核一次，只有真在钩头附近的鱼才算命中。

**4. 背景：循环铺满 + "回到水面还原美术布局"**
`BGController` 按实测的贴图高度把 `bgs` 下的贴图循环铺满视野（少一张都会 Console 报警）。
重开一局时 `ResetScroll()` 除了把滚动量归零，还会**把贴图摆回美术作者摆好的位置**：
否则贴图仍停在"深水那一屏"的排布上，世界根节点一退回原点，覆盖区整体下移几十米，
开始画面上方就会露出天空盒。`Tick()` 里另有一层兜底 —— 背景静止时若发现这一屏没被完全盖住，
会重排甚至整叠平移，让"露天空盒"成为不可能状态。

**5. 教程：独立布景 + 专用相机 → RenderTexture → 面板 RawImage**
教程不是录像，而是**实时演出**：

```
GuideCamera ──(targetTexture)──► RenderTexture ──(RawImage)──► 教程面板
GuideHook / GuideBG（z≈200 的独立布景，和主游戏互不干扰）
```

- `TutorialDirector` 用一段协程序列摆拍：钩子左右滑 → 下潜撞鱼扣氧气 → 上浮抓三条鱼。
  **"什么时候撞上"是算出来的**（鱼的出场时刻 = 相遇时刻 − 游泳耗时），所以调参数不会错位、一次就能演对。
- 道具鱼 `TutorialFishView` 会在 `Awake` 里把自己身上的 `FishView`/`FishHang` 关掉，
  绝不去注册真游戏的鱼槽位；扇形展开复用 `FishHang.FanAngle`，观感和实机一致。
- `TutorialPanel` 运行时按面板尺寸创建 `RenderTexture`，面板是 Overlay Canvas，
  所以取景相机**不会拍到面板自己**，也不会有递归画面。

**6. 结算：先散开，再开面板，最后逐条登场**
`GameSettle` → `SettleFx` 让钩上的鱼散开缩小 + 冒 "+分数" → `SettleAnimDone` → `ResultPanel` 弹面板。
面板用**一条总时间轴**排完所有内容（面板弹入 → 渔获逐条插入 → 总分滚动 → 按钮浮现），
`ResultItem` 只负责"造一段动画交出来"，节奏只有一个地方调。

**7. 美术资源全部由代码造出来**
没有鱼图片、没有 UI 图，于是分两条路生成（都在 `Assets/Scripts/Editor/ResultAssetGenerator.cs`）：

- **鱼图标**：把 `Resources/Prefab/Fish` 下的 3D 模型放进取景台（临时附加场景，跑完即丢），
  一盏平行光 + 一台正交相机侧拍成透明底 PNG（`ForceLOD(0)` 只留 LOD0，
  `Animator.Play(0,0,0.5)` 采样游动姿势而不是绑定姿势）。
- **UI 图**：程序化生成"白色 + alpha"的圆角矩形 / 渐变 / 柔光 / 投影 / 圆徽章，
  运行时靠 `Image.color` 上色 —— **一张图能当任意颜色用**，换配色不用重新出图。
  进度条刻意做成"底槽 + 渐变填充 + 圆角外框"三件套：填充块不做圆角（它会被 `anchorMax.x` 拉伸），
  圆角交给外框盖住。全程只用 `Image.Type.Simple`，不碰 Sliced/Filled。
- **兜底**：`FishIconLibrary` 在找不到烘焙图时，会按鱼的主题色**现场画一张剪影**，
  保证任何时候结算界面都不会出现空白方块。

---

## 六、参数速查（`Assets/Resources/config/Game Config.asset` 实际值）

| 参数 | 值 | 说明 |
| --- | --- | --- |
| `hookStartY` / `hookMiddleY` / `hookBottomY` | 15 / 2 / -3.8 | 抛钩起点 / 常规段停留 / 触底终点 |
| `maxDepth` / `finalDiveDepth` | 80 / 6 | 最大深度；剩这么多时背景停住 |
| `descendSpeed` / `ascendSpeed` / `fastReturnSpeed` | 5 / 4 / 18 | 下潜 / 上浮 / 满仓加速 |
| `hookMoveSpeed` / `hookXLimitRatio` | 7 / 0.86 | 横向跟随速度；可移动范围 = 屏幕半宽 × 该值 |
| `maxHp` / `maxCatch` / `hurtCooldown` | 100 / 10 / 0.35 | 氧气 / 渔获上限 / 受击无敌时间（也是闪烁时长） |
| `spawnInterval` / `spawnIntervalMin` | 0.1 / 0.1 | 刷鱼间隔（浅水 → 深水） |
| `spawnMarginMin` / `spawnMarginMax` | 2 / 2.5 | 生成在屏幕外多远 |
| `despawnMargin` | 1 | 完全出屏后再走这么远才回收 |
| `maxFishAlive` | 34 | 同屏自由游动的鱼上限（真正密度由泳道决定） |
| `spawnWhenStill` | true | 背景静止但已下潜时，也从左右两侧补鱼 |
| `maxOutsideLife` | 2 | 屏幕外待太久没进场就回收（防漏鱼占名额） |
| `laneGap` / `minFishGap` | 0.18 / 0.25 | 泳道判定的纵向余量 / 同泳道最小横向间距 |
| `poolWarmCount` | 10 | 每种鱼预热多少个 |

> `CastDepth = hookStartY - hookMiddleY = 13`，`FinalDiveStartDepth = max(CastDepth, maxDepth - finalDiveDepth) = 74`，
> 最大滚动量 = 61 世界单位。想改纵向手感，动这几个值即可，其余段位会自动跟着走。
> 代码里也有一套默认值，但**资产里的值优先**；菜单 1 会把默认值落成资产。

---

## 七、编辑器菜单一览

| 菜单 | 作用 | 备注 |
| --- | --- | --- |
| 1. 生成配置资源 | 把代码内默认表落成 `Game Config` / `Fish Config` 资产 | 可选 |
| 1b. 重建鱼配置表（覆盖） | 覆盖式重建鱼表 | 会丢掉手改 |
| 2. 搭建界面并接线 | 建/补 HUD 与结算界面，接好 `GameMgr` 引用，清理失效组件 | |
| 3. 给鱼预制体补 FishView | 给 9 个鱼预制体补表现组件 | |
| 4. 检查场景接线 | 打印一份接线体检报告（含教程布景与"两个 GameMgr"这类坑） | 排查首选 |
| 5. 搭建教程面板并接线 | 建 `TutorialCanvas`（取景面板 + 氧气条 + 提示 + 三个按钮）并接 `TutorialDirector/Panel` | 需要先有教程布景 |
| 6. 烘焙鱼的结算图标 | 用真实模型侧拍出 `Resources/Icon/Fish/*.png` | 不跑有剪影兜底 |
| 7. 生成结算界面用的 UI 图片 | 生成 11 张 UI 图到 `Resources/UI/` | |
| 8. 重建结算条目预制体 | 重建 `Resources/Prefab/ResultItem.prefab` | **会覆盖**该预制体 |
| 9. 美化结算面板并补接线 | 结算面板换底图/标题/副标题/表头，并补齐 `ResultPanel` 引用 | |
| 10. 统一美化所有界面 | HUD / 教程 / 结算统一配色、进度条三件套、文字投影、药丸底、软投影 | 会自动补 7 的产物 |

其中 **1b 会覆盖鱼表、8 会覆盖 `ResultItem.prefab`**；**9 / 10 会修改已有界面的配色与底图**
（不删物体、不改层级）；其余都是"只补不改"：按名字复用已有节点，不会删除或挪动美术摆好的东西，
可以反复执行。

---

## 八、注意事项

1. **中文字体**：Unity 内置 `LegacyRuntime.ttf` 不含中文字形，界面中文由 `DynamicFontBootstrap`
   在运行时用系统字体（微软雅黑等）替换。编辑器里看到方块是正常的。
   **运行时克隆出来的文字**（结算条目）赶不上那次替换，所以 `ResultItem` 会主动补一次
   `DynamicFontBootstrap.ApplyTo`。要出 APK 建议换 TextMeshPro + 预烘焙中文字体图集。
2. **教程布景必须与主游戏隔离**：`GuideCamera` **不要**打 `MainCamera` 标签
   （否则 `Camera.main` 可能返回它，视口/镜头计算全错）；`GuideMgr` 上**不要**留 `GameMgr` 组件
   （两个 GameMgr 会抢单例，谁先 `Awake` 谁是真身）。菜单 4/5 会把这两条打红字提示。
3. **`playTutorial`**：`GameMgr` 上的开关。取消勾选就直接进 `Ready`（调试时很方便）。
4. **资源缺失会自动降级**：没跑菜单 6 → 鱼图标用程序化剪影；没跑菜单 7 → 面板/按钮退化成纯色块；
   菜单 8 重建预制体时若找不到 UI 图只会告警，不会失败。
5. **鱼的尺寸差异**：鱼表里的 `scale` 字段目前只做展示参考，没有参与缩放；
   泳道防重叠是按**实测碰撞体尺寸**判定的，所以换模型后不需要改代码。
6. **性能**：鱼是蒙皮模型 + LODGroup，同屏 30+ 条在桌面端没问题；上限由 `maxFishAlive` 兜底。
7. **分辨率**：按 1920×1080 横屏设计，UI 用 `CanvasScaler.MatchWidthOrHeight = 0.5` 自适应。
8. **从旧版本升级**：控制器早期是分别挂在 BG / hook / fishs 上的 MonoBehaviour，现在都是纯 C# 类；
   菜单 2 会自动清理场景里失效的旧组件（Missing Script）。

---

## 九、可以继续做

- 音效与粒子（抓住 / 受击 / 入水 / 触底）
- 触底瞬间的顿帧与震屏，强化冲刺段表现
- 稀有鱼种 / 连抓 Combo / 单局限时 / 排行榜
- 教程里的"手型指针"动效，让"按住左键"这一步更直观
- 手机端竖屏适配；`TextMeshPro + 中文字体图集` 替换 legacy Text
- 把结算的渔获清单接上真实鱼图标之外的稀有度边框、图鉴页
