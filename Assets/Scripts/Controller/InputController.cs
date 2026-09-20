using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【控制层】输入采集。
    /// 把 鼠标 / 触摸 / 键盘 统一成三样东西，只读输入、不含任何游戏规则：
    ///   PressedThisFrame —— 这一帧刚按下（用来触发"开始下潜"）
    ///   PointerActive    —— 本帧是否拿到了有效指针
    ///   PointerWorldX    —— 指针在世界空间的 X（鱼钩的跟随目标）
    /// 以后换 Input System 或加手柄，只改这一个类。
    /// </summary>
    public class InputController
    {
        /// <summary>这一帧是否刚按下。</summary>
        public bool PressedThisFrame { get; private set; }

        /// <summary>本帧是否拿到了有效的指针位置。</summary>
        public bool PointerActive { get; private set; }

        /// <summary>指针在世界空间的 X 坐标。</summary>
        public float PointerWorldX { get; private set; }

        private Camera _cam;

        public void Init(Camera cam) => _cam = cam;

        /// <summary>每帧开头调用一次。</summary>
        public void Tick()
        {
            PressedThisFrame = false;
            PointerActive = false;

            // 触摸优先，其次鼠标左键
            Vector2 screenPos;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                PressedThisFrame = touch.phase == TouchPhase.Began;
                screenPos = touch.position;
            }
            else if (Input.GetMouseButton(0))
            {
                PressedThisFrame = Input.GetMouseButtonDown(0);
                screenPos = Input.mousePosition;
            }
            else
            {
                return;
            }

            PointerActive = true;
            PointerWorldX = ScreenToWorldX(screenPos);
        }

        /// <summary>正交相机下取"世界 z = 0 平面"上的交点即可。</summary>
        private float ScreenToWorldX(Vector2 screenPos)
        {
            if (_cam == null) return 0f;

            float depth = Mathf.Abs(_cam.transform.position.z);
            return _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth)).x;
        }
    }
}