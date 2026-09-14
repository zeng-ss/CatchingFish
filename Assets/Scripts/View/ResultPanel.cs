using System.Collections.Generic;
using System.Text;
using Core;
using Model;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [Header("文案")] [SerializeField] private Text titleText, scoreText, detailText;
        [Header("按钮")] [SerializeField] private Button restartButton;

        private readonly StringBuilder _builder = new();

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDestroy()
        {
            restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.GameSettle, OnSettle);
            EventMgr.Subscribe(GameEvent.GameStart, OnStartGame);
            panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.GameSettle, OnSettle);
            EventMgr.Unsubscribe(GameEvent.GameStart, OnStartGame);
        }

        private void OnStartGame(object obj) => panelRoot.SetActive(false);

        private void OnSettle(object payload)
        {
            if (payload is not SettlePayload settle) return;

            panelRoot.SetActive(true);
            titleText.text = settle.IsWin ? "满载而归！" : "氧气耗尽…";
            scoreText.text = $"总分 {settle.Score}";
            detailText.text = BuildDetail(settle);
        }

        private void OnRestartClicked()
        {
            GameMgr.Instance.RestartGame();
            panelRoot.SetActive(false);
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

                if (countByName.TryAdd(name, 0))
                {
                    scoreByName[name] = 0;
                    order.Add(name);
                }

                countByName[name]++;
                scoreByName[name] += fish.Score;
            }

            foreach (var name in order)
            {
                _builder.AppendLine($"{name}  x{countByName[name]}   +{scoreByName[name]}");
            }

            return _builder.ToString();
        }
    }
}