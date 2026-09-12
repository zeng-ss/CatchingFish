using System.Collections.Generic;
using System.IO;
using Controller;
using Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using View;

namespace GameEditor
{
    /// <summary>
    /// 场景搭建器：把界面和引用一次性接好，让工程开箱即跑。
    ///
    /// 全部操作都是"只补不改"——已存在的物体不会被删除或挪动，
    /// 所以可以放心地在已经摆好美术的场景上反复执行。
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string HudCanvasName = "HUDCanvas";
        private const string ResultCanvasName = "ResultCanvas";
        private const string FishPrefabFolder = "Assets/Resources/Prefab/Fish";

        private static readonly Vector2 RefResolution = new Vector2(1920f, 1080f);

        // ------------------------------------------------------------------
        // 菜单
        // ------------------------------------------------------------------

        [MenuItem("工具/捕鱼/2. 搭建界面并接线", false, 10)]
        public static void BuildScene()
        {
            EnsureSceneLoaded();

            GameMgr gameMgr = EnsureGameMgr();
            if (gameMgr == null)
            {
                return;
            }

            // 旧版本把控制器全挂在 GameMgr 上，直接再跑一次会变成"两处各一份"重复驱动，先摘掉
            StripLegacyControllers(gameMgr.gameObject);

            Transform worldRoot = FindTransform("BG");
            Transform fishRoot = worldRoot != null ? worldRoot.Find("fishs") : null;
            Transform hookRoot = FindTransform("hook");

            // 控制器各自挂到对应的物体上，GameMgr 只保存引用（Inspector 里一眼能看到谁管谁）
            BGController bgCtrl = EnsureComponent<BGController>(worldRoot, "BG");
            EnsureComponent<FishSpawner>(fishRoot, "BG/fishs");
            FishController fishCtrl = EnsureComponent<FishController>(fishRoot, "BG/fishs");
            HookController hookCtrl = EnsureComponent<HookController>(hookRoot, "hook");

            EnsureHookView(hookRoot);

            BuildHudCanvas();
            BuildResultCanvas();

            WireGameMgr(gameMgr, bgCtrl, hookCtrl, fishCtrl);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[捕鱼] 场景搭建完成：控制器已分发到 BG / hook / fishs 上，HUD 与结算界面已生成，GameMgr 引用已接好。");
            ReportSceneWiring();
        }

        [MenuItem("工具/捕鱼/3. 给鱼预制体补 FishView 并烘焙朝向", false, 11)]
        public static void BakeFishPrefabs()
        {
            EnsureSceneLoaded();

            Dictionary<string, Quaternion> facing = CollectSceneFacing();
            if (facing.Count == 0)
            {
                Debug.LogWarning("[捕鱼] 场景 BG/fishs 下没找到可识别的鱼，朝向将使用 FishView 的默认值 (0,-90,0)。");
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { FishPrefabFolder });
            if (guids.Length == 0)
            {
                Debug.LogError($"[捕鱼] {FishPrefabFolder} 下没有预制体。");
                return;
            }

            int touched = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string prefabName = Path.GetFileNameWithoutExtension(path);

                    EditorUtility.DisplayProgressBar("烘焙鱼预制体", prefabName, (float)i / guids.Length);

                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        FishView view = root.GetComponent<FishView>();
                        bool changed = false;

                        if (view == null)
                        {
                            view = root.AddComponent<FishView>();
                            changed = true;
                        }

                        if (facing.TryGetValue(prefabName, out Quaternion q))
                        {
                            view.SetFaceRightEuler(q.eulerAngles);
                            changed = true;
                        }

                        if (changed)
                        {
                            EditorUtility.SetDirty(view);
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            touched++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[捕鱼] 已更新 {touched} 个鱼预制体（补 FishView / 烘焙朝向）。" +
                      "如果发现鱼游动方向与身体朝向相反，把对应预制体上 FishView 的 invertFacing 勾上即可。");
        }

