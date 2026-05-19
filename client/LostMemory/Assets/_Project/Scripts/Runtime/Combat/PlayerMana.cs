using System;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// 플레이어의 마나(공용 자원). 평타 · 스킬 · 연속샷 등 무기 메커닉이
    /// Consume / Recover 로 접근한다.
    /// 자동 회복: 매 프레임 ManaPerSecond * deltaTime 누적, 1 이상이 되면 +N.
    /// 충전 트리거(명중 · 발사 · 처치 보너스)는 별도 컴포넌트가 Recover 를 호출하여 합산.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Mana")]
    public sealed class PlayerMana : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Min(1)] private int maxMana = 100;
        [SerializeField, Min(0)] private int startingMana = 0;

        [Header("Auto Recovery")]
        [SerializeField] private bool autoRecover = true;
        [SerializeField, Min(0f)] private float manaPerSecond = 5f;

        [Header("Debug")]
        [SerializeField] private bool logManaEvents = false;

        private int _currentMana;
        private float _recoveryAccumulator;

        public int CurrentMana => _currentMana;
        public int MaxMana => maxMana;
        public float Ratio => maxMana > 0 ? (float)_currentMana / maxMana : 0f;

        /// <summary>(current, max) — 값이 바뀔 때만 발행.</summary>
        public event Action<int, int> ManaChanged;

        private void Awake()
        {
            _currentMana = Mathf.Clamp(startingMana, 0, maxMana);
        }

        private void OnEnable()
        {
            ManaChanged?.Invoke(_currentMana, maxMana);
        }

        private void Update()
        {
            if (!autoRecover || _currentMana >= maxMana || manaPerSecond <= 0f) return;

            _recoveryAccumulator += manaPerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(_recoveryAccumulator);
            if (whole > 0)
            {
                _recoveryAccumulator -= whole;
                Recover(whole);
            }
        }

        /// <summary>마나 회복. amount &lt;= 0 은 무시.</summary>
        public void Recover(int amount)
        {
            if (amount <= 0) return;
            int newMana = Mathf.Min(maxMana, _currentMana + amount);
            if (newMana == _currentMana) return;
            _currentMana = newMana;
            ManaChanged?.Invoke(_currentMana, maxMana);
            if (logManaEvents) Debug.Log($"[PlayerMana] Recover +{amount} → {_currentMana}/{maxMana}");
        }

        /// <summary>마나 소비 시도. amount &lt;= 0 은 성공 처리(소비 없음).
        /// 잔량 부족 시 false 반환, 잔량 차감 안 됨.</summary>
        public bool Consume(int amount)
        {
            if (amount <= 0) return true;
            if (_currentMana < amount) return false;
            _currentMana -= amount;
            ManaChanged?.Invoke(_currentMana, maxMana);
            if (logManaEvents) Debug.Log($"[PlayerMana] Consume -{amount} → {_currentMana}/{maxMana}");
            return true;
        }

        public bool HasEnough(int amount) => _currentMana >= amount;

        public void SetMaxMana(int newMax)
        {
            if (newMax <= 0 || newMax == maxMana) return;
            maxMana = newMax;
            _currentMana = Mathf.Min(_currentMana, maxMana);
            ManaChanged?.Invoke(_currentMana, maxMana);
        }

        /// <summary>현재 마나를 최대값으로 회복. 맵 전환 등 라운드 리셋 시 사용.</summary>
        public void RestoreToMax()
        {
            if (_currentMana == maxMana) return;
            _currentMana = maxMana;
            _recoveryAccumulator = 0f;
            ManaChanged?.Invoke(_currentMana, maxMana);
            if (logManaEvents) Debug.Log($"[PlayerMana] RestoreToMax → {_currentMana}/{maxMana}");
        }
    }
}
