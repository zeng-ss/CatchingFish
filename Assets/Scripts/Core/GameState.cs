namespace Core
{
    /// <summary>【状态机】一局游戏的状态。由 GameMgr 驱动，切换时广播 StateChanged。</summary>
    public enum GameState
    {
        Tutorial,       // 开局前的玩法教程（面板里放实时演示，不接受操作）
        Ready,          // 准备，等待长按开始
        CastingDown,    // 下潜：碰鱼扣氧，可左右控制
        ReelingUp,      // 上浮：碰鱼抓取，可左右控制
        FastReturn,     // 满仓加速返回：不可操作，不再抓鱼
        Settlement,     // 结算
        Failed          // 氧气耗尽失败
    }
}