        [MenuItem("工具/捕鱼/4. 检查场景接线", false, 12)]
        public static void ReportSceneWiring()
        {
            GameMgr gameMgr = Object.FindObjectOfType<GameMgr>();
            Transform worldRoot = FindTransform("BG");
            Transform fishRoot = worldRoot != null ? worldRoot.Find("fishs") : null;
            Transform hookRoot = FindTransform("hook");

            Debug.Log(
                "[捕鱼] 场景检查：\n" +
                $"- GameMgr                       : {(gameMgr != null ? "OK" : "缺失！")}\n" +
                $"- BG                            : {(worldRoot != null ? "OK" : "缺失！")}\n" +
                $"- BG/bgs                        : {(worldRoot != null && worldRoot.Find("bgs") != null ? "OK" : "缺失！")}\n" +
                $"- BG/fishs                      : {(fishRoot != null ? "OK" : "缺失！")}\n" +
                $"- hook                          : {(hookRoot != null ? "OK" : "缺失！")}\n" +
                "- 控制器挂载位置：\n" +
                $"    BGController   -> BG          : {Describe(worldRoot, typeof(BGController))}\n" +
                $"    FishSpawner    -> BG/fishs    : {Describe(fishRoot, typeof(FishSpawner))}\n" +
                $"    FishController -> BG/fishs    : {Describe(fishRoot, typeof(FishController))}\n" +
                $"    HookController -> hook        : {Describe(hookRoot, typeof(HookController))}\n" +
                $"    HookView       -> hook        : {Describe(hookRoot, typeof(HookView))}\n" +
                $"- HUDCanvas                     : {(Object.FindObjectOfType<HUDView>() != null ? "OK" : "缺失，可执行菜单 2")}\n" +
                $"- ResultCanvas                  : {(Object.FindObjectOfType<ResultPanel>() != null ? "OK" : "缺失，可执行菜单 2")}");
        }

        private static string Describe(Transform host, System.Type type)
        {
            if (host == null)
            {
                return "宿主缺失！";
            }

            return host.GetComponent(type) != null ? "OK" : "未挂载";
        }

        // ------------------------------------------------------------------
        // 装配
        // ------------------------------------------------------------------

