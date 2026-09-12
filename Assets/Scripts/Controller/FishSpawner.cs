using System.Collections.Generic;
using Config;
using Core;
using Entities;
using Tool;
using UnityEngine;
using View;

namespace Controller
{
    /// <summary>
    /// 鱼群生成与回收。挂在 BG/fishs 上（对象池 + 深度区间过滤 + 权重随机）。
    ///
    /// 坐标约定：鱼挂在 fishRoot（BG/fishs）下，所以这里一律用"父节点局部坐标"表述生成位置——
    /// 先把屏幕上下边缘换算成世界 Y，再换算到 fishRoot 局部空间。
    /// 这样 BG 的缩放和滚动都不需要额外处理，永远生成在正确的屏幕外位置。
    ///
    /// 朝向约定：场景里美术手工摆好的那批鱼，它们的旋转就是"朝右"的正确姿态，
    /// 启动时会记录到 _faceRightTable，同类预制体运行时生成时直接复用，
    /// 避免"预制体没旋转、生成出来鱼头朝屏幕里面"的经典问题。
    /// </summary>
    public class FishSpawner : MonoBehaviour
    {
        [Header("鱼群父节点（留空 = 自己）")] [SerializeField]
        private Transform fishRoot;

        [Tooltip("鱼和鱼钩所在的世界 Z 平面。没有手工摆的鱼时，用它来推断生成的 Z")] [SerializeField]
        private float actionPlaneWorldZ = 0f;

        private readonly List<Fish> _actives = new();
        private readonly Dictionary<string, Quaternion> _faceRightTable = new();

        private GameConfig _cfg;
        private FishConfig _fishCfg;
        private Camera _cam;

        private float _timer;
        private float _spawnLocalZ;
        private int _nextId;
        private bool _ready;

        /// <summary>当前存活的鱼（不含已抓住的）。</summary>
        public IReadOnlyList<Fish> Actives => _actives;

        private void Awake()
        {
            // 自初始化：配置、相机、父节点全部自己搞定
            _cfg = GameConfig.Get();
            _fishCfg = FishConfig.Get();
            _cam = Camera.main;

            if (fishRoot == null)
            {
                fishRoot = transform;
            }

            if (_fishCfg == null || _fishCfg.Count == 0)
            {
                Debug.LogError("[FishSpawner] 鱼配置表为空，请先执行菜单 工具/捕鱼/1. 生成配置资源。");
                return;
            }

            _spawnLocalZ = ResolveSpawnLocalZ();
            AdoptSceneFish();
            _ready = true;
        }

        /// <summary>每帧驱动：先把已抓住的摘出去，再回收越界的，最后按需补新鱼。</summary>
        /// <param name="depth">当前下潜深度，用于挑选该深度才会出现的鱼种。</param>
        /// <param name="scrollDir">背景滚动方向：+1 世界上移（下潜，鱼从下方进场），-1 世界下移（上浮，鱼从上方进场），0 背景静止。</param>
        public void Tick(float dt, GameState state, float depth, int scrollDir)
        {
            if (!_ready)
            {
                return;
            }

            PruneCaught();
            RecycleMarked();

            if (state == GameState.Ready)
            {
                // 准备阶段：把鱼铺在视野里，水面看着才不空
                if (_actives.Count < Mathf.Max(3, _cfg.maxFishAlive / 2))
                {
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        SpawnInsideView(depth);
                        _timer = CurrentInterval(depth);
                    }
                }

                return;
            }

            if (state != GameState.CastingDown && state != GameState.ReelingUp)
            {
                return;
            }

            // 背景静止时（抛钩段 / 触底冲刺段）不补新鱼：
            // 这一段鱼在屏幕上是冻结的，硬塞新鱼会凭空出现。
            if (scrollDir == 0 || _actives.Count >= _cfg.maxFishAlive)
            {
                return;
            }

            _timer -= dt;
            if (_timer > 0f)
            {
                return;
            }

            SpawnAtEdge(depth, scrollDir);
            _timer = CurrentInterval(depth);
        }

