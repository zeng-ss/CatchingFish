using Config;
using Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 【表现层】HUD：血条 / 深度条 / 分数 / 渔获 / 操作提示。
    ///
    /// 它**不持有任何 Controller**，只订阅 EventMgr 的数值事件 ——
    /// 数值怎么算归 Model，界面只负责显示，两边互不知道对方存在。
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [SerializeField] private Image hpFill, depthFill;
        [SerializeField] private Text hpText, scoreText, depthText, catchText, hintText;
        [SerializeField] private GameObject hintRoot;

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.HpChanged, OnHpChanged);
            EventMgr.Subscribe(GameEvent.ScoreChanged, OnScoreChanged);
            EventMgr.Subscribe(GameEvent.DepthChanged, OnDepthChanged);
            EventMgr.Subscribe(GameEvent.FishCaught, OnCaughtChanged);
            EventMgr.Subscribe(GameEvent.StateChanged, OnStateChanged);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.HpChanged, OnHpChanged);
            EventMgr.Unsubscribe(GameEvent.ScoreChanged, OnScoreChanged);
            EventMgr.Unsubscribe(GameEvent.DepthChanged, OnDepthChanged);
            EventMgr.Unsubscribe(GameEvent.FishCaught, OnCaughtChanged);
            EventMgr.Unsubscribe(GameEvent.StateChanged, OnStateChanged);
        }

        private void OnHpChanged(object payload)
        {
            if (payload is not HpPayload hp) return;

            SetBar(hpFill, (float)hp.Current / Mathf.Max(1, hp.Max));
            hpText.text = $"HP {hp.Current}/{hp.Max}";
        }

        private void OnScoreChanged(object payload)
        {
            if (payload is int score) scoreText.text = $"SCORE {score}";
        }

        private void OnCaughtChanged(object payload)
        {
            if (payload is CatchPayload caught) catchText.text = $"FISH {caught.Current}/{caught.Max}";
        }

        private void OnDepthChanged(object payload)
        {
            if (payload is not float ratio) return;

            SetBar(depthFill, ratio);

            GameConfig cfg = GameConfig.Get();
            int meters = Mathf.RoundToInt(Sanitize(ratio) * cfg.maxDepth * cfg.depthPerMeter);
            depthText.text = $"DEPTH {meters}m";
        }

        private void OnStateChanged(object payload)
        {
            if (payload is not GameState state) return;

            string hint = state switch
            {
                GameState.Ready => "长按鼠标开始下潜（A/D 也可以左右移动）",
                GameState.CastingDown => "按住鼠标左右移动，躲开鱼群",
                GameState.ReelingUp => "收线中：碰到鱼就会抓住它",
                GameState.FastReturn => "渔获已满，加速返回水面…",
                GameState.Settlement => "收线完成",
                GameState.Failed => "血量归零，下潜失败",
                _ => string.Empty, // 教程阶段由教程面板负责提示
            };

            hintText.text = hint;
            hintRoot.SetActive(hint.Length > 0);

            // 换提示时弹一下，视线更容易被带到新文案上
            hintText.transform.localScale = Vector3.one * 0.6f;
            hintText.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }

        /// <summary>
        /// 进度条用 `anchorMax.x` 表达，**不用 `Image.fillAmount`**：
        /// Filled / Sliced 的网格生成在极端值下会算出非法顶点，Canvas 重建时刷 "Invalid AABB inAABB"。
        /// </summary>
        private static void SetBar(Image bar, float ratio)
        {
            Vector2 anchorMax = bar.rectTransform.anchorMax;
            anchorMax.x = Sanitize(ratio);
            bar.rectTransform.anchorMax = anchorMax;
        }

        /// <summary>把 NaN / Infinity 挡在 UI 之外 —— 它们是 "Invalid AABB" 的直接来源。</summary>
        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }
    }
}
