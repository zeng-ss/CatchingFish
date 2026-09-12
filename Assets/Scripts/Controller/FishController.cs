using System;
using System.Collections.Generic;
using Config;
using Core;
using Entities;
using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 鱼群行为控制。和 FishSpawner 一起挂在 BG/fishs 上，是 GameMgr 眼里鱼群的唯一入口。
    ///
    /// 只处理 X 轴：局部左右游动 + 撞到边界掉头 + 越界打回收标记。
    /// Y 轴完全由 BG 的世界滚动带着走（鱼是 BG 的子物体），
    /// 这正是需求里"鱼和背景速度一致、只做相对左右移动"的落地方式——
    /// 不需要写任何同步代码，也就永远不会出现鱼和背景不同步。
    /// </summary>
    public class FishController : MonoBehaviour
    {
        [Header("生成器（留空自动取同物体上的 FishSpawner）")]
        [SerializeField]
        private FishSpawner _spawner;

        private GameConfig _cfg;
        private Camera _cam;

        /// <summary>当前存活的鱼。</summary>
        public IReadOnlyList<Fish> Actives => _spawner != null ? _spawner.Actives : Array.Empty<Fish>();

        private void Awake()
        {
            _cfg = GameConfig.Get();
            _cam = Camera.main;

            if (_spawner == null)
            {
                _spawner = GetComponent<FishSpawner>();
            }
        }

        /// <summary>每帧驱动鱼群：先补/收，再让存活的鱼游动。</summary>
        public void Tick(float dt, GameState state, float depth, int scrollDir)
        {
            if (_spawner == null)
            {
                return;
            }

            _spawner.Tick(dt, state, depth, scrollDir);
            MoveAll(dt);
        }

        public void RecycleAll()
        {
            if (_spawner != null)
            {
                _spawner.RecycleAll();
            }
        }

        public void RecycleCaught(IReadOnlyList<Fish> caught)
        {
            if (_spawner != null)
            {
                _spawner.RecycleCaught(caught);
            }
        }

        private void MoveAll(float dt)
        {
            IReadOnlyList<Fish> fishList = Actives;
            if (fishList.Count == 0)
            {
                return;
            }

            float halfWidth = ViewportUtil.HalfWidth(_cam) * _cfg.fishEdgeRatio;
            float viewBottom = ViewportUtil.ViewBottomWorldY(_cam);
            float viewTop = ViewportUtil.ViewTopWorldY(_cam);
            float despawnMargin = _cfg.despawnMargin;

            for (int i = 0; i < fishList.Count; i++)
            {
                Fish fish = fishList[i];
                if (fish == null || fish.IsCaught || fish.View == null || fish.Data == null)
                {
                    continue;
                }

                Transform t = fish.View.transform;

                // 左右游动：撞到边界就掉头
                float x = t.position.x;
                if (x <= -halfWidth && fish.Direction < 0)
                {
                    fish.Direction = 1;
                }
                else if (x >= halfWidth && fish.Direction > 0)
                {
                    fish.Direction = -1;
                }

                ViewportUtil.MoveWorldX(t, fish.Direction * fish.Data.moveSpeed * dt);
                fish.View.SetDirection(fish.Direction);

                // 越界回收：下潜时从上方出场，上浮时从下方出场
                float y = t.position.y;
                if (y > viewTop + despawnMargin || y < viewBottom - despawnMargin)
                {
                    fish.NeedRecycle = true;
                }
            }
        }
    }
}
