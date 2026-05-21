using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Combat
{
    /// <summary>
    /// 맵(씬) 전환 시 자기 플레이어의 마나를 최대값으로 회복한다.
    ///
    /// 멀티 정책:
    /// - 플레이어 prefab 자식에 부착 → 각 클라이언트가 자기 인스턴스 보유.
    /// - SceneManager.sceneLoaded 이벤트로 모든 씬 전환에 hook.
    /// - 자기 PlayerMana 만 다룸 → 다른 플레이어에 영향 없음.
    ///
    /// 무기는 의도적으로 다루지 않음: WeaponUpgradeService 가
    /// PlayerRunState 스냅샷을 통해 자체 복구하므로 (CaptureInto / RestoreSnapshotNextFrame),
    /// 여기서 RevertToDefault 를 호출하면 사용자가 픽업한 단검을 잃어버리게 됨.
    ///
    /// 주의: 새 플레이어 인스턴스의 PlayerMana 는 prefab startingMana 로 시작하므로
    /// (보통 0) 이 컴포넌트가 없으면 다음 맵 진입 시 마나 0 으로 시작. 따라서 RestoreToMax 가 의미 있음.
    ///
    /// 부착 위치: 플레이어 prefab (TestKhi_MinimalCharacter2D) 자식 또는 본체.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Scene Transition Reset")]
    public sealed class PlayerSceneTransitionReset : MonoBehaviour
    {
        [Header("Refs (비우면 부모 트리에서 자동 검색)")]
        [SerializeField] private PlayerMana mana;

        [Header("Behavior")]
        [Tooltip("씬 전환 시 마나를 최대값으로 회복.")]
        [SerializeField] private bool restoreMana = true;
        [Tooltip("진단용 로그.")]
        [SerializeField] private bool logResets = false;

        private void Awake()
        {
            if (mana == null) mana = GetComponentInParent<PlayerMana>(true);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // additive 로드는 무시 — 정확한 맵 전환(Single)일 때만 리셋.
            if (mode != LoadSceneMode.Single) return;

            if (restoreMana && mana != null)
            {
                mana.RestoreToMax();
                if (logResets) Debug.Log($"[PlayerSceneTransitionReset] Mana restored on scene '{scene.name}'.", this);
            }
        }
    }
}
