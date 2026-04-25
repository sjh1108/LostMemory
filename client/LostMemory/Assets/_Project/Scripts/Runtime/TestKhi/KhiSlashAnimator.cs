using System.Collections;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-067 2부. 슬래시 이펙트 재생기. SlashRig 컨테이너 하위의 사전 배치된 3개 슬롯을 재사용.
    /// 슬롯들은 Right aim 기준 baseline 위치/회전/크기가 baked되어 있고,
    /// 런타임에는 SlashRig transform만 회전/flip 시켜 전체를 aim 방향으로 매핑.
    /// 이렇게 하면 per-슬래시 offset 수학이 없어지고, 모든 슬래시가 동일 pivot 기준으로 일관되게 변환됨.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Slash Animator")]
    public class KhiSlashAnimator : MonoBehaviour
    {
        [Header("Refs (Awake에서 자동 resolve 가능)")]
        [SerializeField] private KhiMeleeComboController comboController;
        [Tooltip("슬래시 슬롯들을 담는 컨테이너. 비워두면 Awake에서 자동 생성하고 플레이어 자식으로 부착.")]
        [SerializeField] private Transform slashRig;
        [Tooltip("콤보 1/2/3타 전용 슬롯 오브젝트 (SpriteRenderer 보유). 비워두면 Awake에서 자동 생성하고 baseline 위치 적용.")]
        [SerializeField] private GameObject[] slashSlots = new GameObject[3];

        [Header("Combo 1 (1타) — 프레임 시퀀스 + tint만. 위치/크기/회전은 슬롯 transform 에서 튜닝.")]
        [SerializeField] private Sprite[] combo1Frames;
        [SerializeField] private Color combo1Tint = Color.white;

        [Header("Combo 2 (2타)")]
        [SerializeField] private Sprite[] combo2Frames;
        [SerializeField] private Color combo2Tint = Color.white;

        [Header("Combo 3 (3타)")]
        [SerializeField] private Sprite[] combo3Frames;
        [SerializeField] private Color combo3Tint = Color.white;

        [Header("Animation")]
        [SerializeField, Range(0.005f, 0.2f)] private float frameInterval = 0.04f;
        [SerializeField] private int slashSortingOrder = 1001;
        [SerializeField] private string slashSortingLayer = "";

        [Header("Hemisphere Mirror (좌반평면만 거울상 처리)")]
        [Tooltip("ON(권장): aim.x<0 에서만 slashRig.scale.x = -1 + rotation = aim°-180°. Left cardinal arc curl 보존, 우반평면은 순수 회전.\nOFF: 전 영역 순수 회전 (aim≈180° 근처 arc curl 뒤집혀 보일 수 있음).")]
        [SerializeField] private bool autoMirrorOnLeftAim = true;

        [Header("Rig Center")]
        [Tooltip("회전 pivot의 Y 좌표 (플레이어 로컬). 플레이어 가슴 높이로 두면 모든 방향 슬래시가 자연스러운 원형 스윕을 그림. aim.x=0 가로지를 때 시각적 중심 흔들림 방지.")]
        [SerializeField] private float slashRigCenterY = 0.6f;

        // 자동 생성 시 사용하는 baseline. prefab 에서 슬롯을 직접 배치하면 이 값들은 무시됨.
        private static readonly Vector2[] DefaultSlotLocalPositions =
        {
            new Vector2(1.05f, 0.55f),   // 1타: hitbox (1.05, -0.2) + vOff(0, 0.75) = (1.05, 0.55)
            new Vector2(1.05f, 0.2f),    // 2타: hitbox (1.05, 0.2) + vOff 0 = (1.05, 0.2)
            new Vector2(0.75f, 0.3f)     // 3타: hitbox (1.25, 0) + vOff(0, 0.3) + fOff(-0.5, 0) = (0.75, 0.3)
        };
        private static readonly float[] DefaultSlotLocalRotations = { 0f, 0f, -60f };
        private static readonly float[] DefaultSlotLocalScales = { 1.5f, 1.5f, 1.5f };

        private readonly Coroutine[] _playingCoroutines = new Coroutine[3];
        private readonly SpriteRenderer[] _slotRenderers = new SpriteRenderer[3];

        private void Awake()
        {
            if (comboController == null)
            {
                comboController = GetComponent<KhiMeleeComboController>();
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

        [ContextMenu("Reset Slot Positions To Baseline")]
        private void EditorResetSlotPositions()
        {
            if (slashSlots != null)
            {
                int count = Mathf.Min(3, slashSlots.Length);
                for (int i = 0; i < count; i++)
                {
                    if (slashSlots[i] != null)
                    {
                        slashSlots[i].transform.localPosition = DefaultSlotLocalPositions[i];
                        slashSlots[i].transform.localRotation = Quaternion.Euler(0f, 0f, DefaultSlotLocalRotations[i]);
                        float s = DefaultSlotLocalScales[i];
                        slashSlots[i].transform.localScale = new Vector3(s, s, 1f);
                    }
                }
            }
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
                    slotObj.transform.localPosition = DefaultSlotLocalPositions[i];
                    slotObj.transform.localRotation = Quaternion.Euler(0f, 0f, DefaultSlotLocalRotations[i]);
                    float s = DefaultSlotLocalScales[i];
                    slotObj.transform.localScale = new Vector3(s, s, 1f);
                    SpriteRenderer sr = slotObj.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = slashSortingOrder;
                    if (!string.IsNullOrEmpty(slashSortingLayer))
                    {
                        sr.sortingLayerName = slashSortingLayer;
                    }
                    sr.enabled = false;
                    slashSlots[i] = slotObj;
                }

                // SpriteRenderer 캐시 (prefab에서 수동 설정된 슬롯도 처리).
                _slotRenderers[i] = slashSlots[i].GetComponent<SpriteRenderer>();
                if (_slotRenderers[i] == null)
                {
                    _slotRenderers[i] = slashSlots[i].AddComponent<SpriteRenderer>();
                }
                _slotRenderers[i].enabled = false;
            }
        }

        private void HandleAttackActiveStarted(KhiAttackRequest request, KhiMeleeAttackStep step)
        {
            int slotIndex = Mathf.Clamp(step.ComboStep - 1, 0, 2);

            Sprite[] frames = slotIndex switch
            {
                1 => combo2Frames,
                2 => combo3Frames,
                _ => combo1Frames
            };
            Color tint = slotIndex switch
            {
                1 => combo2Tint,
                2 => combo3Tint,
                _ => combo1Tint
            };

            if (frames == null || frames.Length == 0)
            {
                return;
            }

            SpriteRenderer sr = _slotRenderers[slotIndex];
            if (sr == null || slashRig == null)
            {
                return;
            }

            // Rig transform: aim 방향으로 회전 + 좌반평면 hemisphere mirror.
            // slashRig 는 플레이어 자식이므로 localRotation/localScale만 조정.
            Vector2 aimDir = request.AimDirection;
            float aimAngleDeg = request.AimAngleDegrees;
            float rigRot;
            float rigScaleX;

            if (autoMirrorOnLeftAim && aimDir.x < 0f)
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
            float interval = Mathf.Max(0.005f, frameInterval);
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
