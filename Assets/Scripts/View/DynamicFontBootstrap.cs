using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 运行时中文字体修复。
    ///
    /// Unity 内置的 LegacyRuntime/Arial 不含中文字形，用 legacy UI Text 写中文会显示成方块。
    /// 这个组件在 Awake 时用系统字体创建一份动态字体，替换掉 Canvas 下所有 Text 的字体。
    /// —— 之所以放在运行时而不是 Editor 里做：从系统字体创建的 Font 没有对应的资源文件，
    /// 在编辑器里赋值无法序列化进场景，运行时创建反而是最稳的。
    ///
    /// 关键点：这份字体是运行时对象，销毁时机不可控。如果 Text 一直指着它，
    /// 等它被销毁而 Canvas 又刚好重建，ugui 就会生成非法网格并刷 "Invalid AABB inAABB"。
    /// 所以这里把原字体缓存下来，在 OnDestroy 里先换回去，再主动销毁运行时字体。
    ///
    /// 正式项目更推荐 TextMeshPro + 预烘焙的中文字体图集（可打进 APK，不依赖系统字体）。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class DynamicFontBootstrap : MonoBehaviour
    {
        [Tooltip("按顺序尝试的系统字体名；越靠前优先级越高")] [SerializeField]
        private string[] fontNames =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "微软雅黑",
            "SimHei",
            "黑体",
            "PingFang SC",
            "Heiti SC",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "Arial Unicode MS",
            "Arial",
        };

        [SerializeField] private int sampleFontSize = 32;

        private Text[] _texts;
        private Font[] _originalFonts;
        private Font _runtimeFont;

        // 运行时新生成的文字（比如结算面板克隆出来的 ResultItem）也记一份，
        // 销毁时一起还原，避免留下"Text 指着已销毁字体"的状态
        private readonly System.Collections.Generic.List<Text> _patchedTexts = new();
        private readonly System.Collections.Generic.List<Font> _patchedFonts = new();

        private void Awake()
        {
            Apply();
        }

        private void OnDestroy()
        {
            // 先把 Text 换回原来的字体，再销毁运行时字体——
            // 顺序反了就会留下"Text 指着已销毁字体"的状态
            RestoreOriginalFonts();

            if (_runtimeFont != null)
            {
                Destroy(_runtimeFont);
                _runtimeFont = null;
            }
        }

        /// <summary>替换该物体下所有 legacy Text 的字体。</summary>
        public void Apply()
        {
            _texts = GetComponentsInChildren<Text>(true);
            if (_texts == null || _texts.Length == 0)
            {
                return;
            }

            if (!EnsureRuntimeFont())
            {
                return;
            }

            // 缓存原字体，供销毁时还原
            _originalFonts = new Font[_texts.Length];
            for (int i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] == null)
                {
                    continue;
                }

                _originalFonts[i] = _texts[i].font;
                _texts[i].font = _runtimeFont;
            }
        }

        /// <summary>
        /// 给"运行时才生成"的物体补字体。
        /// 结算面板克隆 ResultItem 就是这种情况：它们出生在 Awake 之后，
        /// 赶不上 Apply() 那次统一替换，不补的话中文会显示成方块。
        /// </summary>
        public void ApplyTo(GameObject root)
        {
            if (root == null || !EnsureRuntimeFont())
            {
                return;
            }

            // 顺手把已经销毁的引用清掉（结算条目每局都会重建）
            for (int i = _patchedTexts.Count - 1; i >= 0; i--)
            {
                if (_patchedTexts[i] == null)
                {
                    _patchedTexts.RemoveAt(i);
                    _patchedFonts.RemoveAt(i);
                }
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null || texts[i].font == _runtimeFont)
                {
                    continue;
                }

                _patchedTexts.Add(texts[i]);
                _patchedFonts.Add(texts[i].font);
                texts[i].font = _runtimeFont;
            }
        }

        /// <summary>没有就创建一份运行时中文字体。返回 false 表示这台机器上找不到可用的中文字体。</summary>
        private bool EnsureRuntimeFont()
        {
            if (_runtimeFont != null)
            {
                return true;
            }

            _runtimeFont = Font.CreateDynamicFontFromOSFont(fontNames, sampleFontSize);
            if (_runtimeFont == null)
            {
                Debug.LogWarning("[DynamicFontBootstrap] 找不到可用的系统字体，中文可能显示为方块。");
                return false;
            }

            _runtimeFont.name = "RuntimeCJK";
            return true;
        }

        private void RestoreOriginalFonts()
        {
            if (_texts != null && _originalFonts != null)
            {
                for (int i = 0; i < _texts.Length && i < _originalFonts.Length; i++)
                {
                    if (_texts[i] != null && _originalFonts[i] != null)
                    {
                        _texts[i].font = _originalFonts[i];
                    }
                }
            }

            for (int i = 0; i < _patchedTexts.Count; i++)
            {
                if (_patchedTexts[i] != null && _patchedFonts[i] != null)
                {
                    _patchedTexts[i].font = _patchedFonts[i];
                }
            }

            _patchedTexts.Clear();
            _patchedFonts.Clear();
        }
    }
}