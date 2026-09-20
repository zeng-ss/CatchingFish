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

        /// <summary>下潜时被鱼撞到。负载：int（扣掉的血量）。表现层用它做鱼钩受击闪烁</summary>
        FishHurt,

        GameSettle,

        /// <summary>结算动画播完（散开 + 飘字）。负载：SettlePayload。结算面板监听这个而不是 GameSettle</summary>
        SettleAnimDone,

        /// <summary>某条鱼缩完了，要在它的位置冒 "+分数"。负载：FishBurstPayload</summary>
        SettleFishBurst,

        // ==================================================================
        // 教程（开局前那个面板里的实时演示）
        // ==================================================================

        /// <summary>教程底部提示。负载：string（空字符串 = 隐藏）</summary>
        TutorialHint,

        /// <summary>教程氧气条。负载：float（0~1）</summary>
        TutorialOxygen,

        /// <summary>教程演完了，面板可以弹 [再看一遍][开始游戏]</summary>
        TutorialFinished,

        /// <summary>面板点了 [再看一遍]</summary>
        TutorialReplay,

        /// <summary>面板点了 [开始游戏]，主流程据此进入 Ready</summary>
        TutorialStartGame
    }

    /// <summary>结算飘字负载。</summary>
    public struct FishBurstPayload
    {
        public Vector3 WorldPos;
        public int Score;
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