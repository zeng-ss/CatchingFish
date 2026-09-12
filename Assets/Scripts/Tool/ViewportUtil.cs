using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 视口 / 世界 / 父节点局部 三种坐标系的换算工具。
    /// 背景和鱼群被 BG 根节点整体缩放了（1.17, 1.26, 1），
    /// 所以"屏幕上下边缘"必须换算到父节点局部空间，才能安全地摆放子物体。
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

        /// <summary>parent 局部空间 Y -> 世界坐标 Y。</summary>
        public static float LocalYToWorldY(Transform parent, float localY)
        {
            if (parent == null)
            {
                return localY;
            }

            Vector3 probe = new Vector3(0f, localY, 0f);
            return parent.TransformPoint(probe).y;
        }

        /// <summary>世界坐标 X -> parent 局部空间 X（用传入的 worldY 作为采样高度）。</summary>
        public static float WorldXToLocalX(Transform parent, float worldX, float worldY)
        {
            if (parent == null)
            {
                return worldX;
            }

            Vector3 probe = new Vector3(worldX, worldY, parent.position.z);
            return parent.InverseTransformPoint(probe).x;
        }

        /// <summary>
        /// 让 t 在世界 X 方向平移 worldDeltaX。
        /// 自动除以父节点缩放，这样即使鱼群挂在被缩放的 BG 下面也不会走位错误。
        /// </summary>
        public static void MoveWorldX(Transform t, float worldDeltaX)
        {
            if (t == null || Mathf.Approximately(worldDeltaX, 0f))
            {
                return;
            }

            Vector3 local = t.localPosition;
            local.x += worldDeltaX / SafeScale(t.parent != null ? t.parent.lossyScale.x : 1f);
            t.localPosition = local;
        }

        /// <summary>
        /// 让 t 的世界 X 直接等于 worldX（同样自动处理父节点缩放）。
        /// </summary>
        public static void SetWorldX(Transform t, float worldX)
        {
            if (t == null || t.parent == null)
            {
                return;
            }

            float scaled = t.parent.position.x + (worldX - t.parent.position.x);
            float localX = WorldXToLocalX(t.parent, scaled, t.position.y);
            Vector3 local = t.localPosition;
            local.x = localX;
            t.localPosition = local;
        }

        private static float SafeScale(float scale)
        {
            return Mathf.Approximately(scale, 0f) ? 1f : scale;
        }

        /// <summary>
        /// 聚合一组 Renderer 的世界空间包围盒。
        /// 用渲染器实际包围盒做碰撞，比在配置里手填半径靠谱得多
        /// （模型缩放、蒙皮动画都不用管）。
        /// </summary>
        public static Bounds ComputeWorldBounds(Renderer[] renderers, Vector3 fallbackCenter)
        {
            bool has = false;
            Bounds result = new Bounds(fallbackCenter, Vector3.one * 0.5f);

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || !r.enabled)
                    {
                        continue;
                    }

                    if (!has)
                    {
                        result = r.bounds;
                        has = true;
                    }
                    else
                    {
                        result.Encapsulate(r.bounds);
                    }
                }
            }

            return result;
        }
    }
}