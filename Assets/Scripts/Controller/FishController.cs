using System.Collections.Generic;
using Config;
using Core;
using Entities;
using Tool;
using UnityEngine;

namespace Controller
{
    public class FishController
    {
        private readonly FishSpawner _spawner;
        private readonly GameConfig _cfg;
        private readonly Camera _cam;
        private readonly List<Fish> _actives = new();

        private readonly float _planeZ;
        private float _timer;

        public FishController(Transform fishRoot, Camera cam)
        {
            _cfg = GameConfig.Get();
            _cam = cam;
            _spawner = new FishSpawner(fishRoot);
            _planeZ = _actives.Count > 0 && _actives[0].View != null ? _actives[0].View.transform.position.z : 0f;
        }

        /// <summary>当前存活的鱼（不含已抓住的）。</summary>
        public IReadOnlyList<Fish> Actives => _actives;

        /// <summary>每帧驱动：先摘掉抓走的和出屏的，再看情况补新鱼，最后让鱼游动。</summary>
        /// <param name="depth">当前下潜深度，用于挑选该深度才会出现的鱼种。</param>
        /// <param name="scrollDir">背景滚动方向：+1 世界上移（下潜），-1 世界下移（上浮），0 静止。</param>
        public void Tick(float dt, GameState state, float depth, int scrollDir)
        {
            PruneCaught();
            RecycleOffScreen();

            bool ready = state == GameState.Ready;
            bool diving = state == GameState.CastingDown || state == GameState.ReelingUp;

            // 鱼一律在**屏幕外**生成，所以背景静止的两段（抛钩 / 触底冲刺）照样补鱼——
            // 它们会从屏幕左右两侧横向穿过画面，不会凭空出现在屏幕中间。
            bool wantSpawn = ready
                ? _actives.Count < Mathf.Max(3, _cfg.maxFishAlive / 2)
                : diving && _actives.Count < _cfg.maxFishAlive;

            if (wantSpawn)
            {
                _timer -= dt;
                if (_timer <= 0f)
                {
                    Spawn(depth, scrollDir);
                    _timer = CurrentInterval(depth);
                }
            }

            MoveAll(dt);
        }

        public void RecycleAll()
        {
            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                _spawner.Recycle(_actives[i]);
            }

            _actives.Clear();
            _timer = 0f;
        }

        public void RecycleCaught(IReadOnlyList<Fish> caught)
        {
            if (caught == null)
            {
                return;
            }

            for (int i = caught.Count - 1; i >= 0; i--)
            {
                _spawner.Recycle(caught[i]);
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 把已抓住的鱼从存活列表里摘出去（它们已经挂到鱼钩下面，位置不归这里管）。
        /// 由鱼钩打上 IsCaught 标记，这里被动响应，两边不需要互相持有引用。
        /// </summary>
        private void PruneCaught()
        {
            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                Fish fish = _actives[i];
                if (fish == null || fish.IsCaught)
                {
                    _actives.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 回收判定。
        ///
        /// 鱼一律在屏幕外生成，所以"在屏幕外"本身不能作为回收条件，否则刚生成就被收掉。
        /// 这里分两步：先等它真正进过一次画面，再等它完全离开屏幕、并且多走出一段
        /// despawnMargin 的距离，才允许回收。
        /// </summary>
        private void RecycleOffScreen()
        {
            if (_cam == null)
            {
                return;
            }

            float left = -ViewportUtil.HalfWidth(_cam);
            float right = -left;
            float bottom = ViewportUtil.ViewBottomWorldY(_cam);
            float top = ViewportUtil.ViewTopWorldY(_cam);
            float margin = _cfg.despawnMargin;

            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                Fish fish = _actives[i];
                if (fish == null || fish.View == null)
                {
                    _actives.RemoveAt(i);
                    continue;
                }

                if (fish.IsCaught)
                {
                    continue;
                }

                Bounds b = fish.WorldBounds;

                // 1) 进过画面没有？
                if (!fish.HasEnteredView)
                {
                    bool insideView = b.max.x > left && b.min.x < right && b.max.y > bottom && b.min.y < top;
                    if (insideView)
                    {
                        fish.HasEnteredView = true;
                    }

                    continue; // 还没进过画面，绝不回收
                }

                // 2) 是否已经完全离开屏幕、并且又往外走了一段
                bool farOutside = b.max.x < left - margin || b.min.x > right + margin
                                  || b.max.y < bottom - margin || b.min.y > top + margin;

                if (!farOutside)
                {
                    continue;
                }

                _actives.RemoveAt(i);
                _spawner.Recycle(fish);
            }
        }

        /// <summary>单向横向游动：方向在出生时定好，一路游到底，不回头。</summary>
        private void MoveAll(float dt)
        {
            for (int i = 0; i < _actives.Count; i++)
            {
                Fish fish = _actives[i];
                if (fish == null || fish.IsCaught || fish.View == null || fish.Data == null)
                {
                    continue;
                }

                Transform t = fish.View.transform;
                t.position += Vector3.right * (fish.Direction * fish.Data.moveSpeed * dt);
            }
        }

        /// <summary>
        /// 生成一条鱼，位置**一律在屏幕外**，并且方向保证它一定会穿过画面：
        ///   背景上移（下潜）  → 从屏幕下方进场
        ///   背景下移（上浮）  → 从屏幕上方进场
        ///   背景静止（准备 / 抛钩 / 触底冲刺）→ 从屏幕左右两侧进场，横向穿过
        /// 离屏距离在 spawnMarginMin~Max 之间随机，避免一屏鱼排成一条整齐的横线。
        /// </summary>
        private void Spawn(float depth, int scrollDir)
        {
            FishData data = _spawner.Config.PickByDepth(Random.value);
            if (data == null)
            {
                return;
            }

            float halfWidth = ViewportUtil.HalfWidth(_cam);
            float halfHeight = ViewportUtil.HalfHeight(_cam);
            float camY = _cam != null ? _cam.transform.position.y : 0f;
            float margin = Random.Range(_cfg.spawnMarginMin, _cfg.spawnMarginMax);

            Vector3 position;
            int direction;

            if (scrollDir > 0)
            {
                position = new Vector3(Random.Range(-halfWidth, halfWidth), camY - halfHeight - margin, _planeZ);
                direction = Random.value < 0.5f ? -1 : 1;
            }
            else if (scrollDir < 0)
            {
                position = new Vector3(Random.Range(-halfWidth, halfWidth), camY + halfHeight + margin, _planeZ);
                direction = Random.value < 0.5f ? -1 : 1;
            }
            else
            {
                bool fromLeft = Random.value < 0.5f;
                position = new Vector3(fromLeft ? -halfWidth - margin : halfWidth + margin,
                    camY + Random.Range(-halfHeight, halfHeight), _planeZ);
                direction = fromLeft ? 1 : -1;
            }

            Fish fish = _spawner.Spawn(data, position, direction);
            if (fish != null)
            {
                _actives.Add(fish);
            }
        }

        private float CurrentInterval(float depth)
        {
            float t = _cfg.maxDepth <= 0f ? 0f : Mathf.Clamp01(depth / _cfg.maxDepth);
            return Mathf.Lerp(_cfg.spawnInterval, _cfg.spawnIntervalMin, t);
        }
    }
}