        /// <summary>回收一批已抓住的鱼（重开一局时用）。</summary>
        public void RecycleCaught(IReadOnlyList<Fish> caught)
        {
            if (caught == null)
            {
                return;
            }

            for (int i = caught.Count - 1; i >= 0; i--)
            {
                Recycle(caught[i]);
            }
        }

        /// <summary>回收全部鱼（重开一局时用）。</summary>
        public void RecycleAll()
        {
            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                PushToPool(_actives[i]);
            }

            _actives.Clear();
            _timer = 0f;
        }

        // ------------------------------------------------------------------
        // 内部实现
        // ------------------------------------------------------------------

        /// <summary>
        /// 把已抓住的鱼从存活列表里摘出去（它们已经挂到鱼钩下面，位置不归这里管）。
        /// 由鱼钩打上 IsCaught 标记，这里被动响应，两边不需要互相持有引用。
        /// </summary>
        private void PruneCaught()
        {
            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                Fish fish = _actives[i];
                if (fish == null || fish.IsCaught)
                {
                    _actives.RemoveAt(i);
                }
            }
        }

        private void RecycleMarked()
        {
            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                Fish fish = _actives[i];
                if (fish == null || fish.View == null || fish.NeedRecycle)
                {
                    Recycle(fish);
                }
            }
        }

        private void Recycle(Fish fish)
        {
            if (fish == null)
            {
                return;
            }

            _actives.Remove(fish);
            PushToPool(fish);
        }

        private void PushToPool(Fish fish)
        {
            if (fish == null || fish.Data == null || fish.View == null)
            {
                return;
            }

            string poolName = fish.Data.prefabName;
            if (string.IsNullOrEmpty(poolName))
            {
                return;
            }

            fish.IsCaught = false;
            fish.NeedRecycle = false;

            GameObject go = fish.View.gameObject;
            fish.View.Unbind();

            EnsurePool(poolName);
            PoolMgr.Push(poolName, go);
        }

        private void SpawnAtEdge(float depth, int scrollDir)
        {
            FishData data = _fishCfg.PickByDepth(depth, Random.value);
            if (data == null)
            {
                return;
            }

            float halfHeight = ViewportUtil.HalfHeight(_cam);
            float camY = _cam != null ? _cam.transform.position.y : 0f;

            // 下潜：世界上移，新鱼从屏幕下方进入；上浮则相反
            float worldY = scrollDir > 0
                ? camY - halfHeight - _cfg.spawnMargin
                : camY + halfHeight + _cfg.spawnMargin;

            Spawn(data, worldY);
        }

        private void SpawnInsideView(float depth)
        {
            FishData data = _fishCfg.PickByDepth(depth, Random.value);
            if (data == null)
            {
                return;
            }

            float halfHeight = ViewportUtil.HalfHeight(_cam);
            float camY = _cam != null ? _cam.transform.position.y : 0f;
            float worldY = camY + Random.Range(-halfHeight * 0.85f, halfHeight * 0.85f);

            Spawn(data, worldY);
        }

        private void Spawn(FishData data, float worldY)
        {
            GameObject go = PopFromPool(data.prefabName);
            if (go == null)
            {
                return;
            }

            Transform t = go.transform;
            t.SetParent(fishRoot, false);

            float halfWidth = ViewportUtil.HalfWidth(_cam) * _cfg.fishEdgeRatio;
            float localLeft = ViewportUtil.WorldXToLocalX(fishRoot, -halfWidth, worldY);
            float localRight = ViewportUtil.WorldXToLocalX(fishRoot, halfWidth, worldY);
            if (localLeft > localRight)
            {
                (localLeft, localRight) = (localRight, localLeft);
            }

            float localX = Random.Range(localLeft, localRight);
            float localY = ViewportUtil.WorldYToLocalY(fishRoot, worldY);
            t.localPosition = new Vector3(localX, localY, _spawnLocalZ);

            FishView view = EnsureView(go);
            view.Init(GetFaceRight(data.prefabName));
            view.SetScale(data.scale <= 0f ? 1f : data.scale);

            Fish fish = new Fish();
            fish.ResetForSpawn(data, Random.value < 0.5f ? -1 : 1, ++_nextId);

            view.Bind(fish);
            view.SetDirection(fish.Direction);

            _actives.Add(fish);
        }

        private float CurrentInterval(float depth)
        {
            float t = _cfg.maxDepth <= 0f ? 0f : Mathf.Clamp01(depth / _cfg.maxDepth);
            return Mathf.Lerp(_cfg.spawnInterval, _cfg.spawnIntervalMin, t);
        }

        // --- 对象池 ---

        private GameObject PopFromPool(string prefabName)
        {
            EnsurePool(prefabName);
            return PoolMgr.Pop(prefabName);
        }

        private void EnsurePool(string prefabName)
        {
            if (PoolMgr.HasPool(prefabName))
            {
                return;
            }

            int warm = _cfg != null ? Mathf.Max(1, _cfg.poolWarmCount) : 3;
            PoolMgr.CreatePool(prefabName, warm, FishConfig.GetPrefabPath(prefabName));
        }

        // --- 朝向 ---

        private Quaternion? GetFaceRight(string prefabName)
        {
            if (!string.IsNullOrEmpty(prefabName) && _faceRightTable.TryGetValue(prefabName, out Quaternion q))
            {
                return q;
            }

            // 没记录就交给 FishView 自己的序列化默认值
            return null;
        }

        // --- 场景既有鱼的接管 ---

        /// <summary>
        /// 把场景里手工摆好的鱼接管成运行时鱼群，同时记录它们的朝向作为基准。
        /// 这样既不浪费美术摆位，也保证运行时生成的同类鱼朝向一致。
        /// </summary>
        private void AdoptSceneFish()
        {
            int count = fishRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = fishRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string prefabName = ResolvePrefabName(child.name);
                if (prefabName == null)
                {
                    continue;
                }

                FishData data = _fishCfg.GetByPrefabName(prefabName);
                if (data == null)
                {
                    continue;
                }

                if (!_faceRightTable.ContainsKey(prefabName))
                {
                    _faceRightTable[prefabName] = child.localRotation;
                }

                FishView view = EnsureView(child.gameObject);
                if (view == null)
                {
                    continue;
                }

                view.Init(GetFaceRight(prefabName));
                view.SetScale(data.scale <= 0f ? 1f : data.scale);

                Fish fish = new Fish();
                fish.ResetForSpawn(data, Random.value < 0.5f ? -1 : 1, ++_nextId);

                view.Bind(fish);
                view.SetDirection(fish.Direction);

                _actives.Add(fish);
            }
        }

        /// <summary>把物体名（可能带 (Clone) 或自定义后缀）解析成配置表里的预制体名。</summary>
        private string ResolvePrefabName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            string name = objectName.Replace("(Clone)", string.Empty).Trim();

            if (_fishCfg.GetByPrefabName(name) != null)
            {
                return name;
            }

            // 兼容 "Carp_01" 这类命名
            int cut = name.IndexOf('_');
            if (cut > 0)
            {
                name = name.Substring(0, cut);
                if (_fishCfg.GetByPrefabName(name) != null)
                {
                    return name;
                }
            }

            return null;
        }

        /// <summary>确定鱼应该出现的 Z 平面：优先沿用场景里手工摆好的鱼。</summary>
        private float ResolveSpawnLocalZ()
        {
            for (int i = 0; i < fishRoot.childCount; i++)
            {
                Transform child = fishRoot.GetChild(i);
                if (child != null && ResolvePrefabName(child.name) != null)
                {
                    return child.localPosition.z;
                }
            }

            // 兜底：和鱼钩对齐到同一个平面，保证两者能互相看见
            Vector3 probe = new Vector3(fishRoot.position.x, fishRoot.position.y, actionPlaneWorldZ);
            return fishRoot.InverseTransformPoint(probe).z;
        }

        private static FishView EnsureView(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            FishView view = go.GetComponent<FishView>();
            if (view == null)
            {
                view = go.AddComponent<FishView>();
            }

            return view;
        }
    }
}