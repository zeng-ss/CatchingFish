using System.Collections.Generic;
using Config;
using UnityEngine;

namespace Model
{
    /// <summary>渔获记录，结算列表展示用</summary>
    public struct CaughtFish
    {
        public FishType Type;
        public string DisplayName;
        public int Score;
    }

    public class GameModel
    {
        private readonly List<CaughtFish> _caught = new();

        public int MaxHp { get; private set; }
        public int MaxCatch { get; private set; }
        public float MaxDepth { get; private set; }

        public int Hp { get; private set; }
        public int Score { get; private set; }

        /// <summary>当前下潜深度（世界单位，0 = 水面）。</summary>
        public float Depth { get; private set; }

        public IReadOnlyList<CaughtFish> Caught => _caught;
        public int CaughtCount => _caught.Count;
        public bool IsInventoryFull => CaughtCount >= MaxCatch;
        public bool IsDead => Hp <= 0;

        /// <summary>下潜进度 0~1，HUD 直接用</summary>
        public float DepthRatio => MaxDepth <= 0f ? 0f : Mathf.Clamp01(Depth / MaxDepth);

        /// <summary>是否已经到底</summary>
        public bool IsAtBottom => Depth >= MaxDepth;

        public void Reset(int maxHp, int maxCatch, float maxDepth)
        {
            MaxHp = Mathf.Max(1, maxHp);
            MaxCatch = Mathf.Max(1, maxCatch);
            MaxDepth = Mathf.Max(0.01f, maxDepth);

            Hp = MaxHp;
            Score = 0;
            Depth = 0f;
            _caught.Clear();
        }

        public void SetDepth(float depth)
        {
            Depth = Mathf.Clamp(depth, 0f, MaxDepth);
        }

        public int ApplyDamage(int damage)
        {
            if (damage <= 0 || IsDead)
            {
                return 0;
            }

            int before = Hp;
            Hp = Mathf.Max(0, Hp - damage);
            return before - Hp;
        }

        /// <summary>记一条渔获并加分</summary>
        /// <returns>返回 true 表示渔获已满，需要立刻加速收线</returns>
        public bool AddCaught(FishData data)
        {
            if (data == null || IsInventoryFull)
            {
                return IsInventoryFull;
            }

            _caught.Add(new CaughtFish
            {
                Type = data.type,
                DisplayName = string.IsNullOrEmpty(data.displayName) ? data.prefabName : data.displayName,
                Score = data.score,
            });

            Score += Mathf.Max(0, data.score);
            return IsInventoryFull;
        }

        public bool IsWin => !IsDead;
    }
}
