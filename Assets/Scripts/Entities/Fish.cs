using Config;
using UnityEngine;
using View;

namespace Entities
{
    /// <summary>
    /// 一条鱼的运行时实体 只保存逻辑状态
    /// </summary>
    public class Fish
    {
        public FishData Data;
        // 游动方向：1 向右，-1 向左
        public int Direction = 1;
        // 已被抓住
        public bool IsCaught;
        // 本次下潜是否已经撞过鱼钩，防止同一条鱼连续扣血
        public bool HasHitHook;

        /// <summary>
        /// 是否已经进过屏幕。
        /// </summary>
        public bool HasEnteredView;

        // 表现层
        public FishView View;

        /// <summary>世界空间包围盒：碰撞判定和"是否完全移出屏幕"都用它。</summary>
        public Bounds WorldBounds => View != null
            ? View.WorldBounds
            : new Bounds(View.transform.position, Vector3.one * 0.5f);

        /// <summary>从对象池取出时重置所有运行时状态。</summary>
        public void ResetForSpawn(FishData data, int direction)
        {
            Data = data;
            Direction = direction >= 0 ? 1 : -1;
            IsCaught = false;
            HasHitHook = false;
            HasEnteredView = false;
        }
    }
}