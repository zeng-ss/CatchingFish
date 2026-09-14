using Core;
using Model;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    public class HUDView : MonoBehaviour
    {
        [SerializeField] private Image hpFill, depthFill;
        [SerializeField] private Text hpText, scoreText, depthText, catchText, hintText;
        [SerializeField] private GameObject hintRoot;

        private GameMgr _gameMgr;
        private float _depthRatio;

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.HpChanged, OnHpChanged);
            EventMgr.Subscribe(GameEvent.ScoreChanged, OnScoreChanged);
            EventMgr.Subscribe(GameEvent.DepthChanged, OnDepthChanged);
            EventMgr.Subscribe(GameEvent.FishCaught, OnCaughtChanged);
            EventMgr.Subscribe(GameEvent.StateChanged, OnStateChanged);
            EventMgr.Subscribe(GameEvent.GameStart, OnGameStart);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.HpChanged, OnHpChanged);
            EventMgr.Unsubscribe(GameEvent.ScoreChanged, OnScoreChanged);
            EventMgr.Unsubscribe(GameEvent.DepthChanged, OnDepthChanged);
            EventMgr.Unsubscribe(GameEvent.FishCaught, OnCaughtChanged);
            EventMgr.Unsubscribe(GameEvent.StateChanged, OnStateChanged);
            EventMgr.Unsubscribe(GameEvent.GameStart, OnGameStart);
        }

        private void Start()
        {
            _gameMgr = GameMgr.Instance;
            RefreshAll();
        }

        #region Event

        private void OnGameStart(object obj) => RefreshAll();

        private void OnHpChanged(object payload)
        {
            if (payload is HpPayload hp) SetHp(hp.Current, hp.Max);
        }

        private void OnScoreChanged(object payload)
        {
            if (payload is int score) SetScore(score);
        }

        private void OnDepthChanged(object payload)
        {
            if (payload is float ratio) SetDepth(ratio);
        }

        private void OnCaughtChanged(object payload)
        {
            if (payload is CatchPayload catchInfo) SetCaught(catchInfo.Current, catchInfo.Max);
        }

        private void OnStateChanged(object payload)
        {
            if (payload is GameState state) SetHint(state);
        }

        #endregion

        #region SetUI

        private void RefreshAll()
        {
            if (_gameMgr == null || _gameMgr.GModel == null)
            {
                return;
            }

            GameModel model = _gameMgr.GModel;
            SetHp(model.Hp, model.MaxHp);
            SetScore(model.Score);
            SetCaught(model.CaughtCount, model.MaxCatch);
            SetDepth(model.DepthRatio);
            SetHint(_gameMgr.State);
        }

        private void SetHp(int current, int max)
        {
            SetBar(hpFill, max <= 0 ? 0f : (float)current / max);
            hpText.text = $"HP {current}/{max}";
        }

        private void SetScore(int score) => scoreText.text = $"SCORE {score}";

        private void SetCaught(int current, int max) => catchText.text = $"FISH {current}/{max}";

        private void SetDepth(float ratio)
        {
            _depthRatio = Sanitize(ratio);
            SetBar(depthFill, _depthRatio);
            float maxDepth = _gameMgr != null && _gameMgr.Config != null ? _gameMgr.Config.maxDepth : 1f;
            float perMeter = _gameMgr != null && _gameMgr.Config != null ? _gameMgr.Config.depthPerMeter : 1f;
            int meters = Mathf.RoundToInt(_depthRatio * maxDepth * perMeter);
            depthText.text = $"DEPTH {meters}m";
        }

        /// <summary>
        /// 设置进度条长度。
        ///
        /// 刻意**不用** Image.fillAmount：ugui 的 Filled / Sliced 网格生成在极端值下会算出
        /// 非法顶点，Canvas 重建时就会刷 "Invalid AABB inAABB"。
        /// 直接改 RectTransform 的 anchorMax.x 表达 0~1 的进度，等价、稳定、零副作用。
        /// </summary>
        private static void SetBar(Image bar, float ratio)
        {
            if (bar == null)
            {
                return;
            }

            Vector2 anchorMax = bar.rectTransform.anchorMax;
            anchorMax.x = Sanitize(ratio);
            bar.rectTransform.anchorMax = anchorMax;
        }

        /// <summary>把 NaN / Infinity 挡在 UI 之外——它们是 "Invalid AABB" 的直接来源。</summary>
        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }

        private void SetHint(GameState state)
        {
            var hint = state switch
            {
                GameState.Ready => "长按鼠标开始下潜（A/D 也可以左右移动）",
                GameState.CastingDown => "按住鼠标左右移动，躲开鱼群",
                GameState.ReelingUp => "收线中：碰到鱼就会抓住它",
                GameState.FastReturn => "渔获已满，加速返回水面…",
                GameState.Settlement => "收线完成",
                GameState.Failed => "血量归零，下潜失败",
                _ => string.Empty,
            };

            hintText.text = hint;
            hintRoot.SetActive(!string.IsNullOrEmpty(hint));
        }

        #endregion
    }
}