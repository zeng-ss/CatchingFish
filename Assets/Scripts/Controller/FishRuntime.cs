using Config;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【控制层】一条鱼的运行时数据。
    ///
    /// **纯数据，不含任何引用类型**（Vector3 / Bounds 都是值类型）：
    /// 控制层不持有 View，View 也不持有数据，两边只靠"槽位 id"关联。
    /// </summary>
    public class FishRuntime
    {
        /// <summary>数值配置（来自 FishConfig）。</summary>
        public FishData Data;

        /// <summary>是否还在场上。控制层把它标为 false 表示已回收，表现层看到后自己回池。</summary>
        public bool Alive = true;

        /// <summary>世界坐标（控制层自己模拟，不来自 Transform）。</summary>
        public Vector3 Position;

        /// <summary>
        /// 出生时的纵向基准 = 出生世界 Y - 当时的世界滚动量。
        /// 鱼是 BG 的子物体，纵向位移由父物体带着走；控制层要算逻辑用的世界坐标时，
        /// 用 BaseY + 当前滚动量还原即可，两边永远一致。
        /// </summary>
        public float BaseY;

        /// <summary>世界空间包围盒尺寸，注册时由表现层报上来。</summary>
        public Vector3 Size = Vector3.one * 0.5f;

        /// <summary>游动方向：1 向右，-1 向左。出生时定好，一路不回头。</summary>
        public int Direction = 1;

        /// <summary>已被抓住，挂在鱼钩上（不再参与游动/回收）。</summary>
        public bool IsCaught;

        /// <summary>被抓住的次序（第几条）。表现层用它做"扇形展开"的初始倾角。</summary>
        public int CatchSlot = -1;

        /// <summary>本次下潜是否已经撞过鱼钩，防止同一条鱼连续扣血。</summary>
        public bool HasHitHook;

        /// <summary>
        /// 是否已经进过屏幕。
        /// 鱼一律在屏幕外生成，所以"在屏幕外"本身不能作为回收条件——
        /// 必须等它真正进过画面、再完全离开屏幕一段距离，才允许回收。
        /// </summary>
        public bool HasEnteredView;

        /// <summary>
        /// 出生到现在活了多久（秒）。用来兜底回收"永远进不了画面"的鱼：
        /// 鱼不会掉头，横向游出可视范围就再也回不来了，没有兜底它会一直占着刷鱼名额。
        /// </summary>
        public float Life;

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
            Life = 0f;
        }
    }
}