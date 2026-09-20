using System.Collections;
using System.Collections.Generic;
using Config;
using Core;
using DG.Tweening;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 【表现层】开局教程的"导演"。
    ///
    /// 它在场景另一处（z≈200）的**布景**上按剧本实时演一遍玩法，完全不碰游戏那一套
    /// Model / Controller —— 所以教程和玩家那一局彻底隔离，改剧本也不用动画面的代码。
    ///
    /// 画面链路：GuideCamera --(targetTexture)--> RenderTexture --(RawImage)--> TutorialPanel
    /// 事件链路：发 TutorialHint / TutorialOxygen / TutorialFinished，收 TutorialReplay / TutorialStartGame
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialDirector : MonoBehaviour
    {
        private const float MaxOxygen = 100f;
        private const float OxygenPerHit = 50f;
        private const string OxygenTweenId = "TutorialOxygen";

        [Header("布景")] [SerializeField] private Transform hook;
        [Tooltip("道具鱼的根节点")] [SerializeField] private Transform fishRoot;
        [Tooltip("GuideCamera。面板会在运行时把 RenderTexture 交给它")] [SerializeField]
        private Camera filmCamera;

        [Header("节奏")]
        [Tooltip("下潜多少世界单位")] [SerializeField] private float diveDistance = 6.2f;
        [Tooltip("下潜用时")] [SerializeField] private float diveDuration = 2.4f;
        [Tooltip("上浮用时")] [SerializeField] private float riseDuration = 2.8f;
        [Tooltip("演示左右拖动时，钩子往左走多远")] [SerializeField] private float slideDistance = 2.4f;
        [Tooltip("左右拖动用时")] [SerializeField] private float slideDuration = 1.6f;
        [Tooltip("鱼从多远处游进来")] [SerializeField] private float fishSpawnX = 7.2f;
        [Tooltip("教程里的鱼速")] [SerializeField] private float fishSpeed = 1.6f;

        [Tooltip("下潜时撞到的那条鱼")] [SerializeField] private string hitFishName = "Carp";
        [Tooltip("上浮时依次抓到的三条鱼")] [SerializeField]
        private string[] catchFishNames = { "Frog", "Swan", "Kingfisher" };

        [Header("文案")] [SerializeField] private string hintSlide = "按住鼠标左键，可以左右拖动鱼钩";
        [SerializeField] private string hintDive = "鱼钩下潜时撞到鱼，会损失氧气";
        [SerializeField] private string hintRise = "上浮时碰到鱼，就能把它一起抓上来";

        private readonly List<TutorialFishView> _fish = new();
        private Vector3 _readyPos; // 钩子在场景里摆好的位置
        private float _oxygen;
        private Coroutine _story;

        // ==================================================================

        private void Awake()
        {
            _readyPos = hook.position;
            BuildFish();
        }

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.TutorialReplay, OnReplay);
            EventMgr.Subscribe(GameEvent.TutorialStartGame, OnStartGame);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.TutorialReplay, OnReplay);
            EventMgr.Unsubscribe(GameEvent.TutorialStartGame, OnStartGame);
        }

        private void Start() => Play();

        private void OnDestroy()
        {
            DOTween.Kill(OxygenTweenId);
            hook.DOKill();
        }

        /// <summary>从头演一遍。</summary>
        private void Play()
        {
            if (!isActiveAndEnabled) return;
            if (_story != null) StopCoroutine(_story);
            _story = StartCoroutine(PlayStory());
        }

        private void OnReplay(object payload) => Play();

        /// <summary>玩家点了"开始游戏"：停演并把布景收起来，免得它继续占性能或意外入镜。</summary>
        private void OnStartGame(object payload)
        {
            if (_story != null)
            {
                StopCoroutine(_story);
                _story = null;
            }

            DOTween.Kill(OxygenTweenId);
            hook.DOKill();
            HideStage();
        }

        // ==================================================================
        // 剧本
        // ==================================================================

        private IEnumerator PlayStory()
        {
            ResetStage();
            yield return Wait(0.7f);

            // ① 左右移动
            SetHint(hintSlide);
            yield return Run(hook.DOMoveX(_readyPos.x - slideDistance, slideDuration).SetEase(Ease.InOutSine));
            yield return Wait(0.35f);
            yield return Run(hook.DOMoveX(_readyPos.x, slideDuration * 0.7f).SetEase(Ease.InOutSine));

            // ② 下潜：撞到鱼 → 掉氧气
            SetHint(hintDive);
            yield return Wait(0.5f);

            float diveY = _readyPos.y - diveDistance;
            const float diveMeet = 0.5f; // 下潜到一半的时候撞上
            float meetTime = diveDuration * diveMeet;
            float travel = Travel();

            TutorialFishView blocker = FishAt(0);
            if (blocker != null)
            {
                blocker.SwimIn(Mathf.Lerp(_readyPos.y, diveY, diveMeet), _readyPos.x, _readyPos.z, 1, travel);

                // 鱼比钩子先出场：等它游到位，钩子再开始下潜 —— 两者就正好在半路相遇
                float lead = travel - meetTime;
                if (lead > 0f) yield return Wait(lead);
            }

            Tween dive = hook.DOMoveY(diveY, diveDuration).SetEase(Ease.Linear);
            yield return Wait(meetTime);
            Hurt();
            yield return Run(dive);

            // ③ 上浮：碰到鱼 → 一起抓上来
            SetHint(hintRise);
            yield return Wait(0.4f);
            yield return RiseAndCatch(diveY);

            // ④ 收尾：交给面板弹 [再看一遍][开始游戏]
            SetHint(string.Empty);
            _story = null;
            EventMgr.Publish(GameEvent.TutorialFinished);
        }

        /// <summary>
        /// 上浮段：钩子线性上升，三条鱼分别在上升的不同进度上与它相遇。
        ///
        /// 相遇时刻是**排时间表算出来的**，不是等固定秒数：
        ///   鱼 i 的相遇时刻 = riseDuration * meetRatio[i]
        ///   鱼 i 的出场时刻 = 相遇时刻 − travel（travel = 从出场到经过钩子所需时间，可能为负 = 上浮前就该出场）
        /// 出场和抓取按时刻排序后依次执行，所以调任何参数都不会错位。
        /// </summary>
        private IEnumerator RiseAndCatch(float diveY)
        {
            float topY = _readyPos.y;
            float travel = Travel();
            float[] meetRatio = { 0.30f, 0.55f, 0.80f };

            // 1) 排时间表：time < 0 表示"上浮开始之前"就该出场了
            var schedule = new List<(float time, int fish, bool spawn)>();
            for (int i = 0; i < meetRatio.Length && i < catchFishNames.Length; i++)
            {
                float meetAt = riseDuration * meetRatio[i];
                schedule.Add((meetAt - travel, i, true));
                schedule.Add((meetAt, i, false));
            }

            schedule.Sort((a, b) => a.time.CompareTo(b.time));

            // 2) 上浮之前该出场的先出场
            int index = 0;
            while (index < schedule.Count && schedule[index].time < 0f)
            {
                if (index > 0) yield return Wait(schedule[index].time - schedule[index - 1].time);
                RevealFish(schedule[index].fish, diveY, topY, meetRatio);
                index++;
            }

            // 3) 到点启动上浮
            if (index > 0) yield return Wait(-schedule[index - 1].time);
            Tween rise = hook.DOMoveY(topY, riseDuration).SetEase(Ease.Linear);

            // 4) 剩下的按时刻依次执行
            float clock = 0f;
            for (; index < schedule.Count; index++)
            {
                (float time, int fish, bool spawn) item = schedule[index];
                if (item.time > clock)
                {
                    yield return Wait(item.time - clock);
                    clock = item.time;
                }

                if (item.spawn)
                {
                    RevealFish(item.fish, diveY, topY, meetRatio);
                }
                else
                {
                    FishAt(item.fish + 1)?.Attach(hook, item.fish);
                }
            }

            yield return Run(rise);
        }

        // ==================================================================
        // 零件
        // ==================================================================

        private void RevealFish(int index, float diveY, float topY, float[] meetRatio)
        {
            float lane = Mathf.Lerp(diveY, topY, meetRatio[index]);
            int side = index % 2 == 0 ? 1 : -1; // 左右交替进场，画面不单调

            FishAt(index + 1)?.SwimIn(lane, _readyPos.x, _readyPos.z, side, Travel());
        }

        /// <summary>鱼从出场到经过钩子要游多久 —— 所有"什么时候相遇"都基于它算。</summary>
        private float Travel() => fishSpawnX / Mathf.Max(0.01f, fishSpeed);

        /// <summary>撞鱼：钩子闪烁（复用受击事件）+ 氧气下降。</summary>
        private void Hurt()
        {
            EventMgr.Publish(GameEvent.FishHurt, (int)OxygenPerHit);

            DOTween.To(() => _oxygen, v =>
            {
                _oxygen = v;
                EventMgr.Publish(GameEvent.TutorialOxygen, _oxygen / MaxOxygen);
            }, Mathf.Max(0f, _oxygen - OxygenPerHit), 0.45f).SetEase(Ease.OutQuad).SetId(OxygenTweenId);
        }

        private void ResetStage()
        {
            _oxygen = MaxOxygen;
            EventMgr.Publish(GameEvent.TutorialOxygen, 1f);
            SetHint(string.Empty);

            DOTween.Kill(OxygenTweenId);
            hook.DOKill(); // 重播时把上一遍没跑完的位移掐掉
            hook.position = _readyPos;

            foreach (TutorialFishView fish in _fish)
            {
                fish.Hide();
            }
        }

        private void HideStage()
        {
            foreach (TutorialFishView fish in _fish)
            {
                fish.Hide();
            }

            hook.position = _readyPos;
            hook.gameObject.SetActive(false);

            // 布景根节点（GuideBG）一起收掉：这一局剩下的时间都不会再看到它
            if (fishRoot.root != null && fishRoot.root != fishRoot)
            {
                fishRoot.root.gameObject.SetActive(false);
            }

            filmCamera.enabled = false;
        }

        /// <summary>按名字把道具鱼造出来挂到布景上。只造一次，重播时复用。</summary>
        private void BuildFish()
        {
            var names = new List<string> { hitFishName };
            names.AddRange(catchFishNames);

            foreach (string name in names)
            {
                GameObject prefab = Resources.Load<GameObject>(FishConfig.GetPrefabPath(name));
                if (prefab == null)
                {
                    Debug.LogWarning($"[TutorialDirector] 找不到鱼预制体：{name}");
                    continue;
                }

                GameObject go = Instantiate(prefab, fishRoot);
                go.name = $"TutorialFish_{name}";

                TutorialFishView view = go.GetComponent<TutorialFishView>() ?? go.AddComponent<TutorialFishView>();
                view.SwimSpeed = fishSpeed;
                view.Hide();
                _fish.Add(view);
            }
        }

        private TutorialFishView FishAt(int index) => index >= 0 && index < _fish.Count ? _fish[index] : null;

        private static void SetHint(string text) => EventMgr.Publish(GameEvent.TutorialHint, text ?? string.Empty);

        private static YieldInstruction Wait(float seconds) => new WaitForSeconds(Mathf.Max(0f, seconds));

        /// <summary>把 DOTween 的 Tween 包成协程，剧本里读起来就是一行。</summary>
        private static IEnumerator Run(Tween tween)
        {
            yield return tween.WaitForCompletion();
        }
    }
}
