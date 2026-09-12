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
    /// 正式项目更推荐 TextMeshPro + 预烘焙的中文字体图集（可打进 APK，不依赖系统字体）。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class DynamicFontBootstrap : MonoBehaviour
    {
        [Tooltip("按顺序尝试的系统字体名；越靠前优先级越高")]
        [SerializeField]
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

        [SerializeField]
        private int sampleFontSize = 32;

        private void Awake()
        {
            Apply();
        }

        /// <summary>替换该物体下所有 legacy Text 的字体。</summary>
        public void Apply()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            if (texts == null || texts.Length == 0)
            {
                return;
            }

            Font font = Font.CreateDynamicFontFromOSFont(fontNames, sampleFontSize);
            if (font == null)
            {
                Debug.LogWarning("[DynamicFontBootstrap] 找不到可用的系统字体，中文可能显示为方块。");
                return;
            }

            font.name = "RuntimeCJK";

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].font = font;
                }
            }
        }
    }
}
