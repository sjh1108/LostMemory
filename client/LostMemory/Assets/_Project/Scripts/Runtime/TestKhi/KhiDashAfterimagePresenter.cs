using System.Collections;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public class KhiDashAfterimagePresenter : MonoBehaviour
    {
        [SerializeField] private KhiDashController dash;
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.035f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.22f;
        [SerializeField] private Color afterimageColor = new Color(0.45f, 0.9f, 1f, 0.42f);
        [SerializeField] private int sortingOrderOffset = -1;

        private float _nextSpawnAt;
        private bool _wasDashing;

        private void Awake()
        {
            dash ??= GetComponent<KhiDashController>();
            sourceRenderer ??= GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (dash == null || sourceRenderer == null || sourceRenderer.sprite == null)
            {
                return;
            }

            if (!dash.IsDashing)
            {
                _wasDashing = false;
                return;
            }

            if (!_wasDashing || Time.time >= _nextSpawnAt)
            {
                SpawnAfterimage();
                _nextSpawnAt = Time.time + spawnInterval;
            }

            _wasDashing = true;
        }

        private void SpawnAfterimage()
        {
            GameObject afterimageObject = new GameObject("Khi Dash Afterimage");
            afterimageObject.transform.position = sourceRenderer.transform.position;
            afterimageObject.transform.rotation = sourceRenderer.transform.rotation;
            afterimageObject.transform.localScale = sourceRenderer.transform.lossyScale;

            SpriteRenderer afterimageRenderer = afterimageObject.AddComponent<SpriteRenderer>();
            afterimageRenderer.sprite = sourceRenderer.sprite;
            afterimageRenderer.flipX = sourceRenderer.flipX;
            afterimageRenderer.flipY = sourceRenderer.flipY;
            afterimageRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            afterimageRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            afterimageRenderer.color = afterimageColor;

            StartCoroutine(FadeAndDestroy(afterimageRenderer, afterimageObject));
        }

        private IEnumerator FadeAndDestroy(SpriteRenderer afterimageRenderer, GameObject afterimageObject)
        {
            float startedAt = Time.time;
            Color startColor = afterimageRenderer.color;

            while (afterimageRenderer != null)
            {
                float progress = Mathf.Clamp01((Time.time - startedAt) / lifetime);
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                afterimageRenderer.color = color;

                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            if (afterimageObject != null)
            {
                Destroy(afterimageObject);
            }
        }
    }
}
