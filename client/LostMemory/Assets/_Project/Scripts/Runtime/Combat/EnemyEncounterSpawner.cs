using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Enemies;
using LostMemory.Stage;
using LostMemory.Stage.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    // RoomData.Encounter 를 읽어 wave / entry / spawn point 매칭 후 적 prefab 을 Instantiate.
    // 결정적 순차 선택 (modulo). 시드 무작위는 후속 CL.
    // RoomEntryRuntimeController 가 owner — 같은 GameObject 에 붙여 사용한다.
    //
    // CL-035 변경: Begin 시 모든 wave 의 startDelay 타이머 큐 + wave 별 idempotent 보장.
    //   외부에서 SpawnNextWave() 호출 시 가장 작은 미시작 wave 를 *조기 spawn*.
    //   둘 중 먼저 도착하는 쪽이 spawn — 사망률 트리거(tracker)와 시간 트리거(자체)의 OR 동작.
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Encounter Spawner")]
    public sealed class EnemyEncounterSpawner : MonoBehaviour
    {
        private const string DefaultAnimatorLayerName = "Base Layer";
        private const string WalkingAnimatorParameterName = "Walking";
        private const string DamageAnimatorParameterName = "Damage";
        private const string DeathAnimatorParameterName = "Death";
        private const float HitAnimationMaximumStateDuration = 0.6f;
        private const float HitAnimationRecoveryNormalizedTime = 0.98f;
        private const float HitAnimationTransitionDuration = 0.05f;
        private const float SpawnedEnemyDeathEffectDelay = 1.25f;
        private static readonly HitAnimationRecoveryProfile[] HitAnimationRecoveryProfiles =
        {
            new HitAnimationRecoveryProfile("Orc_Hit", "Orc_Idle", "Orc_Walk"),
            new HitAnimationRecoveryProfile("OrcRider_Hurt", "OrcRider_Idle", "OrcRider_Walk"),
            new HitAnimationRecoveryProfile("SkeletonArcher_Hurt", "SkeletonArcher_Idle", "SkeletonArcher_Walk")
        };

        public event Action<EnemySpawnedPayload> Spawned;
        public event Action<WaveSpawnedPayload> WaveCompleted;

        private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
        private readonly List<HitAnimationRecoverySubscription> hitAnimationRecoverySubscriptions = new List<HitAnimationRecoverySubscription>();

        private string activeRoomId;
        private RoomEncounterSpec activeSpec;
        private RoomEncounterAnchor activeAnchor;
        private EnemyCatalog activeCatalog;
        private bool[] waveStarted = Array.Empty<bool>();

        public IReadOnlyList<GameObject> SpawnedEnemies => spawnedEnemies;
        public int WaveCount => activeSpec != null ? activeSpec.Waves.Count : 0;

        private void OnDisable()
        {
            ClearHitAnimationRecoverySubscriptions();
        }

        public void Begin(string roomId, RoomEncounterSpec spec, RoomEncounterAnchor anchor, EnemyCatalog catalog)
        {
            if (spec == null || anchor == null || catalog == null)
            {
                return;
            }

            activeRoomId = roomId;
            activeSpec = spec;
            activeAnchor = anchor;
            activeCatalog = catalog;

            IReadOnlyList<RoomEncounterWave> waves = spec.Waves;
            waveStarted = new bool[waves.Count];

            for (int i = 0; i < waves.Count; i++)
            {
                RoomEncounterWave wave = waves[i];
                if (wave == null)
                {
                    waveStarted[i] = true;
                    continue;
                }

                StartCoroutine(ScheduleWave(i, wave));
            }
        }

        // 외부 강제 트리거. 가장 작은 미시작 wave 1개 즉시 spawn (startDelay 무시).
        // tracker 가 사망률 임계 도달 시 호출.
        public bool SpawnNextWave()
        {
            if (activeSpec == null)
            {
                return false;
            }

            IReadOnlyList<RoomEncounterWave> waves = activeSpec.Waves;
            for (int i = 0; i < waves.Count; i++)
            {
                if (waveStarted[i])
                {
                    continue;
                }

                StartWaveOnce(i, waves[i]);
                return true;
            }

            return false;
        }

        private IEnumerator ScheduleWave(int waveIndex, RoomEncounterWave wave)
        {
            if (wave.StartDelay > 0f)
            {
                yield return new WaitForSeconds(wave.StartDelay);
            }
            StartWaveOnce(waveIndex, wave);
        }

        private void StartWaveOnce(int waveIndex, RoomEncounterWave wave)
        {
            if (waveStarted[waveIndex])
            {
                return;
            }
            waveStarted[waveIndex] = true;
            StartCoroutine(RunWave(activeRoomId, waveIndex, wave, activeAnchor, activeCatalog));
        }

        private IEnumerator RunWave(
            string roomId,
            int waveIndex,
            RoomEncounterWave wave,
            RoomEncounterAnchor anchor,
            EnemyCatalog catalog)
        {
            // startDelay 는 ScheduleWave 가 이미 처리함 (CL-035 변경).
            int spawnedCount = 0;
            RoomEncounterEnemyEntry[] entries = wave.Enemies;
            for (int i = 0; i < entries.Length; i++)
            {
                RoomEncounterEnemyEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!catalog.TryGetEntry(entry.EnemyId, out EnemyCatalogEntry catalogEntry))
                {
                    Debug.LogWarning(
                        $"[EnemyEncounterSpawner] enemyId '{entry.EnemyId}' not found in catalog. Skip {entry.Count} spawn(s) for room '{roomId}' wave {waveIndex}.",
                        this);
                    continue;
                }

                GameObject prefab = catalogEntry.Prefab;
                IReadOnlyList<RoomEncounterSpawnPoint> candidates = anchor.GetSpawnPoints(entry.SpawnGroupFilter);
                if (candidates.Count == 0)
                {
                    Debug.LogWarning(
                        $"[EnemyEncounterSpawner] no spawn points match filter '{entry.SpawnGroupFilter}' in room '{roomId}'. Skip {entry.Count} spawn(s).",
                        this);
                    continue;
                }

                for (int j = 0; j < entry.Count; j++)
                {
                    int candidateIndex = PickSpawnIndex(candidates.Count, j);
                    RoomEncounterSpawnPoint point = candidates[candidateIndex];
                    if (point == null)
                    {
                        continue;
                    }

                    GameObject instance = Instantiate(prefab, point.transform.position, Quaternion.identity);
                    EnemyDataRuntimeAdapter.ApplyTo(instance, catalogEntry.Data);
                    HardenSpawnedInstance(instance);
                    EnsureDeathAnimationLock(instance);
                    RegisterHitAnimationRecovery(instance);
                    spawnedEnemies.Add(instance);
                    spawnedCount++;
                    Spawned?.Invoke(new EnemySpawnedPayload(roomId, waveIndex, instance));

                    // 같은 프레임에 여러 적이 동시에 init 되면 일부 적의 init (특히 ranged 무기 attach) 이 깨지는 현상 관찰.
                    // entry 별 1프레임 양보로 회피. spawn 합 N마리면 약 N프레임 (~0.02s × N) 지연.
                    yield return null;
                }
            }

            WaveCompleted?.Invoke(new WaveSpawnedPayload(roomId, waveIndex, spawnedCount));
        }

        // 적 prefab 의 Health 가 DestroyOnDeath = false 로 셋업된 경우, 죽어도 GameObject 가 남아
        // sprite 가 서 있는 채로 보이거나 풀 사이클로 재활용되며 좀비 부활하는 현상이 관찰됨.
        // CL-034 spawn 경로에 한해 죽으면 GameObject 통째로 destroy 되도록 강제.
        // 적 prefab 의 원래 lifecycle 의도(풀 재활용 등)와 충돌할 수 있으므로 적 담당자(클라2)와 협의하여
        // 적 prefab 셋업이 정리되면 본 helper 는 제거 가능.
        private static void HardenSpawnedInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Health health = instance.GetComponent<Health>();
            if (health != null)
            {
                health.DestroyOnDeath = true;
                health.DelayBeforeDestruction = Mathf.Max(
                    health.DelayBeforeDestruction,
                    SpawnedEnemyDeathEffectDelay);
            }
        }

        private static void EnsureDeathAnimationLock(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Health health = instance.GetComponent<Health>();
            Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
            if (health == null || animator == null)
            {
                return;
            }

            EnemyDeathAnimationLock animationLock = instance.GetComponent<EnemyDeathAnimationLock>();
            if (animationLock == null)
            {
                animationLock = instance.AddComponent<EnemyDeathAnimationLock>();
            }

            animationLock.Configure(health, animator);
        }

        private void RegisterHitAnimationRecovery(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Health health = instance.GetComponent<Health>();
            Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
            if (health == null || animator == null)
            {
                return;
            }

            HitAnimationRecoverySubscription subscription = new HitAnimationRecoverySubscription
            {
                Health = health,
                Animator = animator
            };

            subscription.OnHit = () => HandleHitAnimationRecoveryHit(subscription);
            subscription.OnDeath = () => UnregisterHitAnimationRecovery(subscription);

            health.OnHit += subscription.OnHit;
            health.OnDeath += subscription.OnDeath;
            hitAnimationRecoverySubscriptions.Add(subscription);
        }

        private void HandleHitAnimationRecoveryHit(HitAnimationRecoverySubscription subscription)
        {
            if (subscription == null || !isActiveAndEnabled || !CanRecoverHitAnimation(subscription))
            {
                return;
            }

            if (subscription.Routine != null)
            {
                StopCoroutine(subscription.Routine);
            }

            subscription.Routine = StartCoroutine(RecoverHitAnimation(subscription));
        }

        private IEnumerator RecoverHitAnimation(HitAnimationRecoverySubscription subscription)
        {
            yield return null;

            float startedAt = Time.time;
            bool observedHitState = false;
            HitAnimationRecoveryProfile observedProfile = default;

            while (CanRecoverHitAnimation(subscription) && Time.time - startedAt <= HitAnimationMaximumStateDuration)
            {
                Animator animator = subscription.Animator;
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);

                if (IsDeathState(currentState) || IsDeathState(nextState))
                {
                    subscription.Routine = null;
                    yield break;
                }

                bool currentIsHit = TryGetHitAnimationRecoveryProfile(currentState, out HitAnimationRecoveryProfile currentProfile);
                bool nextIsHit = TryGetHitAnimationRecoveryProfile(nextState, out HitAnimationRecoveryProfile nextProfile);

                if (currentIsHit)
                {
                    observedHitState = true;
                    observedProfile = currentProfile;

                    if (!animator.IsInTransition(0) && currentState.normalizedTime >= HitAnimationRecoveryNormalizedTime)
                    {
                        RecoverToLocomotion(animator, observedProfile);
                        subscription.Routine = null;
                        yield break;
                    }
                }
                else if (nextIsHit)
                {
                    observedHitState = true;
                    observedProfile = nextProfile;
                }
                else if (observedHitState)
                {
                    subscription.Routine = null;
                    yield break;
                }

                yield return null;
            }

            if (observedHitState && CanRecoverHitAnimation(subscription))
            {
                RecoverToLocomotion(subscription.Animator, observedProfile);
            }

            subscription.Routine = null;
        }

        private static bool CanRecoverHitAnimation(HitAnimationRecoverySubscription subscription)
        {
            return subscription != null
                && subscription.Animator != null
                && subscription.Animator.isActiveAndEnabled
                && subscription.Health != null
                && subscription.Health.CurrentHealth > 0f;
        }

        private static void RecoverToLocomotion(Animator animator, HitAnimationRecoveryProfile profile)
        {
            ResetTriggerIfPresent(animator, DamageAnimatorParameterName);

            bool shouldWalk = GetBoolIfPresent(animator, WalkingAnimatorParameterName);
            string targetStateName = shouldWalk ? profile.WalkStateName : profile.IdleStateName;
            if (!TryGetStateHash(animator, targetStateName, out int targetStateHash))
            {
                targetStateName = profile.IdleStateName;
                if (!TryGetStateHash(animator, targetStateName, out targetStateHash))
                {
                    return;
                }
            }

            animator.CrossFadeInFixedTime(targetStateHash, HitAnimationTransitionDuration, 0);
        }

        private static bool IsDeathState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.shortNameHash == Animator.StringToHash(DeathAnimatorParameterName)
                || stateInfo.shortNameHash == Animator.StringToHash("Orc_Death")
                || stateInfo.shortNameHash == Animator.StringToHash("OrcRider_Death")
                || stateInfo.shortNameHash == Animator.StringToHash("SkeletonArcher_Death");
        }

        private static bool TryGetHitAnimationRecoveryProfile(AnimatorStateInfo stateInfo, out HitAnimationRecoveryProfile profile)
        {
            for (int i = 0; i < HitAnimationRecoveryProfiles.Length; i++)
            {
                if (stateInfo.shortNameHash == HitAnimationRecoveryProfiles[i].HitStateHash)
                {
                    profile = HitAnimationRecoveryProfiles[i];
                    return true;
                }
            }

            profile = default;
            return false;
        }

        private static bool GetBoolIfPresent(Animator animator, string parameterName)
        {
            if (!HasParameter(animator, parameterName, AnimatorControllerParameterType.Bool))
            {
                return false;
            }

            return animator.GetBool(parameterName);
        }

        private static void ResetTriggerIfPresent(Animator animator, string parameterName)
        {
            if (HasParameter(animator, parameterName, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(parameterName);
            }
        }

        private static bool HasParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == parameterType && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStateHash(Animator animator, string stateName, out int stateHash)
        {
            stateHash = Animator.StringToHash(DefaultAnimatorLayerName + "." + stateName);
            if (animator.HasState(0, stateHash))
            {
                return true;
            }

            stateHash = Animator.StringToHash(stateName);
            return animator.HasState(0, stateHash);
        }

        private void ClearHitAnimationRecoverySubscriptions()
        {
            for (int i = hitAnimationRecoverySubscriptions.Count - 1; i >= 0; i--)
            {
                UnregisterHitAnimationRecovery(hitAnimationRecoverySubscriptions[i]);
            }
        }

        private void UnregisterHitAnimationRecovery(HitAnimationRecoverySubscription subscription)
        {
            if (subscription == null)
            {
                return;
            }

            if (subscription.Routine != null)
            {
                StopCoroutine(subscription.Routine);
                subscription.Routine = null;
            }

            if (subscription.Health != null)
            {
                subscription.Health.OnHit -= subscription.OnHit;
                subscription.Health.OnDeath -= subscription.OnDeath;
            }

            hitAnimationRecoverySubscriptions.Remove(subscription);
        }

        private sealed class HitAnimationRecoverySubscription
        {
            public Health Health;
            public Animator Animator;
            public Health.OnHitDelegate OnHit;
            public Health.OnDeathDelegate OnDeath;
            public Coroutine Routine;
        }

        private readonly struct HitAnimationRecoveryProfile
        {
            public readonly string IdleStateName;
            public readonly string WalkStateName;
            public readonly int HitStateHash;

            public HitAnimationRecoveryProfile(string hitStateName, string idleStateName, string walkStateName)
            {
                IdleStateName = idleStateName;
                WalkStateName = walkStateName;
                HitStateHash = Animator.StringToHash(hitStateName);
            }
        }

        // 후보가 entry.Count 보다 적을 때 modulo 로 순환. EditMode 테스트 가능하도록 static.
        public static int PickSpawnIndex(int candidateCount, int sequence)
        {
            if (candidateCount <= 0)
            {
                return 0;
            }

            int wrapped = sequence % candidateCount;
            return wrapped < 0 ? wrapped + candidateCount : wrapped;
        }
    }
}
