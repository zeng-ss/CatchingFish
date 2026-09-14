using Model;
using System.Collections.Generic;

namespace Core
{
    public enum GameEvent
    {
        GameStart,
        StateChanged,
        DepthChanged,
        HpChanged,
        ScoreChanged,
        FishHurt,
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