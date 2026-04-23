using System;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-016 플레이어 카메라 추적 컨트롤러.
    /// Deadzone + SmoothDamp 기반 follow. 플레이어가 deadzone 안에 있을 때는 카메라 정지,
    /// 벗어나면 부드럽게 추적한다. Lookahead는 slot만 예약하고 기본값은 0 → 후속 CL에서
    /// 값만 주입하면 자연스럽게 동작하도록 설계.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Player Camera")]
    [DefaultExecutionOrder(220)]
    [RequireComponent(typeof(Camera))]
    public class KhiPlayerCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("Deadzone (world units, full size)")]
        [SerializeField, Min(0f)] private float deadzoneWidth = 1.2f;
        [SerializeField, Min(0f)] private float deadzoneHeight = 0.7f;

        [Header("Damping")]
        [SerializeField, Min(0f)] private float smoothTime = 0.18f;
        [SerializeField, Min(0f)] private float maxSpeed = 40f;

        [Header("Behavior")]
        [SerializeField] private bool snapOnStart = true;
        [SerializeField] private bool snapOnTargetChange = true;

        [Header("Lookahead (reserved for future CL)")]
        [Tooltip("Inspector에서 수동 테스트용. 런타임 코드는 ExternalLookahead 또는 LookaheadProvider 사용 권장.")]
        [SerializeField] private Vector2 editorLookahead;

        [Header("Debug")]
        [SerializeField] private bool drawDeadzoneGizmo = true;

        private Vector3 _velocity;
        private bool _hasSnapped;

        /// <summary>코드에서 직접 주입하는 lookahead 값. (0,0)이면 무효.</summary>
        public Vector2 ExternalLookahead { get; set; }

        /// <summary>매 프레임 lookahead를 계산해 돌려주는 provider. null이면 ExternalLookahead + editorLookahead 사용.</summary>
        public Func<Vector2> LookaheadProvider { get; set; }

        public Transform FollowTarget => followTarget;

        public void SetFollowTarget(Transform target)
        {
            if (followTarget == target)
            {
                return;
            }

            followTarget = target;

            if (snapOnTargetChange)
            {
                _hasSnapped = false;
            }
        }

        private void OnEnable()
        {
            if (!snapOnStart)
            {
                _hasSnapped = true;
            }
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            Vector2 lookahead = LookaheadProvider != null ? LookaheadProvider() : ExternalLookahead + editorLookahead;
            Vector2 followPoint = (Vector2)followTarget.position + new Vector2(offset.x, offset.y) + lookahead;

            if (!_hasSnapped)
            {
                transform.position = new Vector3(followPoint.x, followPoint.y, offset.z);
                _velocity = Vector3.zero;
                _hasSnapped = true;
                return;
            }

            Vector2 currentCam = transform.position;
            Vector2 desiredCam = ResolveDesiredPosition(currentCam, followPoint);
            Vector3 target3 = new Vector3(desiredCam.x, desiredCam.y, offset.z);

            Vector3 next = Vector3.SmoothDamp(transform.position, target3, ref _velocity, smoothTime, maxSpeed, Time.deltaTime);
            next.z = offset.z;
            transform.position = next;
        }

        private Vector2 ResolveDesiredPosition(Vector2 cam, Vector2 followPoint)
        {
            float halfW = deadzoneWidth * 0.5f;
            float halfH = deadzoneHeight * 0.5f;

            float dx = followPoint.x - cam.x;
            float dy = followPoint.y - cam.y;

            float adjustX = 0f;
            float adjustY = 0f;

            if (dx > halfW) adjustX = dx - halfW;
            else if (dx < -halfW) adjustX = dx + halfW;

            if (dy > halfH) adjustY = dy - halfH;
            else if (dy < -halfH) adjustY = dy + halfH;

            return new Vector2(cam.x + adjustX, cam.y + adjustY);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawDeadzoneGizmo)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3(transform.position.x, transform.position.y, 0f);
            Gizmos.DrawWireCube(center, new Vector3(deadzoneWidth, deadzoneHeight, 0f));
        }
#endif
    }
}
