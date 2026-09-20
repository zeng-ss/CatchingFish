using DG.Tweening;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【表现层 · 挂在 Main Camera 上】镜头纵向移动。
    /// 时长由 GameMgr 算好（= 鱼钩走完这段距离所需时间），所以镜头和鱼钩天然同步。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Tooltip("游玩时的高度")] [SerializeField] private float startY;

        [Tooltip("开始画面（水面）的高度")] [SerializeField] private float endY;

        public void StartMove(bool isEnd, float duration)
        {
            transform.DOMoveY(isEnd ? endY : startY, duration).SetEase(isEnd ? Ease.Linear : Ease.InQuad);
        }
    }
}