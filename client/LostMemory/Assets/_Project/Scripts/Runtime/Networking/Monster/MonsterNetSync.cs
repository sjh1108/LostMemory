using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Monster
{
    /// <summary>
    /// 몬스터 prefab 의 NGO sync 진입점.
    ///
    /// 정책:
    ///   - 위치/회전: `NetworkTransform` (server-authoritative) 가 sync. 본 컴포넌트는 그 옆에 부착.
    ///   - 비-server (게스트) 측: TDE Character / AI / Combat 컴포넌트를 disable 해서 호스트의 AI 결과만 반영.
    ///     (호스트는 AI tick + 데미지 판정. 게스트는 위치 sync 만 받음.)
    ///   - server 측: 본 컴포넌트가 아무 동작 안 함. 일반 TDE 흐름 그대로.
    ///
    /// 부착: 몬스터 prefab 의 root (NetworkObject + NetworkTransform 옆).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Monster Net Sync")]
    public sealed class MonsterNetSync : NetworkBehaviour
    {
        [SerializeField, Tooltip("비-server (게스트) 측에서 TDE Character 컴포넌트 자체를 enable=false 로 둘지. true 면 게스트 측 Character tick 자체 비활성.")]
        private bool disableCharacterOnNonServer = true;

        [SerializeField, Tooltip("비-server 측에서 disable 할 추가 컴포넌트 이름. 끝에 '*' 붙이면 prefix 매칭 (예: 'AIAction*' → AIActionShoot2D / AIActionMoveTowardsTarget 등 모두).")]
        private string[] additionalDisableComponentNames = new[]
        {
            "CharacterMovement",
            "CharacterOrientation2D",
            "CharacterHandleWeapon",
            "AIBrain",
        };

        [SerializeField, Tooltip("비-server 측에서 TDE AIAction 베이스 클래스 자손 (AIActionShoot2D 등) 을 모두 disable. " +
            "기본 true — 게스트 측 AI tick 차단으로 NullRef 방지.")]
        private bool disableAllAIActions = true;

        [SerializeField, Tooltip("비-server 측에서 TDE AIDecision 베이스 클래스 자손도 disable. AIBrain 이 꺼졌으면 영향 적지만 안전벨트.")]
        private bool disableAllAIDecisions = true;

        [SerializeField, Tooltip("호스트의 CharacterOrientation2D.IsFacingRight 을 NetworkVariable 로 sync. " +
            "게스트 측은 CharacterOrientation2D 가 disabled 라 방향이 안 돌아감 → 본 sync 로 보정.")]
        private bool syncFacingDirection = true;

        [SerializeField] private bool verboseLog = false;

        private CharacterOrientation2D _orientation;
        private Character _cachedCharacter;
        private SpriteRenderer _fallbackSpriteRenderer;

        private readonly NetworkVariable<bool> _isFacingRight = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _orientation = GetComponentInChildren<CharacterOrientation2D>(true);
            _cachedCharacter = GetComponent<Character>();
            // CharacterModel slot 미 wireup 케이스 fallback — 자식 SpriteRenderer 의 flipX 사용.
            _fallbackSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (syncFacingDirection)
            {
                _isFacingRight.OnValueChanged += HandleFacingChanged;
            }

            if (IsServer)
            {
                if (syncFacingDirection && _orientation != null)
                {
                    _isFacingRight.Value = _orientation.IsFacingRight;
                }
                if (verboseLog) Debug.Log($"[MonsterNetSync] Server — AI/Combat 정상 동작: {gameObject.name}", this);
                return;
            }

            // 비-server (게스트) — 호스트가 AI tick 하니 본 인스턴스는 sync 받은 위치만 표시.
            DisableOnNonServer();

            // 게스트: 현재 sync 된 facing 즉시 적용 (spawn 시점 방향 보정).
            if (syncFacingDirection)
            {
                ApplyFacing(_isFacingRight.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (syncFacingDirection)
            {
                _isFacingRight.OnValueChanged -= HandleFacingChanged;
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            // 호스트만 facing 모니터 → NetworkVariable write.
            if (!IsServer || !syncFacingDirection || _orientation == null) return;

            bool current = _orientation.IsFacingRight;
            if (current != _isFacingRight.Value)
            {
                if (verboseLog) Debug.Log($"[MonsterNetSync] SERVER facing write {gameObject.name} {_isFacingRight.Value} -> {current}", this);
                _isFacingRight.Value = current;
            }
        }

        private void HandleFacingChanged(bool previous, bool current)
        {
            if (IsServer) return;
            if (verboseLog) Debug.Log($"[MonsterNetSync] CLIENT facing OnValueChanged {gameObject.name} {previous} -> {current}", this);
            ApplyFacing(current);
        }

        private void ApplyFacing(bool faceRight)
        {
            // 게스트 측 CharacterOrientation2D 는 disabled → Initialization() 미실행 → 내부 캐시 (_spriteRenderer 등) 가 null.
            // FaceDirection 호출은 silent fail 가능. 따라서 prefab 의 CharacterModel.localScale 또는 자식 SpriteRenderer 를 직접 조작.

            // 1) Character.CharacterModel slot 이 wireup 됐으면 그 transform.localScale.x 토글.
            if (_cachedCharacter != null && _cachedCharacter.CharacterModel != null)
            {
                Transform modelTr = _cachedCharacter.CharacterModel.transform;
                Vector3 s = modelTr.localScale;
                s.x = faceRight ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
                modelTr.localScale = s;
            }
            // 2) Fallback: CharacterModel slot 미설정 시 자식 SpriteRenderer.flipX 사용.
            else if (_fallbackSpriteRenderer != null)
            {
                _fallbackSpriteRenderer.flipX = !faceRight;
            }

            // CharacterOrientation2D 의 IsFacingRight 도 일관성 유지 (다른 컴포넌트가 참조할 수 있음).
            if (_orientation != null)
            {
                _orientation.IsFacingRight = faceRight;
            }
        }

        private void DisableOnNonServer()
        {
            Character character = GetComponent<Character>();
            if (character != null && disableCharacterOnNonServer)
            {
                character.enabled = false;
            }

            MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);

            if (disableAllAIActions)
            {
                DisableByBaseType(all, typeof(AIAction));
            }
            if (disableAllAIDecisions)
            {
                DisableByBaseType(all, typeof(AIDecision));
            }

            if (additionalDisableComponentNames != null)
            {
                for (int i = 0; i < additionalDisableComponentNames.Length; i++)
                {
                    string typeName = additionalDisableComponentNames[i];
                    if (string.IsNullOrWhiteSpace(typeName)) continue;
                    DisableByName(all, typeName);
                }
            }

            if (verboseLog) Debug.Log($"[MonsterNetSync] Non-server — AI/Combat 비활성: {gameObject.name}", this);
        }

        private static void DisableByBaseType(MonoBehaviour[] all, System.Type baseType)
        {
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null) continue;
                if (baseType.IsAssignableFrom(mb.GetType()))
                {
                    mb.enabled = false;
                }
            }
        }

        private static void DisableByName(MonoBehaviour[] all, string typeName)
        {
            bool wildcard = typeName.EndsWith("*");
            string needle = wildcard ? typeName.Substring(0, typeName.Length - 1) : typeName;

            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null) continue;

                string name = mb.GetType().Name;
                bool match = wildcard ? name.StartsWith(needle) : name == needle;
                if (match)
                {
                    mb.enabled = false;
                }
            }
        }
    }
}
