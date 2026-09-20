using System.Collections.Generic;
using System.Linq;
using Config;
using Core;
using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【控制层】鱼群管理。**只在纯数据上模拟**：生成位置、横向游动、出屏回收全部算在
    /// <see cref="FishRuntime"/> 里，控制层从头到尾不持有任何 View。
    ///
    /// 依赖方向 View → Controller：表现对象出生时来 `Register` 领一个 id，之后每帧用 `Get(id)` 拉状态；
    /// 控制层要回收它就把槽位标成 `Alive = false`，表现层看到后自己回对象池。
    ///
    /// 数据结构（都为了 O(1)）：
    ///   `_slots`      槽位表，下标就是 id，按 id 取鱼不用查找
    ///   `_freeIds`    空闲 id 队列，回收的 id 立刻能被下一条鱼复用
    ///   `_pendingIds` 待认领队列，控制层先备好数据，表现对象出生时按顺序来领
    /// </summary>
    public class FishController
    {
        private readonly FishSpawner _spawner;
        private readonly GameConfig _cfg;
        private readonly Camera _cam;

        private readonly List<FishRuntime> _slots = new(); // 数据槽，null 表示空闲
        private readonly Queue<int> _freeIds = new();
        private readonly Queue<int> _pendingIds = new(); // 已备好数据、等表现对象来认领

        private float _planeZ;
        private float _timer;

        public FishController(Transform fishRoot, Camera cam)
        {
            _cfg = GameConfig.Get();
            _cam = cam;
            _spawner = new FishSpawner(fishRoot);
        }

        // ==================================================================
        // 表现层调用的接口（View → Controller）
        // ==================================================================

        /// <summary>表现对象被启用时认领一个数据槽。</summary>
        /// <returns>槽位 id；没有待投放的鱼时返回 -1。</returns>
        public int Register(Vector3 position, Vector3 size)
        {
            if (_pendingIds.Count == 0) return -1;

            int id = _pendingIds.Dequeue();
            FishRuntime slot = _slots[id];
            if (slot == null) return -1;

            slot.Size = size;
            return id;
        }

        /// <summary>表现对象被禁用时归还槽位。</summary>
        public void Unregister(int id)
        {
            if (id < 0 || id >= _slots.Count || _slots[id] == null) return;

            _slots[id] = null;
            _freeIds.Enqueue(id);
        }

        /// <summary>按 id 拉当前状态；返回 null 表示这条鱼已经被回收了。</summary>
        public FishRuntime Get(int id)
        {
            if (id < 0 || id >= _slots.Count) return null;

            FishRuntime slot = _slots[id];
            return slot is { Alive: true } ? slot : null;
        }

        /// <summary>把钩子上那些鱼的数据槽标死（重开一局时用）。</summary>
        public void RecycleCaught(IReadOnlyList<FishRuntime> caught)
        {
            foreach (FishRuntime fish in caught)
            {
                fish.Alive = false;
            }
        }

        /// <summary>
        /// 表现层发现数据槽已失效后，把 GameObject 交回来回池。
        /// 控制层只在这里"过一下手"，不持有它。
        /// </summary>
        public void Release(int id, GameObject go)
        {
            if (id >= 0 && id < _slots.Count && _slots[id] != null)
            {
                _spawner.Recycle(go, _slots[id].Data.prefabName);
            }
            else if (go != null)
            {
                _spawner.Recycle(go, null);
            }
        }

        /// <summary>钩子上的渔获被清空时，把对应数据槽也标死（重开一局用）。</summary>
        public void RecycleAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].Alive = false;
                _slots[i] = null;
                _freeIds.Enqueue(i);
            }

            _pendingIds.Clear();
            _timer = 0f;
        }

        // ==================================================================
        // 每帧模拟（纯数据）
        // ==================================================================

        /// <summary>每帧驱动：先挑出要回收的，再看情况补新鱼，最后让鱼游动。</summary>
        /// <param name="depth">当前下潜深度，用于挑选该深度才会出现的鱼种。</param>
        /// <param name="scrollDir">背景滚动方向：+1 世界上移（下潜），-1 世界下移（上浮），0 静止。</param>
        public void Tick(float dt, GameState state, float depth, int scrollDir)
        {
            RecycleOffScreen();

            // 补鱼条件：① 在玩（下潜 / 上浮）；② 世界在动（滚动，或已下潜但背景停住的触底冲刺段），
            // 保证鱼一律从**屏幕外**进场，不会凭空出现在画面中间；抛钩段和收钩段不补，
            // 否则鱼会出现在开始画面上；③ 同屏自由游动的鱼没到上限
            // （挂在钩上的不占名额，否则每抓到一条就永久少一个名额，上浮会越来越冷清）
            bool diving = state is GameState.CastingDown or GameState.ReelingUp;
            bool canEnter = scrollDir != 0 || (_cfg.spawnWhenStill && depth > _cfg.CastDepth);
            bool wantSpawn = diving && canEnter && CountSwimming() < _cfg.maxFishAlive;

            if (wantSpawn)
            {
                _timer -= dt;
                if (_timer <= 0f)
                {
                    Spawn(depth, scrollDir);
                    _timer = CurrentInterval(depth);
                }
            }

            MoveAll(dt, depth);
        }

        /// <summary>
        /// 每帧推进所有自由的鱼。纵向位移由背景滚动带着走（鱼是 BG 的子物体），这里只算横向；
        /// 但横向**必须排队**：鱼速差别很大，快的会追尾慢的，一穿插画面上就是两条鱼叠在一起，
        /// 所以追上前面的鱼时这一步只走到"贴着它"的位置，等它让开再继续。
        /// </summary>
        private void MoveAll(float dt, float depth)
        {
            float scroll = _cfg.WorldScrollAt(depth);

            for (int i = 0; i < _slots.Count; i++)
            {
                FishRuntime fish = _slots[i];
                if (fish == null || !fish.Alive || fish.IsCaught || fish.Data == null) continue;

                fish.Life += dt;

                float step = Mathf.Abs(fish.Data.moveSpeed) * dt;
                step = Mathf.Min(step, AllowedStep(fish));

                fish.Position = new Vector3(
                    fish.Position.x + fish.Direction * step,
                    fish.BaseY + scroll,
                    fish.Position.z);
            }
        }

        /// <summary>
        /// 这一步最多能横向走多远：看同一条"泳道"里挡在前面的那条鱼。
        /// 只有纵向会撞上（两条鱼半高之和 + laneGap 之内）的才算同泳道；
        /// 不同泳道的鱼各走各的，谁也不挡谁。
        /// </summary>
        private float AllowedStep(FishRuntime fish)
        {
            float limit = float.MaxValue;

            for (int i = 0; i < _slots.Count; i++)
            {
                FishRuntime other = _slots[i];
                if (other == null || other == fish || !other.Alive || other.IsCaught || other.Data == null)
                {
                    continue;
                }

                float lane = (fish.Size.y + other.Size.y) * 0.5f + _cfg.laneGap;
                if (Mathf.Abs(other.Position.y - fish.Position.y) > lane) continue;

                float ahead = (other.Position.x - fish.Position.x) * fish.Direction;
                if (ahead <= 0f) continue; // 在身后，不用管

                float free = ahead - (fish.Size.x + other.Size.x) * 0.5f - _cfg.minFishGap;
                if (free < limit) limit = free;
            }

            return Mathf.Max(0f, limit);
        }

        private void RecycleOffScreen()
        {
            if (_cam == null) return;

            float left = -ViewportUtil.HalfWidth(_cam);
            float right = -left;
            float bottom = ViewportUtil.ViewBottomWorldY(_cam);
            float top = ViewportUtil.ViewTopWorldY(_cam);
            float margin = _cfg.despawnMargin;

            foreach (var fish in _slots)
            {
                if (fish == null || !fish.Alive || fish.IsCaught) continue;

                Bounds b = fish.WorldBounds;

                // 1) 进过画面没有？鱼一律在屏幕外生成，没进过画面的绝不能按"离开屏幕"回收
                if (!fish.HasEnteredView)
                {
                    if (b.max.x > left && b.min.x < right && b.max.y > bottom && b.min.y < top)
                    {
                        fish.HasEnteredView = true;
                        continue;
                    }

                    // 还没进过画面：它本该在屏幕外等着进场，但两种情况它永远进不来了 ——
                    //   ① 横向游出了可视范围（鱼不会掉头，出去就回不来）；
                    //   ② 在屏幕外等太久（兜底）。
                    // 不回收的话它们会一直 Alive 把名额吃光，表现就是"越到后面鱼越少"。
                    bool lostHorizontally = b.max.x < left - margin || b.min.x > right + margin;
                    if (lostHorizontally || fish.Life > _cfg.maxOutsideLife)
                    {
                        fish.Alive = false; // 表现层下一帧会发现并自己回池
                    }

                    continue;
                }

                // 2) 完全离开屏幕、并且又往外走了一段
                bool farOutside = b.max.x < left - margin || b.min.x > right + margin
                                                          || b.max.y < bottom - margin || b.min.y > top + margin;

                if (!farOutside) continue;

                fish.Alive = false; // 表现层下一帧会发现并自己回池
            }
        }

        /// <summary>
        /// 生成一条鱼。位置一律在屏幕外，方向保证它一定会穿过画面：
        ///   背景上移（下潜）→ 从下方进场；背景下移（上浮）→ 从上方进场；背景静止 → 从左右两侧横向穿过。
        ///
        /// 生成前先做一次"泳道查重"：和已有的鱼纵向挨得太近这次就不生成。
        /// 这是防重叠的第一道关 —— 间隔调得再小，实际密度也会被"一屏能排下几条"限制住。
        /// </summary>
        private void Spawn(float depth, int scrollDir)
        {
            FishData data = _spawner.Config.PickByDepth(Random.value);
            if (data == null) return;

            float halfWidth = ViewportUtil.HalfWidth(_cam);
            float halfHeight = ViewportUtil.HalfHeight(_cam);
            float camY = _cam != null ? _cam.transform.position.y : 0f;
            float margin = Random.Range(_cfg.spawnMarginMin, _cfg.spawnMarginMax);

            Vector3 position;
            int direction;

            switch (scrollDir)
            {
                case > 0:
                    position = new Vector3(Random.Range(-halfWidth, halfWidth), camY - halfHeight - margin, _planeZ);
                    direction = Random.value < 0.5f ? -1 : 1;
                    break;
                case < 0:
                    position = new Vector3(Random.Range(-halfWidth, halfWidth), camY + halfHeight + margin, _planeZ);
                    direction = Random.value < 0.5f ? -1 : 1;
                    break;
                default:
                {
                    bool fromLeft = Random.value < 0.5f;
                    position = new Vector3(fromLeft ? -halfWidth - margin : halfWidth + margin,
                        camY + Random.Range(-halfHeight, halfHeight), _planeZ);
                    direction = fromLeft ? 1 : -1;
                    break;
                }
            }

            if (LaneOccupied(position.y)) return;

            int id = AllocateSlot(data, position, direction, depth);
            if (id < 0) return;

            // 先登记"待认领"，再让对象池激活 GameObject——
            // FishView.OnEnable 会在 SetActive(true) 里同步触发，必须提前备好槽位。
            _pendingIds.Enqueue(id);
            if (!_spawner.Rent(data.prefabName, position)) _pendingIds.Dequeue();
        }

        /// <summary>
        /// 这个高度上已经挤着鱼了吗？纵向间距不够就返回 true（这次不生成）。
        /// 新鱼的尺寸要等表现层注册才知道，所以按一个保守的最小高度估算。
        /// </summary>
        private bool LaneOccupied(float y)
        {
            const float newFishHalfHeight = 0.30f;

            for (int i = 0; i < _slots.Count; i++)
            {
                FishRuntime fish = _slots[i];
                if (fish == null || !fish.Alive || fish.IsCaught || fish.Data == null) continue;

                float need = newFishHalfHeight + Mathf.Max(fish.Size.y, 0.5f) * 0.5f + _cfg.laneGap;
                if (Mathf.Abs(fish.Position.y - y) < need) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------

        private int AllocateSlot(FishData data, Vector3 position, int direction, float depth)
        {
            int id;
            if (_freeIds.Count > 0)
            {
                id = _freeIds.Dequeue();
            }
            else
            {
                id = _slots.Count;
                _slots.Add(null);
            }

            FishRuntime slot = _slots[id] ?? new FishRuntime();
            slot.Reset(data, position, Vector3.one * 0.5f, direction);
            slot.BaseY = position.y - _cfg.WorldScrollAt(depth);
            slot.Alive = true;
            _slots[id] = slot;

            if (_planeZ == 0f && position.z != 0f) _planeZ = position.z;
            return id;
        }

        /// <summary>
        /// 同屏"自由游动"的鱼有几条。**故意不把挂在钩上的算进来**：
        /// 钩上的鱼已经不在场上，却还占着名额的话，每抓到一条就少一个刷鱼名额，
        /// 上浮段自然越抓越冷清。
        /// </summary>
        private int CountSwimming() => _slots.Count(fish => fish is { Alive: true, IsCaught: false });

        private float CurrentInterval(float depth)
        {
            float t = Mathf.Clamp01(depth / _cfg.maxDepth);
            return Mathf.Lerp(_cfg.spawnInterval, _cfg.spawnIntervalMin, t);
        }
    }
}