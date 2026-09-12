using System.Collections.Generic;
using System.Text;
using Core;
using Model;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 结算面板表现层。
    /// 只监听 GameSettle / GameRestart，"再来一局"直接回调 GameMgr.RestartGame()，
    /// 不持有任何游戏逻辑。
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [Header("根节点（显示/隐藏用）")] [SerializeField]
        private GameObject panelRoot;

        [Header("文案")] [SerializeField] private Text titleText;

        [SerializeField] private Text scoreText;

        [SerializeField] private Text detailText;

        [Header("按钮")] [SerializeField] private Button restartButton;

        private readonly StringBuilder _builder = new StringBuilder();

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
        }

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.GameSettle, OnSettle);
            EventMgr.Subscribe(GameEvent.GameRestart, OnRestart);

            // 开局默认收起
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.GameSettle, OnSettle);
            EventMgr.Unsubscribe(GameEvent.GameRestart, OnRestart);
        }

        private void OnSettle(object payload)
        {
            if (!(payload is SettlePayload settle))
            {
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (titleText != null)
            {
                titleText.text = settle.IsWin ? "满载而归！" : "氧气耗尽…";
            }

            if (scoreText != null)
            {
                scoreText.text = $"总分 {settle.Score}";
            }

            if (detailText != null)
            {
                detailText.text = BuildDetail(settle);
            }
        }

        private void OnRestart(object payload)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnRestartClicked()
        {
            if (GameMgr.Exists)
            {
                GameMgr.Instance.RestartGame();
            }
        }

        /// <summary>按鱼种汇总渔获，输出"鲤鱼 x2  60分"这样的清单。</summary>
        private string BuildDetail(SettlePayload settle)
        {
            _builder.Clear();
            _builder.AppendLine($"共捕获 {settle.CaughtCount} 条");

            if (settle.FishList == null || settle.FishList.Count == 0)
            {
                return _builder.ToString();
            }

            Dictionary<string, int> countByName = new Dictionary<string, int>();
            Dictionary<string, int> scoreByName = new Dictionary<string, int>();
            List<string> order = new List<string>();

            for (int i = 0; i < settle.FishList.Count; i++)
            {
                CaughtFish fish = settle.FishList[i];
                string name = string.IsNullOrEmpty(fish.DisplayName) ? fish.Type.ToString() : fish.DisplayName;

                if (!countByName.ContainsKey(name))
                {
                    countByName[name] = 0;
                    scoreByName[name] = 0;
                    order.Add(name);
                }

                countByName[name]++;
                scoreByName[name] += fish.Score;
            }

            for (int i = 0; i < order.Count; i++)
            {
                string name = order[i];
                _builder.AppendLine($"{name}  x{countByName[name]}   +{scoreByName[name]}");
            }

            return _builder.ToString();
        }
    }
}