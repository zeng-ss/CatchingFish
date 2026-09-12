using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 背景与世界滚动。挂在 BG 物体上。
    ///
    /// 滚动量由深度决定（GameConfig.WorldScrollAt），而深度分段是：
    ///   抛钩段 / 触底冲刺段 —— 背景不动，鱼钩自己在走
    ///   常规段            —— 鱼钩不动，背景在走
    /// 所以"下潜"这件事有时候是背景在动、有时候是鱼钩在动，两者拼起来才是一整趟。
    ///
    /// 鱼群是 BG 的子物体，自动跟着世界一起走，不需要任何同步代码。
    /// </summary>
    public class BGController : MonoBehaviour
    {
        [Header("背景贴图容器（留空自动找子物体 bgs）")] [SerializeField]
        private Transform tilesRoot;

        [Tooltip("量不到贴图高度时的兜底值（世界单位）")] [SerializeField]
        private float fallbackTileWorldHeight = 10f;

        private Transform[] _tiles;
        private Vector3 _worldBasePos;
        private float _tileLocalHeight = 8f;
        private float _scroll;
        private Camera _cam;
        private bool _ready;

        /// <summary>当前世界已经滚动了多少（正值 = 世界上移 = 正在下潜）。</summary>
        public float Scroll => _scroll;

        /// <summary>单张背景贴图在世界空间的高度，调参（比如 finalDiveDepth）时可参考。</summary>
        public float TileWorldHeight => _tileLocalHeight * Mathf.Abs(tilesRoot != null ? tilesRoot.lossyScale.y : 1f);

        private void Awake()
        {
            _cam = Camera.main;
            _worldBasePos = transform.position;

            CollectTiles();
            MeasureTiles();
            LayoutTiles();

            _ready = true;

            Debug.Log($"[BGController] 背景贴图高度 {TileWorldHeight:F2} 世界单位，" +
                      $"共 {(_tiles != null ? _tiles.Length : 0)} 张。");
        }

        // ------------------------------------------------------------------
        // 对外接口
        // ------------------------------------------------------------------

        /// <summary>设置世界滚动量（世界单位）。GameMgr 每帧把当前深度换算后传进来。</summary>
        public void SetScroll(float offsetWorld)
        {
            _scroll = offsetWorld;
            transform.position = _worldBasePos + Vector3.up * _scroll;
        }

        /// <summary>回到水面。</summary>
        public void ResetScroll()
        {
            SetScroll(0f);
        }

        // ------------------------------------------------------------------
        // 内部实现
        // ------------------------------------------------------------------

        /// <summary>
        /// 把背景贴图循环铺满视野。
        /// 放在 LateUpdate：保证一定晚于本帧的 SetScroll，贴图不会露接缝。
        /// </summary>
        private void LateUpdate()
        {
            if (!_ready || _tiles == null || _tiles.Length == 0 || _cam == null)
            {
                return;
            }

            float total = _tileLocalHeight * _tiles.Length;
            float viewBottomLocal = ViewportUtil.WorldYToLocalY(tilesRoot, ViewportUtil.ViewBottomWorldY(_cam));
            float viewTopLocal = ViewportUtil.WorldYToLocalY(tilesRoot, ViewportUtil.ViewTopWorldY(_cam));
            float viewHeightLocal = viewTopLocal - viewBottomLocal;

            if (total < viewHeightLocal + _tileLocalHeight)
            {
                Debug.LogWarning(
                    $"[BGController] 背景贴图不够铺满视野：总高 {total:F2} < 需要 {viewHeightLocal + _tileLocalHeight:F2}，" +
                    "请增加 bgs 下的贴图数量或放大贴图。");
            }

            float start = viewBottomLocal - _tileLocalHeight * 0.5f;

            for (int i = 0; i < _tiles.Length; i++)
            {
                Vector3 lp = _tiles[i].localPosition;
                lp.y = start + Mathf.Repeat(lp.y - start, total);
                _tiles[i].localPosition = lp;
            }
        }

        private void CollectTiles()
        {
            if (tilesRoot == null)
            {
                _tiles = new Transform[0];
                return;
            }

            int count = tilesRoot.childCount;
            _tiles = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                _tiles[i] = tilesRoot.GetChild(i);
            }

            if (count == 0)
            {
                Debug.LogWarning("[BGController] 背景容器下没有任何贴图。");
            }
        }

        /// <summary>实测单张贴图的世界高度，避免把 Sprite 尺寸/PPU 写死在代码里。</summary>
        private void MeasureTiles()
        {
            float worldHeight = 0f;
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i] == null)
                {
                    continue;
                }

                Renderer r = _tiles[i].GetComponent<Renderer>();
                if (r == null)
                {
                    r = _tiles[i].GetComponentInChildren<Renderer>();
                }

                if (r != null)
                {
                    worldHeight = Mathf.Max(worldHeight, r.bounds.size.y);
                }
            }

            float parentScaleY = Mathf.Abs(tilesRoot != null ? tilesRoot.lossyScale.y : 1f);
            if (parentScaleY < 0.0001f)
            {
                parentScaleY = 1f;
            }

            if (worldHeight <= 0.01f)
            {
                worldHeight = fallbackTileWorldHeight * parentScaleY;
            }

            _tileLocalHeight = worldHeight / parentScaleY;
        }

        /// <summary>把贴图按量出来的高度等距排好，避免美术手摆时留下的缝隙。</summary>
        private void LayoutTiles()
        {
            int n = _tiles.Length;
            if (n == 0)
            {
                return;
            }

            float center = 0f;
            for (int i = 0; i < n; i++)
            {
                center += _tiles[i].localPosition.y;
            }

            center /= n;

            for (int i = 0; i < n; i++)
            {
                Vector3 lp = _tiles[i].localPosition;
                lp.y = center + (i - (n - 1) * 0.5f) * _tileLocalHeight;
                _tiles[i].localPosition = lp;
            }
        }
    }
}