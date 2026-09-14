# 捕鱼小游戏 Demo

Unity **2022.3.52f1** + URP + **DOTween**，`Assets/Scenes/GameScene.unity` 即主场景。

---

## 一、玩法

1. **准备**：鱼钩悬在水面（屏幕偏上），鱼群在背景里单向游过。
2. **抛钩**：长按鼠标（或触摸）开始，鱼钩甩向屏幕中间；这一段背景不动，只有鱼钩在走。
3. **下潜**：鱼钩停在屏幕中间，**背景负责滚动**，鱼群从屏幕下方进场。碰到鱼会扣血，血量归零即失败。
4. **控钩**：全程按住鼠标左右拖动，鱼钩以有限速度跟随指针横向移动；松手则停在原地。
5. **触底冲刺**：还剩约 3/5 个背景贴图高度时**背景停住**，鱼钩继续往屏幕底部探——
   鱼钩刚好探到底的那一刻，深度也刚好到最大值，转为上浮。
6. **收线起钩**：上浮时**背景先不动**，鱼钩先 DOTween 回到屏幕中间，然后背景才开始往下滚。
   这一段碰到鱼就会被抓住、挂在钩下跟着走。
7. **满仓**：渔获达到上限后立刻**无法再操作**，鱼钩加速收线回到起点。
8. **结算**：展示总分与渔获清单；失败则展示已抓到的鱼。点"再来一局"或按 `R` 重开。

> 越深越危险也越值钱：浅水是低伤小鱼，16m 以下开始出鳄鱼（45 伤害，两下就没了）。

---

## 二、快速开始

```
1. 用 Unity 2022.3.52f1 打开工程
2. 菜单：工具 / 捕鱼 / 1. 生成配置资源          ← 可选，缺了会走代码内默认表
3. 菜单：工具 / 捕鱼 / 2. 搭建界面并接线
4. 菜单：工具 / 捕鱼 / 3. 给鱼预制体补 FishView 并烘焙朝向
5. 打开 Assets/Scenes/GameScene.unity，点 Play
```

三步菜单都是**只补不改**的：已存在的物体不会被删除或挪动，可以反复执行，不会产生重复 UI。
第 4 步烘焙朝向的依据是场景 `BG/fishs` 下美术手工摆好的那 9 条鱼的旋转（当前全部是 `Y = -90`）。

> DOTween 放在 `Assets/Plugins/Demigiant`，`DOTweenSettings.asset` 已生成，开箱可用。

---

## 三、核心设计：一趟下潜由"鱼钩动"和"背景动"拼成

需求原文：「下潜是背景在移动，鱼就和背景一起移动也就是和背景的速度一样，鱼钩也是要移动的」。

所以纵向分成了**由深度直接推导的三段**，不需要额外维护状态机：

| 段 | 深度区间 | 背景 | 鱼钩（DOTween） | 缓动 |
| --- | --- | --- | --- | --- |
| ① 抛钩 Casting | 0 → `CastDepth` | 不动 | `hookStartY` → `hookMiddleY` | OutQuad |
| ② 常规下潜 Cruising | `CastDepth` → `FinalDiveStartDepth` | **滚动** | 停在 `hookMiddleY` | — |
| ③ 触底冲刺 FinalDive | `FinalDiveStartDepth` → `maxDepth` | **停住** | `hookMiddleY` → `hookBottomY` | InQuad |
| ④ 收线起钩 PullUp | 同上，反向 | **停住** | `hookBottomY` → `hookMiddleY` | OutQuad |
| ⑤ 常规上浮 Cruising | 同上，反向 | **往回滚** | 停在 `hookMiddleY` | — |
| ⑥ 收钩出水 ReelingOut | `CastDepth` → 0 | 不动 | `hookMiddleY` → `hookStartY` | InQuad |

关键点：

- **"世界滚动量"和"鱼钩高度"都是深度的纯函数**（`GameConfig.WorldScrollAt` + `HookController.ResolvePhase`），
  所以上浮方向天然对称，不需要写第二套逻辑。
