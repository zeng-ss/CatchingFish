using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using View;

namespace GameEditor
{
    /// <summary>
    /// 结算界面的美术资源生成器。
    ///
    /// 这个工程手头没有鱼的图片、也没有 UI 图，所以干脆**用代码把资源造出来**：
    ///   6. 把 Resources/Prefab/Fish 下的模型用一台临时正交相机侧拍成透明底 PNG
    ///      —— 是真实模型渲染图，不是手画的，九种鱼自动各有一张
    ///   7. 程序化生成面板底 / 卡片 / 按钮 / 色条 / 柔光五张 UI 图
    ///      —— 全部"白色 + alpha"，运行时用 Image.color 上色，一张图能当任意颜色用
    ///   8. 重建 ResultItem 预制体（卡片 + 色条 + 柔光 + 图标 + 名字 + 得分）
    ///   9. 把已有结算面板美化一遍，并把 ResultPanel 的引用补齐
    ///
    /// 全部可重复执行；输出都落在 Assets/Resources 下（跑完记得让 Unity 编译一下）。
    /// </summary>
    public static class ResultAssetGenerator
    {
        private const string FishPrefabFolder = "Assets/Resources/Prefab/Fish";
        private const string IconFolder = "Assets/Resources/Icon/Fish";
        private const string UiFolder = "Assets/Resources/UI";
        private const string ItemPrefabPath = "Assets/Resources/Prefab/ResultItem.prefab";

        private const int IconWidth = 256;
        private const int IconHeight = 160;

        // ==================================================================
        // 6. 烘焙鱼的结算图标
        // ==================================================================

        [MenuItem("工具/捕鱼/6. 烘焙鱼的结算图标", false, 14)]
        public static void BakeFishIcons()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { FishPrefabFolder });
            if (guids.Length == 0)
            {
                Debug.LogError($"[结算美术] {FishPrefabFolder} 下没有预制体。");
                return;
            }

            EnsureFolder("Assets/Resources/Icon");
            EnsureFolder(IconFolder);

            // 临时舞台：一个附加场景 + 一盏灯 + 一台正交相机。
            // 用独立场景而不是当前场景，跑完直接 CloseScene 丢掉，不会把你正在编辑的场景弄脏。
            Scene stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            GameObject lightGo = new GameObject("~IconLight");
            EditorSceneManager.MoveGameObjectToScene(lightGo, stage);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(38f, -35f, 12f);

            GameObject camGo = new GameObject("~IconCamera");
            EditorSceneManager.MoveGameObjectToScene(camGo, stage);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透明底
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.enabled = false; // 只手动 Render，不参与正常渲染

            int done = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string name = Path.GetFileNameWithoutExtension(path);

