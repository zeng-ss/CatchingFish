using Config;
using UnityEngine;
using View;

namespace Entities
{
    /// <summary>
    /// 一条鱼的运行时实体。**纯 C# 类**，只保存逻辑状态；
    /// 位置/朝向都放在 <see cref="FishView"/> 的 Transform 上，
    /// 避免逻辑和表现各存一份坐标导致不同步。
    /// </summary>
    public class Fish
    {
        /// <summary>数值配置引用（只读，来自 FishConfig）。</summary>
        public FishData Data;

        /// <summary>游动方向：1 向右，-1 向左。出生时定好，之后一路不回头。</summary>
        public int Direction = 1;

        /// <summary>已被抓住，正挂在鱼钩上（不再参与游动/回收）。</summary>
        public bool IsCaught;

        /// <summary>本次下潜是否已经撞过鱼钩，防止同一条鱼连续扣血。</summary>
        public bool HasHitHook;

        /// <summary>
        /// 是否已经进过屏幕。
        /// 鱼一律在屏幕外生成，所以"在屏幕外"本身不能作为回收条件——
        /// 必须等它真正进过画面、再完全离开屏幕一段距离，才允许回收。
        /// </summary>
        public bool HasEnteredView;

        /// <summary>表现层。</summary>
        public FishView View;

        public Vector3 WorldPosition => View != null ? View.transform.position : Vector3.zero;

        /// <summary>世界空间包围盒：碰撞判定和"是否完全移出屏幕"都用它。</summary>
        public Bounds WorldBounds => View != null
            ? View.WorldBounds
            : new Bounds(WorldPosition, Vector3.one * 0.5f);

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
