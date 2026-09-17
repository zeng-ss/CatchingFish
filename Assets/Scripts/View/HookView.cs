using Controller;
using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    public class HookView : MonoBehaviour
    {
        private HookController _controller;

        public void Bind(HookController controller) => _controller = controller;

        private void LateUpdate()
        {
            if (_controller == null) return;
            Vector3 p = _controller.Position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null || other == null) return;
            FishView fish = other.GetComponent<FishView>();
            if (fish == null || fish.Id < 0) return;
            _controller.OnHookTouchedFish(fish.Id);
        }
    }
}