using System.Collections.Generic;
using System.Linq;
using Config;
using Core;
using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 鱼群管理（控制层）：**只在纯数据上模拟**——生成位置、游动、出屏回收全部算在
    /// <see cref="FishRuntime"/> 里，控制层从头到尾不持有任何 View。
    ///
    /// 表现层 FishView 自己被启用时来 <c>Register</c> 领一个 id，每帧用 <c>Get(id)</c> 拉状态；
    /// 被回收时由控制层标记 <c>Alive = false</c>，表现层看到后自己回对象池。
    /// 依赖方向始终是 View → Controller。
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
            return slot != null && slot.Alive ? slot : null;
        }

        /// <summary>当前存活的数据槽（可能含 null），供钩子做碰撞判定。</summary>
        public IReadOnlyList<FishRuntime> Actives => _slots;

        /// <summary>把钩子上那些鱼的数据槽标死（重开一局时用）。</summary>
        public void RecycleCaught(IReadOnlyList<FishRuntime> caught)
        {
            if (caught == null) return;

            foreach (FishRuntime fish in caught)
            {
                if (fish != null) fish.Alive = false;
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

            bool diving = state is GameState.CastingDown or GameState.ReelingUp;
            int alive = CountAlive();
            bool wantSpawn = state == GameState.Ready
                ? alive < Mathf.Max(3, _cfg.maxFishAlive / 2)
                : diving && alive < _cfg.maxFishAlive;

            if (wantSpawn)
            {
                _timer -= dt;
                if (_timer <= 0f)
                {
                    Spawn();
                    _timer = CurrentInterval(depth);
                }
            }

            MoveAll(dt);
        }

        private void MoveAll(float dt)
        {
            foreach (FishRuntime fish in _slots)
            {
                if (fish == null || !fish.Alive || fish.IsCaught || fish.Data == null) continue;

                fish.Position += Vector3.right * (fish.Direction * fish.Data.moveSpeed * dt);
            }
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

                // 1) 进过画面没有？鱼一律在屏幕外生成，没进过画面的绝不能回收
                if (!fish.HasEnteredView)
                {
                    if (b.max.x > left && b.min.x < right && b.max.y > bottom && b.min.y < top)
                        fish.HasEnteredView = true;

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
        /// 生成一条鱼：位置一律在屏幕外，方向保证它一定会穿过画面。
        ///   背景上移（下潜）→ 从屏幕下方进场
        ///   背景下移（上浮）→ 从屏幕上方进场
        ///   背景静止       → 从屏幕左右两侧进场，横向穿过
        /// </summary>
        private void Spawn()
        {
            FishData data = _spawner.Config.PickByDepth(Random.value);
            if (data == null) return;

            float halfWidth = ViewportUtil.HalfWidth(_cam);
            float halfHeight = ViewportUtil.HalfHeight(_cam);
            float camY = _cam != null ? _cam.transform.position.y : 0f;
            float margin = Random.Range(_cfg.spawnMarginMin, _cfg.spawnMarginMax);

            Vector3 position;
            int direction;

            switch (ScrollDirHint)
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

            int id = AllocateSlot(data, position, direction);
            if (id < 0) return;

            // 先登记"待认领"，再让对象池激活 GameObject——
            // FishView.OnEnable 会在 SetActive(true) 里同步触发，必须提前备好槽位。
            _pendingIds.Enqueue(id);
            if (!_spawner.Rent(data.prefabName)) _pendingIds.Dequeue();
        }

        /// <summary>当前背景滚动方向，由 GameMgr 每帧写入。</summary>
        public int ScrollDirHint { get; set; }

        // ------------------------------------------------------------------

        private int AllocateSlot(FishData data, Vector3 position, int direction)
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
            slot.Alive = true;
            _slots[id] = slot;

            if (_planeZ == 0f && position.z != 0f) _planeZ = position.z;
            return id;
        }

        private int CountAlive() => _slots.Count(fish => fish is { Alive: true });

        private float CurrentInterval(float depth)
        {
            float t = Mathf.Clamp01(depth / _cfg.maxDepth);
            return Mathf.Lerp(_cfg.spawnInterval, _cfg.spawnIntervalMin, t);
        }
    }
}