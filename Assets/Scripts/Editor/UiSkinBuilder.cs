using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameEditor
{
    /// <summary>
    /// 统一美化所有界面：HUD / 教程面板 / 结算面板走同一套视觉语言。
    ///
    /// 做的事只有四类，都好回退：
    ///   ① 给进度条换"底槽 + 渐变填充 + 圆角外框"三件套；
    ///   ② 给文字统一字号 / 配色 / 投影（在花哨背景上也看得清）；
    ///   ③ 给提示文字垫一条药丸底，给面板垫一层软投影，拉出层次；
    ///   ④ 按钮换底图 + 分主次配色（主按钮绿、次按钮蓝、跳过灰）。
    ///
    /// 全部按名字查找、缺失就跳过，不删任何东西；改完保存场景。
    /// 依赖菜单 7 生成的 UI 图片（没生成会自动先跑一遍）。
    /// </summary>
    public static class UiSkinBuilder
    {
        // ---- 统一调色板 ----
        private static readonly Color PanelColor = new Color(0.10f, 0.17f, 0.29f, 0.98f);
        private static readonly Color BarTrack = new Color(0.05f, 0.08f, 0.14f, 0.88f);
        private static readonly Color HpColor = new Color(0.94f, 0.30f, 0.32f);
        private static readonly Color DepthColor = new Color(0.30f, 0.74f, 1f);
        private static readonly Color OxygenColor = new Color(0.35f, 0.86f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color TextMain = new Color(0.94f, 0.97f, 1f);

        [MenuItem("工具/捕鱼/10. 统一美化所有界面", false, 18)]
        public static void ApplyAllSkins()
        {
            // UI 图片没生成过就先补上（幂等）
            if (AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PanelBg.png") == null)
            {
                ResultAssetGenerator.GenerateUiArt();
            }

            SkinHud();
            SkinTutorial();
            ResultAssetGenerator.BeautifyResultPanel(); // 结算面板复用菜单 9 的逻辑

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[界面美化] HUD / 教程面板 / 结算面板 已统一：进度条三件套、文字投影、提示药丸底、面板软投影、按钮主次配色。");
        }

        // ==================================================================
        // HUD
        // ==================================================================

        private static void SkinHud()
        {
            GameObject canvas = FindRoot("HUDCanvas");
            if (canvas == null)
            {
                Debug.LogWarning("[界面美化] 场景里没有 HUDCanvas，跳过 HUD。");
                return;
            }

            Transform root = canvas.transform;

            // 血条（红）
            SkinBar(FindDeep(root, "HpBarBg"), "HpFrame", HpColor);
            StyleText(FindDeep(root, "HpText"), 26, new Color(1f, 0.84f, 0.84f), TextAnchor.MiddleLeft);

            // 深度条（蓝）
            SkinBar(FindDeep(root, "DepthBarBg"), "DepthFrame", DepthColor);
            StyleText(FindDeep(root, "DepthText"), 28, new Color(0.86f, 0.94f, 1f), TextAnchor.MiddleCenter);

            // 分数 / 渔获：数字放大、描金，形成主次
            StyleText(FindDeep(root, "ScoreText"), 58, GoldColor, TextAnchor.MiddleRight,
                new Vector2(-44f, -30f), new Vector2(640f, 78f));
            StyleText(FindDeep(root, "CatchText"), 30, new Color(0.82f, 0.91f, 1f), TextAnchor.MiddleRight,
                new Vector2(-44f, -114f), new Vector2(640f, 48f));

            // 底部操作提示：加一条药丸底，文字压在它上面
            Transform hintRoot = FindDeep(root, "HintRoot");
            Transform hintText = FindDeep(root, "HintText");
            if (hintRoot != null && hintText != null)
            {
                Image pill = EnsureImage(hintRoot, "HintBg", ResultAssetGenerator.LoadSprite("Pill"),
                    new Color(0.04f, 0.09f, 0.16f, 0.78f));
                SetAnchored(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 48f), new Vector2(1180f, 74f));

                int index = hintText.GetSiblingIndex();
                pill.transform.SetSiblingIndex(index); // 垫在文字正下方
                StyleText(hintText, 32, TextMain, TextAnchor.MiddleCenter, new Vector2(0f, 48f), new Vector2(1120f, 60f));
            }
        }

        // ==================================================================
        // 教程面板
        // ==================================================================

        private static void SkinTutorial()
        {
            GameObject canvas = FindRoot("TutorialCanvas");
            if (canvas == null)
            {
                Debug.LogWarning("[界面美化] 场景里没有 TutorialCanvas，跳过教程面板（可先跑菜单 5）。");
                return;
            }

            Transform root = canvas.transform;
            Transform panel = FindDeep(root, "VideoPanel");

            if (panel != null)
            {
                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.sprite = ResultAssetGenerator.LoadSprite("PanelBg");
                    panelImage.type = Image.Type.Simple;
                    panelImage.color = PanelColor;
                }

                // 面板软投影：压在面板下面，画面马上有层次
                Image shadow = EnsureImage(root, "VideoShadow", ResultAssetGenerator.LoadSprite("Shadow"),
                    new Color(0f, 0f, 0f, 0.55f));
                SetAnchored(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -16f), new Vector2(1040f, 740f));
                shadow.transform.SetSiblingIndex(0);
            }

            SkinBar(FindDeep(root, "OxygenBarBg"), "OxygenFrame", OxygenColor);
            StyleText(FindDeep(root, "OxygenText"), 26, new Color(0.88f, 0.95f, 1f), TextAnchor.MiddleLeft);

            Transform hint = FindDeep(root, "TutorialHintText");
            if (hint != null && panel != null)
            {
                Image pill = EnsureImage(panel, "TutorialHintBg", ResultAssetGenerator.LoadSprite("Pill"),
                    new Color(0.03f, 0.07f, 0.13f, 0.72f));
                SetAnchored(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 34f), new Vector2(912f, 72f));
                pill.transform.SetSiblingIndex(hint.GetSiblingIndex());
            }

            StyleText(hint, 32, TextMain, TextAnchor.MiddleCenter, new Vector2(0f, 34f), new Vector2(880f, 56f));

            StyleButton(FindDeep(root, "SkipButton"), new Color(0.20f, 0.26f, 0.36f), 30);
            StyleButton(FindDeep(root, "ReplayButton"), new Color(0.20f, 0.46f, 0.78f), 32);
            StyleButton(FindDeep(root, "StartButton"), new Color(0.18f, 0.62f, 0.44f), 34);
        }

        // ==================================================================
        // 零件
        // ==================================================================

        /// <summary>
        /// 进度条三件套：
        ///   底槽 BarBg（圆角凹槽）+ 第一层子物体当填充 BarFill（渐变，不圆角——它会被 anchorMax.x 拉伸）
        ///   + 最上层盖一圈 BarFrame（圆角描边，顺带把拉伸出来的方角藏掉）。
        /// </summary>
        private static void SkinBar(Transform barBg, string frameName, Color fillColor)
        {
            if (barBg == null)
            {
                return;
            }

            Image bg = barBg.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = ResultAssetGenerator.LoadSprite("BarBg");
                bg.type = Image.Type.Simple;
                bg.color = BarTrack;
            }

            Transform fill = barBg.childCount > 0 ? barBg.GetChild(0) : null;
            Image fillImage = fill != null ? fill.GetComponent<Image>() : null;
            if (fillImage != null)
            {
                fillImage.sprite = ResultAssetGenerator.LoadSprite("BarFill");
                fillImage.type = Image.Type.Simple;
                fillImage.color = fillColor;
            }

            Image frame = EnsureImage(barBg, frameName, ResultAssetGenerator.LoadSprite("BarFrame"),
                new Color(1f, 1f, 1f, 0.22f));
            Stretch(frame.rectTransform);
            frame.transform.SetAsLastSibling(); // 外框永远盖在填充上
        }

        private static void StyleText(Transform target, int fontSize, Color color, TextAnchor anchor,
            Vector2? anchoredPos = null, Vector2? size = null)
        {
            Text text = target != null ? target.GetComponent<Text>() : null;
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;

            // 一层很淡的投影：背景是大面积水色时，纯白字会糊，加投影立刻清楚
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            if (anchoredPos.HasValue)
            {
                text.rectTransform.anchoredPosition = anchoredPos.Value;
            }

            if (size.HasValue)
            {
                text.rectTransform.sizeDelta = size.Value;
            }
        }

        private static void StyleButton(Transform button, Color color, int labelSize)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = ResultAssetGenerator.LoadSprite("Button");
                image.type = Image.Type.Simple;
                image.color = color;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = labelSize;
                label.color = Color.white;

                Shadow shadow = label.GetComponent<Shadow>();
                if (shadow == null)
                {
                    shadow = label.gameObject.AddComponent<Shadow>();
                }

                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(1f, -1.5f);
            }
        }

        private static Image EnsureImage(Transform parent, string name, Sprite sprite, Color color)
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
                rt.localScale = Vector3.one;
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
            return image;
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
    }
}
