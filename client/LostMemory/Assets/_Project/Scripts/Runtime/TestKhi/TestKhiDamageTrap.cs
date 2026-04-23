using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public class TestKhiDamageTrap : MonoBehaviour
    {
        [SerializeField] private float damage = 10f;
        [SerializeField] private float damageInterval = 0.5f;
        [SerializeField] private Vector2 size = new Vector2(1.8f, 1.2f);
        [SerializeField] private Color color = new Color(1f, 0.1f, 0.1f, 0.35f);
        [SerializeField] private bool logDamageToConsole = false;

        private readonly Dictionary<Health, float> _nextDamageTimes = new Dictionary<Health, float>();
        private BoxCollider2D _collider;
        private SpriteRenderer _renderer;

        private void Awake()
        {
            ConfigureForTest();
        }

        public void ConfigureForTest()
        {
            _collider ??= GetComponent<BoxCollider2D>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider2D>();
            }

            _collider.isTrigger = true;
            _collider.size = size;

            _renderer ??= GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
                _renderer.sprite = CreateSprite("TestKhiDamageTrapSprite", Color.white);
            }

            _renderer.color = color;
            _renderer.sortingOrder = 3;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth == null)
            {
                return;
            }

            Character character = targetHealth.GetComponentInParent<Character>();
            if (character == null || character.CharacterType != Character.CharacterTypes.Player)
            {
                return;
            }

            if (_nextDamageTimes.TryGetValue(targetHealth, out float nextDamageTime) && Time.time < nextDamageTime)
            {
                return;
            }

            _nextDamageTimes[targetHealth] = Time.time + damageInterval;
            targetHealth.Damage(damage, gameObject, 0f, 0f, Vector3.zero);

            if (logDamageToConsole)
            {
                Debug.Log($"[TestKhiDamageTrap] Damage attempted on {targetHealth.name}, invulnerable={targetHealth.Invulnerable}, health={targetHealth.CurrentHealth}.");
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                _nextDamageTimes.Remove(targetHealth);
            }
        }

        private static Sprite CreateSprite(string spriteName, Color fillColor)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = spriteName + "Texture"
            };

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fillColor;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName;
            return sprite;
        }
    }
}
