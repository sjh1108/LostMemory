using System;
using System.Collections;
using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    public class Phase0ScreenShake : MonoBehaviour, IPhase0SceneVisualPlayer
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeMagnitude = 0.1f;

        public void Play(string sceneVisualId, Action onCompleted)
        {
            if (sceneVisualId == "SCN-05-SHAKE")
                StartCoroutine(DoShake(onCompleted));
            else
                onCompleted?.Invoke();
        }

        private IEnumerator DoShake(Action onCompleted)
        {
            Vector3 origin = targetCamera.transform.localPosition;
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude;
                targetCamera.transform.localPosition = origin + new Vector3(x, y, 0f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            targetCamera.transform.localPosition = origin;
            onCompleted?.Invoke();
        }
    }
}
