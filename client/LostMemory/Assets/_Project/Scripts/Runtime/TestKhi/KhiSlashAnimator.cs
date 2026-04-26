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

        [Header("Sorting")]
        [SerializeField] private int slashSortingOrder = 1001;
        [SerializeField] private string slashSortingLayer = "";

        [Header("Rig Center")]
        [Tooltip("회전 pivot의 Y 좌표 (플레이어 로컬). 플레이어 가슴 높이로 두면 모든 방향 슬래시가 자연스러운 원형 스윕을 그림. aim.x=0 가로지를 때 시각적 중심 흔들림 방지.")]
        [SerializeField] private float slashRigCenterY = 0.6f;

        private readonly Coroutine[] _playingCoroutines = new Coroutine[3];
        private readonly SpriteRenderer[] _slotRenderers = new SpriteRenderer[3];

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
                _slotRenderers[i].enabled = false;
            }
        }

        private void HandleAttackActiveStarted(KhiAttackRequest request, AttackStepData step)
        {
            int slotIndex = Mathf.Clamp(step.comboStep - 1, 0, 2);

            // weaponData 의 step 데이터에서 frames/tint 가져옴.
            AttackStepData[] steps = weaponData.Steps;
            if (steps == null || slotIndex >= steps.Length)
            {
                return;
            }
            AttackStepData visualStep = steps[slotIndex];

            Sprite[] frames = visualStep.slashFrames;
            Color tint = visualStep.slashTint;

            if (frames == null || frames.Length == 0)
            {
                return;
            }

            SpriteRenderer sr = _slotRenderers[slotIndex];
            if (sr == null || slashRig == null)
            {
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

            // 슬래시 프레임 재생 시작.
            sr.color = tint;
            sr.sortingOrder = slashSortingOrder;
            if (!string.IsNullOrEmpty(slashSortingLayer))
            {
                sr.sortingLayerName = slashSortingLayer;
            }
            sr.sprite = frames[0];
            sr.enabled = true;

            if (_playingCoroutines[slotIndex] != null)
            {
                StopCoroutine(_playingCoroutines[slotIndex]);
            }
            _playingCoroutines[slotIndex] = StartCoroutine(PlayFrames(slotIndex, sr, frames));
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
            _playingCoroutines[slotIndex] = null;
        }
    }
}
