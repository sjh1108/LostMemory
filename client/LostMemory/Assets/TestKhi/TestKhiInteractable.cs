using UnityEngine;

namespace LostMemory.TestKhi
{
    public class TestKhiInteractable : MonoBehaviour
    {
        [SerializeField] private Color idleColor = new Color(1f, 0.75f, 0.2f);
        [SerializeField] private Color interactedColor = new Color(0.35f, 1f, 0.35f);

        private SpriteRenderer _renderer;
        private int _interactionCount;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            SetColor(idleColor);
        }

        public void Interact(GameObject actor)
        {
            _interactionCount++;
            bool isOddInteraction = (_interactionCount % 2) == 1;
            SetColor(isOddInteraction ? interactedColor : idleColor);
            Debug.Log($"{actor.name} interacted with {name}. Count: {_interactionCount}");
        }

        private void SetColor(Color color)
        {
            if (_renderer != null)
            {
                _renderer.color = color;
            }
        }
    }
}
