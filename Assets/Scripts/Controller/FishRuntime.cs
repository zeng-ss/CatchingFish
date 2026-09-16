using Config;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 控制层持有的一条鱼的运行时数据。
    ///
    /// **纯数据，不含任何引用类型**（Vector3 / Bounds 是值类型）——
    /// 控制层不持有 View，View 也不持有数据，依赖方向只有两条：
    ///     Controller → Model（数据）
    ///     View       → Controller（表现层每帧来"拉"状态）
    /// </summary>
    public class FishRuntime
    {
        /// <summary>数值配置（来自 FishConfig）。</summary>
        public FishData Data;

        /// <summary>是否还在场上。控制层把它标为 false 表示已回收，表现层看到后自己回池。</summary>
        public bool Alive = true;

        /// <summary>世界坐标（控制层自己模拟，不来自 Transform）。</summary>
        public Vector3 Position;

        /// <summary>世界空间包围盒尺寸，注册时由表现层报上来。</summary>
        public Vector3 Size = Vector3.one * 0.5f;

        /// <summary>游动方向：1 向右，-1 向左。出生时定好，一路不回头。</summary>
        public int Direction = 1;

        /// <summary>已被抓住，挂在鱼钩上（不再参与游动/回收）。</summary>
        public bool IsCaught;

        /// <summary>本次下潜是否已经撞过鱼钩，防止同一条鱼连续扣血。</summary>
        public bool HasHitHook;

        /// <summary>
        /// 是否已经进过屏幕。
        /// 鱼一律在屏幕外生成，所以"在屏幕外"本身不能作为回收条件——
        /// 必须等它真正进过画面、再完全离开屏幕一段距离，才允许回收。
        /// </summary>
        public bool HasEnteredView;

        public Bounds WorldBounds => new Bounds(Position, Size);

        public void Reset(FishData data, Vector3 position, Vector3 size, int direction)
        {
            Data = data;
            Position = position;
            Size = size;
            Direction = direction >= 0 ? 1 : -1;

            IsCaught = false;
            HasHitHook = false;
            HasEnteredView = false;
        }
    }
}