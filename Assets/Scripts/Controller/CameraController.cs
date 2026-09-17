using DG.Tweening;
using UnityEngine;

namespace Controller
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float startY;
        [SerializeField] private float endY;

        public void StartMove(bool isEnd, float duration)
        {
            transform.DOMoveY(isEnd ? endY : startY, duration).SetEase(isEnd ? Ease.Linear : Ease.InQuad);
        }
    }
}