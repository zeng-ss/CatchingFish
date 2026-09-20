using System.Collections.Generic;
using Core;
using DG.Tweening;
using Model;
using Tool;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 结算面板。
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private GameObject mask, panelRoot;
        [SerializeField] private Transform content;
        [Header("文案")] [SerializeField] private Text titleText, scoreText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Button restartButton;
        [Header("条目")] [SerializeField] private string itemPath = "Prefab/ResultItem";
        [Tooltip("面板弹出时长")] [SerializeField] private float panelPopDuration = 0.22f;

        [Tooltip("相邻两条渔获出现的间隔")] [SerializeField]
        private float itemStagger = 0.08f;

        [Tooltip("每条渔获的动画时长")] [SerializeField]
        private float itemDuration = 0.4f;

        [Tooltip("总分滚动时长")] [SerializeField] private float scoreCountDuration = 0.6f;

        private readonly List<ResultItem> _items = new();
        private Sequence _seq;

        // ==================================================================

        private void Awake()
        {
            panelRoot = gameObject;
            EventMgr.Subscribe(GameEvent.SettleAnimDone, OnSettle);
            restartButton.onClick.AddListener(OnRestartClicked);
            Hide(); // 开局先收起来
        }

        private void OnDestroy()
        {
            EventMgr.Unsubscribe(GameEvent.SettleAnimDone, OnSettle);
            restartButton.onClick.RemoveListener(OnRestartClicked);
            _seq?.Kill();
        }

        // ==================================================================
        // 显示 / 隐藏
        // ==================================================================

        private void OnSettle(object payload)
        {
            if (payload is not SettlePayload settle) return;
            Show(settle);
        }

        private void Show(SettlePayload settle)
        {
            _seq?.Kill();
            ClearItems();
            panelRoot.SetActive(true);
            mask.SetActive(true);
            titleText.text = settle.IsWin ? "满载而归！" : "氧气耗尽…";
            scoreText.text = "总分 0";
            summaryText.text = $"本次渔获 {settle.CaughtCount} 条";
            panelRoot.transform.localScale = Vector3.zero;

            _seq = DOTween.Sequence();

            // ② 面板弹出
            _seq.Append(panelRoot.transform.DOScale(Vector3.one, panelPopDuration).SetEase(Ease.OutBack, 1.4f));

            // ③ 渔获一条条出现
            IReadOnlyList<CaughtFish> list = settle.FishList;
            int count = list?.Count ?? 0;
            float itemsStart = _seq.Duration();

            for (int i = 0; i < count; i++)
            {
                ResultItem item = SpawnItem(list[i]);
                if (item == null) continue;
                _seq.Insert(itemsStart + i * itemStagger, item.BuildShow(i));
            }

            float itemsEnd = itemsStart + (count > 0 ? (count - 1) * itemStagger + itemDuration : 0f);

            // ④ 总分滚动
            _seq.Insert(itemsEnd, CountUpScore(settle.Score));

            // ⑤ 最后才让"再来一局"浮现，视线被吸到按钮上
            Transform button = restartButton.transform;
            button.localScale = Vector3.zero;
            _seq.Insert(itemsEnd + scoreCountDuration, button.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack));
        }

        private void Hide()
        {
            _seq?.Kill();
            _seq = null;
            ClearItems();
            mask.SetActive(false);
            panelRoot.SetActive(false);
        }

        private void OnRestartClicked()
        {
            GameMgr.Instance.RestartGame();
            _seq?.Kill();
            _seq = DOTween.Sequence();
            _seq.Append(panelRoot.transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack));
            _seq.AppendCallback(Hide);
        }

        private ResultItem SpawnItem(CaughtFish fish)
        {
            GameObject go = ResourcesMgr.Load<GameObject>(itemPath, content);
            if (go == null) return null;
            ResultItem item = go.GetComponent<ResultItem>();
            if (item == null)
            {
                Debug.LogError(
                    $"[ResultPanel] {itemPath} 上没有挂 ResultItem 脚本，请给预制体补上并拖好 nameTxt / scoreTxt / canvasGroup。");
                Destroy(go);
                return null;
            }

            item.UpdateData(fish);
            _items.Add(item);
            return item;
        }

        private void ClearItems()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                ResultItem item = _items[i];
                if (item == null) continue;
                item.Kill(); // 先掐动画，再销毁，免得 tween 指着已销毁的物体
                Destroy(item.gameObject);
            }

            _items.Clear();
        }

        private Tween CountUpScore(int total)
        {
            float value = 0f;
            return DOTween.To(() => value, v =>
            {
                value = v;
                if (scoreText != null)
                {
                    scoreText.text = $"总分 {Mathf.RoundToInt(v)}";
                }
            }, total, scoreCountDuration).SetEase(Ease.OutQuad);
        }
    }
}