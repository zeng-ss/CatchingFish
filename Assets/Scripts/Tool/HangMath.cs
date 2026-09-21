using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 【工具层】"被钩住"这件事的数学：扇形展开角、朝上定姿、悬挂姿态、把嘴贴回钩子。
    ///
    /// 游戏里的鱼（`FishHang`）和教程布景里的道具鱼（`TutorialFishView`）共用这几个纯函数，
    /// 所以两边的手感和观感天然一致 —— 改一处两边一起变。
    ///
    /// 核心技巧：**"先转、再把嘴平移回钩子" 等价于 "绕嘴旋转"**。
    /// 嘴是鱼的子物体，旋转后它的世界位置会跟着变；这时把 (钩子位置 − 嘴位置) 补到鱼身上，
    /// 嘴就精确贴回钩子，视觉上鱼就是绕着自己的嘴在摆 —— 不需要额外的 pivot 物体。
    /// </summary>
    public static class HangMath
    {
        /// <summary>
        /// 扇形展开角：第 1 条挂在正下方，之后左右交替、越往后偏得越多。
        /// 0 → 0°，1 → -step（左），2 → +step（右），3 → -2·step，4 → +2·step …
        /// 绕 Z 轴正转会把下垂的身体推向 +X（右），所以往左取负号。
        /// </summary>
        public static float FanAngle(int slot, float step)
        {
            if (slot <= 0) return 0f;

            int side = slot % 2 == 1 ? -1 : 1;
            int distance = (slot + 1) / 2; // 1,1,2,2,3,3…
            return side * distance * step;
        }

        /// <summary>
        /// 鼻子（transform.forward）朝上的定姿。
        /// 用 FromToRotation 而不是写死角度，左右朝向的鱼都能正确转过来。
        /// </summary>
        public static Quaternion UpFacing(Quaternion current)
        {
            return Quaternion.FromToRotation(current * Vector3.forward, Vector3.up) * current;
        }

        /// <summary>
        /// 悬挂姿态 = （扇形倾角 + 摆动角）绕 Z 旋转 × 从抓住时的姿态平滑转到"朝上"。
        /// </summary>
        /// <param name="align">0 → 1 的转向进度，由 DOTween 驱动。</param>
        /// <param name="swing">随钩子横向速度产生的摆动角（度）。</param>
        public static Quaternion Pose(Quaternion catchRotation, Quaternion hangRotation, float fanAngle,
            float align, float swing)
        {
            return Quaternion.Euler(0f, 0f, fanAngle * align + swing)
                   * Quaternion.Slerp(catchRotation, hangRotation, align);
        }

        /// <summary>把嘴平移回钩子上（见类注释里的"绕嘴旋转"）。</summary>
        public static Vector3 PlaceMouthOnHook(Vector3 position, Vector3 mouthPosition, Vector3 hookPosition)
        {
            return position + hookPosition - mouthPosition;
        }
    }
}