                    EditorUtility.DisplayProgressBar("烘焙鱼图标", name, (float)i / guids.Length);

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        continue;
                    }

                    if (RenderIcon(prefab, cam, stage, $"{IconFolder}/{name}.png"))
                    {
                        done++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.CloseScene(stage, true); // 不保存，整个临时场景扔掉
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[结算美术] 已烘焙 {done} 张鱼的结算图标 → {IconFolder}/（透明底 {IconWidth}×{IconHeight}，相机侧拍）。");
        }

        private static bool RenderIcon(GameObject prefab, Camera cam, Scene stage, string pngPath)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage);
            if (go == null)
            {
                return false;
            }

            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                // 别用绑定姿势：把默认状态采到一半，看起来就是在游
                Animator animator = go.GetComponentInChildren<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    animator.Play(0, 0, 0.5f);
                    animator.Update(0.02f);
                }

                LODGroup lod = go.GetComponentInChildren<LODGroup>();
                if (lod != null)
                {
                    lod.ForceLOD(0); // 只留 LOD0，别把四个 LOD 一起渲了
                }

                if (!TryGetBounds(go, out Bounds bounds))
                {
                    Debug.LogWarning($"[结算美术] {prefab.name} 没有任何 Renderer，跳过。");
                    return false;
                }

                float aspect = (float)IconWidth / IconHeight;
                float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.15f;

                cam.orthographicSize = Mathf.Max(0.01f, halfHeight);
                // 模型朝向 +Z，所以从 +X 侧拍；这样鱼头朝画面右侧
                cam.transform.position = bounds.center + Vector3.right * (bounds.size.magnitude + 1f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = bounds.size.magnitude + 2f;

                rt = new RenderTexture(IconWidth, IconHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "FishIconRT",
                    antiAliasing = 4,
                };
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(IconWidth, IconHeight, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0f, 0f, IconWidth, IconHeight), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                ConfigureImporter(pngPath, true);
                return true;
            }
            finally
            {
                cam.targetTexture = null;
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }

                if (tex != null)
                {
                    Object.DestroyImmediate(tex);
                }

                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any && bounds.size.sqrMagnitude > 1e-6f;
        }

        // ==================================================================
        // 7. 生成 UI 图片（白色 + alpha，运行时上色）
        // ==================================================================

        [MenuItem("工具/捕鱼/7. 生成结算界面用的 UI 图片", false, 15)]
        public static void GenerateUiArt()
        {
            EnsureFolder("Assets/Resources/UI");

            WriteSprite("PanelBg", 960, 660, PanelPixel, false);
            WriteSprite("Card", 200, 200, CardPixel, true);
            WriteSprite("Button", 320, 84, ButtonPixel, true);
            WriteSprite("AccentLine", 480, 8, AccentPixel, true);
            WriteSprite("Glow", 192, 192, GlowPixel, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[结算美术] 已生成 5 张 UI 图片 → {UiFolder}/（白色+alpha，靠 Image.color 上色）。");
        }

        private static Color PanelPixel(float u, float v)
        {
            const float w = 960f, h = 660f, r = 26f;

            float cov = RoundedCoverage(u, v, r, w, h);
            if (cov <= 0.001f)
            {
                return new Color(1f, 1f, 1f, 0f);
            }

            bool border = RoundedCoverage(u, v, r - 3f, w - 6f, h - 6f) < 0.5f;
            float shade = Mathf.Lerp(0.58f, 1f, v); // 下暗上亮
            if (v > 1f - 3f / h)
            {
                shade = 1.4f; // 顶部高光条
            }

            if (border)
            {
                shade *= 1.45f; // 描边
            }

            float c = Mathf.Clamp(shade, 0f, 1.6f);
            return new Color(c, c, c, cov);
        }

        private static Color CardPixel(float u, float v)
        {
            const float w = 200f, h = 200f, r = 22f;

            float cov = RoundedCoverage(u, v, r, w, h);
            if (cov <= 0.001f)
            {
                return new Color(1f, 1f, 1f, 0f);
            }

            bool border = RoundedCoverage(u, v, r - 2f, w - 4f, h - 4f) < 0.5f;
            float shade = Mathf.Lerp(0.70f, 1.05f, v);
            if (v > 1f - 3f / h)
            {
                shade = 1.5f;
            }

            if (border)
            {
                shade *= 1.5f;
            }

            float c = Mathf.Clamp(shade, 0f, 1.6f);
            return new Color(c, c, c, cov);
        }

        private static Color ButtonPixel(float u, float v)
        {
            const float w = 320f, h = 84f, r = 24f;

            float cov = RoundedCoverage(u, v, r, w, h);
            if (cov <= 0.001f)
            {
                return new Color(1f, 1f, 1f, 0f);
            }

            bool border = RoundedCoverage(u, v, r - 2f, w - 4f, h - 4f) < 0.5f;
            float shade = Mathf.Lerp(1.05f, 0.72f, v); // 按钮是上亮下暗
            if (border)
            {
                shade *= 1.3f;
            }

            float c = Mathf.Clamp(shade, 0f, 1.6f);
            return new Color(c, c, c, cov);
        }

        private static Color AccentPixel(float u, float v)
        {
            float a = Mathf.Sin(u * Mathf.PI);          // 两端淡出
            float soft = Mathf.Sin(v * Mathf.PI);       // 上下柔一点
            return new Color(1f, 1f, 1f, a * Mathf.Lerp(0.6f, 1f, soft));
        }

        private static Color GlowPixel(float u, float v)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
            return new Color(1f, 1f, 1f, a);
        }

        /// <summary>圆角矩形的覆盖度（0~1），带 1px 抗锯齿。</summary>
        private static float RoundedCoverage(float u, float v, float radius, float w, float h)
        {
            float px = u * w - 0.5f;
            float py = v * h - 0.5f;
            float dx = Mathf.Max(radius - px, px - (w - radius), 0f);
            float dy = Mathf.Max(radius - py, py - (h - radius), 0f);
            float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(0.5f - d);
        }

        private static void WriteSprite(string name, int w, int h, System.Func<float, float, Color> shade, bool uncompressed)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = shade((x + 0.5f) / w, (y + 0.5f) / h);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            string path = $"{UiFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path, uncompressed);
        }

        private static void ConfigureImporter(string path, bool uncompressed)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = uncompressed
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        // ==================================================================
        // 8. 重建 ResultItem 预制体
        // ==================================================================

        [MenuItem("工具/捕鱼/8. 重建结算条目预制体 ResultItem", false, 16)]
        public static void BuildResultItemPrefab()
        {
            EnsureFolder("Assets/Resources/Prefab");

            GameObject root = new GameObject("ResultItem", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 200f);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // 卡片底
            Image card = MakeImage(rt, "Card", LoadSprite("Card"), new Color(0.11f, 0.18f, 0.30f, 0.97f));
            Stretch(card.rectTransform);

            // 图标后面的柔光
            Image glow = MakeImage(rt, "Glow", LoadSprite("Glow"), new Color(0.45f, 0.80f, 1f, 0.35f));
            SetAnchored(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 12f), new Vector2(170f, 130f));

            // 鱼图标（运行时由 ResultItem 按鱼种填 sprite）
            Image icon = MakeImage(rt, "Icon", null, Color.white);
            icon.preserveAspect = true;
            SetAnchored(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(150f, 96f));

            // 顶部主题色条
            Image accent = MakeImage(rt, "Accent", LoadSprite("AccentLine"), new Color(0.45f, 0.80f, 1f, 0.95f));
            SetAnchored(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(-56f, 8f));

            // 名字 / 得分
            Text nameTxt = MakeText(rt, "NameText", "鲤鱼", 26, new Color(0.95f, 0.98f, 1f));
            SetAnchored(nameTxt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 46f), new Vector2(180f, 32f));

            Text scoreTxt = MakeText(rt, "ScoreText", "+0", 24, new Color(1f, 0.85f, 0.45f));
            SetAnchored(scoreTxt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(180f, 28f));

            ResultItem item = root.AddComponent<ResultItem>();
            SerializedObject so = new SerializedObject(item);
            SetRef(so, "cardImg", card);
            SetRef(so, "accentImg", accent);
            SetRef(so, "glowImg", glow);
            SetRef(so, "iconImg", icon);
            SetRef(so, "nameTxt", nameTxt);
            SetRef(so, "scoreTxt", scoreTxt);
            SetRef(so, "canvasGroup", group);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[结算美术] 已重建 {ItemPrefabPath}（卡片 + 柔光 + 图标 + 色条 + 名字 + 得分）。" +
                      "如果先跑的是菜单 7，卡片/色条/柔光会用到生成的图片。");
        }

        // ==================================================================
        // 9. 美化结算面板 + 补接线
        // ==================================================================

        [MenuItem("工具/捕鱼/9. 美化结算面板并补接线", false, 17)]
        public static void BeautifyResultPanel()
        {
            GameObject canvasGo = FindRoot("ResultCanvas");
            if (canvasGo == null)
            {
                Debug.LogError("[结算美术] 场景里没有 ResultCanvas，先跑菜单 2。");
                return;
            }

            Transform canvas = canvasGo.transform;
            Transform panel = FindDeep(canvas, "Panel");
            Transform mask = FindDeep(canvas, "Mask");
            Transform title = FindDeep(canvas, "TitleText");
            Transform score = FindDeep(canvas, "ScoreText");
            Transform restart = FindDeep(canvas, "RestartButton");
            Transform content = FindDeep(canvas, "Content");

            if (panel == null)
            {
                Debug.LogError("[结算美术] ResultCanvas 下找不到 Panel。");
                return;
            }

            // --- 面板底图 ---
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                Sprite bg = LoadSprite("PanelBg");
                if (bg != null)
                {
                    panelImage.sprite = bg;
                    panelImage.type = Image.Type.Simple;
                }

                panelImage.color = new Color(0.10f, 0.17f, 0.29f, 0.98f);
            }

            // --- 标题 ---
            if (title != null)
            {
                Text t = title.GetComponent<Text>();
                if (t != null)
                {
                    t.fontSize = 58;
                    t.color = new Color(1f, 0.94f, 0.72f);
                    t.alignment = TextAnchor.MiddleCenter;
                }
            }

            // 标题下的强调线
            Image accent = GetOrCreateImage(panel, "TitleAccent", LoadSprite("AccentLine"), new Color(0.45f, 0.8f, 1f, 0.9f));
            SetAnchored(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -96f), new Vector2(420f, 8f));

            // 副标题：本次渔获 N 条
            Text summary = GetOrCreateText(panel, "SummaryText", "本次渔获 0 条", 28, new Color(0.72f, 0.86f, 1f));
            SetAnchored(summary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(700f, 36f));

            // 清单表头
            Text listHeader = GetOrCreateText(panel, "ListHeader", "渔获清单", 26, new Color(0.65f, 0.78f, 0.95f));
            SetAnchored(listHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(72f, -176f), new Vector2(400f, 32f));
            listHeader.alignment = TextAnchor.MiddleLeft;

            // --- 遮罩 ---
            if (mask != null)
            {
                Image maskImage = mask.GetComponent<Image>();
                if (maskImage != null)
                {
                    maskImage.color = new Color(0f, 0f, 0f, 0.72f);
                }
            }

            // --- 按钮换图 ---
            if (restart != null)
            {
                Image buttonImage = restart.GetComponent<Image>();
                if (buttonImage != null)
                {
                    Sprite btn = LoadSprite("Button");
                    if (btn != null)
                    {
                        buttonImage.sprite = btn;
                        buttonImage.type = Image.Type.Simple;
                    }

                    buttonImage.color = new Color(0.26f, 0.60f, 0.95f);
                }

                Text label = restart.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 34;
                    label.color = Color.white;
                }
            }

            // --- 补 ResultPanel 的接线（脚本可能挂在 ResultRoot 或 ResultCanvas 上）---
            ResultPanel resultPanel = Object.FindObjectOfType<ResultPanel>(true);
            if (resultPanel == null)
            {
                Debug.LogWarning("[结算美术] 场景里没有 ResultPanel，跳过接线。");
            }
            else
            {
                SerializedObject so = new SerializedObject(resultPanel);
                SetRef(so, "mask", mask != null ? mask.gameObject : null);
                SetRef(so, "content", content);
                SetRef(so, "titleText", title != null ? title.GetComponent<Text>() : null);
                SetRef(so, "scoreText", score != null ? score.GetComponent<Text>() : null);
                SetRef(so, "restartButton", restart != null ? restart.GetComponent<Button>() : null);
                SetRef(so, "summaryText", summary);

                SerializedProperty path = so.FindProperty("itemPath");
                if (path != null)
                {
                    path.stringValue = "Prefab/ResultItem";
                }

                // 脚本挂在 ResultRoot 上时，panelRoot 留空即可（= 它自己）；
                // 挂在 Canvas 上时必须指到 ResultRoot，否则会把整块 Canvas 关掉
                SerializedProperty panelRootProp = so.FindProperty("panelRoot") ?? so.FindProperty("_panelRoot");
                if (panelRootProp != null && resultPanel.GetComponent<Canvas>() != null && panelRootProp.objectReferenceValue == null)
                {
                    Transform resultRoot = FindDeep(canvas, "ResultRoot");
                    if (resultRoot != null)
                    {
                        panelRootProp.objectReferenceValue = resultRoot.gameObject;
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(resultPanel);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[结算美术] 结算面板已美化：面板底图 / 标题强调线 / 副标题 / 清单表头 / 按钮换图，ResultPanel 引用已补齐。");
        }

        // ==================================================================
        // 工具
        // ==================================================================

        private static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{name}.png");
            if (sprite == null)
            {
                Debug.LogWarning($"[结算美术] 找不到 {UiFolder}/{name}.png，先跑菜单 7 生成 UI 图片。");
            }

            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static GameObject FindRoot(string name)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static Image GetOrCreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            Transform existing = parent.Find(name);
            RectTransform rt;
            if (existing is RectTransform existingRect)
            {
                rt = existingRect;
            }
            else
            {
                rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(parent, false);
            }

            Image image = rt.GetComponent<Image>();
            if (image == null)
            {
                image = rt.gameObject.AddComponent<Image>();
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            rt.localScale = Vector3.one;
            return image;
        }

        private static Text GetOrCreateText(Transform parent, string name, string content, int size, Color color)
        {
            Transform existing = parent.Find(name);
            RectTransform rt;
            if (existing is RectTransform existingRect)
            {
                rt = existingRect;
            }
            else
            {
                rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(parent, false);
            }

            Text text = rt.GetComponent<Text>();
            if (text == null)
            {
                text = rt.gameObject.AddComponent<Text>();
            }

            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            rt.localScale = Vector3.one;
            return text;
        }

        private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(Transform parent, string name, string content, int size, Color color)
        {
            RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Text text = rt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
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

        private static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty p = so.FindProperty(propertyName) ?? so.FindProperty("_" + propertyName);
            if (p == null)
            {
                Debug.LogWarning($"[结算美术] {so.targetObject.GetType().Name} 上找不到字段 {propertyName}，跳过。");
                return;
            }

            p.objectReferenceValue = value;
        }
    }
}
