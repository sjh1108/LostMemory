using System;
using LostMemory.Relics;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 시연용 아이템(회복약 + 유물) 일괄 지급 컴포넌트.
    ///
    /// F9 한 번 → 인스펙터에 채워둔 RelicData[] 를 순회하면서 PlayerRelicInventory.TryAdd 호출.
    /// IsConsumable=true 면 PlayerConsumableInventory 로 자동 라우팅, false 면 그리드 배치 + RelicEffectRegistry 발화까지 자동.
    ///
    /// 멀티: PlayerRelicInventory 는 NetworkBehaviour 가 아니라 각 클라가 독립 인벤토리.
    /// 호스트/게스트 각자 자기 인스턴스에서 F9 눌러야 자기 인벤토리에 들어감.
    ///
    /// 부착: Player prefab 의 root (PlayerRelicInventory 가 있는 자리, AutoPotionController 옆).
    /// 의존 컴포넌트는 Awake 에서 GetComponentInParent 로 자동 해결.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Test Khi/Demo Item Granter")]
    public sealed class DemoItemGranter : MonoBehaviour
    {
        [Header("Demo")]
        [Tooltip("일괄 지급 트리거 키.")]
        [SerializeField] private KeyCode triggerKey = KeyCode.F9;
        [Tooltip("F9 누르면 일괄 TryAdd. 회복약(IsConsumable=true)/유물(false) 혼합 가능 — PlayerRelicInventory.TryAdd 가 자동 분기.")]
        [SerializeField] private RelicData[] itemsToGrant = Array.Empty<RelicData>();
        [Tooltip("같은 프레임/짧은 간격 중복 트리거 방지 안전벨트(초). unscaledTime 기준.")]
        [SerializeField, Min(0f)] private float internalCooldownSeconds = 0.5f;
        [SerializeField] private bool verboseLog = false;

        [Header("Dependencies (Auto-resolved if null)")]
        [SerializeField] private PlayerRelicInventory relicInventory;
        [SerializeField] private NetworkObject networkObject;

        private float _nextAllowedAt;

        private void Awake()
        {
            if (relicInventory == null) relicInventory = GetComponentInParent<PlayerRelicInventory>();
            if (networkObject == null) networkObject = GetComponentInParent<NetworkObject>();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(triggerKey)) return;

            // 멀티: 자기 owner-side 에서만 발동.
            // PlayerRelicInventory 는 단순 MonoBehaviour 라 sync 안 됨 — 호스트/게스트 각자 F9 눌러야 양쪽 다 받음.
            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner) return;

            if (Time.unscaledTime < _nextAllowedAt) return;
            _nextAllowedAt = Time.unscaledTime + internalCooldownSeconds;

            if (relicInventory == null || itemsToGrant == null || itemsToGrant.Length == 0)
            {
                if (verboseLog) Debug.LogWarning("[DemoItemGranter] inventory 또는 itemsToGrant 미설정 — 지급 skip.", this);
                return;
            }

            int added = 0;
            int rejected = 0;
            for (int i = 0; i < itemsToGrant.Length; i++)
            {
                RelicData item = itemsToGrant[i];
                if (item == null) continue;
                if (relicInventory.TryAdd(item)) added++;
                else rejected++;
            }

            if (verboseLog) Debug.Log($"[DemoItemGranter] {triggerKey} → added={added}, rejected={rejected} (인벤토리 가득 / 중복 유물 등)", this);
        }
    }
}
