using System.Collections;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public class TestKhiDamageDummy : MonoBehaviour
    {
        [SerializeField] private float health = 100f;
        [SerializeField] private Vector2 colliderSize = new Vector2(1f, 1f);
        [SerializeField] private Color idleColor = new Color(0.2f, 0.75f, 0.35f, 0.9f);
        [SerializeField] private Color hitColor = new Color(1f, 0.95f, 0.2f, 1f);
        [SerializeField] private float hitFlashDuration = 0.08f;
        [SerializeField] private float reviveDelay = 0.25f;
        [SerializeField] private bool logHits = true;

        private Health _health;
        private SpriteRenderer _spriteRenderer;
        private Coroutine _flashRoutine;
        private Coroutine _reviveRoutine;

        private void Awake()
        {
            EnsureVisuals();
            EnsureCollider();
            EnsureHealth();
        }

        private void OnEnable()
        {
            EnsureHealth();
            _health.OnHit += HandleHit;
            _health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (_health == null)
            {
                return;
            }

            _health.OnHit -= HandleHit;
            _health.OnDeath -= HandleDeath;
        }

        private void EnsureVisuals()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (_spriteRenderer.sprite == null)
            {
                _spriteRenderer.sprite = CreatePixelSprite();
            }

            _spriteRenderer.color = idleColor;
            _spriteRenderer.sortingOrder = 10;
            transform.localScale = new Vector3(colliderSize.x, colliderSize.y, 1f);
        }

        private void EnsureCollider()
        {
            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            boxCollider.size = Vector2.one;
            boxCollider.isTrigger = false;
        }

        private void EnsureHealth()
        {
            _health = GetComponent<Health>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<Health>();
            }

            _health.InitialHealth = health;
            _health.MaximumHealth = health;
            _health.DestroyOnDeath = false;
            _health.DisableCollisionsOnDeath = false;
            _health.DisableChildCollisionsOnDeath = false;
            _health.DisableModelOnDeath = false;
            _health.SetHealth(health);
        }

        private void HandleHit()
        {
            if (logHits)
            {
                float displayHealth = Mathf.Max(0f, _health.CurrentHealth);
                Debug.Log($"[TestKhiDamageDummy] Hit {name}, health={displayHealth}/{_health.MaximumHealth}");
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashRoutine = StartCoroutine(FlashHit());
        }

        private void HandleDeath()
        {
            if (_reviveRoutine != null)
            {
                StopCoroutine(_reviveRoutine);
            }

            _reviveRoutine = StartCoroutine(ReviveAfterDelay());
        }

        private IEnumerator FlashHit()
        {
            _spriteRenderer.color = hitColor;
            yield return new WaitForSeconds(hitFlashDuration);
            _spriteRenderer.color = idleColor;
            _flashRoutine = null;
        }

        private IEnumerator ReviveAfterDelay()
        {
            yield return new WaitForSeconds(reviveDelay);
            _health.Revive();
            _health.SetHealth(health);
            _spriteRenderer.color = idleColor;
            _reviveRoutine = null;
        }

        private static Sprite CreatePixelSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "TestKhiDamageDummyTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "TestKhiDamageDummySprite";
            return sprite;
        }
    }
}
