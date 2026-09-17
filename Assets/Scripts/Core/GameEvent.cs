using Model;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public enum GameEvent
    {
        StateChanged,
        DepthChanged,
        HpChanged,
        ScoreChanged,
        FishCaught,
        GameSettle
    }

    /// <summary>血量事件负载。</summary>
    public struct HpPayload
    {
        public int Current;
        public int Max;
    }

    /// <summary>渔获数量事件负载。</summary>
    public struct CatchPayload
    {
        public int Current;
        public int Max;
        public Transform parent;
    }

    /// <summary>结算事件负载。</summary>
    public struct SettlePayload
    {
        public bool IsWin;
        public int Score;
        public int CaughtCount;
        public List<CaughtFish> FishList;
    }
}