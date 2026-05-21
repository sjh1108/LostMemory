using System.Collections;
using LostMemory.Data;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-067 / CL-090. 슬래시 이펙트 재생기. SlashRig 컨테이너 하위의 사전 배치된 3개 슬롯을 재사용.
    ///
    /// 책임 분리 (CL-090):
    /// - 슬롯의 위치/회전/스케일 = prefab YAML 의 SlashSlot_1/2/3 transform 에서 결정. 본 컴포넌트가 건드리지 않음.
    /// - 슬롯에 표시할 그림 (frames) + 색조 (tint) + 콤보 시각 공통 (autoMirror, frameInterval) = WeaponData SO 에서 가져옴.
    /// - 회전 pivot Y (slashRigCenterY) = 캐릭터 가슴 높이 = 본 컴포넌트 잔존 (캐릭터 속성, 무기 무관).
    ///
    /// 런타임에는 SlashRig transform 만 회전/flip 시켜 전체를 aim 방향으로 매핑.
    /// 모든 슬래시가 동일 pivot 기준으로 일관되게 변환됨.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Slash Animator")]
    public class KhiSlashAnimator : MonoBehaviour
    {
        [Header("Refs (Awake에서 자동 resolve 가능)")]
        [SerializeField] private KhiMeleeComboController comboController;
        [Tooltip("무기 데이터 SO. frames/tint/autoMirror/frameInterval 을 여기서 가져옴.")]
        [SerializeField] private WeaponData weaponData;
        [Tooltip("슬래시 슬롯들을 담는 컨테이너. 비워두면 Awake에서 자동 생성하고 플레이어 자식으로 부착.")]
        [SerializeField] private Transform slashRig;
        [Tooltip("콤보 1/2/3타 전용 슬롯 오브젝트 (SpriteRenderer 보유). 위치/회전/스케일은 본 prefab transform 에서 디자이너가 직접 편집. 비워두면 Awake에서 자동 생성 (default transform).")]
        [SerializeField] private GameObject[] slashSlots = new GameObject[3];

        [Tooltip("CL-230: 추가 슬래시 슬롯. 사용 시 prefab에 SlashSlot_3a, _3b 등 GameObject 부착 후 드래그. " +
                 "AttackStepData.enableExtraSlashes == true 일 때 메인 슬래시와 동시 활성화. " +
                 "각 슬롯의 transform 은 prefab 에서 직접 편집 → 시각 미리보기 가능.")]
        [SerializeField] private GameObject[] extraSlashSlots = new GameObject[0];

        [Header("Sorting")]
        [SerializeField] private int slashSortingOrder = 1001;
        [SerializeField] private string slashSortingLayer = "";

        [Header("Element System (CL-230)")]
        [Tooltip("속성 시각 카탈로그. 비워두면 element 효과 적용 안 함 (slashTint 만 사용).")]
        [SerializeField] private LostMemory.Data.WeaponElementCatalog elementCatalog;
        [Tooltip("슬래시에 적용할 material (SlashElement.shader 사용). " +
                 "비워두면 SpriteRenderer 기본 material 사용 → element 효과 비활성. " +
                 "MaterialPropertyBlock 으로 색/glow/scroll 을 매 swing 마다 갱신하므로 material 한 개만 있으면 모든 인스턴스 공유 가능.")]
        [SerializeField] private Material elementMaterial;

        [Header("Rig Center")]
        [Tooltip("회전 pivot의 Y 좌표 (플레이어 로컬). 플레이어 가슴 높이로 두면 모든 방향 슬래시가 자연스러운 원형 스윕을 그림. aim.x=0 가로지를 때 시각적 중심 흔들림 방지.")]
        [SerializeField] private float slashRigCenterY = 0.6f;

        private readonly Coroutine[] _playingCoroutines = new Coroutine[3];
        private readonly SpriteRenderer[] _slotRenderers = new SpriteRenderer[3];

        // CL-230: extra 슬롯 캐시 (다중 슬래시용)
        private SpriteRenderer[] _extraSlotRenderers;
        private Coroutine[] _extraPlayingCoroutines;

        // CL-230: 메인 슬롯 prefab 초기 transform 캐싱 (override 후 복귀용)
        private Vector3[] _slotInitialPositions;
        private Quaternion[] _slotInitialRotations;
        private Vector3[] _slotInitialScales;

        // CL-230: MaterialPropertyBlock cache + shader property IDs
        private MaterialPropertyBlock _propBlock;
        // CL-230: SpriteRenderer 의 기본 material (Sprites/Default) 캐시. element 비활성 시 복귀용.
        private Material _defaultSlotMaterial;
        private static readonly int InnerColorId = Shader.PropertyToID("_InnerColor");
        private static readonly int OuterColorId = Shader.PropertyToID("_OuterColor");
        private static readonly int GradientThresholdId = Shader.PropertyToID("_GradientThreshold");
        private static readonly int GradientSoftnessId = Shader.PropertyToID("_GradientSoftness");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");

        private void Awake()
        {
            if (comboController == null)
            {
                comboController = GetComponent<KhiMeleeComboController>();
            }
            // weaponData fallback: comboController 의 SO 공유.
            if (weaponData == null && comboController != null)
            {
                weaponData = comboController.WeaponData;
            }
            if (weaponData == null)
            {
                Debug.LogError($"[KhiSlashAnimator] WeaponData 가 할당되지 않음. {name} 의 Inspector 에서 SO 자산을 드래그하세요.", this);
                enabled = false;
                return;
            }
            EnsureRigAndSlots();
        }

#if UNITY_EDITOR
        [ContextMenu("Setup Slash Rig (Edit Mode)")]
        private void EditorSetupSlashRig()
        {
            EnsureRigAndSlots();
            UnityEditor.EditorUtility.SetDirty(this);
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

        /// <summary>
        /// CL-230: KhiMeleeComboController.SetWeaponData 가 호출될 때 함께 호출되어 자체 weaponData 필드를 동기화.
        /// 재생 중인 frame 코루틴은 캡처된 frames 배열로 끝까지 진행되므로 화면 갑작스러운 튐 없음.
        /// 다음 swing 부터 새 SO 의 frames/tint/autoMirror/frameInterval 적용.
        /// </summary>
        public void SetWeaponData(WeaponData next)
        {
            if (next == null || next == weaponData)
            {
                return;
            }
            weaponData = next;
        }

        /// <summary>
        /// CL-230: 외부 (KhiDaggerTeleportController 등) 에서 슬래시 표시를 직접 호출.
        /// comboStep (1/2/3) 에 해당하는 step 의 frames/tint/transform override/extra 슬래시 모두 적용.
        /// aim 방향은 호출자가 결정 (전형적으로 KhiPlayerAim.GetAimDirection()).
        /// </summary>
        public void PlaySlashFromExternal(Vector2 aimDirection, int comboStep)
        {
            if (weaponData == null || weaponData.Steps == null || weaponData.Steps.Length == 0)
            {
                return;
            }
            int slotIndex = Mathf.Clamp(comboStep - 1, 0, weaponData.Steps.Length - 1);
            AttackStepData step = weaponData.Steps[slotIndex];
            PlaySlashFromExternal(aimDirection, step);
        }

        /// <summary>
        /// CL-230 오버로드: 외부에서 직접 AttackStepData 인스턴스 전달 (예: 우클릭 텔레포트 전용 슬래시).
        /// WeaponData 와 무관하게 호출자가 step 직접 구성 (frames/tint/transform/extra slashes 모두 포함).
        /// </summary>
        public void PlaySlashFromExternal(Vector2 aimDirection, AttackStepData step)
        {
            if (step == null)
            {
                return;
            }
            if (aimDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                aimDirection = Vector2.right;
            }
            KhiAttackRequest request = new KhiAttackRequest
            {
                SequenceId = 0,
                ComboStep = step.comboStep,
                AimDirection = aimDirection,
                AimAngleDegrees = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg,
                Origin = transform.position,
                StartedAt = Time.time,
                Attacker = gameObject
            };
            HandleAttackActiveStarted(request, step);
        }

        private void OnEnable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted += HandleAttackActiveStarted;
            }
        }

        private void OnDisable()
        {
            if (comboController != null)
            {
                comboController.AttackActiveStarted -= HandleAttackActiveStarted;
            }
        }

        private void EnsureRigAndSlots()
        {
            if (slashRig == null)
            {
                GameObject rigObj = new GameObject("SlashRig");
                rigObj.transform.SetParent(transform, worldPositionStays: false);
                rigObj.transform.localPosition = Vector3.zero;
                rigObj.transform.localRotation = Quaternion.identity;
                rigObj.transform.localScale = Vector3.one;
                slashRig = rigObj.transform;
            }

            if (slashSlots == null || slashSlots.Length != 3)
            {
                slashSlots = new GameObject[3];
            }

            for (int i = 0; i < 3; i++)
            {
                if (slashSlots[i] == null)
                {
                    GameObject slotObj = new GameObject($"SlashSlot_{i + 1}");
                    slotObj.transform.SetParent(slashRig, worldPositionStays: false);
                    // 디자이너가 prefab 에서 직접 위치/회전/스케일 편집. 자동 생성 시는 default 그대로.
                    slotObj.transform.localPosition = Vector3.zero;
                    slotObj.transform.localRotation = Quaternion.identity;
                    slotObj.transform.localScale = Vector3.one;
                    SpriteRenderer sr = slotObj.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = slashSortingOrder;
                    if (!string.IsNullOrEmpty(slashSortingLayer))
                    {
                        sr.sortingLayerName = slashSortingLayer;
                    }
                    sr.enabled = false;
                    slashSlots[i] = slotObj;
                }

                _slotRenderers[i] = slashSlots[i].GetComponent<SpriteRenderer>();
                if (_slotRenderers[i] == null)
                {
                    _slotRenderers[i] = slashSlots[i].AddComponent<SpriteRenderer>();
                }
                // CL-230: SpriteRenderer 의 기본 material (Sprites/Default) 캐시. element 비활성 시 복귀용.
                // 첫 슬롯의 기본 material 만 캐시 (모든 슬롯이 동일).
                if (_defaultSlotMaterial == null && _slotRenderers[i].sharedMaterial != null)
                {
                    _defaultSlotMaterial = _slotRenderers[i].sharedMaterial;
                }
                _slotRenderers[i].enabled = false;
            }

            // CL-230: 메인 슬롯 prefab 초기 transform 캐싱 (override 후 swing 끝나면 복귀용).
            // 첫 호출 시만 캐싱 (이후 호출은 이미 override된 값일 수 있으므로 skip).
            if (_slotInitialPositions == null)
            {
                _slotInitialPositions = new Vector3[3];
                _slotInitialRotations = new Quaternion[3];
                _slotInitialScales = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    if (slashSlots[i] != null)
                    {
                        Transform t = slashSlots[i].transform;
                        _slotInitialPositions[i] = t.localPosition;
                        _slotInitialRotations[i] = t.localRotation;
                        _slotInitialScales[i] = t.localScale;
                    }
                }
            }

            // CL-230: extra 슬롯 SpriteRenderer 캐시 (다중 슬래시용)
            if (extraSlashSlots != null && extraSlashSlots.Length > 0)
            {
                if (_extraSlotRenderers == null || _extraSlotRenderers.Length != extraSlashSlots.Length)
                {
                    _extraSlotRenderers = new SpriteRenderer[extraSlashSlots.Length];
                    _extraPlayingCoroutines = new Coroutine[extraSlashSlots.Length];
                }
                for (int i = 0; i < extraSlashSlots.Length; i++)
                {
                    if (extraSlashSlots[i] == null) continue;
                    _extraSlotRenderers[i] = extraSlashSlots[i].GetComponent<SpriteRenderer>();
                    if (_extraSlotRenderers[i] == null)
                    {
                        _extraSlotRenderers[i] = extraSlashSlots[i].AddComponent<SpriteRenderer>();
                    }
                    _extraSlotRenderers[i].enabled = false;
                }
            }
        }

        public void HandleAttackActiveStarted(KhiAttackRequest request, AttackStepData step)
        {
            int slotIndex = Mathf.Clamp(step.comboStep - 1, 0, 2);

            // CL-230: step 인자를 직접 사용 (외부에서 전달한 step 도 그대로 적용되도록).
            // 기존엔 weaponData.Steps[slotIndex] 로 재참조했지만, PlaySlashFromExternal(step) 같은
            // 외부 호출 시 외부 step 이 무시되는 버그가 있었음.
            // 일반 콤보 (KhiMeleeComboController) 호출 시에도 같은 SO step 전달되므로 동작 동일.
            AttackStepData visualStep = step;

            Sprite[] frames = visualStep.slashFrames;
            Color tint = visualStep.slashTint;

            if (frames == null || frames.Length == 0)
            {
                // [DiagVfx-Slash] H1 frames=null — 비-owner 측 weaponData 가 owner 와 다른 SO 일 때 자주 발생.
                Debug.Log($"[DiagVfx-Slash] H1 frames=null comboStep={step.comboStep} weaponData={(weaponData != null ? weaponData.name : "NULL")} visualStep.frames={(visualStep.slashFrames == null ? "null" : "len=0")}", this);
                return;
            }

            if (_slotRenderers == null || slotIndex >= _slotRenderers.Length)
            {
                Debug.Log($"[DiagVfx-Slash] H5 slot index out of range _slotRenderers={(_slotRenderers == null ? "null" : _slotRenderers.Length.ToString())} slotIndex={slotIndex}", this);
                return;
            }
            SpriteRenderer sr = _slotRenderers[slotIndex];
            if (sr == null || slashRig == null)
            {
                Debug.Log($"[DiagVfx-Slash] H5 sr/rig null sr={(sr == null ? "null" : "OK")} slashRig={(slashRig == null ? "null" : slashRig.name)}", this);
                return;
            }

            // Rig transform: aim 방향으로 회전 + 좌반평면 hemisphere mirror (옵션).
            Vector2 aimDir = request.AimDirection;
            float aimAngleDeg = request.AimAngleDegrees;
            float rigRot;
            float rigScaleX;

            if (weaponData.AutoMirrorOnLeftAim && aimDir.x < 0f)
            {
                // aim.x<0: Y축 대칭 (Q1 기준의 거울상)
                rigRot = aimAngleDeg - 180f;
                rigScaleX = -1f;
            }
            else
            {
                // 우반평면: 순수 회전
                rigRot = aimAngleDeg;
                rigScaleX = 1f;
            }

            slashRig.localRotation = Quaternion.Euler(0f, 0f, rigRot);
            slashRig.localScale = new Vector3(rigScaleX, 1f, 1f);
            // 회전 pivot은 플레이어 로컬 Y에 고정. 모든 슬래시가 동일 원형 궤도 위에서 스윕.
            slashRig.localPosition = new Vector3(0f, slashRigCenterY, 0f);

            // [DiagVfx-Slash] OK 경로 진입 시 parent transform / sr 상태 1회 dump. H2/H4 (parent disable/transform issue) 검증.
            // 비-owner 측에서 sr.enabled=true 인데도 visual 안 보이면 sr.gameObject.activeInHierarchy=false 또는 parent.localScale=(0,0,0) 의심.
            if (Time.frameCount % 60 == 0 || step.comboStep == 1) // 1초 throttle + 콤보 1타 매번
            {
                Transform parent = slashRig.parent;
                Debug.Log($"[DiagVfx-Slash] OK seq={request.SequenceId} comboStep={step.comboStep} slashRig.parent='{(parent != null ? parent.name : "null")}' parent.localScale={(parent != null ? parent.localScale.ToString() : "n/a")} parent.activeInHierarchy={(parent != null ? parent.gameObject.activeInHierarchy : false)} sr.enabled={sr.enabled} sr.activeInHierarchy={sr.gameObject.activeInHierarchy} sr.color.a={sr.color.a:F2}", this);
            }

            // 슬래시 프레임 재생 시작.
            sr.color = tint;
            sr.sortingOrder = slashSortingOrder;
            if (!string.IsNullOrEmpty(slashSortingLayer))
            {
                sr.sortingLayerName = slashSortingLayer;
            }
            sr.sprite = frames[0];
            sr.enabled = true;

            // CL-230: SlashSlot transform override (무기별 자세).
            // override ON 일 때만 SO 값으로 덮어씀. OFF (기본) 면 prefab transform 그대로 사용 → 기존 무기 동작 보존.
            if (visualStep.overrideSlashSlotTransform)
            {
                Transform slotTransform = sr.transform;
                slotTransform.localPosition = visualStep.slashSlotLocalPosition;
                slotTransform.localRotation = Quaternion.Euler(0f, 0f, visualStep.slashSlotRotationZ);
                slotTransform.localScale = visualStep.slashSlotLocalScale;
            }

            // CL-230: 속성 시각 적용 (Fire/Ice/Lightning 등 색·glow·scroll).
            ApplyElementVisual(sr);

            if (_playingCoroutines[slotIndex] != null)
            {
                StopCoroutine(_playingCoroutines[slotIndex]);
            }
            _playingCoroutines[slotIndex] = StartCoroutine(PlayFrames(slotIndex, sr, frames));

            // CL-230: extra 슬래시 활성화 (step.enableExtraSlashes 일 때만, 단검 3타 등 화려한 마무리용).
            // 각 extra 슬롯의 transform 은 prefab 에서 디자이너가 직접 편집 → 시각 미리보기 가능.
            if (visualStep.enableExtraSlashes && _extraSlotRenderers != null)
            {
                for (int i = 0; i < _extraSlotRenderers.Length; i++)
                {
                    SpriteRenderer extraSr = _extraSlotRenderers[i];
                    if (extraSr == null) continue;

                    extraSr.color = tint;
                    extraSr.sortingOrder = slashSortingOrder;
                    if (!string.IsNullOrEmpty(slashSortingLayer))
                    {
                        extraSr.sortingLayerName = slashSortingLayer;
                    }
                    extraSr.sprite = frames[0];
                    extraSr.enabled = true;

                    // 메인과 동일한 element material/색 적용
                    ApplyElementVisual(extraSr);

                    // 같은 frame animation 재생
                    if (_extraPlayingCoroutines[i] != null)
                    {
                        StopCoroutine(_extraPlayingCoroutines[i]);
                    }
                    _extraPlayingCoroutines[i] = StartCoroutine(PlayFramesGeneric(extraSr, frames));
                }
            }
        }

        /// <summary>
        /// CL-230: 슬롯 인덱스 없는 frame animation 코루틴 (extra 슬래시용).
        /// </summary>
        private IEnumerator PlayFramesGeneric(SpriteRenderer sr, Sprite[] frames)
        {
            float interval = Mathf.Max(0.005f, weaponData.FrameInterval);
            for (int i = 0; i < frames.Length; i++)
            {
                if (sr == null) yield break;
                Sprite s = frames[i];
                if (s != null) sr.sprite = s;
                yield return new WaitForSeconds(interval);
            }
            if (sr != null) sr.enabled = false;
        }

        /// <summary>
        /// CL-230: 매 swing 시작 시 element 여부에 따라 sr.sharedMaterial 동적 결정 + PropertyBlock 갱신.
        ///
        /// element 활성 (Fire/Ice/Lightning 등): sr.sharedMaterial = elementMaterial + PropertyBlock 으로 색/glow/scroll set.
        /// element 비활성 (None / catalog 없음 / visual 없음 / material 없음): sr.sharedMaterial = _defaultSlotMaterial 복귀 + PropertyBlock clear.
        ///
        /// 이 분기 덕분에 element 없는 무기 (검 등) 슬래시는 기존 동작 그대로 (셰이더 영향 X).
        /// </summary>
        private void ApplyElementVisual(SpriteRenderer sr)
        {
            if (sr == null) return;

            bool useElement = elementMaterial != null
                              && elementCatalog != null
                              && weaponData != null
                              && weaponData.CurrentElement != LostMemory.Data.WeaponElement.None;

            LostMemory.Data.WeaponElementVisual visual = null;
            if (useElement)
            {
                visual = elementCatalog.Get(weaponData.CurrentElement);
                if (visual == null) useElement = false;
            }

            if (useElement)
            {
                // element 적용: elementMaterial + 색/glow/scroll PropertyBlock.
                if (sr.sharedMaterial != elementMaterial)
                {
                    sr.sharedMaterial = elementMaterial;
                }

                if (_propBlock == null)
                {
                    _propBlock = new MaterialPropertyBlock();
                }
                sr.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(InnerColorId, visual.innerColor);
                _propBlock.SetColor(OuterColorId, visual.outerColor);
                _propBlock.SetFloat(GradientThresholdId, visual.gradientThreshold);
                _propBlock.SetFloat(GradientSoftnessId, visual.gradientSoftness);
                _propBlock.SetFloat(GlowIntensityId, visual.glowIntensity);
                _propBlock.SetVector(ScrollSpeedId, new Vector4(visual.scrollSpeed.x, visual.scrollSpeed.y, 0f, 0f));
                sr.SetPropertyBlock(_propBlock);
            }
            else
            {
                // element 비활성: 기본 material 복귀 + PropertyBlock clear (이전 element 색 잔존 방지).
                if (_defaultSlotMaterial != null && sr.sharedMaterial != _defaultSlotMaterial)
                {
                    sr.sharedMaterial = _defaultSlotMaterial;
                }
                if (_propBlock != null)
                {
                    sr.GetPropertyBlock(_propBlock);
                    _propBlock.Clear();
                    sr.SetPropertyBlock(_propBlock);
                }
            }
        }

        private IEnumerator PlayFrames(int slotIndex, SpriteRenderer sr, Sprite[] frames)
        {
            float interval = Mathf.Max(0.005f, weaponData.FrameInterval);
            for (int i = 0; i < frames.Length; i++)
            {
                if (sr == null)
                {
                    yield break;
                }
                Sprite s = frames[i];
                if (s != null)
                {
                    sr.sprite = s;
                }
                yield return new WaitForSeconds(interval);
            }
            if (sr != null)
            {
                sr.enabled = false;
            }
            // CL-230: swing 끝나면 slot transform 을 prefab 초기값으로 복귀.
            // 텔레포트 step 의 overrideSlashSlotTransform 이 변경한 transform 이 다음 swing 에 잔존하지 않도록.
            RestoreSlotTransform(slotIndex);
            _playingCoroutines[slotIndex] = null;
        }

        /// <summary>
        /// CL-230: 메인 슬롯 transform 을 prefab 초기값 (Awake 시 캐싱) 으로 복귀.
        /// override 가 다음 swing 에 누적되지 않도록 보장.
        /// </summary>
        private void RestoreSlotTransform(int slotIndex)
        {
            if (_slotInitialPositions == null || slotIndex < 0 || slotIndex >= _slotInitialPositions.Length) return;
            if (slashSlots == null || slotIndex >= slashSlots.Length || slashSlots[slotIndex] == null) return;

            Transform t = slashSlots[slotIndex].transform;
            t.localPosition = _slotInitialPositions[slotIndex];
            t.localRotation = _slotInitialRotations[slotIndex];
            t.localScale = _slotInitialScales[slotIndex];
        }
    }
}
