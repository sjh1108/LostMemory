using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// 테스트 씬 전용 - Play 진입 시 지정한 dialogueGroupId 를 자동 발화.
    /// 시각/입력/타이프라이터 점검 용도.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Dialogue/Dialogue Test Starter")]
    public sealed class DialogueTestStarter : MonoBehaviour
    {
        [SerializeField] private string dialogueGroupId = "boss_intro";

        [Tooltip("Start 후 몇 초 뒤 발화 (controller Awake 보장)")]
        [Min(0f)]
        [SerializeField] private float startDelay = 0.2f;

        [Tooltip("키 누를 때마다 다시 발화 (한 번만이 아니라 반복 테스트)")]
        [SerializeField] private KeyCode replayKey = KeyCode.R;

        private void Start()
        {
            if (startDelay > 0f) Invoke(nameof(Fire), startDelay);
            else Fire();
        }

        private void Update()
        {
            if (Input.GetKeyDown(replayKey)) Fire();
        }

        private void Fire()
        {
            DialogueController controller = DialogueController.Instance;
            if (controller == null)
            {
                Debug.LogError("[DialogueTestStarter] DialogueController 가 scene 에 없음. DialoguePanel.prefab 을 Canvas 에 배치하세요.");
                return;
            }
            if (string.IsNullOrEmpty(dialogueGroupId))
            {
                Debug.LogWarning("[DialogueTestStarter] dialogueGroupId 가 비어 있음.");
                return;
            }
            controller.Show(dialogueGroupId);
        }
    }
}
