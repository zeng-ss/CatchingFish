using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 输入采集层。
    /// 把 鼠标 / 触摸 / 键盘 统一成三样东西：
    ///   PressedThisFrame —— 这一帧刚按下（用来触发"开始游戏"）
    ///   Holding          —— 是否按住（长按控制鱼钩左右）
    ///   PointerWorldX    —— 指针在世界空间的 X（鱼钩的跟随目标）
    /// 只读输入、不含任何游戏规则，以后换 Input System 或加手柄只需要改这一个类。
    /// </summary>
    public class InputController
    {
        /// <summary>这一帧是否刚按下。</summary>
        public bool PressedThisFrame { get; private set; }

        /// <summary>是否处于按住（长按）状态。</summary>
        public bool Holding { get; private set; }

        /// <summary>本帧是否拿到了有效的指针位置。</summary>
        public bool PointerActive { get; private set; }

        /// <summary>指针在世界空间的 X 坐标。</summary>
        public float PointerWorldX { get; private set; }

        private Camera _cam;

        public void Init(Camera cam)
        {
            _cam = cam;
        }

        /// <summary>每帧开头调用一次。</summary>
        public void Tick()
        {
            PressedThisFrame = false;
            Holding = false;
            PointerActive = false;

            // --- 触摸优先，其次鼠标左键 ---
            Vector2 screenPos = Vector2.zero;
            bool hasPointer = false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Holding = true;
                PressedThisFrame = touch.phase == TouchPhase.Began;
                screenPos = touch.position;
                hasPointer = true;
            }
            else if (Input.GetMouseButton(0))
            {
                Holding = true;
                PressedThisFrame = Input.GetMouseButtonDown(0);
                screenPos = Input.mousePosition;
                hasPointer = true;
            }

            PointerActive = hasPointer;
            if (hasPointer)
            {
                PointerWorldX = ScreenToWorldX(screenPos);
            }
        }

        private float ScreenToWorldX(Vector2 screenPos)
        {
            if (_cam == null)
            {
                return 0f;
            }

            // 正交相机下取"世界 z = 0 平面"上的交点即可
            float depth = Mathf.Abs(_cam.transform.position.z);
            Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            return world.x;
        }
    }
}