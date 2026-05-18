using System.Collections;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 활 발사 애니메이션. KhiBowController.ArrowFired 이벤트를 받아서
    /// drawFrames 순차 → releaseFrame → idleFrame 복귀 시퀀스를 코루틴으로 실행.
    /// KhiSlashAnimator 의 활 버전 (단순화 — SO 없이 직렬화 필드 직접).
    /// 같은 SpriteRenderer 의 sprite 를 매 frameInterval 마다 교체.
    /// 활 sprite GameObject (BowVisual 자식) 에 부착.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Bow Animator")]
    public class KhiBowAnimator : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private SpriteRenderer bowSprite;
        [SerializeField] private KhiBowController bowController;

        [Header("Frames")]
        [Tooltip("평소 표시 sprite (Bow4).")]
        [SerializeField] private Sprite idleFrame;
        [Tooltip("시위 당기기 단계 (Draw_1 → 2 → 3 순서).")]
        [SerializeField] private Sprite[] drawFrames;
        [Tooltip("시위 놓기 sprite (Release_1).")]
        [SerializeField] private Sprite releaseFrame;

        [Header("Timing")]
        [Tooltip("Draw 프레임 사이 간격.")]
        [SerializeField, Min(0.005f)] private float drawFrameInterval = 0.04f;
        [Tooltip("Release 프레임 유지 시간.")]
        [SerializeField, Min(0.005f)] private float releaseHold = 0.08f;

        private Coroutine _firingRoutine;

        private void Awake()
        {
            if (bowSprite == null) bowSprite = GetComponent<SpriteRenderer>();
            if (bowController == null) bowController = GetComponentInParent<KhiBowController>();
            ApplyIdle();
        }

        private void OnEnable()
        {
            if (bowController != null) bowController.ArrowFired += HandleArrowFired;
            ApplyIdle();
        }

        private void OnDisable()
        {
            if (bowController != null) bowController.ArrowFired -= HandleArrowFired;
            if (_firingRoutine != null)
            {
                StopCoroutine(_firingRoutine);
                _firingRoutine = null;
            }
            ApplyIdle();
        }

        private void HandleArrowFired(KhiAttackRequest _, bool __)
        {
            if (bowSprite == null) return;
            if (_firingRoutine != null) StopCoroutine(_firingRoutine);
            _firingRoutine = StartCoroutine(PlayFireSequence());
        }

        private IEnumerator PlayFireSequence()
        {
            if (drawFrames != null)
            {
                for (int i = 0; i < drawFrames.Length; i++)
                {
                    if (drawFrames[i] != null)
                    {
                        bowSprite.sprite = drawFrames[i];
                        yield return new WaitForSeconds(drawFrameInterval);
                    }
                }
            }

            if (releaseFrame != null)
            {
                bowSprite.sprite = releaseFrame;
                yield return new WaitForSeconds(releaseHold);
            }

            ApplyIdle();
            _firingRoutine = null;
        }

        private void ApplyIdle()
        {
            if (bowSprite != null && idleFrame != null)
            {
                bowSprite.sprite = idleFrame;
            }
        }
    }
}
