using UnityEngine;

namespace LostMemory.TestKhi
{
    public class TestKhiDoorAction : MonoBehaviour
    {
        [SerializeField] private string doorLabel = "TestKhi Door";
        [SerializeField] private GameObject doorRoot;
        [SerializeField] private SpriteRenderer doorRenderer;
        [SerializeField] private Collider2D doorCollider;
        [SerializeField] private bool startsOpen = false;
        [SerializeField] private Color closedColor = new Color(0.55f, 0.58f, 0.62f, 1f);
        [SerializeField] private Color openColor = new Color(0.2f, 0.9f, 0.55f, 0.35f);

        private bool _isOpen;
        private bool _initialized;
        private Collider2D[] _doorColliders;

        public void Configure(SpriteRenderer renderer, Collider2D collider)
        {
            doorRenderer = renderer;
            doorCollider = collider;
            doorRoot = renderer != null ? renderer.gameObject : collider != null ? collider.gameObject : doorRoot;
            RefreshDoorColliders();

            if (!_initialized)
            {
                _isOpen = startsOpen;
                _initialized = true;
            }

            ApplyState();
        }

        private void Awake()
        {
            if (!_initialized)
            {
                _isOpen = startsOpen;
                _initialized = true;
            }

            ApplyState();
        }

        public void Activate()
        {
            SetOpen(!_isOpen);
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;
            _initialized = true;
            ApplyState();
            Debug.Log($"[TestKhiDoor] {doorLabel} is now {(_isOpen ? "open" : "closed")}. colliders={ColliderCount}, blocking={!_isOpen}");
        }

        private void ApplyState()
        {
            RefreshDoorColliders();

            if (doorRenderer != null)
            {
                doorRenderer.enabled = !_isOpen;
                doorRenderer.color = _isOpen ? openColor : closedColor;
            }

            foreach (Collider2D collider in _doorColliders)
            {
                if (collider != null)
                {
                    collider.enabled = !_isOpen;
                }
            }
        }

        private int ColliderCount => _doorColliders != null ? _doorColliders.Length : 0;

        private void RefreshDoorColliders()
        {
            if (doorRoot == null)
            {
                doorRoot = doorCollider != null ? doorCollider.gameObject : doorRenderer != null ? doorRenderer.gameObject : null;
            }

            if (doorRoot == null)
            {
                _doorColliders = new Collider2D[0];
                return;
            }

            _doorColliders = doorRoot.GetComponentsInChildren<Collider2D>(true);
        }
    }
}