- **补间时长 = 该段剩余深度 ÷ 当前速度**，因此鱼钩探到屏幕底部的那一刻，深度也刚好到最大值；
  收钩出水同理。不会出现"钩子到底了深度还差一截"的割裂感。
- **只在跨段的那一帧触发一次 DOTween**，不会每帧打断补间。
- 触底冲刺和收线起钩这两段背景是静止的，鱼在屏幕上被"冻住"，只有鱼钩在往下压——
  这正是营造"要潜到最深了"压迫感的来源。

### 鱼群：单向游动 + 世界坐标

```csharp
// 出生时定好方向，一路游到底，不回头、不反弹
t.position += Vector3.right * (fish.Direction * fish.Data.moveSpeed * dt);
```

- 位置一律用**世界坐标**（`transform.position`），父节点 BG 的缩放完全不用管，
  省掉了一整套"世界 → 父节点局部"的坐标换算。
- **完全移出屏幕**（上下左右任意一边）才回收，用渲染器包围盒判定。
- 生成位置：X 在屏幕宽度内完全随机，Y 落在屏幕外、离屏距离也在配置区间内随机，
  避免一屏鱼排成一条整齐的横线进场。
- **纵向一行代码都不用写**：鱼是 BG 的子物体，下潜/上浮时世界整体滚动，
  鱼自动与背景同速，永远不可能不同步。
  上浮时世界原路退回，同一批没被抓住的鱼会从上往下再经过一次，
  正好形成"下去躲鱼、上来抓鱼"的节奏。

### 调参对照表（GameConfig）

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `hookStartY` | 3.6 | 抛钩起点（相机正交半高为 5） |
| `hookMiddleY` | 0 | 常规段鱼钩停留的屏幕中间 |
| `hookBottomY` | -3.8 | 触底冲刺终点，接近屏幕底部 |
| `maxDepth` | 26 | 最大深度 |
| `finalDiveDepth` | 6 | 剩这么多深度时背景停住（≈ 3/5 个背景贴图高） |
| `descendSpeed` / `ascendSpeed` | 3.5 / 3.0 | 下潜 / 上浮速度，也是背景滚动速度 |
| `fastReturnSpeed` | 16 | 满仓后的加速返回 |
| `hookXLimitRatio` | 0.86 | 鱼钩左右可移动范围 = 屏幕半宽 × 该值 |
| `spawnMarginMin` / `spawnMarginMax` | 1.5 / 4 | 新鱼生成在屏幕外多远（区间内随机） |

`finalDiveDepth` 想精确对齐背景贴图高度的话，运行一次看 Console 里
`[BGController] 背景贴图高度 x.xx 世界单位` 即可。

---

## 四、架构：MonoBehaviour 只剩"入口 + 表现"

MVC + 观察者 + 状态机 + 对象池 + 数据驱动。
**场景里唯一的逻辑 MonoBehaviour 是 `GameMgr`，其余全部是纯 C# 类。**

| 类型 | 脚本 | 说明 |
| --- | --- | --- |
| MonoBehaviour（逻辑入口） | `GameMgr` | 状态机 + 驱动控制器 + 数据广播 |
| MonoBehaviour（表现） | `HookView`、`FishView` | 挂在 hook 和每条鱼上，只负责显示 |
| MonoBehaviour（表现） | `HUDView`、`ResultPanel`、`DynamicFontBootstrap` | 挂在 Canvas 上，只订阅事件 |
| 纯 C# 类 | `BGController`、`HookController`、`FishController`、`FishSpawner` | 游戏逻辑，构造函数注入依赖 |
| 纯 C# 类 | `GameModel`、`Fish`、`Hook`、`InputController` | 数据与实体 |
| 纯 C# 类 | `EventMgr`、`PoolMgr`、`ResourcesMgr`、`ViewportUtil`、`MathUtil`、`MonoSingleton` | 工具 |

