using System;
using System.Collections;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Player
{
    /// <summary>
    /// 씬 전환 시 TDE Health 컴포넌트의 CurrentHealth / MaximumHealth 를 PlayerRunState 스냅샷
    /// 으로 보관·복구한다. Player 프리팹에 부착 (Health 와 같은 GameObject 권장).
    ///
    /// 복구 시점: Start 다음 프레임. Unity 의 Start 호출 순서는 보장되지 않으므로
    /// TDE Health.Start() (CurrentHealth = MaximumHealth 강제) 가 우리 SetHealth 호출 뒤에
    /// 실행되어 max 로 덮어쓰는 버그를 방지하기 위해 한 프레임 늦춰 적용한다.
    /// (RelicEffectRegistry.OnEnable 의 effect replay / TalentStartupApplier modifier 변화도
    /// 같은 프레임 안에 끝나므로 ratio 유지 정책이 올바르게 작동.)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Player/Player Health Snapshotter")]
    public sealed class PlayerHealthSnapshotter : MonoBehaviour
    {
        public static event Action<Health> SnapshotRestoreCompleted;

        [SerializeField] private Health health;
        [SerializeField] private bool logRestore = true;

        public bool RestorePending { get; private set; }

        private void Reset()
        {
            health = GetComponent<Health>();
        }

        private void Awake()
        {
            ResolveHealth();
            RestorePending = TryGetHealthSnapshot(out _);
        }

        /// <summary>현재 HP 를 스냅샷 구조체에 기록한다. StageRouteManager 가 씬 전환 직전 호출.</summary>
        public void CaptureInto(ref PlayerSnapshot snapshot)
        {
            ResolveHealth();
            if (health == null) return;
            snapshot.HasHealth = true;
            snapshot.CurrentHealth = health.CurrentHealth;
            snapshot.MaximumHealth = health.MaximumHealth;
        }

        private void Start()
        {
            StartCoroutine(RestoreSnapshotNextFrame());
        }

        private IEnumerator RestoreSnapshotNextFrame()
        {
            // 다음 프레임까지 대기 — TDE Health.Start 의 CurrentHealth=MaximumHealth 초기화 이후
            // 적용되도록 보장. 첫 yield return null 만으로 모든 컴포넌트 Start 완료가 보장됨.
            yield return null;

            ResolveHealth();
            if (health == null)
            {
                CompleteRestore();
                yield break;
            }

            if (!TryGetHealthSnapshot(out PlayerSnapshot snap))
            {
                CompleteRestore();
                yield break;
            }

            // MaximumHealth 를 먼저 맞춰야 SetHealth 의 clamp 와 modifier 재계산의 ratio 가 정확.
            if (snap.MaximumHealth > 0f)
            {
                health.MaximumHealth = snap.MaximumHealth;
            }
            float clamped = Mathf.Clamp(snap.CurrentHealth, 0f, health.MaximumHealth);
            health.SetHealth(clamped);

            if (logRestore)
                Debug.Log($"[PlayerHealthSnapshotter] Restored {clamped:F1}/{health.MaximumHealth:F1} (from snapshot {snap.CurrentHealth:F1}/{snap.MaximumHealth:F1})", this);

            CompleteRestore();
        }

        public static bool TryGetPendingSnapshot(Health targetHealth, out PlayerSnapshot snapshot)
        {
            snapshot = default;
            PlayerHealthSnapshotter snapshotter = ResolveSnapshotter(targetHealth);
            if (snapshotter == null || !snapshotter.RestorePending)
            {
                return false;
            }

            return TryGetHealthSnapshot(out snapshot);
        }

        private static bool TryGetHealthSnapshot(out PlayerSnapshot snapshot)
        {
            snapshot = default;
            PlayerRunState runState = PlayerRunState.Instance;
            if (runState == null || !runState.HasSnapshot)
            {
                return false;
            }

            snapshot = runState.Snapshot;
            return snapshot.HasHealth;
        }

        private static PlayerHealthSnapshotter ResolveSnapshotter(Health targetHealth)
        {
            if (targetHealth == null)
            {
                return null;
            }

            PlayerHealthSnapshotter snapshotter = targetHealth.GetComponent<PlayerHealthSnapshotter>();
            if (snapshotter != null)
            {
                return snapshotter;
            }

            snapshotter = targetHealth.GetComponentInParent<PlayerHealthSnapshotter>();
            if (snapshotter != null)
            {
                return snapshotter;
            }

            return targetHealth.GetComponentInChildren<PlayerHealthSnapshotter>(true);
        }

        private void ResolveHealth()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }

        private void CompleteRestore()
        {
            RestorePending = false;
            SnapshotRestoreCompleted?.Invoke(health);
        }
    }
}
