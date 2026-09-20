using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 【工具层】视口 / 世界 / 父节点局部 三种坐标系的换算。
    ///
    /// 鱼和鱼钩的位置一律直接写世界坐标（`transform.position`），不需要经过这里；
    /// 只有"背景贴图循环铺满视野"和"鱼钩左右边界"两处需要它 —— 因为背景根节点带缩放。
    /// </summary>
    public static class ViewportUtil
    {
        /// <summary>正交相机的可视半高（世界单位）。</summary>
        public static float HalfHeight(Camera cam)
        {
            return cam != null && cam.orthographic ? cam.orthographicSize : 5f;
        }

        /// <summary>可视半宽（世界单位）。</summary>
        public static float HalfWidth(Camera cam)
        {
            return cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect : 8.89f;
        }

        public static float ViewBottomWorldY(Camera cam)
        {
            return cam == null ? -5f : cam.transform.position.y - HalfHeight(cam);
        }

        public static float ViewTopWorldY(Camera cam)
        {
            return cam == null ? 5f : cam.transform.position.y + HalfHeight(cam);
        }

        /// <summary>世界坐标 Y → 父节点局部空间 Y。</summary>
        public static float WorldYToLocalY(Transform parent, float worldY)
        {
            if (parent == null)
            {
                return worldY;
            }

            return parent.InverseTransformPoint(new Vector3(parent.position.x, worldY, parent.position.z)).y;
        }
    }
}
