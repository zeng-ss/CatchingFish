using Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 【表现层】开局教程面板。
    ///
    /// 它只做两件事：
    ///   ① 运行时建一张和面板同尺寸的 RenderTexture 交给取景相机，再把画面画进 RawImage
    ///      —— 教程是**实时演出**，不依赖视频文件，任意分辨率都清晰；
    ///   ② 显示底部提示、氧气条，以及演完之后的 [再看一遍] / [开始游戏]。
    ///
    /// 它不认识 TutorialDirector，两边只走事件：
    ///   收 TutorialHint / TutorialOxygen / TutorialFinished
    ///   发 TutorialReplay / TutorialStartGame
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialPanel : MonoBehaviour
    {
        [SerializeField] private GameObject mask;
        [SerializeField] private RawImage screen;
        [SerializeField] private Camera filmCamera;

        [Tooltip("面板底部的操作提示")] [SerializeField]
        private Text hintText;

        [Tooltip("氧气条填充块")] [SerializeField] private Image oxygenFill;
        [Tooltip("氧气数值文字")] [SerializeField] private Text oxygenText;
        [Tooltip("演示途中就能点的 [跳过]")] [SerializeField] private Button skipButton;
        [Tooltip("演示结束后出现的按钮组")] [SerializeField] private GameObject finishRoot;
        [Tooltip("[再看一遍]")] [SerializeField] private Button replayButton;
        [Tooltip("[开始游戏]")] [SerializeField] private Button startButton;

        private RenderTexture _rt;

        // ==================================================================

        private void OnEnable()
        {
            CreateScreen();

            EventMgr.Subscribe(GameEvent.TutorialHint, OnHint);
            EventMgr.Subscribe(GameEvent.TutorialOxygen, OnOxygen);
            EventMgr.Subscribe(GameEvent.TutorialFinished, OnFinished);
            skipButton.onClick.AddListener(OnStartClicked);
            replayButton.onClick.AddListener(OnReplayClicked);
            startButton.onClick.AddListener(OnStartClicked);

            mask.SetActive(true);
            finishRoot.SetActive(false);
            hintText.text = string.Empty;
            SetOxygen(1f);

            transform.localScale = Vector3.one * 0.6f;
            transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.TutorialHint, OnHint);
            EventMgr.Unsubscribe(GameEvent.TutorialOxygen, OnOxygen);
            EventMgr.Unsubscribe(GameEvent.TutorialFinished, OnFinished);
            skipButton.onClick.RemoveListener(OnStartClicked);
            replayButton.onClick.RemoveListener(OnReplayClicked);
            startButton.onClick.RemoveListener(OnStartClicked);

            ReleaseScreen();
        }

        // ==================================================================
        // 取景：GuideCamera → RenderTexture → RawImage
        // ==================================================================

        private void CreateScreen()
        {
            if (_rt != null) return;

            Vector2 size = screen.rectTransform.rect.size;
            _rt = new RenderTexture(
                Mathf.RoundToInt(Mathf.Clamp(size.x, 128f, 4096f)),
                Mathf.RoundToInt(Mathf.Clamp(size.y, 128f, 4096f)),
                24, RenderTextureFormat.Default)
            {
                name = "TutorialRT",
                antiAliasing = 2,
            };
            _rt.Create();

            screen.texture = _rt;

            if (filmCamera != null)
            {
                filmCamera.targetTexture = _rt;
            }
            else
            {
                Debug.LogWarning("[TutorialPanel] 没有指定取景相机，面板里不会有画面。");
            }
        }

        private void ReleaseScreen()
        {
            if (_rt == null) return;

            if (filmCamera != null) filmCamera.targetTexture = null;
            screen.texture = null;

            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        // ==================================================================
        // 事件
        // ==================================================================

        private void OnHint(object payload)
        {
            hintText.text = payload as string;

            // 换提示时弹一下，视线更容易被带到新文案上
            hintText.transform.localScale = Vector3.one * 0.6f;
            hintText.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }

        private void OnOxygen(object payload)
        {
            if (payload is float ratio) SetOxygen(ratio);
        }

        private void OnFinished(object payload)
        {
            finishRoot.SetActive(true);

            replayButton.transform.localScale = Vector3.zero;
            startButton.transform.localScale = Vector3.zero;
            replayButton.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
            startButton.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        private void OnReplayClicked()
        {
            finishRoot.SetActive(false);
            EventMgr.Publish(GameEvent.TutorialReplay);
        }

        private void OnStartClicked()
        {
            EventMgr.Publish(GameEvent.TutorialStartGame);

            // 面板收起来，OnDisable 会把 RenderTexture 一起释放
            transform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack).OnComplete(() =>
            {
                gameObject.SetActive(false);
                mask.SetActive(false);
            });
        }

        /// <summary>氧气条和 HUD 一样用 anchorMax.x 表达（不用 Image.fillAmount，见 HUDView 的说明）。</summary>
        private void SetOxygen(float ratio)
        {
            float value = float.IsNaN(ratio) || float.IsInfinity(ratio) ? 0f : Mathf.Clamp01(ratio);

            Vector2 anchorMax = oxygenFill.rectTransform.anchorMax;
            anchorMax.x = value;
            oxygenFill.rectTransform.anchorMax = anchorMax;

            oxygenText.text = $"OXYGEN {Mathf.RoundToInt(value * 100)}/100";
        }
    }
}
