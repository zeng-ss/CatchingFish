using Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 开局教程面板
    /// 通过 EventMgr 的教程事件通信：
    ///   收：TutorialHint / TutorialOxygen / TutorialFinished
    ///   发：TutorialReplay / TutorialStartGame
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialPanel : MonoBehaviour
    {
        #region UI

        [SerializeField] private GameObject mask;
        [SerializeField] private RawImage screen;
        [SerializeField] private Camera filmCamera;

        [Tooltip("面板底部的操作提示")] [SerializeField]
        private Text hintText;

        [Tooltip("氧气条填充块")] [SerializeField] private Image oxygenFill;
        [Tooltip("氧气文字")] [SerializeField] private Text oxygenText;
        [Tooltip("[跳过]")] [SerializeField] private Button skipButton;
        [Tooltip("[再看一遍]")] [SerializeField] private Button replayButton;
        [Tooltip("[开始游戏]")] [SerializeField] private Button startButton;
        [Tooltip("演示结束后出现的按钮组")] [SerializeField] private GameObject finishRoot;

        #endregion

        private RenderTexture _rt;

        #region Bind

        private void OnEnable()
        {
            mask.SetActive(true);
            transform.localScale = Vector3.one * 0.6f;
            transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
            CreateScreen();
            Bind(true);
            finishRoot.SetActive(false);
            hintText.text = string.Empty;
            SetOxygen(1f);
        }

        private void OnDisable()
        {
            Bind(false);
            ReleaseScreen();
        }

        private void Bind(bool subscribe)
        {
            if (subscribe)
            {
                EventMgr.Subscribe(GameEvent.TutorialHint, OnHint);
                EventMgr.Subscribe(GameEvent.TutorialOxygen, OnOxygen);
                EventMgr.Subscribe(GameEvent.TutorialFinished, OnFinished);
                replayButton.onClick.AddListener(OnReplayClicked);
                startButton.onClick.AddListener(OnStartClicked);
                skipButton.onClick.AddListener(OnStartClicked);
                return;
            }

            EventMgr.Unsubscribe(GameEvent.TutorialHint, OnHint);
            EventMgr.Unsubscribe(GameEvent.TutorialOxygen, OnOxygen);
            EventMgr.Unsubscribe(GameEvent.TutorialFinished, OnFinished);
            replayButton.onClick.RemoveListener(OnReplayClicked);
            startButton.onClick.RemoveListener(OnStartClicked);
            skipButton.onClick.RemoveListener(OnStartClicked);
        }

        #endregion

        /// <summary>
        /// 建一张和面板同尺寸的 RenderTexture 交给取景相机
        /// </summary>
        private void CreateScreen()
        {
            if (screen == null || _rt != null) return;

            Vector2 size = screen.rectTransform.rect.size;
            int width = Mathf.RoundToInt(Mathf.Clamp(size.x, 128f, 4096f));
            int height = Mathf.RoundToInt(Mathf.Clamp(size.y, 128f, 4096f));

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "TutorialRT",
                antiAliasing = 2,
            };
            _rt.Create();

            screen.texture = _rt;
            if (filmCamera != null)
                filmCamera.targetTexture = _rt;
            else
                Debug.LogWarning("[TutorialPanel] 没有指定取景相机，面板里不会有画面。");
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

        #region 事件

        private void OnHint(object payload)
        {
            hintText.transform.localScale = Vector3.one * 0.6f;
            hintText.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            hintText.text = payload as string;
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
            transform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack).OnComplete(() =>
            {
                gameObject.SetActive(false); // 面板收起来：OnDisable 会把 RenderTexture 一起释放
                mask.SetActive(false);
            });
        }

        #endregion

        /// <summary>
        /// 氧气条
        /// </summary>
        private void SetOxygen(float ratio)
        {
            float value = float.IsNaN(ratio) || float.IsInfinity(ratio) ? 0f : Mathf.Clamp01(ratio);
            Vector2 anchorMax = oxygenFill.rectTransform.anchorMax;
            anchorMax.x = value;
            oxygenFill.rectTransform.anchorMax = anchorMax;
            oxygenText.text = $"OXYGEN {Mathf.RoundToInt(value * 100)}/{Mathf.RoundToInt(100)}";
        }
    }
}