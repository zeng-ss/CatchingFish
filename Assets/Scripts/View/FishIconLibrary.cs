using System.Collections.Generic;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 【表现层】结算条目用的鱼图标，两级来源：
    ///   ① 菜单「工具/捕鱼/6. 烘焙鱼的结算图标」生成的真实渲染图（`Resources/Icon/Fish/{预制体名}.png`）；
    ///   ② 找不到就按鱼种主题色**现场画一张剪影**兜底 —— 保证结算界面永远不会出现空白方块。
    /// 结果全部缓存，一局最多生成一次。
    /// </summary>
    public static class FishIconLibrary
    {
        private const string BakedFolder = "Icon/Fish/";
        private const int FallbackSize = 128;

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>取鱼图标。取不到烘焙图就用 <paramref name="tint"/> 画一张兜底剪影。</summary>
        public static Sprite Get(string prefabName, Color tint)
        {
            bool named = !string.IsNullOrEmpty(prefabName);
            string key = named ? prefabName : $"#{ColorUtility.ToHtmlStringRGB(tint)}";

            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite sprite = named ? Resources.Load<Sprite>(BakedFolder + prefabName) : null;
            if (sprite == null)
            {
                sprite = BuildFallback(tint);
            }

            Cache[key] = sprite;
            return sprite;
        }

        // ==================================================================
        // 兜底剪影：椭圆身子 + 三角尾鳍 + 背鳍/腹鳍 + 眼睛 + 深色描边
        // ==================================================================

        private static Sprite BuildFallback(Color tint)
        {
            const int w = FallbackSize;
            const int h = FallbackSize;

            // 主题色太暗的话先提亮，保证在深色面板上看得见
            Color body = tint;
            if (body.r + body.g + body.b < 0.9f)
            {
                body = Color.Lerp(body, Color.white, 0.35f);
            }

            Color outline = body * 0.45f;
            outline.a = 1f;

            bool[] solid = new bool[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;

                    bool inside = InEllipse(u, v, 0.47f, 0.50f, 0.30f, 0.185f)     // 身子
                                  || InTriangle(u, v, 0.22f, 0.50f, 0.02f, 0.30f, 0.02f, 0.70f)   // 尾鳍
                                  || InTriangle(u, v, 0.33f, 0.66f, 0.57f, 0.66f, 0.45f, 0.82f)   // 背鳍
                                  || InTriangle(u, v, 0.38f, 0.35f, 0.57f, 0.35f, 0.48f, 0.20f);  // 腹鳍

                    solid[y * w + x] = inside;
                }
            }

            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!solid[i])
                    {
                        pixels[i] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                                || !solid[i - 1] || !solid[i + 1] || !solid[i - w] || !solid[i + w];

                    if (edge)
                    {
                        pixels[i] = outline;
                        continue;
                    }

                    float v = (y + 0.5f) / h;
                    Color shade = body * Mathf.Lerp(1.12f, 0.82f, v); // 上亮下暗，假一点体积感
                    shade.a = 1f;

                    // 眼睛
                    float u = (x + 0.5f) / w;
                    float eye = Mathf.Sqrt((u - 0.66f) * (u - 0.66f) + (v - 0.55f) * (v - 0.55f));
                    if (eye < 0.045f)
                    {
                        shade = new Color(0.12f, 0.14f, 0.18f, 1f);
                    }
                    else if (eye < 0.075f)
                    {
                        shade = Color.Lerp(shade, Color.white, 0.25f);
                    }

                    pixels[i] = shade;
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "FishIconFallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "FishIconFallback";
            return sprite;
        }

        private static bool InEllipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        private static bool InTriangle(float u, float v, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Sign(u, v, ax, ay, bx, by);
            float d2 = Sign(u, v, bx, by, cx, cy);
            float d3 = Sign(u, v, cx, cy, ax, ay);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(float u, float v, float ax, float ay, float bx, float by)
        {
            return (u - bx) * (ay - by) - (ax - bx) * (v - by);
        }
    }
}
