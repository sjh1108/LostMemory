using System.Collections;
using LostMemory.Data;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-067 1부. 플레이어 자식 Weapon 오브젝트에 부착.
    /// 탑다운 360° 회전 친화적인 단순 검을 코드로 생성하거나 외부 Sprite 슬롯 사용.
    /// 콤보별 sprite 전환 없음 — 슬래시 호 효과는 KhiAttackVisualPresenter가 담당.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Weapon Presenter")]
    public class KhiWeaponPresenter : MonoBehaviour
    {
        [Header("Refs (Awake에서 자동 resolve 가능)")]
        [SerializeField] private SpriteRenderer weaponSprite;
        [SerializeField] private KhiPlayerAim playerAim;
        [SerializeField] private KhiMeleeComboController comboController;
        [SerializeField] private KhiDownController downController;

        [Header("Sprite Source")]
        [SerializeField] private Sprite externalSprite;
        [SerializeField] private bool useProceduralIfNoExternal = true;

        [Header("Procedural Sword (placeholder)")]
        [SerializeField, Min(8)] private int swordWidthPx = 96;
        [SerializeField, Min(4)] private int swordHeightPx = 16;
        [SerializeField, Min(8f)] private float pixelsPerUnit = 64f;
        [SerializeField] private Color bladeColor = new Color(0.85f, 0.88f, 0.95f, 1f);
        [SerializeField] private Color hiltColor = new Color(0.40f, 0.25f, 0.10f, 1f);
        [SerializeField] private Color guardColor = new Color(0.85f, 0.70f, 0.20f, 1f);

        [Header("Behavior")]
        [SerializeField] private bool flipYWhenAimLeft = true;

        [Header("Orbit (캐릭터 주위 회전)")]
        [SerializeField, Min(0f)] private float orbitRadius = 0.4f;
        [SerializeField] private float verticalOffset = 0.4f;

        [Header("Swing Animation (공격 시 검을 호 모양으로 휘두름)")]
        [SerializeField, Min(0f)] private float swingArcDegrees = 90f;
        [SerializeField, Min(0.01f)] private float swingDuration = 0.15f;
        [SerializeField, Min(0f)] private float swingForwardThrust = 0.1f;

        [Header("HLD-style: 공격 active 동안 검 숨김 (슬래시 VFX가 모든 시각 담당)")]
        [Tooltip("ON: AttackActiveStarted에 검 안 보이고, AttackActiveEnded에 다시 보임. Windup/Recovery에선 보임 유지.")]
        [SerializeField] private bool hideWeaponDuringActive = true;

        private Sprite _proceduralSprite;
        private float _swingOffsetAngle;
        private float _swingOffsetRadius;
        private Coroutine _swingCoroutine;

        private void Awake()
        {
            if (weaponSprite == null)
            {
                weaponSprite = GetComponent<SpriteRenderer>();
            }
            if (playerAim == null)
            {
                playerAim = GetComponentInParent<KhiPlayerAim>();
            }

            if (comboController == null)
            {
                comboController = GetComponentInParent<KhiMeleeComboController>();
            }

            if (downController == null)
            {
                KhiPlayerActionGate.TryResolveDownController(this, out downController);
            }

            if (weaponSprite == null)
            {
                return;
            }

            Sprite chosen = externalSprite;
            if (chosen == null && useProceduralIfNoExternal)
            {
                _proceduralSprite ??= CreateProceduralSwordSprite();
                chosen = _proceduralSprite;
            }

            if (chosen != null)
            {
                weaponSprite.sprite = chosen;
            }
        }

        private void OnEnable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted += HandleAttackActiveStarted;
                comboController.AttackActiveEnded += HandleAttackActiveEnded;
            }
        }

        private void OnDisable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted -= HandleAttackActiveStarted;
                comboController.AttackActiveEnded -= HandleAttackActiveEnded;
            }
        }

        public void HandleAttackActiveStarted(KhiAttackRequest request, AttackStepData step)
        {
            if (hideWeaponDuringActive && weaponSprite != null)
            {
                weaponSprite.enabled = false;
            }

            if (_swingCoroutine != null)
            {
                StopCoroutine(_swingCoroutine);
            }

            // 콤보 단계별 swing 변형 (방향/호 크기/속도 차등)
            float arc;
            float direction; // +1 = ccw (-→+), -1 = cw (+→-)
            float duration;
            switch (step.comboStep)
            {
                case 2: arc = swingArcDegrees;        direction = +1f; duration = swingDuration;        break; // 좌→우 올려치기
                case 3: arc = swingArcDegrees * 1.6f; direction = -1f; duration = swingDuration * 1.3f; break; // 큰 회전 마무리
                default: arc = swingArcDegrees;       direction = -1f; duration = swingDuration;        break; // 1타: 우→좌 내려치기
            }

            _swingCoroutine = StartCoroutine(SwingArc(arc, direction, duration));
        }

        public void HandleAttackActiveEnded(KhiAttackRequest request, AttackStepData step)
        {
            // HLD-style: active 종료 시 검 다시 표시 + swing offset 즉시 reset (snap 복귀)
            if (hideWeaponDuringActive && weaponSprite != null)
            {
                weaponSprite.enabled = true;
            }

            if (_swingCoroutine != null)
            {
                StopCoroutine(_swingCoroutine);
                _swingCoroutine = null;
            }
            _swingOffsetAngle = 0f;
            _swingOffsetRadius = 0f;
        }

        private IEnumerator SwingArc(float arcDegrees, float direction, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float halfArc = arcDegrees * 0.5f;
            float startAngle = -halfArc * direction;
            float endAngle = +halfArc * direction;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // smoothstep ease-in-out: 가속 → 피크 → 감속 (실제 검 휘두름 곡선)
                float eased = t * t * (3f - 2f * t);
                _swingOffsetAngle = Mathf.Lerp(startAngle, endAngle, eased);
                // 전방 push: sin 곡선 (호 가운데에서 가장 멀리)
                _swingOffsetRadius = Mathf.Sin(t * Mathf.PI) * swingForwardThrust;
                yield return null;
            }

            _swingOffsetAngle = 0f;
            _swingOffsetRadius = 0f;
            _swingCoroutine = null;
        }

        private void Update()
        {
            if (weaponSprite == null || playerAim == null)
            {
                return;
            }

            if (KhiPlayerActionGate.IsBlocked(downController))
            {
                return;
            }

            Vector2 aim = playerAim.GetAimDirection();
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float finalAngle = baseAngle + _swingOffsetAngle;
            float effectiveRadius = orbitRadius + _swingOffsetRadius;

            // orbit + swing offset
            float rad = finalAngle * Mathf.Deg2Rad;
            Vector3 orbitPos = new Vector3(Mathf.Cos(rad) * effectiveRadius, verticalOffset + Mathf.Sin(rad) * effectiveRadius, 0f);
            weaponSprite.transform.localPosition = orbitPos;
            weaponSprite.transform.localRotation = Quaternion.Euler(0f, 0f, finalAngle);

            if (flipYWhenAimLeft)
            {
                weaponSprite.flipY = aim.x < 0f;
            }
        }

        private Sprite CreateProceduralSwordSprite()
        {
            int w = swordWidthPx;
            int h = swordHeightPx;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "KhiProceduralSword",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // 투명 초기화
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            // 레이아웃 (검은 +X 방향을 가리킴, pivot은 손잡이 = 좌측):
            // [손잡이][가드][블레이드 ────────[팁]
            int handleEnd = Mathf.Max(2, w / 6);
            int guardEnd = handleEnd + Mathf.Max(2, w / 24);
            int bladeEnd = w - Mathf.Max(2, h / 4);
            int centerY = h / 2;
            int handleHalfHeight = Mathf.Max(1, h / 6);
            int bladeHalfHeight = Mathf.Max(1, h / 5);

            // 손잡이 (좁은 직사각형)
            for (int x = 0; x < handleEnd; x++)
            {
                for (int y = centerY - handleHalfHeight; y <= centerY + handleHalfHeight; y++)
                {
                    tex.SetPixel(x, y, hiltColor);
                }
            }

            // 가드 (수직 바, 거의 위아래 끝까지)
            for (int x = handleEnd; x < guardEnd; x++)
            {
                for (int y = 1; y < h - 1; y++)
                {
                    tex.SetPixel(x, y, guardColor);
                }
            }

            // 블레이드 (긴 직사각형)
            for (int x = guardEnd; x < bladeEnd; x++)
            {
                for (int y = centerY - bladeHalfHeight; y <= centerY + bladeHalfHeight; y++)
                {
                    tex.SetPixel(x, y, bladeColor);
                }
            }

            // 팁 (삼각형 테이퍼)
            int taperLength = w - bladeEnd;
            for (int i = 0; i < taperLength; i++)
            {
                int x = bladeEnd + i;
                int half = Mathf.Max(0, bladeHalfHeight - 1 - i);
                for (int y = centerY - half; y <= centerY + half; y++)
                {
                    tex.SetPixel(x, y, bladeColor);
                }
            }

            // 블레이드 하이라이트 (위쪽 가장자리에 1픽셀 밝은 줄)
            Color highlight = Color.Lerp(bladeColor, Color.white, 0.4f);
            int highlightY = centerY + bladeHalfHeight;
            for (int x = guardEnd; x < bladeEnd; x++)
            {
                tex.SetPixel(x, highlightY, highlight);
            }

            tex.Apply();

            // pivot: 손잡이 끝 (좌측 약간 안쪽, 세로 중앙)
            Vector2 pivot = new Vector2(0.04f, 0.5f);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), pivot, pixelsPerUnit);
            sprite.name = "KhiProceduralSwordSprite";
            return sprite;
        }
    }
}
