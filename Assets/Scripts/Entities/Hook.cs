using System.Collections.Generic;

namespace Entities
{
    /// <summary>
    /// 鱼钩实体。
    /// 位置由 HookController 负责：X 来自玩家的长按拖动，Y 来自按深度分段的 DOTween 补间。
    /// </summary>
    public class Hook
    {
        /// <summary>世界坐标 X（玩家左右控制的唯一位置分量）。</summary>
        public float X;

        /// <summary>世界坐标 Y（由 HookController 按深度分段驱动，不是固定值）。</summary>
        public float Y;

        /// <summary>碰撞半径（世界单位），由表现层实测后回填。</summary>
        public float Radius = 0.25f;

        /// <summary>当前挂在钩上的鱼，按抓取顺序排列。</summary>
        public readonly List<Fish> Caught = new();

        public int CaughtCount => Caught.Count;

        public void Reset(float x, float y)
        {
            X = x;
            Y = y;
            Caught.Clear();
        }

        public void ClearCaught()
        {
            Caught.Clear();
        }
    }
}