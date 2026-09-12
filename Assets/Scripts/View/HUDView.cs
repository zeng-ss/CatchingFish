using Config;
using Core;
using Model;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// HUD 表现层：血条 / 分数 / 深度 / 渔获数量 / 操作提示。
    ///
    /// 严格只订阅事件 + 读取 Model，绝不反向调用 Controller，
    /// 所以以后要做"血条飘字""屏幕震动"之类的表现，改这个文件就够了。
    /// 所有引用都做了空判断，缺哪个 UI 元素都不会报错，方便分步搭界面。
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("血量")] [SerializeField] private Image hpFill;

        [SerializeField] private Text hpText;

        [Header("分数")] [SerializeField] private Text scoreText;

        [Header("深度")] [SerializeField] private Image depthFill;

        [SerializeField] private Text depthText;

        [Header("渔获")] [SerializeField] private Text catchText;

        [Header("操作提示")] [SerializeField] private Text hintText;

        [SerializeField] private GameObject hintRoot;

        private float _depthRatio;

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.HpChanged, OnHpChanged);
            EventMgr.Subscribe(GameEvent.ScoreChanged, OnScoreChanged);
            EventMgr.Subscribe(GameEvent.DepthChanged, OnDepthChanged);
            EventMgr.Subscribe(GameEvent.CaughtChanged, OnCaughtChanged);
            EventMgr.Subscribe(GameEvent.StateChanged, OnStateChanged);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.HpChanged, OnHpChanged);
            EventMgr.Unsubscribe(GameEvent.ScoreChanged, OnScoreChanged);
            EventMgr.Unsubscribe(GameEvent.DepthChanged, OnDepthChanged);
            EventMgr.Unsubscribe(GameEvent.CaughtChanged, OnCaughtChanged);
            EventMgr.Unsubscribe(GameEvent.StateChanged, OnStateChanged);
        }

        private void Start()
        {
            // Start 一定晚于所有 Awake，这时 GameMgr 的初始数值已经就绪
            RefreshAll();
        }

        /// <summary>从 Model 拉一次全量数据（事件漏收时也能自愈）。</summary>
        public void RefreshAll()
        {
            if (!GameMgr.Exists || GameMgr.Instance.Data == null)
            {
                return;
            }

            GameModel model = GameMgr.Instance.Data;

            SetHp(model.Hp, model.MaxHp);
            SetScore(model.Score);
            SetCaught(model.CaughtCount, model.MaxCatch);
            SetDepth(model.DepthRatio);
            SetHint(GameMgr.Instance.State);
        }

        private void OnHpChanged(object payload)
        {
            if (payload is HpPayload hp)
            {
                SetHp(hp.Current, hp.Max);
            }
        }

        private void OnScoreChanged(object payload)
        {
            if (payload is int score)
            {
                SetScore(score);
            }
        }

        private void OnDepthChanged(object payload)
        {
            if (payload is float ratio)
            {
                SetDepth(ratio);
            }
        }

        private void OnCaughtChanged(object payload)
        {
            if (payload is CatchPayload catchInfo)
            {
                SetCaught(catchInfo.Current, catchInfo.Max);
            }
        }

        private void OnStateChanged(object payload)
        {
            if (payload is GameState state)
            {
                SetHint(state);
            }
        }

        private void SetHp(int current, int max)
        {
            if (hpFill != null)
            {
                hpFill.fillAmount = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
            }

            if (hpText != null)
            {
                hpText.text = $"HP {current}/{max}";
            }
        }

        private void SetScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE {score}";
            }
        }

        private void SetCaught(int current, int max)
        {
            if (catchText != null)
            {
                catchText.text = $"FISH {current}/{max}";
            }
        }

        private void SetDepth(float ratio)
        {
            _depthRatio = Mathf.Clamp01(ratio);

            if (depthFill != null)
            {
                depthFill.fillAmount = _depthRatio;
            }

            if (depthText != null)
            {
                float maxDepth = GameMgr.Exists ? GameMgr.Instance.Config.maxDepth : 1f;
                float perMeter = GameMgr.Exists ? GameMgr.Instance.Config.depthPerMeter : 1f;
                int meters = Mathf.RoundToInt(_depthRatio * maxDepth * perMeter);
                depthText.text = $"DEPTH {meters}m";
            }
        }

        private void SetHint(GameState state)
        {
            string hint = state switch
            {
                GameState.Ready => "长按鼠标开始下潜（A/D 也可以左右移动）",
                GameState.CastingDown => "按住鼠标左右移动，躲开鱼群",
                GameState.ReelingUp => "收线中：碰到鱼就会抓住它",
                GameState.FastReturn => "渔获已满，加速返回水面…",
                GameState.Settlement => "收线完成",
                GameState.Failed => "血量归零，下潜失败",
                _ => string.Empty,
            };

            if (hintText != null)
            {
                hintText.text = hint;
            }

            if (hintRoot != null)
            {
                hintRoot.SetActive(!string.IsNullOrEmpty(hint));
            }
        }
    }
}