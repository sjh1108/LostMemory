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
        [Tooltip("CL-230: WeaponData 가 할당되면 procedural sword 의 크기/색/orbit 을 SO 에서 가져옴. " +
                 "비워두면 comboController.WeaponData 로 자동 fallback. 그조차 없으면 아래 Inspector 필드값 사용 (legacy).")]
        [SerializeField] private WeaponData weaponData;
        [Tooltip("CL-230: 본 인스턴스가 보조 손(Weapon_Sub)인지. " +
                 "ON: orbit 각도에 WeaponData.SecondaryOrbitAngleOffset 적용, swing 방향도 mirror. " +
                 "OFF (기본): 메인 손. 평소 그대로 동작.")]
        [SerializeField] private bool isSecondaryHand = false;

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

            // CL-230: weaponData fallback — comboController 의 SO 공유.
            if (weaponData == null && comboController != null)
            {
                weaponData = comboController.WeaponData;
            }

            if (downController == null)
            {
                KhiPlayerActionGate.TryResolveDownController(this, out downController);
            }

            if (weaponSprite == null)
            {
                return;
            }

            RebuildSpriteFromCurrentSource();
        }

        private void OnEnable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted += HandleAttackActiveStarted;
                comboController.AttackActiveEnded += HandleAttackActiveEnded;
                // CL-230: WeaponUpgradeService 가 controller.SetWeaponData 호출 시 자동 동기화.
                comboController.WeaponDataChanged += HandleWeaponDataChanged;

                // CL-230: 본 컴포넌트가 disable 된 동안 (활/스태프 모드 시) SO 가 교체되었을 가능성.
                // 이벤트는 그동안 unsubscribe 되어 있었으므로 재진입 시 controller 의 현재 SO 와 자기 weaponData 비교 후 sync.
                if (comboController.WeaponData != null && comboController.WeaponData != weaponData)
                {
                    weaponData = comboController.WeaponData;
                    RebuildSpriteFromCurrentSource();
                }
            }
        }

        private void OnDisable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted -= HandleAttackActiveStarted;
                comboController.AttackActiveEnded -= HandleAttackActiveEnded;
                comboController.WeaponDataChanged -= HandleWeaponDataChanged;
            }
        }

        /// <summary>
        /// CL-230: 외부에서 명시적으로 WeaponData 를 교체할 때 호출. controller.SetWeaponData 와 별개로 직접 호출도 허용.
        /// procedural sprite cache 무효화 후 재생성. externalSprite 가 있으면 그쪽 우선.
        /// </summary>
        public void SetWeaponData(WeaponData next)
        {
            if (next == null || next == weaponData)
            {
                return;
            }
            weaponData = next;
            RebuildSpriteFromCurrentSource();
        }

        /// <summary>
        /// CL-230: 외부 sprite 를 런타임에 교체. null 넘기면 procedural 로 복귀 (useProceduralIfNoExternal=true 일 때).
        /// KhiDaggerSpriteCycler 등 디버그 도구가 호출.
        /// </summary>
        public void SetExternalSprite(Sprite next)
        {
            externalSprite = next;
            RebuildSpriteFromCurrentSource();
        }

        private void HandleWeaponDataChanged(WeaponData previous, WeaponData next)
        {
            if (next == null || next == weaponData)
            {
                return;
            }
            weaponData = next;
            RebuildSpriteFromCurrentSource();
        }

        /// <summary>
        /// 현재 외부 sprite / WeaponData / Inspector 값에 따라 weaponSprite.sprite 를 다시 결정.
        /// 우선순위 (높은 → 낮은):
        ///   1) externalSprite (KhiDaggerSpriteCycler 등 디버그/런타임 override)
        ///   2) weaponData.WeaponSprite (SO 의 기본 sprite — 무기별 영구 설정)
        ///   3) procedural sword (useProceduralIfNoExternal=true 일 때)
        /// </summary>
        private void RebuildSpriteFromCurrentSource()
        {
            if (weaponSprite == null)
            {
                return;
            }

            Sprite chosen = externalSprite;
            // CL-230: externalSprite 가 없으면 SO 의 기본 sprite 우선 사용 (단검 SO 에 Dagger_2 같은 sprite 영구 등록).
            if (chosen == null && weaponData != null && weaponData.WeaponSprite != null)
            {
                chosen = weaponData.WeaponSprite;
            }
            if (chosen == null && useProceduralIfNoExternal)
            {
                // SO 의 procedural 파라미터가 바뀌었을 수 있으므로 cache 폐기 후 재생성.
                _proceduralSprite = CreateProceduralSwordSprite();
                chosen = _proceduralSprite;
            }

            if (chosen != null)
            {
                weaponSprite.sprite = chosen;
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

            // CL-230: swing 호/속도 배수를 SO 의 AttackStepData 에서 가져옴.
            // hard-coded 1.6/1.3 제거 → 무기마다 SO 에서 자유 설정.
            // 검 SO 3타 = swingArcMultiplier 1.6 / swingDurationMultiplier 1.3 (기존 큰 회전 보존)
            // 단검 SO 3타 = 둘 다 1.0 (회전 없음, 1/2타와 동일 swing)
            float arcMul = step.swingArcMultiplier > 0f ? step.swingArcMultiplier : 1f;
            float durMul = step.swingDurationMultiplier > 0f ? step.swingDurationMultiplier : 1f;

            // 콤보 단계별 swing 변형 (방향만 hard-coded, 호/속도는 SO multiplier 로 자유)
            float arc = swingArcDegrees * arcMul;
            float duration = swingDuration * durMul;
            float direction; // +1 = ccw (-→+), -1 = cw (+→-)
            switch (step.comboStep)
            {
                case 2: direction = +1f; break; // 좌→우 올려치기
                case 3: direction = -1f; break; // 큰 회전 마무리 (회전량은 SO multiplier 로 결정)
                default: direction = -1f; break; // 1타: 우→좌 내려치기
            }

            // CL-230: 보조 손이면 swing 방향 mirror (가위 모션). 메인 -1 → 보조 +1, 메인 +1 → 보조 -1.
            if (isSecondaryHand && weaponData != null && weaponData.UseSecondaryWeapon && weaponData.SecondaryMirrorSwing)
            {
                direction = -direction;
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
            // CL-230: 두 자루 위치/회전 분기.
            // - HandOrbitSpread: 메인/보조 위치를 aim 기준 대칭 시프트 (메인 +half, 보조 -half). 120 이면 X 좌표 같음.
            // - SecondaryOrbitAngleOffset: 보조만 추가 비대칭 시프트 (절충/특수 자세용).
            // - HandAngleSpread: sprite 회전 분기 (∧ vs X자 대기 자세).
            float orbitBaseAngle = baseAngle;
            float spriteAngleAdjust = 0f;
            if (weaponData != null && weaponData.UseSecondaryWeapon)
            {
                float halfOrbitSpread = weaponData.HandOrbitSpread * 0.5f;
                orbitBaseAngle += isSecondaryHand ? -halfOrbitSpread : +halfOrbitSpread;

                if (isSecondaryHand)
                {
                    orbitBaseAngle += weaponData.SecondaryOrbitAngleOffset;
                }

                float halfAngleSpread = weaponData.HandAngleSpread * 0.5f;
                // 메인은 +spread/2 (반시계, 칼끝 위쪽으로 모임), 보조는 -spread/2 (시계, 칼끝 위쪽으로 모임).
                // 둘 다 가운데 위쪽 한 점을 향함 → ∧ 자세. spread 90+ 이면 X자 교차.
                spriteAngleAdjust = isSecondaryHand ? -halfAngleSpread : +halfAngleSpread;

                // CL-230: reverse grip — 해당 손이 역수면 sprite 180도 회전 (칼끝이 캐릭터 쪽).
                bool isReverse = isSecondaryHand ? weaponData.SecondaryGripReverse : weaponData.MainGripReverse;
                if (isReverse)
                {
                    spriteAngleAdjust += 180f;
                }
            }
            else if (weaponData != null && !isSecondaryHand && weaponData.MainGripReverse)
            {
                // 단일 무기(useSecondaryWeapon=false) 라도 메인이 역수면 적용. 단검 한 자루 역수 모드 가능.
                spriteAngleAdjust += 180f;
            }
            float orbitAngle = orbitBaseAngle + _swingOffsetAngle;
            float finalAngle = baseAngle + _swingOffsetAngle + spriteAngleAdjust;
            // CL-230: _overridePresenterVisual 가 ON 이면 SO 의 orbit, 아니면 Inspector orbitRadius fallback.
            float baseOrbit = (weaponData != null && weaponData.OverridePresenterVisual) ? weaponData.PresenterOrbitRadius : orbitRadius;
            float effectiveRadius = baseOrbit + _swingOffsetRadius;

            // orbit position 은 orbitAngle (메인+보조 분기), sprite rotation 은 finalAngle (둘 다 aim 방향).
            // 보조 손은 위치만 캐릭터 반대편/측면, 칼날은 같은 방향 향함.
            float orbitRad = orbitAngle * Mathf.Deg2Rad;
            Vector3 orbitPos = new Vector3(Mathf.Cos(orbitRad) * effectiveRadius, verticalOffset + Mathf.Sin(orbitRad) * effectiveRadius, 0f);
            weaponSprite.transform.localPosition = orbitPos;
            weaponSprite.transform.localRotation = Quaternion.Euler(0f, 0f, finalAngle);

            if (flipYWhenAimLeft)
            {
                weaponSprite.flipY = aim.x < 0f;
            }
        }

        private Sprite CreateProceduralSwordSprite()
        {
            // CL-230: WeaponData._overridePresenterVisual 가 ON 일 때만 SO 의 procedural 파라미터 사용.
            // OFF (default) 면 KhiWeaponPresenter 의 Inspector 필드값 그대로 사용 (사용자가 손수 튜닝한 값 보존).
            bool useSO = weaponData != null && weaponData.OverridePresenterVisual;
            int w = useSO ? weaponData.PresenterWidthPx : swordWidthPx;
            int h = useSO ? weaponData.PresenterHeightPx : swordHeightPx;
            float ppu = useSO ? weaponData.PresenterPixelsPerUnit : pixelsPerUnit;
            Color resolvedBlade = useSO ? weaponData.PresenterBladeColor : bladeColor;
            Color resolvedHilt = useSO ? weaponData.PresenterHiltColor : hiltColor;
            Color resolvedGuard = useSO ? weaponData.PresenterGuardColor : guardColor;

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
                    tex.SetPixel(x, y, resolvedHilt);
                }
            }

            // 가드 (수직 바, 거의 위아래 끝까지)
            for (int x = handleEnd; x < guardEnd; x++)
            {
                for (int y = 1; y < h - 1; y++)
                {
                    tex.SetPixel(x, y, resolvedGuard);
                }
            }

            // 블레이드 (긴 직사각형)
            for (int x = guardEnd; x < bladeEnd; x++)
            {
                for (int y = centerY - bladeHalfHeight; y <= centerY + bladeHalfHeight; y++)
                {
                    tex.SetPixel(x, y, resolvedBlade);
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
                    tex.SetPixel(x, y, resolvedBlade);
                }
            }

            // 블레이드 하이라이트 (위쪽 가장자리에 1픽셀 밝은 줄)
            Color highlight = Color.Lerp(resolvedBlade, Color.white, 0.4f);
            int highlightY = centerY + bladeHalfHeight;
            for (int x = guardEnd; x < bladeEnd; x++)
            {
                tex.SetPixel(x, highlightY, highlight);
            }

            tex.Apply();

            // pivot: 손잡이 끝 (좌측 약간 안쪽, 세로 중앙)
            Vector2 pivot = new Vector2(0.04f, 0.5f);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), pivot, ppu);
            sprite.name = "KhiProceduralSwordSprite";
            return sprite;
        }
    }
}
