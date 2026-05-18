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

        [SerializeField, Tooltip("비-server 측에서 disable 할 추가 컴포넌트 이름 (CharacterMovement / CharacterHandleWeapon 등). 비워두면 기본값.")]
        private string[] additionalDisableComponentNames = new[]
        {
            "CharacterMovement",
            "CharacterOrientation2D",
            "CharacterHandleWeapon",
            "AIBrain",
        };

        [SerializeField] private bool verboseLog = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (verboseLog) Debug.Log($"[MonsterNetSync] Server — AI/Combat 정상 동작: {gameObject.name}", this);
                return;
            }

            // 비-server (게스트) — 호스트가 AI tick 하니 본 인스턴스는 sync 받은 위치만 표시.
            DisableOnNonServer();
        }

        private void DisableOnNonServer()
        {
            Character character = GetComponent<Character>();
            if (character != null && disableCharacterOnNonServer)
            {
                character.enabled = false;
            }

            if (additionalDisableComponentNames != null)
            {
                for (int i = 0; i < additionalDisableComponentNames.Length; i++)
                {
                    string typeName = additionalDisableComponentNames[i];
                    if (string.IsNullOrWhiteSpace(typeName)) continue;
                    DisableComponentByName(typeName);
                }
            }

            if (verboseLog) Debug.Log($"[MonsterNetSync] Non-server — AI/Combat 비활성: {gameObject.name}", this);
        }

        private void DisableComponentByName(string typeName)
        {
            MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null) continue;
                if (mb.GetType().Name == typeName)
                {
                    mb.enabled = false;
                }
            }
        }
    }
}
