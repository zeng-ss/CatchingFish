using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    /// <summary>与业务无关的纯函数工具，方便单独做单元测试。</summary>
    public static class MathUtil
    {
        /// <summary>
        /// 按权重随机取下标。
        /// roll 由外部传入（[0,1)），这样随机源可替换，逻辑可复现。
        /// </summary>
        /// <returns>全部权重都 &lt;= 0 时返回 -1。</returns>
        public static int PickWeighted(IReadOnlyList<int> weights, float roll)
        {
            if (weights == null || weights.Count == 0)
            {
                return -1;
            }

            int total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Mathf.Max(0, weights[i]);
            }

            if (total <= 0)
            {
                return -1;
            }

            float target = Mathf.Clamp01(roll) * total;
            int acc = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += Mathf.Max(0, weights[i]);
                if (target < acc)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }

        /// <summary>
        /// 圆 vs 轴对齐包围盒 的相交判定（二维，忽略 Z）。
        /// 用 AABB 而不是圆-圆，长条形的鱼（带鱼）判定会准得多。
        /// </summary>
        public static bool CircleIntersectsBounds(Vector2 center, float radius, Bounds bounds)
        {
            Vector2 min = bounds.min;
            Vector2 max = bounds.max;

            float nearestX = Mathf.Clamp(center.x, min.x, max.x);
            float nearestY = Mathf.Clamp(center.y, min.y, max.y);

            float dx = center.x - nearestX;
            float dy = center.y - nearestY;

            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
