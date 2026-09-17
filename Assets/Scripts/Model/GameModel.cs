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

        private int _maxHp;
        private int _maxCatch;
        private float _maxDepth;
        private int _hp;
        public int Score { get; private set; }
        public float Depth { get; private set; } // 当前下潜深度

        public IReadOnlyList<CaughtFish> Caught => _caught;
        public int CaughtCount => _caught.Count;
        public bool IsInventoryFull => CaughtCount >= _maxCatch;
        public bool IsDead => _hp <= 0;
        public bool IsWin => !IsDead;

        /// <summary>是否已经到底</summary>
        public bool IsAtBottom => Depth >= _maxDepth;

        public void Reset(int maxHp, int maxCatch, float maxDepth)
        {
            _maxHp = maxHp;
            _maxCatch = maxCatch;
            _maxDepth = maxDepth;

            _hp = _maxHp;
            Score = 0;
            Depth = 0f;
            _caught.Clear();

            EventMgr.Publish(GameEvent.ScoreChanged, Score);
            EventMgr.Publish(GameEvent.DepthChanged, Mathf.Clamp01(Depth / _maxDepth));
            EventMgr.Publish(GameEvent.HpChanged, new HpPayload { Current = _hp, Max = _maxHp });
            EventMgr.Publish(GameEvent.FishCaught, new CatchPayload { Current = CaughtCount, Max = _maxCatch });
        }

        public void SetDepth(float depth)
        {
            Depth = Mathf.Clamp(depth, 0f, _maxDepth);
            EventMgr.Publish(GameEvent.DepthChanged, Mathf.Clamp01(Depth / _maxDepth));
        }

        public int ApplyDamage(int damage)
        {
            if (IsDead) return 0;
            int before = _hp;
            _hp = Mathf.Max(0, _hp - damage);
            EventMgr.Publish(GameEvent.HpChanged, new HpPayload { Current = _hp, Max = _maxHp });
            return before - _hp;
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
            EventMgr.Publish(GameEvent.FishCaught, new CatchPayload { Current = CaughtCount, Max = _maxCatch });
        }
    }
}