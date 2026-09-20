using Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 【表现层】结算前的动画（挂在 HUDCanvas 上）：
    ///   ① 钩上每条鱼一起**散开 + 缩到 0**（由 FishHang 播）；
    ///   ② 缩完立刻回收，并在原位冒出一段 "+xxx" 飘字；
    ///   ③ 全部播完才发 `SettleAnimDone` —— 结算面板监听的是它，不是 `GameSettle`。
    ///
    /// 所有鱼同时起跑，所以总时长是固定的，不需要计数器等"最后一条播完"。
    /// </summary>
    public class SettleFx : MonoBehaviour
    {
        [Header("散开")] [Tooltip("鱼散开并缩小到 0 的时长")] [SerializeField]
        private float disperseDuration = 0.45f;

        [Tooltip("散开的距离（世界单位）")] [SerializeField]
        private float disperseRadius = 2.2f;

        [Header("飘字")] [Tooltip("Scale 0 → 1 的时长")] [SerializeField]
        private float popDuration = 0.5f;

        [Tooltip("Scale 1 → 0 的时长")] [SerializeField]
        private float fadeDuration = 0.3f;

        [Tooltip("全程上浮的距离（世界单位）")] [SerializeField]
        private float riseDistance = 1.6f;

        [SerializeField] private int fontSize = 44;
        [SerializeField] private Color textColor = new Color(1f, 0.86f, 0.35f);

        private SettlePayload _settle;
        private float _waitTimer;

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.GameSettle, OnSettle);
            EventMgr.Subscribe(GameEvent.SettleFishBurst, OnFishBurst);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.GameSettle, OnSettle);
            EventMgr.Unsubscribe(GameEvent.SettleFishBurst, OnFishBurst);
        }

        private void OnSettle(object payload)
        {
            if (payload is not SettlePayload settle) return;

            _settle = settle;
            FishHang.DisperseAll(disperseDuration, disperseRadius);

            // 三段动画同时起跑，所以总时长可以直接算出来
            _waitTimer = disperseDuration + popDuration + fadeDuration + 0.05f;
        }

        private void Update()
        {
            if (_waitTimer <= 0f) return;

            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f) EventMgr.Publish(GameEvent.SettleAnimDone, _settle);
        }

        /// <summary>某条鱼缩完了：在它的位置冒一个 "+分数" 并全程上浮。</summary>
        private void OnFishBurst(object payload)
        {
            if (payload is not FishBurstPayload burst) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(burst.WorldPos);
            if (screen.z < 0f) return; // 在相机背后就不显示

            Text text = CreateText();
            text.text = $"+{burst.Score}";
            text.rectTransform.position = screen; // ScreenSpaceOverlay 下屏幕坐标即 RectTransform 位置

            // 世界单位 → 屏幕像素，保证上浮距离和世界尺度一致
            float pixelsPerUnit = Screen.height / (2f * cam.orthographicSize);

            Sequence seq = DOTween.Sequence();
            seq.Append(text.rectTransform.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack));
            seq.Insert(0f, text.rectTransform
                .DOMoveY(screen.y + riseDistance * pixelsPerUnit, popDuration + fadeDuration)
                .SetEase(Ease.OutQuad));
            seq.Append(text.rectTransform.DOScale(Vector3.zero, fadeDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => Destroy(text.gameObject));
        }

        private Text CreateText()
        {
            GameObject go = new GameObject("SettlePop", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // "+120" 全是 ASCII，够用
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            text.rectTransform.sizeDelta = new Vector2(400f, 90f);
            text.rectTransform.localScale = Vector3.zero; // 初始 Scale 为 0
            return text;
        }
    }
}
