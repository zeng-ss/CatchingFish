using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【控制层 → 表现层】鱼的表现对象与控制层之间的**唯一契约**。
    ///
    /// 表现层（`FishView` / `FishHang`）只持有这个接口，不持有 `FishController` 具体类 ——
    /// 所以它拿不到、也不会去碰 `Tick` / `RecycleAll` 这些只属于控制层的规则，
    /// 想换一套实现（比如给控制层写单元测试用的假数据源）也不需要动表现层。
    ///
    /// 生命周期：`Register` 领槽位 → 每帧 `Get` 拉状态 → 槽位失效时自己 `Release` 回池 → 对象禁用时 `Unregister`。
    /// </summary>
    public interface IFishStateSource
    {
        /// <summary>表现对象出生时认领一个数据槽。</summary>
        /// <returns>槽位 id；没有待投放的鱼时返回 -1。</returns>
        int Register(Vector3 position, Vector3 size);

        /// <summary>表现对象被禁用时归还槽位。</summary>
        void Unregister(int id);

        /// <summary>按 id 拉当前状态；返回 null 表示这条鱼已经被控制层回收了。</summary>
        FishRuntime Get(int id);

        /// <summary>把 GameObject 交回对象池（控制层只"过一下手"，不持有它）。</summary>
        void Release(int id, GameObject go);
    }
}
