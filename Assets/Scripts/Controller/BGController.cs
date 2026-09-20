using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【控制层】背景与世界滚动。由 GameMgr 创建并在每帧最后驱动。
    ///
    /// 滚动量是深度的纯函数（`GameConfig.WorldScrollAt`），
    /// 这里只负责"把世界根节点挪到位 + 把 bgs 下的贴图循环铺满视野"。
    /// </summary>
    public class BGController
    {
        private readonly Transform _worldRoot;
        private readonly Transform _tilesRoot;
        private readonly Transform[] _tiles;

        /// <summary>美术在场景里摆好的贴图位置。回到水面时要还原成这一套，见 ResetScroll()。</summary>
        private readonly Vector3[] _authoredLocal;

        private readonly Vector3 _worldBasePos;
        private readonly Camera _cam;

        private float _tileLocalHeight;
        private float _scroll; // 当前滚动值
        private bool _laidOut; // 贴图是否已经重排过（第一次真正滚动时才排）

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

            // 趁现在还没人动过它们，把美术摆好的位置记下来
            _authoredLocal = new Vector3[_tiles.Length];
            for (int i = 0; i < _tiles.Length; i++)
            {
                _authoredLocal[i] = _tiles[i] != null ? _tiles[i].localPosition : Vector3.zero;
            }

            MeasureTiles();
            // 这里**不要**调 LayoutTiles()：一上来就重排会破坏美术摆好的"头部背景"（开始画面）。
            // 推迟到第一次真正滚动时再排，见 Tick()。
        }

        /// <summary>设置世界滚动量。GameMgr 每帧把当前深度换算后传进来。</summary>
        public void SetScroll(float offsetWorld)
        {
            _scroll = offsetWorld;
            _worldRoot.position = _worldBasePos + Vector3.up * _scroll;
        }

        /// <summary>
        /// 回到水面：滚动归零，**并把贴图摆回美术摆好的位置**。
        /// 不还原的话，贴图还停在"深水那一屏"的排布上，而世界根节点已退回原点，
        /// 整片覆盖区跟着下移几十米 —— 开始画面上方就会露出天空盒。
        /// </summary>
        public void ResetScroll()
        {
            SetScroll(0f);
            RestoreAuthoredLayout();
        }

        /// <summary>把贴图摆回美术摆好的位置，并允许下一次滚动时重新按视口铺满。</summary>
        public void RestoreAuthoredLayout()
        {
            if (_tiles == null || _authoredLocal == null) return;

            int count = Mathf.Min(_tiles.Length, _authoredLocal.Length);
            for (int i = 0; i < count; i++)
            {
                if (_tiles[i] != null) _tiles[i].localPosition = _authoredLocal[i];
            }

            _laidOut = false;
        }

        /// <summary>把背景贴图循环铺满视野。由 GameMgr 每帧最后调用。</summary>
        public void Tick()
        {
            if (_tiles.Length == 0) return;

            // 还没开始下潜时不去动贴图位置，保留美术摆好的开始画面。
            // 但必须守住一件事：**这一屏得被贴图盖住** —— 世界根节点动过而贴图没跟着回来的话，
            // 画面上方就会露出天空盒，所以盖不住就重排一次。
            if (_scroll <= 0.0001f)
            {
                if (!CoversView()) LayoutTiles();
                return;
            }

            if (!_laidOut)
            {
                _laidOut = true;
                LayoutTiles();
            }

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

        /// <summary>
        /// 这一屏是否处处都有贴图盖着（只看纵向；贴图本来就比屏幕宽）。
        /// 采样几个点检查，几张拼起来盖住也算数。
        /// </summary>
        private bool CoversView()
        {
            if (_cam == null || _tileLocalHeight <= 0f) return true;

            float bottom = ViewportUtil.WorldYToLocalY(_tilesRoot, ViewportUtil.ViewBottomWorldY(_cam));
            float top = ViewportUtil.WorldYToLocalY(_tilesRoot, ViewportUtil.ViewTopWorldY(_cam));
            const int samples = 9;
            float half = _tileLocalHeight * 0.5f;

            for (int s = 0; s < samples; s++)
            {
                float y = Mathf.Lerp(bottom, top, s / (samples - 1f));
                bool covered = false;

                for (int i = 0; i < _tiles.Length; i++)
                {
                    if (_tiles[i] == null) continue;
                    float tileY = _tiles[i].localPosition.y;
                    if (y >= tileY - half && y <= tileY + half)
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered) return false;
            }

            return true;
        }

        #region tiles

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
            if (n == 0 || _tiles[0] == null) return;

            // 以**第一张**贴图作者摆好的位置为锚点，后面的依次往上排。
            // 不按"平均值居中"重排——那样第一张也会被挪走，
            // 而第一张正是开始画面里露出来的那块背景。
            float baseY = _tiles[0].localPosition.y;

            for (int i = 0; i < n; i++)
            {
                if (_tiles[i] == null) continue;

                Vector3 lp = _tiles[i].localPosition;
                lp.y = baseY + i * _tileLocalHeight;
                _tiles[i].localPosition = lp;
            }

            // 排完还是盖不住（说明整叠贴图被挪到别处去了），就整叠平移到视野中间，
            // 保证"回到水面"这一屏永远有背景，不会露天空盒。
            if (!CoversView()) CenterTilesOnView();
        }

        /// <summary>把整叠贴图纵向平移到视野正中。</summary>
        private void CenterTilesOnView()
        {
            if (_cam == null || _tiles.Length == 0 || _tiles[0] == null) return;

            float bottom = ViewportUtil.WorldYToLocalY(_tilesRoot, ViewportUtil.ViewBottomWorldY(_cam));
            float top = ViewportUtil.WorldYToLocalY(_tilesRoot, ViewportUtil.ViewTopWorldY(_cam));
            float viewCenter = (bottom + top) * 0.5f;
            float stackCenter = _tiles[0].localPosition.y + (_tiles.Length - 1) * _tileLocalHeight * 0.5f;
            float delta = viewCenter - stackCenter;

            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i] == null) continue;

                Vector3 lp = _tiles[i].localPosition;
                lp.y += delta;
                _tiles[i].localPosition = lp;
            }
        }

        #endregion
    }
}