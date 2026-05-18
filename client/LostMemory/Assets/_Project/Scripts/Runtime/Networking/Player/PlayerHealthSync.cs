using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 플레이어 Health 의 server-authoritative sync.
    ///
    /// - server (host): Update 에서 Health.CurrentHealth 변경 감지 → NetworkVariable write.
    /// - 비-server (게스트): OnNetworkSpawn 에서 Health.DamageDisabled() 호출 → 자체 Damage 호출 차단.
    ///     OnValueChanged → Health.SetHealth(value).
    ///
    /// 사망 sync: server 측 Damage 누적 → CurrentHealth=0 → Health.Kill() (자체 OnDeath 흐름).
    /// 게스트 측은 NetworkVariable sync 로 health=0 보고 → SetHealth 적용 → TDE Health 가 자체 처리.
    ///
    /// 부착: player prefab 의 root (NetworkObject + PlayerMovementSync 옆).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Lost Memory/Networking/Player Health Sync")]
    public sealed class PlayerHealthSync : NetworkBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private float minDelta = 0.01f;

        [Header("Hit/Death sync")]
        [SerializeField, Tooltip("server 측 health 감소 시 게스트 측 Animator SetTrigger(피격 trigger) 호출 + flicker SFX 등 시각 sync.")]
        private bool syncHit = true;

        [SerializeField, Tooltip("게스트 측 피격 시 호출할 Animator Trigger 이름. Hero_Animator 에는 'Hit' 가 정의되어 있음.")]
        private string hitAnimatorTrigger = "Hit";

        [SerializeField, Tooltip("호스트의 KhiDownController.DownEntered event 발화 시 게스트 측 Animator SetTrigger(down trigger) 호출. " +
            "임계값 추측 없이 실제 EnterDown 시점을 hook.")]
        private bool syncDown = true;

        [SerializeField, Tooltip("게스트 측 down 모션용 Animator Trigger 이름. Hero_Animator 에는 'Down' 정의됨.")]
        private string downAnimatorTrigger = "Down";

        [SerializeField, Tooltip("사망 시 모든 클라에서 Animator SetTrigger + Collider2D disable + 입력 컴포넌트 disable.")]
        private bool syncDeath = true;

        [SerializeField, Tooltip("게스트 측 사망 시 호출할 Animator Trigger 이름. Hero_Animator 에는 'Down' (TDE 표준 'Death' 미사용). " +
            "다른 prefab 이면 적절한 이름으로 교체.")]
        private string deathAnimatorTrigger = "Down";

        [SerializeField, Tooltip("사망 시 disable 할 player 입력 컴포넌트 이름. 본 prefab 의 자기 + 자식에서 검색.")]
        private string[] inputComponentNamesToDisableOnDeath = new[]
        {
            "KhiMeleeComboController",
            "KhiParryController",
            "KhiDashController",
            "KhiFinisherLunge",
        };

        [SerializeField, Tooltip("owner 측 사망 시 SetActive(true) 할 UI GameObject. " +
            "예: NicknameCanvas 자식에 비활성 'You Died' 텍스트 두고 wireup. 비워두면 OnGUI fallback 으로 'You Died' 표시.")]
        private GameObject deathOverlayObject;

        [SerializeField, Tooltip("deathOverlayObject 가 wireup 안 됐을 때 OnGUI 로 표시할 사망 문구.")]
        private string deathOverlayFallbackText = "You Died";

        [SerializeField, Tooltip("OnNetworkSpawn / NetworkVariable write / OnValueChanged / Death 로그 출력.")]
        private bool verboseLog = false;

        private readonly NetworkVariable<float> _syncedHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _deathTriggered;
        private bool _hasBeenAlive;
        private bool _showDeathOverlayOnGui;
        private KhiDownController _downController;
        private bool _downSubscribed;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            _downController = GetComponent<KhiDownController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (health == null)
            {
                Debug.LogWarning($"[PlayerHealthSync] Health 컴포넌트 누락: {gameObject.name}", this);
                return;
            }

            _syncedHealth.OnValueChanged += HandleSyncedHealthChanged;

            if (IsServer)
            {
                _syncedHealth.Value = health.CurrentHealth;
                if (verboseLog) Debug.Log($"[PlayerHealthSync] OnNetworkSpawn SERVER {gameObject.name} initial={health.CurrentHealth}", this);

                // server 만 KhiDownController.DownEntered 구독 → 발화 시 ClientRpc 로 모든 클라에 down 시각 sync.
                if (syncDown && _downController != null)
                {
                    _downController.DownEntered += HandleDownEntered;
                    _downSubscribed = true;
                }
            }
            else
            {
                health.DamageDisabled();
                if (_syncedHealth.Value > 0f)
                {
                    health.SetHealth(_syncedHealth.Value);
                }
                if (verboseLog) Debug.Log($"[PlayerHealthSync] OnNetworkSpawn CLIENT {gameObject.name} synced={_syncedHealth.Value}", this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _syncedHealth.OnValueChanged -= HandleSyncedHealthChanged;
            if (_downSubscribed && _downController != null)
            {
                _downController.DownEntered -= HandleDownEntered;
                _downSubscribed = false;
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || health == null) return;

            float current = health.CurrentHealth;
            if (Mathf.Abs(current - _syncedHealth.Value) >= minDelta)
            {
                float prev = _syncedHealth.Value;
                if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER write {gameObject.name} {prev} -> {current}", this);
                _syncedHealth.Value = current;

                // 피격 sync — health 가 감소했을 때 (= 데미지) 게스트 측 시각 효과 ClientRpc 발화.
                // _hasBeenAlive 게이트 통과 후에만 (Initialization 직후의 0→100 sync 를 hit 으로 잘못 트리거하지 않도록).
                if (syncHit && _hasBeenAlive && current < prev && current > 0f)
                {
                    TriggerHitClientRpc();
                }
            }

            // 한 번이라도 살아있었음을 확인 — TDE Health 의 Initialization 전 일시적 0 으로 인한 false positive 사망 감지 차단.
            if (current > 0f) _hasBeenAlive = true;

            // 사망 감지 — 살아있던 적이 있고 현재 0 이하 → 모든 클라에 시각 + 입력 차단 sync.
            if (syncDeath && !_deathTriggered && _hasBeenAlive && current <= 0f)
            {
                _deathTriggered = true;
                if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER death detected. Broadcast: {gameObject.name}", this);
                TriggerDeathClientRpc();
            }
        }

        private void HandleSyncedHealthChanged(float previous, float current)
        {
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT OnValueChanged {gameObject.name} {previous} -> {current}", this);
            health.SetHealth(current);
            // 사망 시각 / 입력 차단 / UI 는 TriggerDeathClientRpc 에서 처리.
        }

        [ClientRpc]
        private void TriggerHitClientRpc()
        {
            // 호스트는 자체 Health.Damage 흐름으로 시각 처리. 게스트만 모방.
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT hit broadcast received. IsOwner={IsOwner} {gameObject.name}", this);

            Animator animator = health.GetComponentInChildren<Animator>();
            if (animator != null && !string.IsNullOrWhiteSpace(hitAnimatorTrigger))
            {
                animator.SetTrigger(hitAnimatorTrigger);
            }
        }

        private void HandleDownEntered(float duration)
        {
            // KhiDownController.DownEntered 는 호스트 측 자기 인스턴스 + host-side 게스트 인스턴스 둘 다에서 발화 가능.
            // 본 PlayerHealthSync 는 server 만 구독 (OnNetworkSpawn 분기). 발화 시 모든 클라에 시각 sync.
            if (!IsServer) return;
            if (verboseLog) Debug.Log($"[PlayerHealthSync] SERVER DownEntered. Broadcast: {gameObject.name}", this);
            TriggerDownClientRpc();
        }

        [ClientRpc]
        private void TriggerDownClientRpc()
        {
            // 호스트는 자체 KhiDownController.EnterDown 자체 흐름으로 시각 처리. 게스트만 모방.
            if (IsServer) return;
            if (health == null) return;

            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT down broadcast received. IsOwner={IsOwner} {gameObject.name}", this);

            Animator animator = health.GetComponentInChildren<Animator>();
            if (animator != null && !string.IsNullOrWhiteSpace(downAnimatorTrigger))
            {
                animator.SetTrigger(downAnimatorTrigger);
            }

            // owner 측만 — Animator parameter 갱신 컴포넌트들을 disable. Down state 유지.
            // (호스트의 host-side 게스트 인스턴스는 IsServer=true 라 위 첫 줄 return. 호스트 자기 흐름 보존.)
            if (IsOwner)
            {
                CharacterMovement cm = GetComponent<CharacterMovement>();
                if (cm != null) cm.enabled = false;

                // KhiAnimatorMovementBinder 는 자식 GameObject (MinimalCharacterModel) 의 컴포넌트.
                // 매 frame Walking/Speed/Idle Animator parameter 갱신 → Down state 에서 다른 state 로 transition 유발.
                // 그래서 같이 disable.
                MonoBehaviour[] allBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < allBehaviours.Length; i++)
                {
                    MonoBehaviour mb = allBehaviours[i];
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName == "KhiAnimatorMovementBinder" || typeName == "KhiSpriteFlipBinder")
                    {
                        mb.enabled = false;
                    }
                }
            }
        }

        [ClientRpc]
        private void TriggerDeathClientRpc()
        {
            if (verboseLog) Debug.Log($"[PlayerHealthSync] CLIENT death broadcast received. IsServer={IsServer} IsOwner={IsOwner} {gameObject.name}", this);

            // 비-server (게스트) — 호스트는 자체 Health.Kill 흐름으로 처리됨. 게스트만 시각 모방.
            if (!IsServer && health != null)
            {
                Animator animator = health.GetComponentInChildren<Animator>();
                if (animator != null && !string.IsNullOrWhiteSpace(deathAnimatorTrigger))
                {
                    animator.SetTrigger(deathAnimatorTrigger);
                }
                foreach (Collider2D col in health.GetComponentsInChildren<Collider2D>())
                {
                    col.enabled = false;
                }
            }

            // 모든 클라 — owner 측 입력 컴포넌트 disable.
            // (비-owner 측은 PlayerMovementSync.disableInputComponentsOnNonOwner 로 이미 disable 됨 — 중복 호출 무해.)
            DisableInputComponentsOnDeath();

            // owner 측 — 사망 UI 표시.
            if (IsOwner)
            {
                if (deathOverlayObject != null)
                {
                    // 사용자가 Inspector 에서 wireup 한 GameObject 가 prefab asset 이 아닌 scene instance 인지 검사.
                    // prefab asset 이면 SetActive 가 런타임 효과 없음 → 그 경우 OnGUI fallback 으로 대체.
                    if (deathOverlayObject.scene.IsValid())
                    {
                        deathOverlayObject.SetActive(true);
                    }
                    else
                    {
                        if (verboseLog) Debug.LogWarning($"[PlayerHealthSync] deathOverlayObject 가 prefab asset reference — OnGUI fallback 사용: {gameObject.name}", this);
                        _showDeathOverlayOnGui = true;
                    }
                }
                else
                {
                    _showDeathOverlayOnGui = true;
                }
            }
        }

        private void OnGUI()
        {
            if (!_showDeathOverlayOnGui) return;
            if (!IsOwner) return;
            if (string.IsNullOrWhiteSpace(deathOverlayFallbackText)) return;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 80,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(1f, 0.15f, 0.15f, 1f);

            Rect rect = new Rect(0, Screen.height / 2f - 60f, Screen.width, 120f);
            // 검정 그림자 (가독성)
            GUIStyle shadow = new GUIStyle(style);
            shadow.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), deathOverlayFallbackText, shadow);
            GUI.Label(rect, deathOverlayFallbackText, style);
        }

        private void DisableInputComponentsOnDeath()
        {
            DisableComponentsByName(inputComponentNamesToDisableOnDeath);
        }

        private void DisableComponentsByName(string[] names)
        {
            if (names == null) return;
            MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < names.Length; i++)
            {
                string typeName = names[i];
                if (string.IsNullOrWhiteSpace(typeName)) continue;
                for (int j = 0; j < all.Length; j++)
                {
                    MonoBehaviour mb = all[j];
                    if (mb == null) continue;
                    if (mb.GetType().Name == typeName)
                    {
                        mb.enabled = false;
                    }
                }
            }
        }
    }
}