控制器由 `GameMgr.Awake` 直接 `new` 出来并每帧驱动：

```csharp
_bgCtrl   = new BGController(_worldRoot, bgTiles, cam);
_hookCtrl = new HookController(_hookView, cam);
_fishCtrl = new FishController(fishRoot, cam);
```

GameMgr 在 Inspector 里只要两个引用：`_worldRoot`（BG）和 `_hookView`（hook 上的 HookView），
留空会自动按名字查找；`bgs` 和 `fishs` 都从 `_worldRoot` 下面找。

```
Assets/Scripts
├── Config/       GameConfig / FishConfig / DefaultGameData    数据层
├── Core/         GameMgr(MonoBehaviour) / GameState / GameEvent / EventMgr
├── Model/        GameModel                                    纯数据，不依赖 Unity
├── Entities/     Fish / Hook                                  运行时实体
├── Controller/   BGController / HookController / FishController / FishSpawner / InputController
├── View/         HookView / FishView / HUDView / ResultPanel / DynamicFontBootstrap
├── Tool/         MonoSingleton / PoolMgr / ResourcesMgr / ViewportUtil / MathUtil
└── Editor/       FishAssetGenerator / GameSceneBuilder        一键生成配置 + 搭建场景
```

### GameMgr 只做三件事

```csharp
private void Update()
{
    float dt = Time.deltaTime;

    _input.Tick();

    bool canControl = _state == GameState.CastingDown || _state == GameState.ReelingUp;
    _hookCtrl.TickHorizontal(dt, canControl, _input);   // ① 驱动控制器
    TickState(dt);                                      // ② 驱动状态机
    _fishCtrl.Tick(dt, _state, _model.Depth, ScrollDir);
    _bgCtrl.Tick();                                     // 背景贴图循环，放最后

    BroadcastIfChanged();                               // ③ 把 Model 变化广播给 View
}
```

没有 `Find`、没有 `AddComponent`、没有 `Init` 转发；碰撞判定这类"鱼钩碰到鱼"的逻辑归 `HookController`。
`BroadcastIfChanged` 是脏检查——Hp / 分数 / 渔获 / 深度只有真变了才发事件，省掉一堆手动 `Publish` 调用。

### 数据流

```
InputController ──► GameMgr（状态机）
                      ├─► BGController.SetScroll(深度换算)     背景滚动
                      ├─► HookController.ApplyDepth(...)       鱼钩 DOTween 分段
                      ├─► HookController.CheckHurt / CheckCatch 碰撞 -> 改 Model
                      └─► FishController.Tick(...)             鱼群
                              │
                       GameMgr.BroadcastIfChanged（脏检查）
                              ▼
                 HUDView / ResultPanel（只订阅，不反向调用）
```

**约束**：View 不持有 Controller 引用，Controller 不碰 UI，Model 不引用 `MonoBehaviour`。

### 设计模式落点

| 模式 | 位置 |
| --- | --- |
| 单例 | `MonoSingleton<T>` / `GameMgr` |
| 状态机 | `GameState` + `GameMgr.TickState` |
| 观察者 | `EventMgr`：Model/Controller 广播，View 订阅 |
| 对象池 | `PoolMgr`，每种鱼一个池，由 `FishSpawner` 懒创建 |
| 数据驱动 | `GameConfig` / `FishConfig` 两张 ScriptableObject 表 |
| 脏检查 | `GameMgr.BroadcastIfChanged` 只在数值变化时发事件 |
| 工厂 | `FishSpawner`：只管造鱼/收鱼，不管鱼怎么动 |

---

## 五、几个值得说明的实现细节

**1. 为什么补间的是 float 而不是 `DOMoveY`**

`DOMoveY` 会把整条 `position` 都锁成补间创建时的值，而 X 是我们每帧手改的，两者会打架。
所以 `HookController` 补间的是一个私有 float `_visualY`，再统一写进 Transform：

```csharp
_yTween = DOTween.To(() => _visualY, v => _visualY = v, targetY, duration).SetEase(ease);
```

