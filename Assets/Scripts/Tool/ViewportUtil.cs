using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 视口 / 世界 / 父节点局部 三种坐标系的换算工具。
    ///
    /// 背景和鱼群被 BG 根节点整体缩放了（1.17, 1.26, 1），
    /// 所以"屏幕上下边缘"必须换算到父节点局部空间，才能安全地循环摆放背景贴图。
    ///
    /// 注意：鱼和鱼钩的位置一律用**世界坐标**直接写（`transform.position`），
    /// 不需要经过这里换算——只有"背景贴图循环铺满视野"和"鱼钩左右边界"两处需要这个工具。
    /// </summary>
    public static class ViewportUtil
    {
        /// <summary>正交相机的可视半高（世界单位）。</summary>
        public static float HalfHeight(Camera cam)
        {
            if (cam == null)
            {
                return 5f;
            }

            return cam.orthographic ? cam.orthographicSize : 5f;
        }

        /// <summary>可视半宽（世界单位）。</summary>
        public static float HalfWidth(Camera cam)
        {
            if (cam == null)
            {
                return 8.89f;
            }

            return cam.orthographic ? cam.orthographicSize * cam.aspect : 8.89f;
        }

        public static float ViewBottomWorldY(Camera cam)
        {
            return cam == null ? -5f : cam.transform.position.y - HalfHeight(cam);
        }

        public static float ViewTopWorldY(Camera cam)
        {
            return cam == null ? 5f : cam.transform.position.y + HalfHeight(cam);
        }

        /// <summary>世界坐标 Y -> parent 局部空间 Y。</summary>
        public static float WorldYToLocalY(Transform parent, float worldY)
        {
            if (parent == null)
            {
                return worldY;
            }

            Vector3 probe = new Vector3(parent.position.x, worldY, parent.position.z);
            return parent.InverseTransformPoint(probe).y;
        }

        /// <summary>
        /// 聚合一组 Renderer 的世界空间包围盒（碰撞判定与出屏回收都用它）。
        /// 用渲染器实际包围盒而不是配置里手填半径，模型缩放、蒙皮动画都不用管。
        /// </summary>
        public static Bounds ComputeWorldBounds(Renderer r)
        {
            var result = r.bounds;
            return result;
        }
    }
}
