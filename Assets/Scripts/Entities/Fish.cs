using Config;
using UnityEngine;
using View;

namespace Entities
{
    /// <summary>
    /// 一条鱼的运行时实体。
    /// 只保存"逻辑状态"，位置/朝向都放在 <see cref="FishView"/> 的 Transform 上，
    /// 避免逻辑和表现各存一份坐标导致不同步。
    /// </summary>
    public class Fish
    {
        /// <summary>自增实例 Id，方便日志排查。</summary>
        public int Id;

        /// <summary>数值配置引用（只读，来自 FishConfig）。</summary>
        public FishData Data;

        /// <summary>游动方向：1 向右，-1 向左。</summary>
        public int Direction = 1;

        /// <summary>已被抓住，正挂在鱼钩上（不再参与游动/回收）。</summary>
        public bool IsCaught;

        /// <summary>本次下潜是否已经撞过鱼钩，防止同一条鱼连续扣血。</summary>
        public bool HasHitHook;

        /// <summary>已越界，等待生成器回收。</summary>
        public bool NeedRecycle;

        /// <summary>表现层。</summary>
        public FishView View;

        public Transform Transform => View != null ? View.transform : null;

        public Vector3 WorldPosition => View != null ? View.transform.position : Vector3.zero;

        /// <summary>世界空间包围盒，碰撞用（自动跟随模型缩放与蒙皮）。</summary>
        public Bounds WorldBounds => View != null
            ? View.WorldBounds
            : new Bounds(WorldPosition, Vector3.one * 0.5f);

        /// <summary>用于日志/调试的可读名字。</summary>
        public string DebugName => Data == null ? "(null)" : $"{Data.displayName}#{Id}";

        /// <summary>从对象池取出时重置所有运行时状态。</summary>
        public void ResetForSpawn(FishData data, int direction, int id)
        {
            Data = data;
            Direction = direction >= 0 ? 1 : -1;
            Id = id;
            IsCaught = false;
            HasHitHook = false;
            NeedRecycle = false;
        }
    }
}