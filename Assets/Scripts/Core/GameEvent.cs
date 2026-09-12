using System.Collections.Generic;
using Config;
using Model;

namespace Core
{
    /// <summary>
    /// 全局事件键（观察者模式的"主题"）。
    /// 约定：Model / Controller 只负责 Publish，View 只负责 Subscribe，
    /// 两者互不持有引用，MVC 各层才能真正解耦。
    /// </summary>
    public enum GameEvent
    {
        /// <summary>开局（玩家第一次按下）。参数：无</summary>
        GameStart,

        /// <summary>重开一局。参数：无</summary>
        GameRestart,

        /// <summary>状态机切换。参数：GameState</summary>
        StateChanged,

        /// <summary>下潜进度变化。参数：float（0~1）</summary>
        DepthChanged,

        /// <summary>血量变化。参数：HpPayload</summary>
        HpChanged,

        /// <summary>分数变化。参数：int</summary>
        ScoreChanged,

        /// <summary>渔获数量变化。参数：CatchPayload</summary>
        CaughtChanged,

        /// <summary>下潜时撞到鱼。参数：int（扣血量）</summary>
        FishHurt,

        /// <summary>抓到一条鱼。参数：FishData</summary>
        FishCaught,

        /// <summary>结算。参数：SettlePayload</summary>
        GameSettle,
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