        /// <summary>
        /// 保证主场景已经打开。
        /// 菜单调用时场景本来就是开的；这条主要是为了支持命令行 -executeMethod 批处理调用。
        /// </summary>
        private static void EnsureSceneLoaded()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded && Object.FindObjectOfType<GameMgr>() != null)
            {
                return;
            }

            string scenePath = "Assets/Scenes/GameScene.unity";
            if (File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath);
                Debug.Log($"[捕鱼] 已打开场景 {scenePath}。");
            }
        }

        private static GameMgr EnsureGameMgr()
        {
            GameMgr gameMgr = Object.FindObjectOfType<GameMgr>();
            if (gameMgr != null)
            {
                return gameMgr;
            }

            GameObject go = new GameObject("GameMgr");
            gameMgr = go.AddComponent<GameMgr>();
            Debug.Log("[捕鱼] 场景里没有 GameMgr，已新建一个。");
            return gameMgr;
        }

        /// <summary>把组件挂到它"该在"的物体上；已经挂过就复用。</summary>
        private static T EnsureComponent<T>(Transform host, string label) where T : Component
        {
            if (host == null)
            {
                Debug.LogError($"[捕鱼] 找不到物体 {label}，{typeof(T).Name} 没能挂上。");
                return null;
            }

            T comp = host.GetComponent<T>();
            if (comp == null)
            {
                comp = host.gameObject.AddComponent<T>();
                Debug.Log($"[捕鱼] 已在 {label} 上挂载 {typeof(T).Name}。");
            }

            return comp;
        }

        /// <summary>
        /// 兼容旧版本：控制器曾经全部挂在 GameMgr 物体上。
        /// 迁移到"各自挂在自己的物体上"之后，那些残留会造成重复驱动，这里统一摘掉。
        /// </summary>
        private static void StripLegacyControllers(GameObject gameMgrObject)
        {
            int removed = 0;
            removed += RemoveIfPresent<BGController>(gameMgrObject);
            removed += RemoveIfPresent<HookController>(gameMgrObject);
            removed += RemoveIfPresent<FishController>(gameMgrObject);
            removed += RemoveIfPresent<FishSpawner>(gameMgrObject);

            if (removed > 0)
            {
                Debug.Log($"[捕鱼] 已从 GameMgr 物体上摘掉 {removed} 个旧版控制器" +
                          "（它们现在各自挂在自己的物体上）。");
            }
        }

        private static int RemoveIfPresent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                return 0;
            }

            Object.DestroyImmediate(comp);
            return 1;
        }

        private static void EnsureHookView(Transform hookRoot)
        {
            if (hookRoot == null)
            {
                Debug.LogWarning("[捕鱼] 场景里没有 hook 节点，鱼钩相关表现无法接线。");
                return;
            }

            HookView view = hookRoot.GetComponent<HookView>();
            if (view == null)
            {
                view = hookRoot.gameObject.AddComponent<HookView>();
            }

            EditorUtility.SetDirty(view);
        }

        private static void WireGameMgr(
            GameMgr gameMgr,
            BGController bgCtrl,
            HookController hookCtrl,
            FishController fishCtrl)
        {
            SerializedObject so = new SerializedObject(gameMgr);
            SetRef(so, "_bgCtrl", bgCtrl);
            SetRef(so, "_hookCtrl", hookCtrl);
            SetRef(so, "_fishCtrl", fishCtrl);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty p = so.FindProperty(propertyName);
            if (p == null)
            {
                Debug.LogWarning($"[捕鱼] {so.targetObject.GetType().Name} 上找不到字段 {propertyName}，跳过。");
                return;
            }

            p.objectReferenceValue = value;
        }

        private static Transform FindTransform(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private static HUDView BuildHudCanvas()
        {
            Canvas canvas = ReuseOrCreateCanvas(HudCanvasName, 0);
            Transform root = canvas.transform;

            EnsureFontBootstrap(root);

            // --- 血条 ---
            Image hpBg = GetOrCreateImage(root, "HpBarBg", new Color(0.06f, 0.09f, 0.14f, 0.8f), GetBackgroundSprite());
            SetAnchored(hpBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(420f, 36f));

            Image hpFill = GetOrCreateImage(hpBg.transform, "HpBarFill", new Color(0.92f, 0.28f, 0.28f), GetUiSprite());
            Stretch(hpFill.rectTransform, 4f);
            MakeFilled(hpFill);

            Text hpText = GetOrCreateText(root, "HpText", "HP 100/100", 30, TextAnchor.MiddleLeft, Color.white);
            SetAnchored(hpText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -84f), new Vector2(420f, 40f));

            // --- 分数 / 渔获 ---
            Text scoreText = GetOrCreateText(root, "ScoreText", "SCORE 0", 52, TextAnchor.MiddleRight, Color.white);
            SetAnchored(scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -36f), new Vector2(560f, 64f));

            Text catchText = GetOrCreateText(root, "CatchText", "FISH 0/6", 36, TextAnchor.MiddleRight,
                new Color(0.86f, 0.92f, 1f));
            SetAnchored(catchText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -106f), new Vector2(560f, 48f));

            // --- 深度 ---
            Image depthBg = GetOrCreateImage(root, "DepthBarBg", new Color(0.06f, 0.09f, 0.14f, 0.8f), GetBackgroundSprite());
            SetAnchored(depthBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(640f, 22f));

            Image depthFill = GetOrCreateImage(depthBg.transform, "DepthBarFill", new Color(0.32f, 0.76f, 1f), GetUiSprite());
            Stretch(depthFill.rectTransform, 3f);
            MakeFilled(depthFill);

            Text depthText = GetOrCreateText(root, "DepthText", "DEPTH 0m", 30, TextAnchor.MiddleCenter,
                new Color(0.86f, 0.94f, 1f));
            SetAnchored(depthText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 184f), new Vector2(420f, 40f));

            // --- 操作提示 ---
            RectTransform hintRoot = GetOrCreateRect(root, "HintRoot");
            SetAnchored(hintRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(1920f, 200f));

            Text hintText = GetOrCreateText(hintRoot, "HintText", "长按鼠标开始下潜", 34, TextAnchor.LowerCenter,
                new Color(1f, 1f, 1f, 0.92f));
            SetAnchored(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(1500f, 56f));

            // --- HUDView ---
            HUDView hud = canvas.GetComponent<HUDView>();
            if (hud == null)
            {
                hud = canvas.gameObject.AddComponent<HUDView>();
            }

            SerializedObject so = new SerializedObject(hud);
            SetRef(so, "hpFill", hpFill);
            SetRef(so, "hpText", hpText);
            SetRef(so, "scoreText", scoreText);
            SetRef(so, "depthFill", depthFill);
            SetRef(so, "depthText", depthText);
            SetRef(so, "catchText", catchText);
            SetRef(so, "hintText", hintText);
            SetRef(so, "hintRoot", hintRoot.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        // ------------------------------------------------------------------
        // 结算界面
        // ------------------------------------------------------------------

        private static ResultPanel BuildResultCanvas()
        {
            Canvas canvas = ReuseOrCreateCanvas(ResultCanvasName, 100);
            Transform root = canvas.transform;

            EnsureFontBootstrap(root);

            // 全屏遮罩，同时作为 panelRoot
            Image overlayImage = GetOrCreateImage(root, "ResultRoot", new Color(0f, 0f, 0f, 0.72f), null);
            overlayImage.type = Image.Type.Simple;
            overlayImage.raycastTarget = true;
            RectTransform overlay = overlayImage.rectTransform;
            Stretch(overlay);

            Image panelImage = GetOrCreateImage(overlay, "Panel", new Color(0.07f, 0.15f, 0.25f, 0.97f), GetUiSprite());
            RectTransform panel = panelImage.rectTransform;
            SetAnchored(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(960f, 660f));

            Text titleText = GetOrCreateText(panel, "TitleText", "满载而归！", 56, TextAnchor.MiddleCenter, Color.white);
            SetAnchored(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -50f), new Vector2(880f, 80f));

            Text scoreText = GetOrCreateText(panel, "ScoreText", "总分 0", 44, TextAnchor.MiddleCenter,
                new Color(1f, 0.86f, 0.35f));
            SetAnchored(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(880f, 60f));

            Text detailText = GetOrCreateText(panel, "DetailText", string.Empty, 30, TextAnchor.UpperLeft,
                new Color(0.9f, 0.95f, 1f));
            RectTransform detailRt = detailText.rectTransform;
            detailRt.anchorMin = Vector2.zero;
            detailRt.anchorMax = Vector2.one;
            detailRt.offsetMin = new Vector2(90f, 150f);
            detailRt.offsetMax = new Vector2(-90f, -220f);
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailText.verticalOverflow = VerticalWrapMode.Overflow;
            detailText.lineSpacing = 1.15f;

            Button restartButton = GetOrCreateButton(panel, "RestartButton", "再来一局", new Vector2(0f, 52f),
                new Vector2(320f, 80f));

            ResultPanel resultPanel = canvas.GetComponent<ResultPanel>();
            if (resultPanel == null)
            {
                resultPanel = canvas.gameObject.AddComponent<ResultPanel>();
            }

            SerializedObject so = new SerializedObject(resultPanel);
            SetRef(so, "panelRoot", overlay.gameObject);
            SetRef(so, "titleText", titleText);
            SetRef(so, "scoreText", scoreText);
            SetRef(so, "detailText", detailText);
            SetRef(so, "restartButton", restartButton);
            so.ApplyModifiedPropertiesWithoutUndo();

            return resultPanel;
        }

        // ------------------------------------------------------------------
        // UI 工具
        // ------------------------------------------------------------------

        private static Canvas ReuseOrCreateCanvas(string name, int sortingOrder)
        {
            GameObject existing = GameObject.Find(name);
            Canvas canvas;

            if (existing != null && existing.GetComponent<Canvas>() != null)
            {
                canvas = existing.GetComponent<Canvas>();
            }
            else
            {
                canvas = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
                    .GetComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        private static void EnsureFontBootstrap(Transform canvasRoot)
        {
            if (canvasRoot.GetComponent<DynamicFontBootstrap>() == null)
            {
                canvasRoot.gameObject.AddComponent<DynamicFontBootstrap>();
            }
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>按名字复用已有节点，没有才新建——保证搭建器可以反复执行而不产生重复 UI。</summary>
        private static RectTransform GetOrCreateRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform rt)
            {
                return rt;
            }

            return CreateRect(parent, name);
        }

        private static Image GetOrCreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            RectTransform rt = GetOrCreateRect(parent, name);
            Image image = rt.GetComponent<Image>();
            if (image == null)
            {
                image = rt.gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static Text GetOrCreateText(Transform parent, string name, string content, int fontSize,
            TextAnchor anchor, Color color)
        {
            RectTransform rt = GetOrCreateRect(parent, name);
            Text text = rt.GetComponent<Text>();
            if (text == null)
            {
                text = rt.gameObject.AddComponent<Text>();
            }

            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button GetOrCreateButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Vector2 size)
        {
            RectTransform rt = GetOrCreateRect(parent, name);
            Image image = rt.GetComponent<Image>();
            if (image == null)
            {
                image = rt.gameObject.AddComponent<Image>();
            }

            image.sprite = GetUiSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.20f, 0.56f, 0.90f, 1f);
            image.raycastTarget = true;

            Button button = rt.GetComponent<Button>();
            if (button == null)
            {
                button = rt.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = image;

            Text text = GetOrCreateText(rt, "Label", label, 34, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);

            SetAnchored(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPos, size);

            return button;
        }

        private static void SetAnchored(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>把 Image 设成横向填充条（血条 / 深度条）。</summary>
        private static void MakeFilled(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;

            if (image.sprite == null)
            {
                Debug.LogWarning($"[捕鱼] {image.name} 没有 Sprite，填充条不会生效（Filled 模式需要 Sprite）。");
            }
        }

        private static Font GetDefaultFont()
        {
            // Unity 2022 起内置 Arial 已换成 LegacyRuntime.ttf，字体本身不含中文，
            // 运行时由 DynamicFontBootstrap 替换成系统字体，这里只是让编辑器里能预览。
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite GetUiSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static Sprite GetBackgroundSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        }

        // ------------------------------------------------------------------
        // 朝向
        // ------------------------------------------------------------------

        /// <summary>
        /// 从场景 BG/fishs 下手工摆好的鱼身上读取旋转，作为"朝右"的基准。
        /// 美术在场景里调好的姿态，直接复用到运行时生成的同类鱼上。
        /// </summary>
        private static Dictionary<string, Quaternion> CollectSceneFacing()
        {
            Dictionary<string, Quaternion> table = new Dictionary<string, Quaternion>();

            Transform worldRoot = FindTransform("BG");
            Transform fishRoot = worldRoot != null ? worldRoot.Find("fishs") : null;
            if (fishRoot == null)
            {
                return table;
            }

            for (int i = 0; i < fishRoot.childCount; i++)
            {
                Transform child = fishRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string key = child.name.Replace("(Clone)", string.Empty).Trim();
                if (!table.ContainsKey(key))
                {
                    table[key] = child.localRotation;
                }
            }

            return table;
        }
    }
}
