using DG.Tweening;
using Model;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 【表现层】结算面板里的一条渔获（预制体 `Resources/Prefab/ResultItem`，菜单 8 一键重建）。
    ///
    /// 视觉构成：卡片底 + 顶部鱼种色条 + 图标柔光 + 鱼图标 + 名字 + 得分；
    /// 配色取自 <see cref="CaughtFish.Color"/>，所以九种鱼在列表里天然有区分度，不需要额外美术资源。
    ///
    /// 动画只碰 **scale / localRotation / CanvasGroup.alpha**，**绝不碰 anchoredPosition** ——
    /// Content 上挂着 GridLayoutGroup，布局每帧都会写回 anchoredPosition，动位置一定会被覆盖。
    ///
    /// 出场顺序由 <see cref="ResultPanel"/> 统一排：这里只负责"造一段动画交出去"。
    /// </summary>
    public class ResultItem : MonoBehaviour
    {
        [Header("引用")] [SerializeField] private Image cardImg;
        [SerializeField] private Image accentImg;
        [SerializeField] private Image glowImg;
        [SerializeField] private Image iconImg;
        [SerializeField] private Text nameTxt, scoreTxt;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("入场动画")] [Tooltip("单条动画时长")] [SerializeField]
        private float duration = 0.42f;

        [Tooltip("起始倾斜角度（左右交替，像一张张卡片甩进来）")] [SerializeField]
        private float tilt = 20f;

        [Tooltip("得分滚动的时长")] [SerializeField]
        private float scoreDuration = 0.35f;

        [Header("配色")] [Tooltip("卡片底色")] [SerializeField]
        private Color cardColor = new Color(0.10f, 0.17f, 0.28f, 0.96f);

        [Tooltip("色条 / 柔光的不透明度")] [SerializeField, Range(0f, 1f)]
        private float accentAlpha = 0.95f;

        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.35f;

        private Sequence _seq;
        private int _score;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            // 克隆出来的物体赶不上 DynamicFontBootstrap 在 Awake 那次统一换字体，
            // 不补这一下，中文鱼名会显示成方块
            DynamicFontBootstrap bootstrap = GetComponentInParent<DynamicFontBootstrap>(true);
            if (bootstrap != null) bootstrap.ApplyTo(gameObject);
        }

        /// <summary>填数据：文字 + 图标 + 配色。图标走 <see cref="FishIconLibrary"/>，任何情况下都不会是空白。</summary>
        public void UpdateData(CaughtFish fish)
        {
            _score = fish.Score;
            nameTxt.text = fish.DisplayName;
            scoreTxt.text = "0";

            Color tint = fish.Color.a <= 0f ? Color.white : fish.Color;
            cardImg.color = cardColor;

            tint.a = accentAlpha;
            accentImg.color = tint;

            tint.a = glowAlpha;
            glowImg.color = tint;

            iconImg.sprite = FishIconLibrary.Get(fish.PrefabName, fish.Color);
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }

        /// <summary>造出"入场动画"并返回，由面板插进总时间轴统一播放。</summary>
        public Tween BuildShow(int index)
        {
            _seq?.Kill();

            // 先摆到"未出现"的状态，避免重播时看到上一遍的残留
            canvasGroup.alpha = 0f;
            transform.localScale = Vector3.zero;
            transform.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? -tilt : tilt);

            _seq = DOTween.Sequence();
            _seq.Join(canvasGroup.DOFade(1f, duration * 0.55f).SetEase(Ease.OutQuad));
            _seq.Join(transform.DOScale(1f, duration).SetEase(Ease.OutBack, 1.5f));
            _seq.Join(transform.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.OutCubic));

            // 图标"缩进来"：卡片落位的同时鱼在卡片里定住，比整块一起弹更有层次
            Transform icon = iconImg.transform;
            icon.localScale = new Vector3(1.45f, 1.45f, 1f);
            _seq.Join(icon.DOScale(1f, duration * 0.9f).SetEase(Ease.OutQuad));

            // 顶部色条从中间展开
            Transform accent = accentImg.transform;
            accent.localScale = new Vector3(0f, 1f, 1f);
            _seq.Join(accent.DOScaleX(1f, duration * 0.8f).SetEase(Ease.OutCubic));

            // 得分从 0 滚上去，视线会被数字吸住
            if (_score > 0)
            {
                float value = 0f;
                _seq.Join(DOTween.To(() => value, v =>
                {
                    value = v;
                    scoreTxt.text = $"+{Mathf.RoundToInt(v)}";
                }, _score, scoreDuration).SetEase(Ease.OutQuad).SetDelay(duration * 0.35f));
            }

            return _seq;
        }

        /// <summary>回收前先把动画掐掉，别留下指着已销毁物体的 tween。</summary>
        public void Kill()
        {
            _seq?.Kill();
            _seq = null;
        }
    }
}
