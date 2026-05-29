using LostMemory.Networking.Player;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Combat Targetable")]
    public sealed class CombatTargetable : MonoBehaviour
    {
        private const string EnemyTag = "Enemy";
        private const string BossTag = "Boss";
        private const string EnemiesLayerName = "Enemies";

        [SerializeField] private bool isTargetable = true;

        public bool IsTargetable
        {
            get => isTargetable;
            set => isTargetable = value;
        }

        public static bool CanBeTargeted(Health health)
        {
            if (health == null)
            {
                return false;
            }

            CombatTargetable targetable = health.GetComponentInParent<CombatTargetable>();
            return targetable == null || targetable.IsTargetable;
        }

        /// <summary>
        /// player(host & guest 모두)인지 판정 — 자기편 자동공격(MagicalGirl, ChainOnHit, WindAOE 등)이
        /// 다른 player 를 target 하지 않도록 막는 가드.
        ///
        /// 멀티 환경에선 `PlayerMovementSync.convertNonOwnerToAi=true` 로 host 측 게스트 캐릭터의
        /// `Character.CharacterType` 이 AI 로 변환되므로, CharacterType 만 보면 게스트 player 가
        /// AI 로 인식되어 자기편 공격에 맞음. 이를 막기 위해 `NetworkObject.IsPlayerObject` 도 같이 본다.
        ///
        /// 싱글환경에서 NetworkObject 없으면 두 번째 가드는 자동 skip → 기존 동작 보존.
        /// </summary>
        public static bool IsAuthoritativePlayer(GameObject go)
        {
            if (go == null) return false;

            // 1) CharacterType 검사 — 싱글환경 / host 자기 캐릭터 / 변환 안 된 player.
            Character ch = go.GetComponentInParent<Character>();
            if (ch != null && ch.CharacterType == Character.CharacterTypes.Player) return true;

            // 2) NGO PlayerObject 검사 — 멀티 환경에서 host 측 게스트 캐릭터(AI 변환됨) 식별.
            NetworkObject netObj = go.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject) return true;

            return false;
        }

        /// <summary>Overload: Health 컴포넌트에서 GameObject 추출 후 위 메소드에 위임.</summary>
        public static bool IsAuthoritativePlayer(Health health)
        {
            return health != null && IsAuthoritativePlayer(health.gameObject);
        }

        /// <summary>
        /// PvP 미상정 — 모든 player(자기/팀원 무관)에 대한 친아군 가드.
        /// 여러 sentinel 을 OR 로 묶어 prefab 부착 누락에 강건:
        ///   1) PlayerHealthSync — 가장 명시적
        ///   2) PlayerMovementSync — 모든 networked player prefab 에 부착
        ///   3) NetworkObject.IsPlayerObject — NGO SpawnAsPlayerObject 로 spawn 된 player
        /// 4인 환경 자동 확장. host 측에서 AI 변환된 게스트 player 도 항상 포함.
        /// </summary>
        public static bool IsFriendlyPlayer(Health health)
        {
            return health != null && IsFriendlyPlayerGameObject(health.gameObject);
        }

        /// <summary>Overload: Collider2D 의 부모 chain 에서 sentinel 검색.</summary>
        public static bool IsFriendlyPlayer(Collider2D collider)
        {
            return collider != null && IsFriendlyPlayerGameObject(collider.gameObject);
        }

        private static bool IsFriendlyPlayerGameObject(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponentInParent<PlayerHealthSync>() != null) return true;
            if (go.GetComponentInParent<PlayerMovementSync>() != null) return true;
            NetworkObject netObj = go.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject) return true;
            return false;
        }

        /// <summary>
        /// 적 자동공격 타겟팅 가드 — 자기편 자동공격(MagicalGirl, ChainOnHit, WindAOE 등)이
        /// 적만 target 하도록 통합한 헬퍼. develop 의 Tag/Layer fallback 과
        /// multi_test 의 `IsAuthoritativePlayer` 가드를 한 번에 적용.
        /// </summary>
        public static bool CanBeAutoTargetedEnemy(Health health)
        {
            if (health == null || health.CurrentHealth <= 0f || !CanBeTargeted(health))
            {
                return false;
            }

            // 멀티 가드 — host 측에서 AI 변환된 게스트 player 명시적 제외.
            // 싱글환경에서 NetworkObject 없으면 자동 skip → 기존 동작 보존.
            // IsFriendlyPlayer 안전벨트 — PlayerHealthSync 컴포넌트로도 한 번 더 차단 (4인 환경).
            if (IsAuthoritativePlayer(health) || IsFriendlyPlayer(health))
            {
                return false;
            }

            Character character = health.GetComponentInParent<Character>();
            if (character != null && character.CharacterType == Character.CharacterTypes.AI)
            {
                return true;
            }

            Transform target = health.transform;
            return HasTag(target, BossTag)
                   || HasTag(target, EnemyTag)
                   || IsOnLayer(target, EnemiesLayerName);
        }

        private static bool HasTag(Transform target, string tagName)
        {
            if (target == null)
            {
                return false;
            }

            Transform root = target.root;
            return target.CompareTag(tagName)
                   || (root != null && root.CompareTag(tagName));
        }

        private static bool IsOnLayer(Transform target, string layerName)
        {
            if (target == null)
            {
                return false;
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return false;
            }

            Transform root = target.root;
            return target.gameObject.layer == layer
                   || (root != null && root.gameObject.layer == layer);
        }
    }
}
