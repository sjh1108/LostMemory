using System;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-108: 보호막 잔량 관리 + 만료 처리 + 데미지 흡수 진입점.
    /// 반격의 표식 (ShieldOnParry) 의 패링 성공 시 GrantShield 호출.
    /// 적 공격 진입점 (KhiParryDamageOnTouch) 이 TryAbsorb 로 차감.
    /// 누적 정책 (CL-108 결정 #4): 잔량 합산 + 만료 시간 더 늦은 쪽.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Shield")]
    public sealed class PlayerShield : MonoBehaviour
    {
        [SerializeField] private bool logShieldEvents = false;

        private float _currentShield;
        private float _expiresAt;

        public float CurrentShield => _currentShield;
        public bool IsActive => _currentShield > 0f && Time.time < _expiresAt;

        public event Action<float> ShieldChanged;

        /// <summary>
        /// 보호막 부여. 누적 정책: 잔량 합산, 만료 시간은 더 늦은 쪽.
        /// </summary>
        public void GrantShield(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f) return;
            _currentShield = (IsActive ? _currentShield : 0f) + amount;
            _expiresAt = Mathf.Max(IsActive ? _expiresAt : 0f, Time.time + duration);
            ShieldChanged?.Invoke(_currentShield);
            if (logShieldEvents)
            {
                Debug.Log($"[PlayerShield] Grant {amount:F1} for {duration}s, total={_currentShield:F1}, expiresAt={_expiresAt:F2}");
            }
        }

        /// <summary>
        /// 들어온 데미지를 보호막이 먼저 흡수. 남은 데미지 반환.
        /// </summary>
        public float TryAbsorb(float incomingDamage)
        {
            if (!IsActive || incomingDamage <= 0f) return incomingDamage;
            float absorbed = Mathf.Min(_currentShield, incomingDamage);
            _currentShield -= absorbed;
            ShieldChanged?.Invoke(_currentShield);
            if (logShieldEvents)
            {
                Debug.Log($"[PlayerShield] Absorb {absorbed:F1}, remain={_currentShield:F1}, dmg passthrough={incomingDamage - absorbed:F1}");
            }
            if (_currentShield <= 0f) ClearShield();
            return incomingDamage - absorbed;
        }

        private void Update()
        {
            if (_currentShield > 0f && Time.time >= _expiresAt) ClearShield();
        }

        private void ClearShield()
        {
            _currentShield = 0f;
            _expiresAt = 0f;
            ShieldChanged?.Invoke(0f);
            if (logShieldEvents) Debug.Log("[PlayerShield] Expired/Cleared");
        }
    }
}
