using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Player
{
    /// <summary>
    /// 씬 전환 시 TDE Health 컴포넌트의 CurrentHealth / MaximumHealth 를 PlayerRunState 스냅샷
    /// 으로 보관·복구한다. Player 프리팹에 부착 (Health 와 같은 GameObject 권장).
    ///
    /// 복구 시점: Start 단계. RelicEffectRegistry.OnEnable 의 effect replay 가 끝난 후 적용해야
    /// PlayerHealthStatApplier 의 modifier 재계산이 우리 값을 덮어쓰지 않는다.
    /// (Applier 는 OnEnable 에서 _baseMaxHealth 를 캐싱 — 복원 전 SO 기본값으로 캡처되므로
    /// 이후 modifier 변화 시 ratio 유지 정책으로 CurrentHealth 가 자연스럽게 비율 유지된다.)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Player/Player Health Snapshotter")]
    public sealed class PlayerHealthSnapshotter : MonoBehaviour
    {
        [SerializeField] private Health health;

        private void Reset()
        {
            health = GetComponent<Health>();
        }

        /// <summary>현재 HP 를 스냅샷 구조체에 기록한다. StageRouteManager 가 씬 전환 직전 호출.</summary>
        public void CaptureInto(ref PlayerSnapshot snapshot)
        {
            if (health == null) return;
            snapshot.HasHealth = true;
            snapshot.CurrentHealth = health.CurrentHealth;
            snapshot.MaximumHealth = health.MaximumHealth;
        }

        private void Start()
        {
            if (health == null) return;
            PlayerRunState runState = PlayerRunState.Instance;
            if (runState == null || !runState.HasSnapshot) return;

            PlayerSnapshot snap = runState.Snapshot;
            if (!snap.HasHealth) return;

            // MaximumHealth 를 먼저 맞춰야 SetHealth 의 clamp 와 modifier 재계산의 ratio 가 정확.
            if (snap.MaximumHealth > 0f)
            {
                health.MaximumHealth = snap.MaximumHealth;
            }
            float clamped = Mathf.Clamp(snap.CurrentHealth, 0f, health.MaximumHealth);
            health.SetHealth(clamped);
        }
    }
}
