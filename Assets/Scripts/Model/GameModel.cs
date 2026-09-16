using System.Collections.Generic;
using Config;
using Core;
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
            MaxHp = maxHp;
            MaxCatch = maxCatch;
            MaxDepth = maxDepth;

            Hp = MaxHp;
            Score = 0;
            Depth = 0f;
            _caught.Clear();

            EventMgr.Publish(GameEvent.ScoreChanged, Score);
            EventMgr.Publish(GameEvent.DepthChanged, DepthRatio);
            EventMgr.Publish(GameEvent.HpChanged, new HpPayload { Current = Hp, Max = MaxHp });
            EventMgr.Publish(GameEvent.FishCaught, new CatchPayload { Current = CaughtCount, Max = MaxCatch });
        }

        public void SetDepth(float depth)
        {
            Depth = Mathf.Clamp(depth, 0f, MaxDepth);
            EventMgr.Publish(GameEvent.DepthChanged, DepthRatio);
        }

        public int ApplyDamage(int damage)
        {
            if (IsDead) return 0;
            int before = Hp;
            Hp = Mathf.Max(0, Hp - damage);
            EventMgr.Publish(GameEvent.HpChanged, new HpPayload { Current = Hp, Max = MaxHp });
            return before - Hp;
        }

        public void AddCaught(FishData data)
        {
            if (data == null || IsInventoryFull) return;
            _caught.Add(new CaughtFish
            {
                Type = data.type,
                DisplayName = string.IsNullOrEmpty(data.displayName) ? data.prefabName : data.displayName,
                Score = data.score
            });

            Score += data.score;
            EventMgr.Publish(GameEvent.ScoreChanged, Score);
            EventMgr.Publish(GameEvent.FishCaught, new CatchPayload { Current = CaughtCount, Max = MaxCatch });
        }

        public bool IsWin => !IsDead;
    }
}