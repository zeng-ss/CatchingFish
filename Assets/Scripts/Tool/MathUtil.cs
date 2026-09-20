using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 【工具层】与业务无关的纯函数，方便单独做单元测试。
    /// </summary>
    public static class MathUtil
    {
        /// <summary>
        /// 按权重随机取下标（鱼表里的 spawnWeight 用它挑鱼种）。
        /// roll 由外部传入（[0,1)），随机源可替换、逻辑可复现。
        /// </summary>
        /// <returns>权重全为 0 或列表为空时返回 -1。</returns>
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
    }
}