**2. 竿尖锚点必须在移动鱼钩之前量**

鱼线是鱼钩的子物体，而竿尖是固定的世界坐标，所以锚点要在"鱼钩还在美术摆的位置上"时量。
Unity 不保证组件间 `Awake` 的顺序，因此 `HookView.Prepare()` 做成幂等的显式初始化，
由 `HookController` 的构造函数在移动鱼钩前主动调用。

**3. 鱼线底端对齐的是"钩子本体"而不是钩子根节点**

根节点和钩子本体之间还有 -0.672 的偏移，直接用根节点会让鱼线和钩子之间露出一截空隙。

**4. 碰撞不用 Unity 物理**

"钩子圆形 vs 鱼的轴对齐包围盒"做解析判定（`MathUtil.CircleIntersectsBounds`）：

- 包围盒来自 `Renderer.bounds` 实时聚合，模型缩放、蒙皮动画都不用管，配置里的 `radius` 留 0 即可自动。
- 长条形的鱼（鳄鱼、天鹅）用 AABB 比圆-圆准得多。
- 同一条鱼在一次下潜里**只扣一次血**（`Fish.HasHitHook`），再加 0.35 秒全局受击冷却，
  避免贴着大鱼被瞬间清空血量。

**5. 鱼的位置一律用世界坐标**

`BG` 根节点带缩放 `(1.17, 1.26, 1)`，`fishs` 又是它的子物体。但因为我们**直接写 `transform.position`**，
父节点的缩放完全不用管：

```csharp
t.position = worldPosition;                                      // 生成
t.position += Vector3.right * (dir * speed * dt);                // 单向游动
Bounds b = fish.WorldBounds;                                     // 出屏判定
```

只有两处还需要坐标换算（`ViewportUtil`）：背景贴图的循环铺满、以及鱼钩的左右边界。

**6. 被抓的鱼怎么从鱼群里摘出去**

`HookController` 只给鱼打 `IsCaught` 标记，`FishController` 每帧自己 `PruneCaught()` 摘掉——
两边不需要互相持有引用。被打上标记的鱼会一直挂在钩子下，直到重开一局才还回对象池。

---

## 六、可以继续做

- 音效与粒子（抓住 / 受击 / 入水 / 触底）
- 触底瞬间的顿帧与震屏，强化冲刺段的表现
- 稀有鱼种 / 连抓 Combo / 单局限时
- 手机端竖屏适配（当前按 1920×1080 横屏设计）
- 用 TextMeshPro + 预烘焙中文字体图集替换 legacy Text（打 APK 更稳，见下）

---

## 七、注意事项

1. **中文字体**：Unity 内置 `LegacyRuntime.ttf` 不含中文字形，界面上的中文由
   `DynamicFontBootstrap` 在运行时用系统字体（微软雅黑等）替换。
   编辑器里看到方块是正常的，运行起来就正常了。
   要出 **APK** 建议换成 TextMeshPro + 预先烘焙好的中文字体图集，避免依赖系统字体。
2. **鱼头朝向**：鱼预制体的"朝右"姿态由 `FishView.faceRightEuler` 决定（默认 `Y = -90`，
   取自场景里手工摆好的鱼）。如果发现鱼游动方向与身体朝向相反，勾上 `invertFacing` 即可。
3. **竿尖高度**：`HookView.rodTipWorldYOverride` 默认自动读取美术摆好的鱼线顶端（约 6.58）。
   如果鱼线看起来太长/太短，在这里手填一个世界 Y 即可。
4. **手感调整入口**：鱼钩纵向起止高度在 `GameConfig` 的 `hookStartY / hookMiddleY / hookBottomY`，
   背景停住的时刻在 `finalDiveDepth`。
5. **从旧版本升级**：控制器曾经是 MonoBehaviour 分别挂在 BG / hook / fishs 上，
   现在都变成纯 C# 类了。菜单 2 会自动清理场景里失效的旧组件（Missing Script），跑一次即可。
