using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 背景与世界滚动。由 GameMgr 创建并在 Update 末尾驱动。滚动量由深度决定（GameConfig.WorldScrollAt）
    /// </summary>
    public class BGController
    {
        private readonly Transform _worldRoot;
        private readonly Transform _tilesRoot;
        private readonly Transform[] _tiles;
        private readonly Vector3 _worldBasePos;
        private readonly Camera _cam;

        private float _tileLocalHeight = 8f;
        private float _scroll;

        /// <param name="worldRoot">整体滚动的世界根节点。</param>
        /// <param name="tilesRoot">背景贴图容器。</param>
        public BGController(Transform worldRoot, Transform tilesRoot, Camera cam)
        {
            _worldRoot = worldRoot;
            _tilesRoot = tilesRoot;
            _cam = cam;

            if (_worldRoot == null)
            {
                Debug.LogError("[BGController] worldRoot 为空，背景不会滚动。");
                _tiles = new Transform[0];
                return;
            }

            _worldBasePos = _worldRoot.position;

            _tiles = CollectTiles(tilesRoot);
            MeasureTiles();
            LayoutTiles();
            //Debug.Log($"[BGController] 背景贴图高度 {TileWorldHeight:F2} 世界单位，共 {_tiles.Length} 张。");
        }

        public float Scroll => _scroll; // 当前世界已经滚动了多少，正值 = 世界上移 = 正在下潜

        /// <summary>单张背景贴图在世界空间的高度，用来校准 finalDiveDepth。</summary>
        private float TileWorldHeight =>
            _tileLocalHeight * Mathf.Abs(_tilesRoot != null ? _tilesRoot.lossyScale.y : 1f);

        /// <summary>设置世界滚动量。GameMgr 每帧把当前深度换算后传进来。</summary>
        public void SetScroll(float offsetWorld)
        {
            _scroll = offsetWorld;
            _worldRoot.position = _worldBasePos + Vector3.up * _scroll;
        }

        /// <summary>回到水面</summary>
        public void ResetScroll() => SetScroll(0f);

        /// <summary>
        /// 把背景贴图循环铺满视野
        /// </summary>
        public void Tick()
        {
            float total = _tileLocalHeight * _tiles.Length;
            float viewBottomLocal = ViewportUtil.WorldYToLocalY(_tilesRoot, ViewportUtil.ViewBottomWorldY(_cam));
            float viewTopLocal = ViewportUtil.WorldYToLocalY(_tilesRoot, ViewportUtil.ViewTopWorldY(_cam));
            float viewHeightLocal = viewTopLocal - viewBottomLocal;

            if (total < viewHeightLocal + _tileLocalHeight)
            {
                Debug.LogWarning(
                    $"[BGController] 背景贴图不够铺满视野：总高 {total:F2} < 需要 {viewHeightLocal + _tileLocalHeight:F2}，" +
                    "请增加 bgs 下的贴图数量或放大贴图。");
            }

            float start = viewBottomLocal - _tileLocalHeight * 0.5f;

            foreach (var tile in _tiles)
            {
                Vector3 lp = tile.localPosition;
                lp.y = start + Mathf.Repeat(lp.y - start, total);
                tile.localPosition = lp;
            }
        }

        private static Transform[] CollectTiles(Transform tilesRoot)
        {
            if (tilesRoot == null)
            {
                Debug.LogWarning("[BGController] 找不到背景贴图容器 bgs。");
                return new Transform[0];
            }

            int count = tilesRoot.childCount;
            Transform[] tiles = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                tiles[i] = tilesRoot.GetChild(i);
            }

            return tiles;
        }

        /// <summary>实测单张贴图的世界高度，避免把 Sprite 尺寸/PPU 写死在代码里。</summary>
        private void MeasureTiles()
        {
            float worldHeight = 0f;
            foreach (var tile in _tiles)
            {
                Renderer r = tile.GetComponent<Renderer>();
                if (r == null)
                {
                    r = tile.GetComponentInChildren<Renderer>();
                }

                worldHeight = Mathf.Max(worldHeight, r.bounds.size.y);
            }

            float parentScaleY = Mathf.Abs(_tilesRoot != null ? _tilesRoot.lossyScale.y : 1f);
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