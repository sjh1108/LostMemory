// CL-178 draft (김회인) — 비활성 보관.
// 사유: CL-197 (이용호, BerthaLightAttack1Bootstrap.ApplySavedBossData) + CL-199 (이용호, EnemyDataLiveTuneHook)
// 가 이미 같은 책임 (Bertha BossData 적용 + 라이브 튠) 처리. Resources/BossDataRegistry.asset 도 미생성.
// 활성화하려면 #if false → #if true 변경 + Resources/BossDataRegistry.asset 생성.
// 단 활성화 시 CL-197/199 와 이중 Configure 호출 발생 — 디버깅 혼동 주의.
// 자세한 사유: docs/khi/cl178_implementation.md
#if false
using LostMemory.Enemies.Boss.Bertha;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    /// <summary>
    /// CL-178: Bertha_Boss.asset 값을 BerthaBossPhaseController 에 자동 주입.
    /// 정적 hook 패턴 — Bertha prefab / 씬 무수정 (이용호 영역 회피).
    /// 라이브 튜닝: EnemyDataEvents.OnAssetSaved 구독 → Save Current Values 시 즉시 재적용.
    /// </summary>
    public static class BerthaBossDataInjector
    {
        private const string RegistryResourcePath = "BossDataRegistry";
        private static BossDataRegistry _registry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            _registry = Resources.Load<BossDataRegistry>(RegistryResourcePath);
            if (_registry == null)
            {
                Debug.LogWarning(
                    "[BerthaBossDataInjector] Resources/BossDataRegistry.asset 미발견. " +
                    "BossData 값 게임 미반영. CL-178 §사용자 작업 참조.");
                return;
            }

            // 중복 구독 방지 (도메인 리로드 후 재진입)
            EnemyDataEvents.OnAssetSaved -= OnBossDataSaved;
            EnemyDataEvents.OnAssetSaved += OnBossDataSaved;

            ApplyAll();
        }

        private static void OnBossDataSaved(EnemyData data)
        {
            if (data is BossData boss) ApplyForBoss(boss);
        }

        private static void ApplyAll()
        {
            if (_registry == null) return;
            foreach (var boss in _registry.Entries)
            {
                if (boss != null) ApplyForBoss(boss);
            }
        }

        private static void ApplyForBoss(BossData boss)
        {
            // MVP: BerthaBossPhaseController 단일 매칭. 일반 몹 추가 시 매칭 전략 확장 필요.
            var controller = Object.FindAnyObjectByType<BerthaBossPhaseController>();
            if (controller == null)
            {
                // Bertha 가 씬에 없는 경우 (다른 씬 / 보스 미등장) — 정상
                return;
            }

            var health = controller.GetComponent<Health>();
            controller.Configure(
                configuredHealth: health,
                configuredPhase2ThresholdNormalized: boss.Phase2ThresholdNormalized,
                configuredPhase3ThresholdNormalized: boss.Phase3ThresholdNormalized,
                configuredDebugLogging: false);

            Debug.Log(
                $"[BerthaBossDataInjector] Applied {boss.name} → BerthaBossPhaseController " +
                $"(phase2={boss.Phase2ThresholdNormalized}, phase3={boss.Phase3ThresholdNormalized})");
        }
    }
}
#endif
